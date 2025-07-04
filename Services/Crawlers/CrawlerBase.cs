using CijeneScraper.Models;
using CijeneScraper.Services.Caching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CijeneScraper.Crawler
{
    public abstract class CrawlerBase : ICrawler
    {
        protected readonly HttpClient _http;
        protected readonly ICacheProvider _cache;

        protected CrawlerBase(HttpClient http, ICacheProvider cache)
        {
            _http = http;
            _cache = cache;
        }

        public abstract string Chain { get; }
        public abstract Task<Dictionary<StoreInfo, List<PriceInfo>>> Crawl(DateTime? date = null, CancellationToken cancellationToken = default);
        public abstract Task<Dictionary<StoreInfo, List<PriceInfo>>> CrawlAsync(string outputFolder, DateTime? date = null, CancellationToken cancellationToken = default);

        public virtual async Task<string> FetchTextAsync(string url)
        {
            var response = await _http.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        protected void SaveCsv(string folder, string fileName, IEnumerable<string[]> rows)
        {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, fileName + ".csv");
            using var writer = new StreamWriter(path, false, Encoding.UTF8);
            foreach (var cols in rows)
            {
                writer.WriteLine(string.Join(',', cols.Select(c => $"\"{c.Replace("\"", "\"\"")}\"")));
            }
        }
    }
}
