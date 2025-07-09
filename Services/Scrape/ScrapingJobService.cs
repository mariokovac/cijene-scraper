using CijeneScraper.Crawler;
using CijeneScraper.Data;
using CijeneScraper.Models.Database;
using CijeneScraper.Services.DataProcessor;
using CijeneScraper.Services.Notification;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Net.Mail;

namespace CijeneScraper.Services.Scrape
{
    public class ScrapingJobService : IScrapingJobService
    {
        private readonly Dictionary<string, ICrawler> _crawlers;
        private readonly IScrapingDataProcessor _dataProcessor;
        private readonly IEmailNotificationService _emailService;
        private readonly ScrapingQueue _queue;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<ScrapingJobService> _logger;

        private const string outputFolder = "ScrapedData";

        public ScrapingJobService(
            IEnumerable<ICrawler> crawlers,
            IScrapingDataProcessor dataProcessor,
            IEmailNotificationService emailService,
            ScrapingQueue queue,
            ApplicationDbContext dbContext,
            ILogger<ScrapingJobService> logger)
        {
            _crawlers = crawlers.ToDictionary(c => c.Chain, StringComparer.OrdinalIgnoreCase);
            _dataProcessor = dataProcessor;
            _emailService = emailService;
            _queue = queue;
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<ScrapingJobResult> RunScrapingJobAsync(
            string chain, 
            DateOnly date, 
            CancellationToken cancellationToken, 
            bool force = false)
        {
            if (string.IsNullOrWhiteSpace(chain))
                return new ScrapingJobResult { Success = false, ErrorMessage = "Chain name cannot be null or empty." };

            ICrawler[] crawlers;
            if (chain == "*")
                crawlers = _crawlers.Values.ToArray();
            else if (!_crawlers.TryGetValue(chain, out var crawler))
                return new ScrapingJobResult { Success = false, ErrorMessage = $"Unknown chain: {chain}" };
            else
                crawlers = new[] { crawler };

            int totalChanges = 0;
            foreach (var c in crawlers)
            {
                if (c == null)
                    return new ScrapingJobResult { Success = false, ErrorMessage = $"Crawler for chain 'NULL' is not registered." };

                if (cancellationToken.IsCancellationRequested)
                    return new ScrapingJobResult { Success = false, ErrorMessage = "Scraping request was cancelled." };

                Chain dbChain = null;

                // Check if job already done
                if (!force)
                {
                    // find out chain id
                    dbChain = await _dbContext.Chains.FirstOrDefaultAsync(o => o.Name == c.Chain, cancellationToken);

                    // if chain is not found it may not be registered yet, continue with scraping
                    if (dbChain != null)
                    {
                        bool alreadyDone = await _dbContext.ScrapingJobs
                            .AnyAsync(j => j.ChainID == dbChain.Id && j.Date == date, cancellationToken);
                        if (alreadyDone)
                            continue;
                            //return new ScrapingJobResult { Success = false, ErrorMessage = $"Scraping already completed for {chain} on {date:yyyy-MM-dd}." };
                    }
                }

                int changes = 0;
                try
                {
                    var timer = Stopwatch.StartNew();
                    var results = await c.CrawlAsync(outputFolder, date, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    changes = await _dataProcessor.ProcessScrapingResultsAsync(c, results, date, cancellationToken);
                    totalChanges += changes;

                    // load chain from database if not already done
                    if (dbChain == null)
                    {
                        dbChain = await _dbContext.Chains.FirstOrDefaultAsync(o => o.Name == c.Chain, cancellationToken);
                    }

                    // After successful completion, log the job
                    _dbContext.ScrapingJobs.Add(new ScrapingJob
                    {
                        Chain = dbChain,
                        Date = date,
                        CompletedAt = DateTime.UtcNow
                    });
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();
                    await c.ClearCacheAsync(outputFolder, date, cancellationToken);

                    _queue.Enqueue(async token =>
                    {
                        await _dbContext.Database.ExecuteSqlRawAsync("REINDEX TABLE 'Prices'");
                    });

                    try
                    {
                        timer.Stop();
                        await _emailService.SendAsync(
                            $"Scraping completed for [{c.Chain} - {date:yyyy-MM-dd}]",
                            $"The scraping job for chain '{c.Chain}' on date {date:yyyy-MM-dd} has completed successfully.\n\r" +
                            $"Time taken {timer.Elapsed:hh\\:mm\\:ss\\.fff}\n\r" +
                            $"Total changes detected: {changes}\n\r"
                        );
                    }
                    catch (SmtpException smtpEx)
                    {
                        _logger.LogError(smtpEx, "Failed to send email notification for scraping completion.");
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation($"Scraping job for chain {c.Chain} was cancelled.");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error occurred during scraping for chain {c.Chain}");
                    try
                    {
                        await _emailService.SendAsync(
                            $"Scraping failed for [{c.Chain} - {date:yyyy-MM-dd}]",
                            $"An error occurred during the scraping job for chain '{c.Chain}' on date {date:yyyy-MM-dd}.\n\r" +
                            $"Error: {ex.Message}\n\rStack Trace: {ex.StackTrace}"
                        );
                    }
                    catch (SmtpException smtpEx)
                    {
                        _logger.LogError(smtpEx, "Failed to send email notification for scraping failure.");
                    }
                    throw;
                }
            }

            return new ScrapingJobResult
            {
                Success = true,
                Message = $"Scraping job for chain '{chain}' completed for date {date:yyyy-MM-dd}. Total changes: {totalChanges}"
            };
        }
    }
}