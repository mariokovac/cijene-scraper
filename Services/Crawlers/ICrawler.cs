
using CijeneScraper.Models;

namespace CijeneScraper.Crawler
{
    public interface ICrawler
    {
        public Task<Dictionary<StoreInfo, List<PriceInfo>>> Crawl(DateTime? date = null, CancellationToken cancellationToken = default);
        public Task<Dictionary<StoreInfo, List<PriceInfo>>> CrawlAsync(string outputFolder, DateTime? date = null, CancellationToken cancellationToken = default);
        public Task<string> FetchTextAsync(string url);

        public string Chain { get; }
    }
}
