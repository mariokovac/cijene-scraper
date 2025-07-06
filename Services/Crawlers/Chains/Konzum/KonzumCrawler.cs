using CijeneScraper.Crawler;
using CijeneScraper.Models;
using CijeneScraper.Services.Caching;
using HtmlAgilityPack;
using Microsoft.AspNetCore.WebUtilities;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CijeneScraper.Services.Crawlers.Chains.Konzum
{
    /// <summary>
    /// Crawler implementation for Konzum chain. Handles crawling, parsing, and caching of price lists for Konzum stores.
    /// </summary>
    public class KonzumCrawler : CrawlerBase
    {
        /// <summary>
        /// Chain identifier for Konzum.
        /// </summary>
        private const string CHAIN = "konzum";
        /// <summary>
        /// Base URL for Konzum website.
        /// </summary>
        private const string BASE_URL = "https://www.konzum.hr";
        /// <summary>
        /// URL for the index of price lists.
        /// </summary>
        private const string INDEX_URL = BASE_URL + "/cjenici";
        /// <summary>
        /// Regular expression for parsing store addresses.
        /// </summary>
        private static readonly Regex AddressPattern = new Regex(@"^(.*)\s+(\d{5})\s+(.*)$", RegexOptions.Compiled);

        /// <summary>
        /// Path to the cache folder for Konzum data.
        /// </summary>
        private string cacheFolder = Path.Combine("cache", CHAIN);

        /// <summary>
        /// Gets the name of the store chain this crawler is associated with.
        /// </summary>
        public override string Chain { get => CHAIN; }

        /// <summary>
        /// Initializes a new instance of the <see cref="KonzumCrawler"/> class.
        /// </summary>
        /// <param name="http">HTTP client for web requests.</param>
        /// <param name="cache">Cache provider for storing and retrieving data.</param>
        /// <param name="logger">Logger for logging information and errors.</param>
        public KonzumCrawler(HttpClient http, ICacheProvider cache, ILogger<KonzumCrawler> logger)
            : base(http, cache, logger) { }

        /// <summary>
        /// Crawls all Konzum stores and retrieves price information for the specified date.
        /// </summary>
        /// <param name="date">The date for which to retrieve prices.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// A dictionary mapping <see cref="StoreInfo"/> to a list of <see cref="PriceInfo"/> objects.
        /// </returns>
        public override async Task<Dictionary<StoreInfo, List<PriceInfo>>> Crawl(
            DateOnly date,
            CancellationToken cancellationToken = default)
        {
            return await _crawlAndProcess(date, cancellationToken, (store, products) =>
            {
                _logger.LogInformation($"Processed store: {store.StoreId}, Products: {products.Count}");
            });
        }

        /// <summary>
        /// Asynchronously crawls all Konzum stores and saves the results to the specified output folder.
        /// </summary>
        /// <param name="outputFolder">The folder where the results will be saved.</param>
        /// <param name="date">The date for which to retrieve prices.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// A dictionary mapping <see cref="StoreInfo"/> to a list of <see cref="PriceInfo"/> objects.
        /// </returns>
        public override async Task<Dictionary<StoreInfo, List<PriceInfo>>> CrawlAsync(
            string outputFolder,
            DateOnly date,
            CancellationToken cancellationToken = default)
        {
            cacheFolder = Path.Combine(outputFolder);

            var data = await _crawlAndProcess(date, cancellationToken, async (store, products) =>
            {
                var storeFolder = Path.Combine(cacheFolder, CHAIN);
                var fileName = $"{store.StoreId}-{date:yyyy-MM-dd}";

                await _cache.SaveAsync(storeFolder, fileName, products, cancellationToken);

                _logger.LogInformation($"Saved {products.Count} products for store {store.StoreId} to {storeFolder}/{fileName}{_cache.Extension}");
            });

            return data;
        }

        #region Private Methods

        /// <summary>
        /// Crawls and processes all available price lists for the given date, optionally invoking a callback for each store.
        /// </summary>
        /// <param name="date">The date for which to crawl price lists.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <param name="onStoreProcessed">Optional callback invoked after each store is processed.</param>
        /// <returns>
        /// A dictionary mapping <see cref="StoreInfo"/> to a list of <see cref="PriceInfo"/> objects.
        /// </returns>
        private async Task<Dictionary<StoreInfo, List<PriceInfo>>> _crawlAndProcess(
            DateOnly date,
            CancellationToken cancellationToken = default,
            Action<StoreInfoDto, List<KonzumCsvRecord>>? onStoreProcessed = null)
        {
            var result = new Dictionary<StoreInfo, List<PriceInfo>>();

            // Get all CSV URLs for the given date
            var csvUrls = await getDatasourceUrls(date);
            if (!csvUrls.Any())
            {
                _logger.LogWarning($"No price list found for {date:yyyy-MM-dd}");
                return new Dictionary<StoreInfo, List<PriceInfo>>();
            }

            // Dictionary to keep unique store records and their URLs
            var uniqueRecords = new Dictionary<StoreInfoDto, string>();
            // HashSet for fast O(1) lookup of processed store IDs
            var processedStoreIds = new HashSet<string>();

            foreach (var url in csvUrls)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var storeInfo = ParseStoreInfo(url);
                    // Add returns false if already exists
                    if (processedStoreIds.Add(storeInfo.StoreId))
                    {
                        uniqueRecords[storeInfo] = url;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse store info from URL: {Url}", url);
                }
            }

            foreach (var store in uniqueRecords.Keys)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    List<KonzumCsvRecord> products = null;

                    var storeFolder = Path.Combine(cacheFolder, CHAIN);
                    var fileName = $"{store.StoreId}-{date:yyyy-MM-dd}";
                    var filePath = Path.Combine(storeFolder, fileName + _cache.Extension);
                    if (_cache.Exists(filePath))
                    {
                        // If file already exists, read from it
                        _logger.LogInformation($"Using cached data for store {store.StoreId} from {filePath}");
                        products = await readStorePricesCsv(filePath);
                        transformToResult(result, store, products);

                        continue;
                    }
                    else
                    {
                        // Otherwise, fetch from the URL
                        _logger.LogInformation($"Cache miss for store {store.StoreId}, fetching online");
                        products = await getUniqueRecordsFromCsv<KonzumCsvRecord>(await FetchTextAsync(uniqueRecords[store]), o => o.ProductCode);
                    }

                    // Add the store and products to the result dictionary
                    transformToResult(result, store, products);

                    _logger.LogInformation($"Read {products.Count} products for store {store.StoreId}");
                    if (onStoreProcessed != null)
                        onStoreProcessed(store, products);
                }
                catch
                {
                    throw;
                }
            }

            _logger.LogInformation($"Crawled {result.Count} stores for {date:yyyy-MM-dd}");
            return result;
        }

        /// <summary>
        /// Adds the store and its products to the result dictionary.
        /// </summary>
        /// <param name="result">The result dictionary to populate.</param>
        /// <param name="store">Store information DTO.</param>
        /// <param name="products">List of products for the store.</param>
        private void transformToResult(Dictionary<StoreInfo, List<PriceInfo>> result,
            StoreInfoDto store, List<KonzumCsvRecord> products)
        {
            result.Add(
                new StoreInfo
                {
                    Chain = CHAIN,
                    Code = store.StoreId,
                    Name = store.Name,
                    StreetAddress = store.StreetAddress,
                    PostalCode = store.Zipcode,
                    City = store.City
                },
                products.Select(p => (PriceInfo)p).ToList()
            );
        }

        /// <summary>
        /// Reads store prices from a cached CSV file.
        /// </summary>
        /// <param name="filePath">Path to the cached CSV file.</param>
        /// <returns>List of <see cref="KonzumCsvRecord"/> objects.</returns>
        private async Task<List<KonzumCsvRecord>> readStorePricesCsv(string filePath)
        {
            if (!_cache.Exists(filePath))
            {
                throw new FileNotFoundException($"CSV file not found: {filePath}");
            }

            // Read from cache
            var results = await _cache.ReadAsync<KonzumCsvRecord>(filePath); // Ensure cache is read
            return results.ToList();
        }

        /// <inheritdoc/>>
        protected async override Task<List<string>> getDatasourceUrls(DateOnly date)
        {
            var urls = new List<string>();
            for (int page = 1; page <= 10; page++)
            {
                var pageUrl = $"{INDEX_URL}?date={date:yyyy-MM-dd}&page={page}";
                var content = await FetchTextAsync(pageUrl);
                if (string.IsNullOrEmpty(content)) break;

                var links = ParseIndex(content);
                if (!links.Any()) break;

                urls.AddRange(links);
            }
            return urls.Distinct().ToList();
        }

        /// <summary>
        /// Parses the HTML content of the index page and extracts CSV links.
        /// </summary>
        /// <param name="html">HTML content of the index page.</param>
        /// <returns>List of CSV URLs.</returns>
        private List<string> ParseIndex(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var nodes = doc.DocumentNode.SelectNodes("//a[@format='csv']");
            if (nodes == null) return new List<string>();

            return nodes
                .Select(n => n.GetAttributeValue("href", string.Empty))
                .Where(h => !string.IsNullOrEmpty(h))
                .Select(h => BASE_URL + h)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Parses store information from the CSV URL.
        /// </summary>
        /// <param name="url">CSV URL containing store information in the query string.</param>
        /// <returns>Parsed <see cref="StoreInfoDto"/> object.</returns>
        /// <exception cref="Exception">Thrown if the title parameter is missing or address cannot be parsed.</exception>
        private StoreInfoDto ParseStoreInfo(string url)
        {
            var uri = new Uri(url);
            var query = uri.Query.TrimStart('?');
            var dict = QueryHelpers.ParseQuery(query);

            if (!dict.TryGetValue("title", out var titleVals))
                throw new Exception($"No title parameter in URL: {url}");

            var title = Uri.UnescapeDataString(titleVals.First()).Replace("_", " ");
            var parts = title.Split(',').Select(p => p.Trim()).ToList();
            if (parts.Count < 6)
                throw new Exception($"Invalid CSV title format: {title}");

            var storeType = parts[0].ToLower();
            var storeId = parts.Count == 6 ? parts[2] : parts[3];
            var addressPart = parts.Count == 6 ? parts[1] : $"{parts[1]} {parts[2]}";

            var match = AddressPattern.Match(addressPart);
            if (!match.Success)
                throw new Exception($"Could not parse address from: {addressPart}");

            var street = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(match.Groups[1].Value.ToLower());
            var zipcode = match.Groups[2].Value;
            var city = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(match.Groups[3].Value.ToLower());

            return new StoreInfoDto
            {
                StoreId = storeId,
                StoreType = storeType,
                Name = $"{CHAIN} {city}",
                StreetAddress = street,
                Zipcode = zipcode,
                City = city
            };
        }
        #endregion
    }
}