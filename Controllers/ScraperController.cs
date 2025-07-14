using CijeneScraper.Data;
using CijeneScraper.Services;
using CijeneScraper.Services.Scrape;
using CijeneScraper.Services.Logging;
using CijeneScraper.Models.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        private readonly IScrapingJobLogService _jobLogService;
        private readonly IServiceScopeFactory _scopeFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScraperController"/> class.
        /// </summary>
        /// <param name="queue">The scraping queue for managing and processing scraping tasks.</param>
        /// <param name="logger">The logger instance for logging information and errors.</param>
        /// <param name="dbContext">The database context for accessing application data.</param>
        /// <param name="scrapingJobService">The service responsible for executing and managing scraping jobs.</param>
        /// <param name="jobLogService">The service for detailed job logging and tracking.</param>
        public ScraperController(ScrapingQueue queue,
            ILogger<ScraperController> logger,
            ApplicationDbContext dbContext,
            IScrapingJobLogService jobLogService,
            IServiceScopeFactory scopeFactory
            )
        {
            _queue = queue;
            _logger = logger;
            _dbContext = dbContext;
            _jobLogService = jobLogService;
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

            // Capture user information
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers.UserAgent.ToString();
            var initiatedBy = $"API:{ipAddress}";

            _logger.LogInformation("Scraping request received for chain {Chain} on {Date} from {IpAddress} (Force: {Force})", 
                chain, date, ipAddress, force);

            if (_queue.IsRunning)
            {
                if (force)
                {
                    _logger.LogWarning("Cancelling current scraping job to start new one for {Chain} on {Date}", chain, date);
                    _queue.CancelCurrent();
                    _queue.Enqueue(async ct =>
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var scrapingJobService = scope.ServiceProvider.GetRequiredService<IScrapingJobService>();

                        var result = await scrapingJobService.RunScrapingJobAsync(chain, date.Value, ct, force, initiatedBy, RequestSource.API);
                        _logger.LogInformation("Queued scraping job completed: {Success}, Message: {Message}, Error: {Error}",
                            result.Success, result.Message, result.ErrorMessage);
                    });
                    return Accepted($"Previous scraping job cancelled. New job for chain '{chain}' and date '{date.Value}' has been queued.");
                }
                else
                {
                    _logger.LogWarning("Scraping request rejected - job already running for {Chain} on {Date}", chain, date);
                    return Conflict("Scraping job is already running. Only one job can run at a time.");
                }
            }

            _queue.Enqueue(async ct =>
            {
                using var scope = _scopeFactory.CreateScope();
                var scrapingJobService = scope.ServiceProvider.GetRequiredService<IScrapingJobService>();

                var result = await scrapingJobService.RunScrapingJobAsync(chain, date.Value, ct, force, initiatedBy, RequestSource.API);
                _logger.LogInformation("Queued scraping job completed: {Success}, Message: {Message}, Error: {Error}",
                    result.Success, result.Message, result.ErrorMessage);
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
            var results = _dbContext.ScrapingJobs
                .Include(j => j.Chain)
                .Include(j => j.ScrapingJobLog)
                .Select(o => new
                {
                    o.Id,
                    Chain = o.Chain.Name,
                    o.Date,
                    o.StartedAt,
                    o.CompletedAt,
                    o.InitiatedBy,
                    o.IsForced,
                    o.PriceChanges,
                    DetailedLog = o.ScrapingJobLog != null ? new
                    {
                        o.ScrapingJobLog.Status,
                        o.ScrapingJobLog.StoresProcessed,
                        o.ScrapingJobLog.ProductsFound,
                        o.ScrapingJobLog.DurationMs,
                        o.ScrapingJobLog.ErrorMessage
                    } : null
                }).OrderByDescending(o => o.StartedAt)
                .Take(10);
                
            return Ok(results);
        }

        /// <summary>
        /// Retrieves detailed job logs with filtering options
        /// </summary>
        [HttpGet("logs")]
        public async Task<IActionResult> GetJobLogs(
            [FromQuery] string? chain = null,
            [FromQuery] int take = 50,
            CancellationToken cancellationToken = default)
        {
            var logs = await _jobLogService.GetRecentJobsAsync(chain, take, cancellationToken);
            
            var result = logs.Select(log => new
            {
                log.Id,
                Chain = log.Chain.Name,
                log.Date,
                log.StartedAt,
                log.CompletedAt,
                log.Status,
                log.InitiatedBy,
                log.RequestSource,
                log.IsForced,
                log.StoresProcessed,
                log.ProductsFound,
                log.PriceChanges,
                DurationSeconds = log.DurationMs.HasValue ? (double?)log.DurationMs.Value / 1000.0 : null,
                log.SuccessMessage,
                log.ErrorMessage
            });

            return Ok(result);
        }

        /// <summary>
        /// Retrieves scraping job statistics
        /// </summary>
        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics(
            [FromQuery] DateOnly? fromDate = null,
            [FromQuery] DateOnly? toDate = null,
            [FromQuery] string? chain = null,
            CancellationToken cancellationToken = default)
        {
            var stats = await _jobLogService.GetStatisticsAsync(fromDate, toDate, chain, cancellationToken);
            return Ok(stats);
        }
    }
}