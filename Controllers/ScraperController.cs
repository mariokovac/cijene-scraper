using CijeneScraper.Crawler;
using CijeneScraper.Data;
using CijeneScraper.Models;
using CijeneScraper.Models.Database;
using CijeneScraper.Services;
using CijeneScraper.Services.DataProcessor;
using CijeneScraper.Services.Notification;
using CijeneScraper.Utility;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Net.Mail;

namespace CijeneScraper.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScraperController : ControllerBase
    {
        private readonly ScrapingQueue _queue;
        private readonly ILogger<ScraperController> _logger;
        private readonly Dictionary<string, ICrawler> _crawlers;
        private readonly IScrapingDataProcessor _dataProcessor;
        private readonly IEmailNotificationService _emailService;

        private const string outputFolder = "ScrapedData";

        /// <summary>
        /// Initializes a new instance of the <see cref="ScraperController"/> class.
        /// </summary>
        /// <param name="queue">The scraping queue for managing scraping tasks.</param>
        /// <param name="logger">The logger instance for logging information and errors.</param>
        /// <param name="crawlers">A collection of available crawlers, mapped by chain name.</param>
        /// <param name="dbContext"> The database context for accessing application data.</param>
        /// <param name="dataProcessor"> The data processor for handling scraping results.</param>
        public ScraperController(ScrapingQueue queue,
            ILogger<ScraperController> logger,
            IEnumerable<ICrawler> crawlers,
            ApplicationDbContext dbContext,
            IScrapingDataProcessor dataProcessor,
            IEmailNotificationService emailService
            )
        {
            _queue = queue;
            _logger = logger;
            _crawlers = crawlers.ToDictionary(c => c.Chain, StringComparer.OrdinalIgnoreCase);
            _dataProcessor = dataProcessor;
            _emailService = emailService;
        }

        /// <summary>
        /// Starts a scraping job for the specified chain and optional date.
        /// </summary>
        /// <param name="chain">The name of the chain to scrape.</param>
        /// <param name="date">The date for which to scrape data. If null, the current date is used.</param>
        /// <returns>
        /// Returns 202 Accepted if the scraping job is added to the queue,
        /// 400 Bad Request if the chain is unknown.
        /// </returns>
        [HttpPost("start/{chain}")]
        public IActionResult StartScraping(string chain, DateOnly? date = null)
        {
            if (string.IsNullOrWhiteSpace(chain))
            {
                _logger.LogError("Chain name cannot be null or empty.");
                return BadRequest("Chain name cannot be null or empty.");
            }
            date ??= DateOnly.FromDateTime(DateTime.UtcNow);

            if (!_crawlers.TryGetValue(chain, out var crawler))
            {
                _logger.LogError("Unknown chain: {chain}", chain);
                return BadRequest($"Unknown chain: {chain}");
            }

            _logger.LogInformation($"Received scraping request for chain: {chain} on date: {date:yyyy-MM-dd}", chain, date);

            bool wasRunning = _queue.IsRunning;
            if (wasRunning)
            {
                _logger.LogInformation("Previous scraping job was running and will be cancelled.");
            }

            _queue.Enqueue(async token =>
            {
                try
                {
                    var timer = Stopwatch.StartNew();
                    _logger.LogInformation($"Starting scraping job for chain {chain} on date {date:yyyy-MM-dd}");

                    var results = await crawler.CrawlAsync(outputFolder, date.Value, token);

                    // Provjeri da li je zadatak prekinut
                    token.ThrowIfCancellationRequested();

                    await _dataProcessor.ProcessScrapingResultsAsync(crawler, results, date.Value, token);

                    // Provjeri da li je zadatak prekinut
                    token.ThrowIfCancellationRequested();

                    await crawler.ClearCacheAsync(outputFolder, date.Value, token);

                    results = null; // Clear results to free memory
                    _logger.LogInformation($"Scraping job for chain {chain} completed successfully.", crawler.Chain);

                    // Send email notification
                    try
                    {
                        timer.Stop();
                        await _emailService.SendAsync(
                            $"Scraping completed for [{chain} - {date:yyyy-MM-dd}]",
                            $"The scraping job for chain '{chain}' on date {date:yyyy-MM-dd} has completed successfully.\n\r " +
                            $"Time taken {timer.Elapsed.ToString("hh\\:mm\\:ss\\.fff")}"
                        );
                        _logger.LogInformation($"Email notification sent for scraping completion of chain {chain}.");
                    }
                    catch (SmtpException smtpEx)
                    {
                        _logger.LogError(smtpEx, "Failed to send email notification for scraping completion.");
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation($"Scraping job for chain {chain} was cancelled.");
                    throw; // Re-throw to let the queue handle it
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error occurred during scraping for chain {chain}");
                    try
                    {
                        await _emailService.SendAsync(
                            $"Scraping failed for [{chain} - {date:yyyy-MM-dd}]",
                            $"An error occurred during the scraping job for chain '{chain}' on date {date:yyyy-MM-dd}.\n\r" +
                            $"Error: {ex.Message}\n\rStack Trace: {ex.StackTrace}"
                        );
                        _logger.LogInformation($"Email notification sent for scraping failure of chain {chain}.");
                    }
                    catch (SmtpException smtpEx)
                    {
                        _logger.LogError(smtpEx, "Failed to send email notification for scraping failure.");
                    }
                    throw; // Re-throw to let the queue handle it
                }
            });

            if (wasRunning)
                return Accepted($"Previous scraping job was cancelled. New scraping job for chain '{chain}' started for date {date:yyyy-MM-dd}.");
            else
                return Accepted($"Scraping job for chain '{chain}' added to the queue for date {date:yyyy-MM-dd}.");
        }
    }
}