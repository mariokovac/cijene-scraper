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
        private readonly ScrapingQueue _queue;
        private readonly ILogger<ScraperController> _logger;
        private readonly ApplicationDbContext _dbContext;
        //private readonly IScrapingJobService _scrapingJobService;
        private readonly IServiceScopeFactory _scopeFactory;

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
            //IScrapingJobService scrapingJobService,
            IServiceScopeFactory scopeFactory
            )
        {
            _queue = queue;
            _logger = logger;
            _dbContext = dbContext;
            //_scrapingJobService = scrapingJobService;
            _scopeFactory = scopeFactory;
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
        /// - <see cref="AcceptedResult"/> (HTTP 202) if the scraping job is successfully queued.
        /// - <see cref="ConflictObjectResult"/> (HTTP 409) if a scraping job is already running.
        /// </returns>
        /// <remarks>
        /// This endpoint:
        /// <list type="number">
        /// <item>Checks if a scraping job is already running.</item>
        /// <item>If not, enqueues a new scraping job.</item>
        /// <item>Returns appropriate HTTP status codes based on the operation's outcome.</item>
        /// </list>
        /// </remarks>
        [HttpPost("start/{chain}")]
        public IActionResult StartScraping(
            CancellationToken cancellationToken,
            string chain, 
            DateOnly? date = null,
            [FromQuery] bool force = false)
        {
            date ??= DateOnly.FromDateTime(DateTime.UtcNow);

            if (_queue.IsRunning)
            {
                if (force)
                {
                    _queue.CancelCurrent();
                    _queue.Enqueue(async ct =>
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var scrapingJobService = scope.ServiceProvider.GetRequiredService<IScrapingJobService>();

                        var result = await scrapingJobService.RunScrapingJobAsync(chain, date.Value, ct, force);
                        // Optionally log result or send notification here
                    });
                    return Accepted($"Previous scraping job cancelled. New job for chain '{chain}' and date '{date.Value}' has been queued.");
                }
                else
                {
                    return Conflict("Scraping job is already running. Only one job can run at a time.");
                }
            }

            _queue.Enqueue(async ct =>
            {
                using var scope = _scopeFactory.CreateScope();
                var scrapingJobService = scope.ServiceProvider.GetRequiredService<IScrapingJobService>();

                var result = await scrapingJobService.RunScrapingJobAsync(chain, date.Value, ct, force);
                // Optionally log result or send notification here
            });

            return Accepted($"Scraping job for chain '{chain}' and date '{date.Value}' has been queued.");
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