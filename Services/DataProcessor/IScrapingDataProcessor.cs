using CijeneScraper.Crawler;
using CijeneScraper.Models;

namespace CijeneScraper.Services.DataProcessor
{
    public interface IScrapingDataProcessor
    {
        Task<int> ProcessScrapingResultsAsync(ICrawler crawler, Dictionary<StoreInfo, List<PriceInfo>> results, DateOnly date, CancellationToken token);
    }
}
