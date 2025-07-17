using CijeneScraper.Crawler;
using CijeneScraper.Models.Crawler;
using CijeneScraper.Models.Database;
using CijeneScraper.Services.Caching;
using CijeneScraper.Services.Crawlers.Chains.Kaufland;
using CijeneScraper.Services.Crawlers.Common;
using CsvHelper.Configuration;
using HtmlAgilityPack;
using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace CijeneScraper.Services.Crawlers.Chains.Plodine
{
    /// <summary>
    /// Crawler implementation for Plodine chain. Handles crawling, parsing, and caching of price lists for Plodine stores.
    /// </summary>
    public class PlodineCrawler : CrawlerBase
    {
        /// <summary>
        /// Chain identifier for Plodine.
        /// </summary>
        private const string CHAIN = "plodine";
        /// <summary>
        /// Base URL for Plodine website.
        /// </summary>
        private const string BASE_URL = "https://www.plodine.hr";
        /// <summary>
        /// URL for the index of price lists.
        /// </summary>
        private const string INDEX_URL = BASE_URL + "/info-o-cijenama";
        /// <summary>
        /// Regular expression for parsing store information from CSV filename.
        /// </summary>
        private static readonly Regex FilenamePattern = new Regex(
            @"^(SUPERMARKET|HIPERMARKET)_(.+?)_(\d{5})_(.+)_(\d+)_\d+_\d+.*\.csv$", 
            RegexOptions.Compiled);

        /// <summary>
        /// Path to the cache folder for Plodine data.
        /// </summary>
        private string cacheFolder = Path.Combine("cache", CHAIN);

        /// <summary>
        /// Gets the name of the store chain this crawler is associated with.
        /// </summary>
        public override string Chain { get => CHAIN; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlodineCrawler"/> class.
        /// </summary>
        /// <param name="http">HTTP client for web requests.</param>
        /// <param name="cache">Cache provider for storing and retrieving data.</param>
        /// <param name="logger">Logger for logging information and errors.</param>
        public PlodineCrawler(HttpClient http, ICacheProvider cache, ILogger<PlodineCrawler> logger)
            : base(http, cache, logger) { }

        /// <summary>
        /// Crawls all Plodine stores and retrieves price information for the specified date.
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
        /// Asynchronously crawls all Plodine stores and saves the results to the specified output folder.
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
            Action<StoreInfoDto, List<PlodineCsvRecord>>? onStoreProcessed = null)
        {
            var result = new Dictionary<StoreInfo, List<PriceInfo>>();

            // Get ZIP URL for the given date
            var zipUrls = await getDatasourceUrls(date);
            if (!zipUrls.Any())
            {
                _logger.LogWarning($"No price list found for {date:yyyy-MM-dd}");
                return new Dictionary<StoreInfo, List<PriceInfo>>();
            }

            var zipUrl = zipUrls.First(); // Should only be one ZIP file per date

            // Process all CSV files in the ZIP
            var zipContents = await GetZipContents(zipUrl, cancellationToken);
            
            foreach (var (filename, csvContent) in zipContents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var storeInfo = ParseStoreFromFilename(filename);
                    if (storeInfo == null)
                    {
                        _logger.LogWarning($"Skipping CSV {filename} due to store parsing failure");
                        continue;
                    }

                    List<PlodineCsvRecord> products = null;

                    var storeFolder = Path.Combine(cacheFolder, CHAIN);
                    var fileName = $"{storeInfo.StoreId}-{date:yyyy-MM-dd}";
                    var filePath = Path.Combine(storeFolder, fileName + _cache.Extension);
                    
                    if (_cache.Exists(filePath))
                    {
                        // If file already exists, read from it
                        _logger.LogInformation($"Using cached data for store {storeInfo.StoreId} from {filePath}");
                        products = await ReadStorePricesCsv(filePath);
                    }
                    else
                    {
                        // Otherwise, parse from CSV content
                        _logger.LogInformation($"Cache miss for store {storeInfo.StoreId}, parsing CSV");
                        products = await getUniqueRecordsFromCsv<PlodineCsvRecord>(
                            csvContent,
                            o => o.ProductCode,
                            new CsvConfiguration(CultureInfo.InvariantCulture)
                            {
                                Delimiter = ";",
                                MissingFieldFound = null,
                                BadDataFound = null
                            }, cancellationToken);
                    }

                    // Add the store and products to the result dictionary
                    transformToResult(result, storeInfo, products);

                    _logger.LogInformation($"Read {products.Count} products for store {storeInfo.StoreId}");
                    if (onStoreProcessed != null)
                        onStoreProcessed(storeInfo, products);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process CSV file: {Filename}", filename);
                }
            }

            _logger.LogInformation($"Crawled {result.Count} stores for {date:yyyy-MM-dd}");
            return result;
        }

        /// <summary>
        /// Reads store prices from a cached CSV file.
        /// </summary>
        /// <param name="filePath">Path to the cached CSV file.</param>
        /// <returns>List of <see cref="PlodineCsvRecord"/> objects.</returns>
        private async Task<List<PlodineCsvRecord>> ReadStorePricesCsv(string filePath)
        {
            if (!_cache.Exists(filePath))
            {
                throw new FileNotFoundException($"CSV file not found: {filePath}");
            }

            // Read from cache
            var results = await _cache.ReadAsync<PlodineCsvRecord>(filePath);
            return results.ToList();
        }

        /// <inheritdoc/>
        protected override async Task<List<string>> getDatasourceUrls(DateOnly date)
        {
            var content = await FetchTextAsync(INDEX_URL);
            var zipUrls = ParseIndexForZip(content);
            
            var dateStr = date.ToString("yyyy-MM-dd");
            _logger.LogDebug($"Available price lists: {string.Join(", ", zipUrls.Keys)}");
            
            if (!zipUrls.ContainsKey(dateStr))
            {
                throw new InvalidOperationException($"No price list found for {dateStr}");
            }

            return new List<string> { zipUrls[dateStr] };
        }

        /// <summary>
        /// Parses the HTML content of the index page and extracts ZIP URLs by date.
        /// </summary>
        /// <param name="html">HTML content of the index page.</param>
        /// <returns>Dictionary mapping date strings to ZIP URLs.</returns>
        private Dictionary<string, string> ParseIndexForZip(string html)
        {
            var result = new Dictionary<string, string>();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            
            // Look for links with ZIP files
            var nodes = doc.DocumentNode.SelectNodes("//a[contains(@href, '.zip')]");
            if (nodes == null) return result;

            foreach (var node in nodes)
            {
                var href = node.GetAttributeValue("href", string.Empty);
                if (string.IsNullOrEmpty(href)) continue;

                var fullUrl = href.StartsWith("http") ? href : BASE_URL + href;
                
                // Try to extract date from the URL or link text
                var linkText = node.InnerText?.Trim() ?? "";
                var dateMatch = Regex.Match(linkText + " " + href, @"(\d{4}-\d{2}-\d{2}|\d{2}\.\d{2}\.\d{4}|\d{8})");
                
                if (dateMatch.Success)
                {
                    var dateStr = ParseDateString(dateMatch.Value);
                    if (!string.IsNullOrEmpty(dateStr))
                    {
                        result[dateStr] = fullUrl;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Parses various date formats and returns a normalized date string.
        /// </summary>
        /// <param name="dateStr">Date string in various formats.</param>
        /// <returns>Normalized date string in yyyy-MM-dd format, or null if parsing fails.</returns>
        private string? ParseDateString(string dateStr)
        {
            // Try different date formats
            string[] formats = { "yyyy-MM-dd", "dd.MM.yyyy", "yyyyMMdd" };
            
            foreach (var format in formats)
            {
                if (DateTime.TryParseExact(dateStr, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    return date.ToString("yyyy-MM-dd");
                }
            }

            return null;
        }

        /// <summary>
        /// Downloads and extracts CSV files from a ZIP archive.
        /// </summary>
        /// <param name="zipUrl">URL of the ZIP file.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>List of tuples containing filename and CSV content.</returns>
        private async Task<List<(string filename, string csvContent)>> GetZipContents(string zipUrl, CancellationToken cancellationToken)
        {
            var result = new List<(string, string)>();

            try
            {
                var response = await _http.GetAsync(zipUrl, cancellationToken);
                response.EnsureSuccessStatusCode();

                using var zipStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

                foreach (var entry in archive.Entries)
                {
                    if (!entry.FullName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                        continue;

                    using var entryStream = entry.Open();
                    using var reader = new StreamReader(entryStream, System.Text.Encoding.UTF8);
                    var content = await reader.ReadToEndAsync();

                    result.Add((entry.FullName, content));
                    _logger.LogDebug($"Extracted CSV: {entry.FullName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract ZIP contents from: {ZipUrl}", zipUrl);
                throw;
            }

            return result;
        }

        /// <summary>
        /// Parses store information from CSV filename.
        /// Example: SUPERMARKET_SJEVERNA_VEZNA_CESTA_31_35000_SLAVONSKI_BROD_022_6_20052025014212.csv
        /// </summary>
        /// <param name="filename">CSV filename containing store information.</param>
        /// <returns>Parsed <see cref="StoreInfoDto"/> object, or null if parsing fails.</returns>
        private StoreInfoDto? ParseStoreFromFilename(string filename)
        {
            _logger.LogDebug($"Parsing store information from filename: {filename}");

            try
            {
                var match = FilenamePattern.Match(filename);
                if (!match.Success)
                {
                    _logger.LogWarning($"Failed to match filename pattern: {filename}");
                    return null;
                }

                var storeType = match.Groups[1].Value.ToLower();
                var streetAddress = match.Groups[2].Value.Replace("_", " ");
                var zipcode = match.Groups[3].Value;
                var city = match.Groups[4].Value.Replace("_", " ");
                var storeId = match.Groups[5].Value;

                // Apply title case formatting
                streetAddress = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(streetAddress.ToLower());
                city = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(city.ToLower());

                var store = new StoreInfoDto
                {
                    StoreId = storeId,
                    StoreType = storeType,
                    Name = $"Plodine {city}",
                    StreetAddress = streetAddress,
                    Zipcode = zipcode,
                    City = city
                };

                _logger.LogInformation(
                    $"Parsed store: {store.Name} ({store.StoreId}), {store.StoreType}, {store.City}, {store.StreetAddress}, {store.Zipcode}");

                return store;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to parse store from filename {filename}");
                return null;
            }
        }

        #endregion
    }
}
