using CijeneScraper.Data;
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
        private readonly ApplicationDbContext _dbContext;
        private readonly IScrapingJobService _scrapingJobService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScraperController"/> class.
        /// </summary>
        /// <param name="queue">The scraping queue for managing and processing scraping tasks.</param>
        /// <param name="logger">The logger instance for logging information and errors.</param>
        /// <param name="dbContext">The database context for accessing application data.</param>
        /// <param name="scrapingJobService">The service responsible for executing and managing scraping jobs.</param>
        public ScraperController(ScrapingQueue queue,
            ILogger<ScraperController> logger,
            ApplicationDbContext dbContext,
            IScrapingJobService scrapingJobService
            )
        {
            _logger = logger;
            _dbContext = dbContext;
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
        /// <param name="force">
        /// A flag indicating whether to force the scraping operation, bypassing certain checks.
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
            DateOnly? date = null,
            [FromQuery] bool force = false)
        {
            date ??= DateOnly.FromDateTime(DateTime.UtcNow);

            var result = await _scrapingJobService.RunScrapingJobAsync(chain, date.Value, cancellationToken, force);

            if (!result.Success)
                return BadRequest(result.ErrorMessage);

            return Ok(result.Message);
        }

        /// <summary>
        /// Retrieves the status of recent scraping jobs.
        /// </summary>
        /// <returns>
        /// - <see cref="OkObjectResult"/> (HTTP 200) containing the 10 most recent scraping jobs,
        ///   ordered by date in descending order.
        /// </returns>
        /// <remarks>
        /// This endpoint returns limited information about each job:
        /// <list type="bullet">
        ///   <item>The store chain that was scraped</item>
        ///   <item>The date for which the scraping was performed</item>
        ///   <item>The timestamp when the scraping job was completed</item>
        /// </list>
        /// The results are limited to the 10 most recent jobs to prevent excessive data transfer.
        /// </remarks>
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            var results = _dbContext.ScrapingJobs.Select(o => new
            {
                o.Chain,
                o.Date,
                o.CompletedAt
            }).OrderByDescending(o => o.Date)
            .Take(10);
            return Ok(results);
        }
    }
}