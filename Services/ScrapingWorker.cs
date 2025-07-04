namespace CijeneScraper.Services
{
    public class ScrapingWorker : BackgroundService
    {
        private readonly ScrapingQueue _queue;
        private readonly ILogger<ScrapingWorker> _logger;

        public ScrapingWorker(ScrapingQueue queue, ILogger<ScrapingWorker> logger)
        {
            _queue = queue;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ScrapingWorker pokrenut.");

            while (!stoppingToken.IsCancellationRequested)
            {
                if (_queue.TryDequeue(out var task))
                {
                    try
                    {
                        _logger.LogInformation("Počinjem scrapanje...");
                        await task(stoppingToken);
                        _logger.LogInformation("Scrapanje završeno.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Greška u scrapanju.");
                    }
                }
                else
                {
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
    }
}
