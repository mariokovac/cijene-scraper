using CijeneScraper.Crawler;
using CijeneScraper.Models;
using CijeneScraper.Services.Caching;
using CijeneScraper.Services.Crawlers.Chains.Konzum;
using CsvHelper.Configuration;
using HtmlAgilityPack;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CijeneScraper.Services.Crawlers.Chains.Kaufland
{
    /// <summary>
    /// Crawler implementation for the Kaufland chain. Retrieves and processes price lists for all Kaufland stores.
    /// </summary>
    public class KauflandCrawler : CrawlerBase
    {
        /// <summary>
        /// Chain identifier for Kaufland.
        /// </summary>
        private const string CHAIN = "kaufland";
        /// <summary>
        /// Base URL for Kaufland Croatia.
        /// </summary>
        private const string BASE_URL = "https://www.kaufland.hr";
        /// <summary>
        /// URL for the Kaufland price list index page.
        /// </summary>
        private const string INDEX_URL = BASE_URL + "/akcije-novosti/popis-mpc.html";

        /// <summary>
        /// Path to the cache folder for Kaufland data.
        /// </summary>
        private string cacheFolder = Path.Combine("cache", CHAIN);

        /// <summary>
        /// Gets the name of the store chain this crawler is associated with.
        /// </summary>
        public override string Chain { get => CHAIN; }

        /// <summary>
        /// Initializes a new instance of the <see cref="KauflandCrawler"/> class.
        /// </summary>
        /// <param name="http">HTTP client for web requests.</param>
        /// <param name="cache">Cache provider for storing and retrieving data.</param>
        /// <param name="logger">Logger for logging information and errors.</param>
        public KauflandCrawler(HttpClient http, ICacheProvider cache, ILogger<KauflandCrawler> logger)
            : base(http, cache, logger) { }

        /// <summary>
        /// Crawls all Kaufland stores and retrieves price information for the specified date.
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
        /// Asynchronously crawls all Kaufland stores and saves the results to the specified output folder.
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
        /// Crawls and processes Kaufland price lists for all stores for the specified date.
        /// </summary>
        /// <param name="date">The date for which to retrieve prices.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <param name="onStoreProcessed">Optional callback invoked after each store is processed.</param>
        /// <returns>
        /// A dictionary mapping <see cref="StoreInfo"/> to a list of <see cref="PriceInfo"/> objects.
        /// </returns>
        private async Task<Dictionary<StoreInfo, List<PriceInfo>>> _crawlAndProcess(
            DateOnly date,
            CancellationToken cancellationToken = default,
            Action<StoreInfoDto, List<KauflandCsvRecord>>? onStoreProcessed = null)
        {
            var result = new Dictionary<StoreInfo, List<PriceInfo>>();

            // Get all CSV URLs for the given date
            var csvUrls = await getDatasourceUrls(date);
            if (!csvUrls.Any())
            {
                _logger.LogWarning($"No price list found for {date:yyyy-MM-dd}");
                return new Dictionary<StoreInfo, List<PriceInfo>>();
            }

            // Use a dictionary to ensure unique store records by StoreId
            var uniqueRecords = new Dictionary<StoreInfoDto, string>();
            var processedStoreIds = new HashSet<string>(); // O(1) lookup for processed store IDs

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

            // Process each unique store
            foreach (var store in uniqueRecords.Keys)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    List<KauflandCsvRecord> products = null;

                    var storeFolder = Path.Combine(cacheFolder, CHAIN);
                    var fileName = $"{store.StoreId}-{date:yyyy-MM-dd}";
                    var filePath = Path.Combine(storeFolder, fileName + _cache.Extension);
                    if (_cache.Exists(filePath))
                    {
                        // If file already exists, read from it
                        _logger.LogInformation($"Using cached data for store {store.StoreId} from {filePath}");
                        products = await readStorePricesCsv(filePath);
                        continue;
                    }
                    else
                    {
                        // Otherwise, fetch from the URL
                        _logger.LogInformation($"Cache miss for store {store.StoreId}, fetching online!");
                        products = await getUniqueRecordsFromCsv<KauflandCsvRecord>(
                            await FetchTextAsync(uniqueRecords[store], Encoding.GetEncoding("windows-1252")),
                            o => o.ProductCode,
                            new CsvConfiguration(CultureInfo.InvariantCulture)
                            {
                                Delimiter = "\t",
                                MissingFieldFound = null,
                                BadDataFound = null
                            }, cancellationToken);
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
        /// Transforms the list of KauflandCsvRecord products into PriceInfo and adds them to the result dictionary for the given store.
        /// </summary>
        /// <param name="result">The result dictionary to populate.</param>
        /// <param name="store">The store information DTO.</param>
        /// <param name="products">The list of products for the store.</param>
        private void transformToResult(Dictionary<StoreInfo, List<PriceInfo>> result,
            StoreInfoDto store, List<KauflandCsvRecord> products)
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
        /// Reads the list of KauflandCsvRecord products from the specified CSV file path in the cache.
        /// </summary>
        /// <param name="filePath">The path to the cached CSV file.</param>
        /// <returns>List of KauflandCsvRecord products.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the CSV file does not exist.</exception>
        private async Task<List<KauflandCsvRecord>> readStorePricesCsv(string filePath)
        {
            if (!_cache.Exists(filePath))
            {
                throw new FileNotFoundException($"CSV file not found: {filePath}");
            }

            // Read from cache
            var results = await _cache.ReadAsync<KauflandCsvRecord>(filePath);
            return results.ToList();
        }

        /// <inheritdoc/>>
        protected async override Task<List<string>> getDatasourceUrls(DateOnly date)
        {
            var urls = new List<string>();

            var content = await FetchTextAsync(INDEX_URL);

            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            // Find the root node containing the asset list
            var rootNode = doc.DocumentNode.SelectSingleNode("//div[@data-component='AssetList']");
            if (rootNode == null)
                throw new Exception("Scraping Kaufland index failed: Failed to find asset list.");

            var dataProps = rootNode.Attributes["data-props"].Value;
            var decodedJson = WebUtility.HtmlDecode(dataProps);

            using var docJson = JsonDocument.Parse(decodedJson);
            var root = docJson.RootElement;

            if (root.TryGetProperty("settings", out var settingsElem))
            {
                if (settingsElem.TryGetProperty("dataUrlAssets", out var dataUrlAssetsElem))
                {
                    string dataUrlAssets = dataUrlAssetsElem.GetString();

                    var assetListJsonValue = await FetchTextAsync($"{BASE_URL}{dataUrlAssets}");

                    if (string.IsNullOrEmpty(assetListJsonValue))
                        throw new Exception("Scraping Kaufland index failed: Failed to fetch asset list JSON.");

                    using var jsonAssetsFile = JsonDocument.Parse(assetListJsonValue);
                    var urlsDictionary = new Dictionary<string, string>();

                    var dateStr = date.ToString("_dd_MM_yyyy_");
                    var dateStr2 = date.ToString("_ddMMyyyy_");
                    foreach (var item in jsonAssetsFile.RootElement.EnumerateArray())
                    {
                        var label = item.TryGetProperty("label", out var labelProp) ? labelProp.GetString() : null;
                        var url = item.TryGetProperty("path", out var urlProp) ? urlProp.GetString() : null;

                        if (string.IsNullOrEmpty(label) || string.IsNullOrEmpty(url))
                            continue;

                        if (!label.Contains(dateStr) && !label.Contains(dateStr2))
                            continue;

                        urlsDictionary[label] = $"{BASE_URL}{url}";
                        urls.Add(urlsDictionary[label]);
                    }
                }
                else
                    throw new Exception("Scraping Kaufland index failed: Failed to find dataUrlAssets in settings.");
            }
            else
                throw new Exception("Scraping Kaufland index failed: Failed to find settings in data-props.");

            return urls.Distinct().ToList();
        }

        /// <summary>
        /// Parses store information from the given CSV URL.
        /// </summary>
        /// <param name="url">The URL of the CSV file.</param>
        /// <returns>StoreInfoDto containing parsed store information.</returns>
        /// <exception cref="FormatException">Thrown if the URL format is invalid or missing required segments.</exception>
        private StoreInfoDto ParseStoreInfo(string url)
        {
            var uri = new Uri(url);

            // Extract "title" as last segment of the URL, e.g. /something/another/label.csv
            var titleSegment = Uri.UnescapeDataString(uri.Segments.LastOrDefault());

            // 1) Extract the file name without its extension
            var fileName = Path.GetFileNameWithoutExtension(titleSegment);
            //    e.g. "Hipermarket_Andrije_Hebranga_2_Zadar_2030_05072025_7-30"

            // 2) Split the file name into parts on underscore
            var parts = fileName.Split('_', StringSplitOptions.RemoveEmptyEntries);

            // 3) StoreType is the first segment (e.g. "Hipermarket")
            string storeType = parts[0];

            // 4) Locate the segment that is exactly 8 digits long (the date in ddMMyyyy format)
            int idxDate = Array.FindIndex(parts, p => Regex.IsMatch(p, @"^\d{8}$"));
            if (idxDate < 2)
                throw new FormatException("Cannot find a ddMMyyyy date segment in the file name.");

            // 5) Store code is the segment immediately before the date
            string storeCode = parts[idxDate - 1];

            // 6) City is the segment two positions before the date
            string city = parts[idxDate - 2];

            // 7) Street name and house number are all segments between the category and the city
            var streetSegments = parts
                .Skip(1)               // skip the storeType
                .Take(idxDate - 2)     // up to just before the city segment
                .ToArray();
            if (streetSegments.Length < 1)
                throw new FormatException("Insufficient segments for street name and number.");

            //    The last segment in this subset is the house number
            string houseNumber = streetSegments.Last();

            //    All prior segments form the street name
            string streetName = string.Join(" ", streetSegments.Take(streetSegments.Length - 1));

            string address = $"{streetName} {houseNumber}";

            return new StoreInfoDto
            {
                StoreId = storeCode,
                StoreType = parts[0], // e.g. "Hipermarket"
                Name = $"{CHAIN} {CultureInfo.CurrentCulture.TextInfo.ToTitleCase(city.ToLower())}",
                StreetAddress = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(address.ToLower()),
                Zipcode = null, // not provided in input string -> TODO: Add postal code if available in the future
                City = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(city.ToLower())
            };
        }
        #endregion
    }
}