using CijeneScraper.Models;
using CijeneScraper.Services.Caching;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;

namespace CijeneScraper.Crawler
{
    /// <summary>
    /// Provides a base implementation for crawlers that retrieve and process price information from stores.
    /// </summary>
    public abstract class CrawlerBase : ICrawler
    {
        /// <summary>
        /// HTTP client used for making web requests.
        /// </summary>
        protected readonly HttpClient _http;

        /// <summary>
        /// Cache provider for storing and retrieving cached data.
        /// </summary>
        protected readonly ICacheProvider _cache;

        /// <summary>
        /// Initializes a new instance of the <see cref="CrawlerBase"/> class.
        /// </summary>
        /// <param name="http">The HTTP client to use for requests.</param>
        /// <param name="cache">The cache provider to use for caching data.</param>
        protected CrawlerBase(HttpClient http, ICacheProvider cache)
        {
            _http = http;
            _cache = cache;
        }

        /// <inheritdoc/>
        public abstract string Chain { get; }

        /// <inheritdoc/>
        public abstract Task<Dictionary<StoreInfo, List<PriceInfo>>> Crawl(
            DateTime? date = null,
            CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract Task<Dictionary<StoreInfo, List<PriceInfo>>> CrawlAsync(
            string outputFolder,
            DateTime? date = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously fetches the raw text content from the specified URL.
        /// </summary>
        /// <param name="url">The URL to fetch content from.</param>
        /// <returns>The raw text content of the page.</returns>
        public virtual async Task<string> FetchTextAsync(string url)
        {
            var response = await _http.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Saves the provided rows as a CSV file in the specified folder.
        /// </summary>
        /// <param name="folder">The folder where the CSV file will be saved.</param>
        /// <param name="fileName">The name of the CSV file (without extension).</param>
        /// <param name="rows">The rows to write to the CSV file.</param>
        protected void SaveCsv(string folder, string fileName, IEnumerable<string[]> rows)
        {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, fileName + ".csv");
            using var writer = new StreamWriter(path, false, Encoding.UTF8);
            foreach (var cols in rows)
            {
                // Each value is quoted and inner quotes are escaped
                writer.WriteLine(string.Join(',', cols.Select(c => $"\"{c.Replace("\"", "\"\"")}\"")));
            }
        }
    }
}