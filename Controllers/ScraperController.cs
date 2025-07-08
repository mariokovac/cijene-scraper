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
    /// <summary>
    /// API controller responsible for managing scraping operations.
    /// This controller handles requests to start scraping jobs for specific store chains,
    /// processes the scraped data, and manages notifications for job completion or failure.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ScraperController : ControllerBase
    {
        private readonly ScrapingQueue _queue;
        private readonly ILogger<ScraperController> _logger;
        private readonly Dictionary<string, ICrawler> _crawlers;
        private readonly ApplicationDbContext _dbContext;
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
            _dbContext = dbContext;
            _dataProcessor = dataProcessor;
            _emailService = emailService;
        }

        /// <summary>
        /// Handles HTTP POST requests to initiate a scraping job for a specified store chain and date.
        /// Validates the chain name, retrieves the appropriate crawler, and executes the scraping process.
        /// The method processes and persists the results, clears related cache, and schedules a database reindex.
        /// Email notifications are sent on both success and failure, and all major steps are logged.
        /// </summary>
        /// <param name="chain">
        /// The identifier of the store chain to scrape. Must not be null or empty.
        /// </param>
        /// <param name="date">
        /// The date for which to perform scraping. If not provided, the current UTC date is used.
        /// </param>
        /// <returns>
        /// - <see cref="OkObjectResult"/> (HTTP 200) if the scraping job completes successfully.
        /// - <see cref="BadRequestObjectResult"/> (HTTP 400) if the chain name is invalid or unknown.
        /// </returns>
        /// <remarks>
        /// Handles cancellation requests and exceptions, ensuring that errors are logged and notifications are sent.
        /// </remarks>
        [HttpPost("start/{chain}")]
        public async Task<IActionResult> StartScraping(
            CancellationToken cancellationToken,
            string chain, 
            DateOnly? date = null)
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

            int changes = 0;
            try
            {
                var timer = Stopwatch.StartNew();
                _logger.LogInformation($"Starting scraping job for chain {chain} on date {date:yyyy-MM-dd}");

                var results = await crawler.CrawlAsync(outputFolder, date.Value, cancellationToken);

                // Check if the task was cancelled before processing results
                cancellationToken.ThrowIfCancellationRequested();

                changes = await _dataProcessor.ProcessScrapingResultsAsync(crawler, results, date.Value, cancellationToken);

                // Check again if the task was cancelled after processing results
                cancellationToken.ThrowIfCancellationRequested();

                await crawler.ClearCacheAsync(outputFolder, date.Value, cancellationToken);

                // reindex db => async background task
                _queue.Enqueue(async token =>
                {
                    await _dbContext.Database.ExecuteSqlRawAsync("REINDEX TABLE 'Prices'");
                });

                results = null; // Clear results to free memory
                _logger.LogInformation($"Scraping job for chain {chain} completed successfully.", crawler.Chain);

                // Send email notification
                try
                {
                    timer.Stop();
                    await _emailService.SendAsync(
                        $"Scraping completed for [{chain} - {date:yyyy-MM-dd}]",
                        $"The scraping job for chain '{chain}' on date {date:yyyy-MM-dd} has completed successfully.\n\r" +
                        $"Time taken {timer.Elapsed.ToString("hh\\:mm\\:ss\\.fff")}\n\r" +
                        $"Total changes detected: {changes}\n\r"
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

            return Ok($"Scraping job for chain '{chain}' completed for date {date:yyyy-MM-dd}. Total changes: {changes}");
        }
    }
}