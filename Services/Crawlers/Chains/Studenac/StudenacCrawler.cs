using CijeneScraper.Crawler;
using CijeneScraper.Models.Crawler;
using CijeneScraper.Services.Caching;
using CijeneScraper.Services.Crawlers.Common;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Serialization;

namespace CijeneScraper.Services.Crawlers.Chains.Studenac
{
    /// <summary>
    /// Crawler implementation for the Studenac chain. Retrieves and processes price lists from ZIP files containing XML data.
    /// </summary>
    public class StudenacCrawler : CrawlerBase
    {
        /// <summary>
        /// Chain identifier for Studenac.
        /// </summary>
        private const string CHAIN = "studenac";

        /// <summary>
        /// Base URL for Studenac Croatia.
        /// </summary>
        private const string BASE_URL = "https://www.studenac.hr";

        /// <summary>
        /// Path to the cache folder for Studenac data.
        /// </summary>
        private string cacheFolder = Path.Combine("cache", CHAIN);

        /// <summary>
        /// Gets the name of the store chain this crawler is associated with.
        /// </summary>
        public override string Chain => CHAIN;

        /// <summary>
        /// Initializes a new instance of the <see cref="StudenacCrawler"/> class.
        /// </summary>
        /// <param name="http">HTTP client for web requests.</param>
        /// <param name="cache">Cache provider for storing and retrieving data.</param>
        /// <param name="logger">Logger for logging information and errors.</param>
        public StudenacCrawler(HttpClient http, ICacheProvider cache, ILogger<StudenacCrawler> logger)
            : base(http, cache, logger) { }

        /// <summary>
        /// Crawls all Studenac stores and retrieves price information for the specified date.
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
            return await _crawlAndProcess(date, cancellationToken, async (store, products) =>
            {
                _logger.LogInformation($"Processed store: {store.StoreId}, Products: {products.Count}");
                await Task.CompletedTask;
            });
        }

