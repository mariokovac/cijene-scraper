using CijeneScraper.Crawler;
using CijeneScraper.Data;
using CijeneScraper.Models.Database;
using CijeneScraper.Services.DataProcessor;
using CijeneScraper.Services.Logging;
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
        private readonly ApplicationDbContext _dbContext;
        private readonly IScrapingJobLogService _jobLogService;
        private readonly ILogger<ScrapingJobService> _logger;

        private const string outputFolder = "ScrapedData";

        public ScrapingJobService(
            IEnumerable<ICrawler> crawlers,
            IScrapingDataProcessor dataProcessor,
            IEmailNotificationService emailService,
            ApplicationDbContext dbContext,
            IScrapingJobLogService jobLogService,
            ILogger<ScrapingJobService> logger)
        {
            _crawlers = crawlers.ToDictionary(c => c.Chain, StringComparer.OrdinalIgnoreCase);
            _dataProcessor = dataProcessor;
            _emailService = emailService;
            _dbContext = dbContext;
            _jobLogService = jobLogService;
            _logger = logger;
        }

        public async Task<ScrapingJobResult> RunScrapingJobAsync(
            string chain, 
            DateOnly date, 
            CancellationToken cancellationToken, 
            bool force = false,
            string? initiatedBy = null,
            string? requestSource = null)
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
            var jobLogs = new List<ScrapingJobLog>();

            foreach (var c in crawlers)
            {
                if (c == null)
                    return new ScrapingJobResult { Success = false, ErrorMessage = $"Crawler for chain 'NULL' is not registered." };

                if (cancellationToken.IsCancellationRequested)
                    return new ScrapingJobResult { Success = false, ErrorMessage = "Scraping request was cancelled." };

                // Start detailed job logging
                var jobLog = await _jobLogService.StartJobAsync(c.Chain, date, initiatedBy, requestSource, force, cancellationToken);
                jobLogs.Add(jobLog);

                Chain? dbChain = null;

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
                        {
                            await _jobLogService.CompleteJobAsync(jobLog.Id, 0, "Job already completed previously - skipped");
                            _logger.LogInformation("Scraping already completed for {Chain} on {Date} - skipping", c.Chain, date);
                            continue;
                        }
                    }
                }

                int changes = 0;
                try
                {
                    _logger.LogInformation("Starting scraping for chain {Chain} on {Date} (JobLogId: {JobLogId})", 
                        c.Chain, date, jobLog.Id);

                    var timer = Stopwatch.StartNew();
                    var results = await c.CrawlAsync(outputFolder, date, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    // Update progress with stores processed
                    await _jobLogService.UpdateProgressAsync(jobLog.Id, 
                        storesProcessed: results.Keys.Count, 
                        productsFound: results.Values.SelectMany(p => p).Count(),
                        cancellationToken: cancellationToken);

                    changes = await _dataProcessor.ProcessScrapingResultsAsync(c, results, date, cancellationToken);
                    totalChanges += changes;

                    // Update with final price changes
                    await _jobLogService.UpdateProgressAsync(jobLog.Id, priceChanges: changes, cancellationToken: cancellationToken);

                    // load chain from database if not already done
                    if (dbChain == null)
                    {
                        dbChain = await _dbContext.Chains.FirstOrDefaultAsync(o => o.Name == c.Chain, cancellationToken);
                    }

                    // Create legacy ScrapingJob record
                    var scrapingJob = new ScrapingJob
                    {
                        ChainID = dbChain!.Id,
                        Date = date,
                        StartedAt = jobLog.StartedAt,
                        CompletedAt = DateTime.UtcNow,
                        InitiatedBy = initiatedBy,
                        IsForced = force,
                        PriceChanges = changes,
                        ScrapingJobLogId = jobLog.Id
                    };

                    _dbContext.ScrapingJobs.Add(scrapingJob);
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();
                    await c.ClearCacheAsync(outputFolder, date, cancellationToken);

                    timer.Stop();

                    // Complete the detailed job log
                    await _jobLogService.CompleteJobAsync(jobLog.Id, changes, 
                        $"Successfully processed {results.Keys.Count} stores with {changes} price changes",
                        new { 
                            StoresProcessed = results.Keys.Count, 
                            ProductsFound = results.Values.SelectMany(p => p).Count(),
                            DurationSeconds = timer.Elapsed.TotalSeconds 
                        });

                    _logger.LogInformation("Successfully completed scraping for {Chain} on {Date}. " +
                        "Processed {StoreCount} stores, found {ProductCount} products, detected {Changes} price changes in {Duration}",
                        c.Chain, date, results.Keys.Count, results.Values.SelectMany(p => p).Count(), changes, timer.Elapsed);

                    try
                    {
                        await _emailService.SendAsync(
                            $"Scraping completed for [{c.Chain} - {date:yyyy-MM-dd}]",
                            $"The scraping job for chain '{c.Chain}' on date {date:yyyy-MM-dd} has completed successfully.\n\r" +
                            $"Job Log ID: {jobLog.Id}\n\r" +
                            $"Initiated by: {initiatedBy ?? "System"}\n\r" +
                            $"Time taken: {timer.Elapsed:hh\\:mm\\:ss\\.fff}\n\r" +
                            $"Stores processed: {results.Keys.Count}\n\r" +
                            $"Products found: {results.Values.SelectMany(p => p).Count()}\n\r" +
                            $"Price changes detected: {changes}\n\r"
                        );
                    }
                    catch (SmtpException smtpEx)
                    {
                        _logger.LogError(smtpEx, "Failed to send email notification for scraping completion of {Chain}", c.Chain);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Scraping job for chain {Chain} was cancelled (JobLogId: {JobLogId})", c.Chain, jobLog.Id);
                    await _jobLogService.CancelJobAsync(jobLog.Id, "Operation was cancelled by user or system", cancellationToken);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during scraping for chain {Chain} (JobLogId: {JobLogId})", c.Chain, jobLog.Id);
                    
                    await _jobLogService.FailJobAsync(jobLog.Id, ex.Message, ex.StackTrace, 
                        new { ExceptionType = ex.GetType().Name, InnerException = ex.InnerException?.Message }, 
                        cancellationToken);

                    try
                    {
                        await _emailService.SendAsync(
                            $"Scraping failed for [{c.Chain} - {date:yyyy-MM-dd}]",
                            $"An error occurred during the scraping job for chain '{c.Chain}' on date {date:yyyy-MM-dd}.\n\r" +
                            $"Job Log ID: {jobLog.Id}\n\r" +
                            $"Initiated by: {initiatedBy ?? "System"}\n\r" +
                            $"Error: {ex.Message}\n\r" +
                            $"Stack Trace: {ex.StackTrace}"
                        );
                    }
                    catch (SmtpException smtpEx)
                    {
                        _logger.LogError(smtpEx, "Failed to send email notification for scraping failure of {Chain}", c.Chain);
                    }
                    throw;
                }
            }

            var message = chain == "*" 
                ? $"Scraping jobs for all chains completed for date {date:yyyy-MM-dd}. Total changes: {totalChanges}. Job log IDs: {string.Join(", ", jobLogs.Select(j => j.Id))}"
                : $"Scraping job for chain '{chain}' completed for date {date:yyyy-MM-dd}. Total changes: {totalChanges}. Job log ID: {jobLogs.FirstOrDefault()?.Id}";

            return new ScrapingJobResult
            {
                Success = true,
                Message = message
            };
        }
    }
}