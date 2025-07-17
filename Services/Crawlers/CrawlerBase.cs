using CijeneScraper.Controllers;
using CijeneScraper.Models.Crawler;
using CijeneScraper.Services.Caching;
using CijeneScraper.Services.Crawlers.Common;
using CijeneScraper.Utility;
using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
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
        /// <param name="logger">The logger to use for logging information and errors.</param>
        protected CrawlerBase(HttpClient http, ICacheProvider cache, ILogger<CrawlerBase> logger)
        {
            _http = http;
            _cache = cache;
            _logger = logger;
        }

        /// <summary>
        /// Gets the name of the store chain this crawler is associated with.
        /// </summary>
        public abstract string Chain { get; }

        /// <summary>
        /// Crawls all stores and retrieves price information for the specified date.
        /// </summary>
        /// <param name="date">The date for which to retrieve prices.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// A dictionary mapping <see cref="StoreInfo"/> to a list of <see cref="PriceInfo"/> objects.
        /// </returns>
        public abstract Task<Dictionary<StoreInfo, List<PriceInfo>>> Crawl(
            DateOnly date,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously crawls all stores and saves the results to the specified output folder.
        /// </summary>
        /// <param name="outputFolder">The folder where the results will be saved.</param>
        /// <param name="date">The date for which to retrieve prices.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// A dictionary mapping <see cref="StoreInfo"/> to a list of <see cref="PriceInfo"/> objects.
        /// </returns>
        public abstract Task<Dictionary<StoreInfo, List<PriceInfo>>> CrawlAsync(
            string outputFolder,
            DateOnly date,
            CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public virtual async Task<string> FetchTextAsync(string url, Encoding[]? encodings = null)
        {
            var response = await _http.GetAsync(url);
            response.EnsureSuccessStatusCode();

            encodings ??= new[] { Encoding.UTF8 };
            var contentBytes = await response.Content.ReadAsByteArrayAsync();

            return EncodingDetector.GetText(contentBytes, encodings);
        }


        /// <summary>
        /// Retrieves a list of data source URLs (e.g., CSV files) for the specified date.
        /// </summary>
        /// <param name="date">The date for which to retrieve data source URLs.</param>
        /// <returns>A list of URLs as strings.</returns>
        protected abstract Task<List<string>> getDatasourceUrls(DateOnly date);

        /// <summary>
        /// Clears the cache for the specified output folder and date.
        /// </summary>
        /// <param name="outputFolder">The folder whose cache should be cleared.</param>
        /// <param name="date">The date for which the cache should be cleared.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task ClearCacheAsync(string outputFolder, DateOnly date, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Clearing cache for {Chain} crawler.", Chain);
            outputFolder = Path.Combine(outputFolder, Chain);
            return _cache.ClearAsync(outputFolder, date, cancellationToken);
        }

        /// <summary>
        /// Reads unique records from a CSV text, using the specified key selector.
        /// Only the last occurrence of each key is kept.
        /// </summary>
        /// <typeparam name="T">The type of record to read from the CSV.</typeparam>
        /// <param name="csvText">The CSV content as a string.</param>
        /// <param name="keySelector">A function to extract the unique key from each record.</param>
        /// <returns>A list of unique records by key.</returns>
        protected virtual async Task<List<T>> getUniqueRecordsFromCsv<T>(
            string csvText,
            Func<T, string> keySelector)
        {
            // Calls the overload with default configuration and cancellation token
            return await getUniqueRecordsFromCsv(csvText, keySelector, null, default);
        }

        /// <summary>
        /// Reads unique records from a CSV text, using the specified key selector and CSV configuration.
        /// Only the last occurrence of each key is kept.
        /// </summary>
        /// <typeparam name="T">The type of record to read from the CSV.</typeparam>
        /// <param name="csvText">The CSV content as a string.</param>
        /// <param name="keySelector">A function to extract the unique key from each record.</param>
        /// <param name="csvConfig">Optional CSV configuration. If null, uses the default configuration.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A list of unique records by key.</returns>
        protected virtual async Task<List<T>> getUniqueRecordsFromCsv<T>(
            string csvText,
            Func<T, string> keySelector,
            CsvConfiguration csvConfig = null,
            CancellationToken cancellationToken = default)
        {
            csvConfig ??= new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                MissingFieldFound = null,
                BadDataFound = null
            };
            using var reader = new StringReader(csvText);
            using var csv = new CsvReader(reader, csvConfig);

            var uniqueRecords = new Dictionary<string, T>();

            await csv.ReadAsync();
            csv.ReadHeader();

            while (await csv.ReadAsync())
            {
                var record = csv.GetRecord<T>();
                var key = keySelector(record);

                // Skip records without a key
                if (string.IsNullOrEmpty(key))
                    continue;

                // Dictionary automatically ensures uniqueness - the last record "wins"
                uniqueRecords[key] = record;
            }

            return uniqueRecords.Values.ToList();
        }

        
        protected virtual void transformToResult<T>(Dictionary<StoreInfo, List<PriceInfo>> result,
            StoreInfoDto store, List<T> products)
            where T : CsvRecordBase
        {
            result.Add(
                new StoreInfo
                {
                    Chain = Chain,
                    Code = store.StoreId,
                    Name = store.Name,
                    StreetAddress = store.StreetAddress,
                    PostalCode = store.Zipcode,
                    City = store.City
                },
                products.Select(p => p.ToPriceInfo()).ToList()
            );
        }
    }
}