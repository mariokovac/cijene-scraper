using CijeneScraper.Controllers;
using CijeneScraper.Models;
using CijeneScraper.Services.Caching;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

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
        /// Logger for logging information and errors during crawling operations.
        /// </summary>
        protected readonly ILogger<CrawlerBase> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CrawlerBase"/> class.
        /// </summary>
        /// <param name="http">The HTTP client to use for requests.</param>
        /// <param name="cache">The cache provider to use for caching data.</param>
        protected CrawlerBase(HttpClient http, ICacheProvider cache, ILogger<CrawlerBase> logger)
        {
            _http = http;
            _cache = cache;
            _logger = logger;
        }

        /// <inheritdoc/>
        public abstract string Chain { get; }

        /// <inheritdoc/>
        public abstract Task<Dictionary<StoreInfo, List<PriceInfo>>> Crawl(
            DateOnly date,
            CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract Task<Dictionary<StoreInfo, List<PriceInfo>>> CrawlAsync(
            string outputFolder,
            DateOnly date,
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
    }
}