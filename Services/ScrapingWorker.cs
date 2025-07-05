
using CijeneScraper.Data;

namespace CijeneScraper.Services
{
    /// <summary>
    /// Background service that processes scraping tasks from the queue.
    /// </summary>
    public class ScrapingWorker : BackgroundService
    {
        private readonly ScrapingQueue _queue;
        private readonly ILogger<ScrapingWorker> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScrapingWorker"/> class.
        /// </summary>
        /// <param name="queue">The scraping task queue.</param>
        /// <param name="logger">The logger instance.</param>
        public ScrapingWorker(
            ScrapingQueue queue, 
            ILogger<ScrapingWorker> logger
            )
        {
            _queue = queue;
            _logger = logger;
        }

        /// <summary>
        /// Executes the background service, processing tasks from the queue until cancellation is requested.
        /// </summary>
        /// <param name="stoppingToken">Token to signal cancellation.</param>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ScrapingWorker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                if (_queue.TryDequeue(out var task))
                {
                    try
                    {
                        _logger.LogInformation("Starting scraping task...");
                        try
                        {
                            await task(stoppingToken);
                        }
                        catch
                        {
                            _logger.LogWarning("[ERROR] Scraping task failed!");
                            throw;
                        }
                        _logger.LogInformation("Scraping task completed.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error occurred during scraping task.");
                    }
                }
                else
                {
                    // No task available, wait before checking again
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
    }
}