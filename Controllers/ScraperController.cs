using CijeneScraper.Crawler;
using CijeneScraper.Data;
using CijeneScraper.Models;
using CijeneScraper.Models.Database;
using CijeneScraper.Services;
using CijeneScraper.Services.DataProcessor;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
            IScrapingDataProcessor dataProcessor
            )
        {
            _queue = queue;
            _logger = logger;
            _crawlers = crawlers.ToDictionary(c => c.Chain, StringComparer.OrdinalIgnoreCase);
            _dataProcessor = dataProcessor;
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
            if (!_crawlers.TryGetValue(chain, out var crawler))
            {
                _logger.LogError("Unknown chain: {chain}", chain);
                return BadRequest($"Unknown chain: {chain}");
            }

            date ??= DateOnly.FromDateTime(DateTime.UtcNow);

            _logger.LogInformation($"Received scraping request for chain: {chain} on date: {date:yyyy-MM-dd}", chain, date);

            _queue.Enqueue(async token =>
            {
                var results = await crawler.CrawlAsync(outputFolder, date.Value, token);
                await _dataProcessor.ProcessScrapingResultsAsync(crawler, results, date.Value, token);
                await crawler.ClearCacheAsync(outputFolder, date.Value, token);
                results = null; // Clear results to free memory

                _logger.LogInformation($"Scraping job for chain {chain} completed successfully.", crawler.Chain);
            });

            return Accepted($"Scraping job for chain '{chain}' added to the queue for date {date:yyyy-MM-dd}.");
        }
    }
}