using CijeneScraper.Crawler;
using CijeneScraper.Models.Crawler;
using CijeneScraper.Services.Caching;
using CijeneScraper.Services.Crawlers.Common;
using CsvHelper.Configuration;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CijeneScraper.Services.Crawlers.Chains.Spar
{
    /// <summary>
    /// Crawler implementation for the Spar chain. Retrieves and processes price lists for all Spar stores.
    /// </summary>
    public class SparCrawler : CrawlerBase
    {
        /// <summary>
        /// Chain identifier for Spar.
        /// </summary>
        private const string CHAIN = "spar";

        /// <summary>
        /// Base URL for Spar Croatia.
        /// </summary>
        private const string BASE_URL = "https://www.spar.hr";

        /// <summary>
        /// Path to the cache folder for Spar data.
        /// </summary>
        private string cacheFolder = Path.Combine("cache", CHAIN);

        /// <summary>
        /// Gets the name of the store chain this crawler is associated with.
        /// </summary>
        public override string Chain => CHAIN;

        /// <summary>
        /// Initializes a new instance of the <see cref="SparCrawler"/> class.
        /// </summary>
        /// <param name="http">HTTP client for web requests.</param>
        /// <param name="cache">Cache provider for storing and retrieving data.</param>
        /// <param name="logger">Logger for logging information and errors.</param>
        public SparCrawler(HttpClient http, ICacheProvider cache, ILogger<SparCrawler> logger)
            : base(http, cache, logger) { }

        /// <summary>
        /// Crawls all Spar stores and retrieves price information for the specified date.
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
        /// Asynchronously crawls all Spar stores and saves the results to the specified output folder.
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
        /// Crawls and processes Spar price lists for all stores for the specified date.
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
            Action<StoreInfoDto, List<SparCsvRecord>>? onStoreProcessed = null)
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
                    List<SparCsvRecord> products = null;

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
                        _logger.LogInformation($"Cache miss for store {store.StoreId}, fetching online!");
                        products = await getUniqueRecordsFromCsv<SparCsvRecord>(
                            await FetchTextAsync(uniqueRecords[store], [Encoding.GetEncoding("windows-1250"), Encoding.UTF8]),
                            o => o.ProductCode,
                            new CsvConfiguration(CultureInfo.InvariantCulture)
                            {
                                Delimiter = ";",
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
        /// Reads the list of SparCsvRecord products from the specified CSV file path in the cache.
        /// </summary>
        /// <param name="filePath">The path to the cached CSV file.</param>
        /// <returns>List of SparCsvRecord products.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the CSV file does not exist.</exception>
        private async Task<List<SparCsvRecord>> readStorePricesCsv(string filePath)
        {
            if (!_cache.Exists(filePath))
            {
                throw new FileNotFoundException($"CSV file not found: {filePath}");
            }

            // Read from cache
            var results = await _cache.ReadAsync<SparCsvRecord>(filePath);
            return results.ToList();
        }

        /// <summary>
        /// Retrieves a list of data source URLs (CSV files) for the specified date from Spar.
        /// </summary>
        /// <param name="date">The date for which to retrieve data source URLs.</param>
        /// <returns>A list of CSV URLs as strings.</returns>
        protected override async Task<List<string>> getDatasourceUrls(DateOnly date)
        {
            var urls = new List<string>();

            // Construct the JSON index URL for the given date
            // Format: Cijenik{year}{month}{day}.json
            var jsonUrl = $"{BASE_URL}/datoteke_cjenici/Cjenik{date:yyyyMMdd}.json";

            try
            {
                var jsonContent = await FetchTextAsync(jsonUrl);
                var priceListJson = JsonSerializer.Deserialize<SparPriceListJson>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (priceListJson?.Files != null)
                {
                    urls.AddRange(priceListJson.Files.Select(f => f.URL));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch Spar price list JSON from {Url}", jsonUrl);
            }

            return urls.Distinct().ToList();
        }

        /// <summary>
        /// Parses store information from the given CSV URL.
        /// Expected format: hipermarket_zadar_bleiburskih_zrtava_18_8701_interspar_zadar_0077_20250717_0330.csv
        /// Uses regex pattern to extract: store_type, city_and_address, store_id, store_name
        /// </summary>
        /// <param name="url">The URL of the CSV file.</param>
        /// <returns>StoreInfoDto containing parsed store information.</returns>
        /// <exception cref="FormatException">Thrown if the URL format is invalid or missing required segments.</exception>
        private StoreInfoDto ParseStoreInfo(string url)
        {
            var uri = new Uri(url);
            var fileName = Path.GetFileNameWithoutExtension(uri.Segments.LastOrDefault() ?? string.Empty);
            
            // Regex pattern to match: store_type_city_address_store_id_store_name_...
            // Pattern: ^([a-zA-Z]+)_([a-zA-Z0-9_\.]+)_(\d{4,5})_([a-zA-Z_]+)_
            var addressPattern = new Regex(@"^([a-zA-Z]+)_([a-zA-Z0-9_\.]+)_(\d{4,5})_([a-zA-Z_]+)_", RegexOptions.Compiled);
            
            var match = addressPattern.Match(fileName);
            if (!match.Success)
                throw new FormatException($"Invalid filename format: {fileName}");
            
            var storeType = match.Groups[1].Value;
            var cityAndAddress = match.Groups[2].Value;
            var storeId = match.Groups[3].Value;
            var storeName = match.Groups[4].Value;
            
            // Parse city and address from the cityAndAddress part
            var cityAndAddressParts = cityAndAddress.Split('_');
            var city = cityAndAddressParts[0]; // First part is always the city
            var addressParts = cityAndAddressParts.Skip(1).ToArray(); // Rest is the address
            var streetAddress = string.Join(" ", addressParts);
            
            return new StoreInfoDto
            {
                StoreId = storeId,
                StoreType = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(storeType),
                Name = $"{CHAIN} {CultureInfo.CurrentCulture.TextInfo.ToTitleCase(city)}",
                StreetAddress = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(streetAddress.Replace("_", " ")),
                Zipcode = string.Empty, // Not available in filename
                City = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(city)
            };
        }

        #endregion
    }
}
