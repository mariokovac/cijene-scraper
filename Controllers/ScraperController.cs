using CijeneScraper.Crawler;
using CijeneScraper.Services;
using Microsoft.AspNetCore.Mvc;

namespace CijeneScraper.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScraperController : ControllerBase
    {
        private readonly ScrapingQueue _queue;
        private readonly ILogger<ScraperController> _logger;
        private readonly Dictionary<string, ICrawler> _crawlers;
        private const string outputFolder = "ScrapedData";

        /// <summary>
        /// Initializes a new instance of the <see cref="ScraperController"/> class.
        /// </summary>
        /// <param name="queue">The scraping queue for managing scraping tasks.</param>
        /// <param name="logger">The logger instance for logging information and errors.</param>
        /// <param name="crawlers">A collection of available crawlers, mapped by chain name.</param>
        public ScraperController(ScrapingQueue queue,
            ILogger<ScraperController> logger,
            IEnumerable<ICrawler> crawlers)
        {
            _queue = queue;
            _logger = logger;
            _crawlers = crawlers.ToDictionary(
                c => c.Chain,
                StringComparer.OrdinalIgnoreCase);
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
        public IActionResult StartScraping(string chain, DateTime? date = null)
        {
            _logger.LogInformation("Scraping request received.");

            if (!_crawlers.TryGetValue(chain, out var crawler))
            {
                _logger.LogError("Unknown chain: {chain}", chain);
                return BadRequest($"Unknown chain: {chain}");
            }

            if (date == null)
            {
                date = DateTime.Now;
            }

            _queue.Enqueue(async token =>
            {
                await crawler.CrawlAsync(outputFolder, date, token);
            });

            return Accepted("Scraping job added to the queue.");
        }
    }
}