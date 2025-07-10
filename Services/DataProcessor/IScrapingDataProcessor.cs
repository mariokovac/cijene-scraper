using CijeneScraper.Crawler;
using CijeneScraper.Models.Crawler;

namespace CijeneScraper.Services.DataProcessor
{
    public interface IScrapingDataProcessor
    {
        Task<int> ProcessScrapingResultsAsync(ICrawler crawler, Dictionary<StoreInfo, List<PriceInfo>> results, DateOnly date, CancellationToken token);
    }
}
