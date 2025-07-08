namespace CijeneScraper.Services.Scrape
{
    public class ScrapingJobResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public interface IScrapingJobService
    {
        Task<ScrapingJobResult> RunScrapingJobAsync(string chain, DateOnly date, CancellationToken cancellationToken, bool force = false);
    }
}