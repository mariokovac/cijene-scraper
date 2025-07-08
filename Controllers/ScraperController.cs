using CijeneScraper.Services;
using CijeneScraper.Services.Scrape;
using Microsoft.AspNetCore.Mvc;

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
        private readonly ILogger<ScraperController> _logger;
        private readonly IScrapingJobService _scrapingJobService;

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
            IScrapingJobService scrapingJobService
            )
        {
            _logger = logger;
            _scrapingJobService = scrapingJobService;
        }

        /// <summary>
        /// Initiates a scraping job for the specified store chain(s) and date.
        /// </summary>
        /// <param name="cancellationToken">
        /// Token to observe for cancellation requests during the operation.
        /// </param>
        /// <param name="chain">
        /// The identifier of the store chain to scrape. Use "*" to scrape all registered chains.
        /// </param>
        /// <param name="date">
        /// The date for which to perform scraping. If not provided, the current UTC date is used.
        /// </param>
        /// <returns>
        /// - <see cref="OkObjectResult"/> (HTTP 200) if the scraping job(s) complete successfully.
        /// - <see cref="BadRequestObjectResult"/> (HTTP 400) if the chain name is invalid, unknown, or the request is cancelled.
        /// </returns>
        /// <remarks>
        /// This endpoint:
        /// <list type="number">
        /// <item>Validates the chain parameter and resolves the appropriate crawler(s).</item>
        /// <item>For each crawler:
        ///   <list type="bullet">
        ///     <item>Performs the scraping operation for the specified date.</item>
        ///     <item>Processes and persists the scraped data to the database.</item>
        ///     <item>Clears the crawler's cache for the date.</item>
        ///     <item>Schedules a background database reindex for the Prices table.</item>
        ///     <item>Sends email notifications on both success and failure.</item>
        ///     <item>Logs all major steps and errors.</item>
        ///   </list>
        /// </item>
        /// <item>Handles cancellation and exceptions, ensuring resources are released and notifications are sent as appropriate.</item>
        /// </list>
        /// </remarks>
        [HttpPost("start/{chain}")]
        public async Task<IActionResult> StartScraping(
            CancellationToken cancellationToken,
            string chain, 
            DateOnly? date = null)
        {
            date ??= DateOnly.FromDateTime(DateTime.UtcNow);

            var result = await _scrapingJobService.RunScrapingJobAsync(chain, date.Value, cancellationToken);

            if (!result.Success)
                return BadRequest(result.ErrorMessage);

            return Ok(result.Message);
        }
    }
}