        /// <summary>
        /// Asynchronously crawls all Studenac stores and saves the results to the specified output folder.
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
        /// Crawls and processes Studenac price lists for all stores for the specified date.
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
            Func<StoreInfoDto, List<StudenacXmlRecord>, Task>? onStoreProcessed = null)
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

            // Process all XML files in the ZIP
            var zipContents = await GetZipContents(zipUrl, cancellationToken);
            
            foreach (var (filename, xmlContent) in zipContents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var storeDataDto = ParseStoreFromXml(xmlContent);
                    if (storeDataDto == null || storeDataDto.Value.StoreInfo == null || !storeDataDto.Value.Products.Any())
                    {
                        _logger.LogWarning($"Skipping XML {filename} due to store/product parsing failure");
                        continue;
                    }

                    var storeData = storeDataDto.Value;
                    List<StudenacXmlRecord> products = null;

                    var storeFolder = Path.Combine(cacheFolder, CHAIN);
                    var fileName = $"{storeData.StoreInfo.StoreId}-{date:yyyy-MM-dd}";
                    var filePath = Path.Combine(storeFolder, fileName + _cache.Extension);
                    
                    if (_cache.Exists(filePath))
                    {
                        // If file already exists, read from it
                        _logger.LogInformation($"Using cached data for store {storeData.StoreInfo.StoreId} from {filePath}");
                        products = await ReadStorePricesXml(filePath);
                    }
                    else
                    {
                        // Otherwise, use parsed data from XML content
                        _logger.LogInformation($"Cache miss for store {storeData.StoreInfo.StoreId}, parsing XML");
                        products = storeData.Products;
                    }

                    // Add the store and products to the result dictionary
                    var storeInfo = new StoreInfo
                    {
                        Chain = Chain,
                        Code = storeData.StoreInfo.StoreId,
                        Name = storeData.StoreInfo.Name,
                        StreetAddress = storeData.StoreInfo.StreetAddress,
                        PostalCode = storeData.StoreInfo.Zipcode,
                        City = storeData.StoreInfo.City
                    };

                    result.Add(storeInfo, products.Select(p => p.ToPriceInfo()).ToList());

                    _logger.LogInformation($"Read {products.Count} products for store {storeData.StoreInfo.StoreId}");
                    if (onStoreProcessed != null)
                        await onStoreProcessed(storeData.StoreInfo, products);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process XML file: {Filename}", filename);
                }
            }

            _logger.LogInformation($"Crawled {result.Count} stores for {date:yyyy-MM-dd}");
            return result;
        }

        /// <summary>
        /// Reads store prices from a cached file.
        /// </summary>
        /// <param name="filePath">Path to the cached file.</param>
        /// <returns>List of <see cref="StudenacXmlRecord"/> objects.</returns>
        private async Task<List<StudenacXmlRecord>> ReadStorePricesXml(string filePath)
        {
            if (!_cache.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            // Read from cache
            var results = await _cache.ReadAsync<StudenacXmlRecord>(filePath);
            return results.ToList();
        }

        /// <summary>
        /// Retrieves a list of data source URLs (ZIP files) for the specified date from Studenac.
        /// </summary>
        /// <param name="date">The date for which to retrieve data source URLs.</param>
        /// <returns>A list of ZIP URLs as strings.</returns>
        protected override async Task<List<string>> getDatasourceUrls(DateOnly date)
        {
            // Studenac has a predictable URL pattern: PROIZVODI-YYYY-MM-DD.zip
            var dateString = date.ToString("yyyy-MM-dd");
            var zipUrl = $"{BASE_URL}/cjenici/PROIZVODI-{dateString}.zip";
            
            _logger.LogInformation($"Constructed ZIP URL for date {date}: {zipUrl}");
            
            // Return as list since the interface expects List<string>
            return new List<string> { zipUrl };
        }

        /// <summary>
        /// Downloads and extracts XML files from a ZIP archive.
        /// </summary>
        /// <param name="zipUrl">URL of the ZIP file.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>List of tuples containing filename and XML content.</returns>
        private async Task<List<(string filename, string xmlContent)>> GetZipContents(string zipUrl, CancellationToken cancellationToken)
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
                    if (!entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        continue;

                    using var entryStream = entry.Open();
                    using var reader = new StreamReader(entryStream, Encoding.UTF8);
                    var content = await reader.ReadToEndAsync();

                    result.Add((entry.FullName, content));
                    _logger.LogDebug($"Extracted XML: {entry.FullName}");
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
        /// Parses store information and products from XML content.
        /// </summary>
        /// <param name="xmlContent">XML content containing store and product data.</param>
        /// <returns>Parsed store data with info and products, or null if parsing fails.</returns>
        private (StoreInfoDto StoreInfo, List<StudenacXmlRecord> Products)? ParseStoreFromXml(string xmlContent)
        {
            try
            {
                var serializer = new XmlSerializer(typeof(StudenacProizvodiXml));
                using var reader = new StringReader(xmlContent);
                var proizvodiData = (StudenacProizvodiXml)serializer.Deserialize(reader);

                if (proizvodiData?.ProdajniObjekt == null)
                {
                    _logger.LogWarning("Failed to deserialize XML or missing ProdajniObjekt");
                    return null;
                }

                var objekt = proizvodiData.ProdajniObjekt;
                
                // Parse store information from XML data
                var storeInfo = ParseStoreInfo(objekt);
                if (storeInfo == null)
                {
                    _logger.LogWarning("Failed to parse store info from XML");
                    return null;
                }

                // Get products with valid product codes and ensure uniqueness per ProductCode
                // Only the last occurrence of each ProductCode is kept (similar to getUniqueRecordsFromCsv)
                var productDictionary = new Dictionary<string, StudenacXmlRecord>();
                var totalProducts = 0;
                
                foreach (var product in objekt.Proizvodi)
                {
                    totalProducts++;
                    if (!string.IsNullOrWhiteSpace(product.ProductCode))
                    {
                        // Dictionary automatically ensures uniqueness - the last record "wins"
                        productDictionary[product.ProductCode] = product;
                    }
                }
                
                var products = productDictionary.Values.ToList();
                var duplicatesRemoved = totalProducts - products.Count;

                _logger.LogInformation($"Parsed store: {storeInfo.Name} ({storeInfo.StoreId}), Total products: {totalProducts}, Unique products: {products.Count}, Duplicates removed: {duplicatesRemoved}");

                return (storeInfo, products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse XML content");
                return null;
            }
        }

        /// <summary>
        /// Parses store information from ProdajniObjekt XML element.
        /// </summary>
        /// <param name="objekt">ProdajniObjekt containing store data.</param>
        /// <returns>Parsed <see cref="StoreInfoDto"/> object, or null if parsing fails.</returns>
        private StoreInfoDto? ParseStoreInfo(StudenacProdajniObjekt objekt)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(objekt.Oznaka) || string.IsNullOrWhiteSpace(objekt.Adresa))
                {
                    _logger.LogWarning("Missing required store info: Oznaka or Adresa");
                    return null;
                }

                // Parse address format: "Obrtnička ulica 2 PREGRADA"
                var address = objekt.Adresa.Trim();
                var addressParts = address.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                
                if (addressParts.Length < 2)
                {
                    _logger.LogWarning($"Invalid address format: {address}");
                    return null;
                }

                // Extract city (usually the last word in uppercase)
                var city = addressParts.Last();
                
                // Everything except the city is considered the street address
                var streetAddress = string.Join(" ", addressParts.Take(addressParts.Length - 1));

                var storeInfo = new StoreInfoDto
                {
                    StoreId = objekt.Oznaka,
                    StoreType = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(objekt.Oblik?.ToLower() ?? "Supermarket"),
                    Name = $"Studenac {CultureInfo.CurrentCulture.TextInfo.ToTitleCase(city.ToLower())}",
                    StreetAddress = streetAddress,
                    City = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(city.ToLower()),
                    Zipcode = "" // Postal code not available in XML, could be geocoded later
                };

                _logger.LogInformation($"Parsed store: {storeInfo.Name} ({storeInfo.StoreId}), {storeInfo.City}, {storeInfo.StreetAddress}");

                return storeInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse store info from XML object");
                return null;
            }
        }

        #endregion
    }
}