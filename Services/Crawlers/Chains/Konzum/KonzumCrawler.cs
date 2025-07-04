using CijeneScraper.Crawler;
using CijeneScraper.Models;
using CijeneScraper.Services.Caching;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using HtmlAgilityPack;
using Microsoft.AspNetCore.WebUtilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CijeneScraper.Services.Crawlers.Chains.Konzum
{
    public class KonzumCrawler : CrawlerBase
    {
        private const string CHAIN = "konzum";
        private const string BASE_URL = "https://www.konzum.hr";
        private const string INDEX_URL = BASE_URL + "/cjenici";
        private static readonly Regex AddressPattern = new Regex(@"^(.*)\s+(\d{5})\s+(.*)$", RegexOptions.Compiled);

        private string cacheFolder = Path.Combine("cache", CHAIN);

        public override string Chain { get => CHAIN; }

        public KonzumCrawler(HttpClient http, ICacheProvider cache) : base(http, cache) { }

        public override async Task<Dictionary<StoreInfo, List<PriceInfo>>> Crawl(DateTime? date = null, CancellationToken cancellationToken = default)
        {
            return await _crawlAndProcess(date, cancellationToken, (store, products) =>
            {
                Console.WriteLine($"Store: {store.StoreId}, Products: {products.Count}");
            });
        }

        public override async Task<Dictionary<StoreInfo, List<PriceInfo>>> CrawlAsync(
            string outputFolder, 
            DateTime? date = null, 
            CancellationToken cancellationToken = default)
        {
            cacheFolder = Path.Combine(outputFolder);

            var data = await _crawlAndProcess(date, cancellationToken, async (store, products) =>
            {
                var storeFolder = Path.Combine(cacheFolder, CHAIN);
                var fileName = $"{store.StoreId}-{date:yyyy-MM-dd}";

                await _cache.SaveAsync(storeFolder, fileName, products, cancellationToken);

                Console.WriteLine($"Saved {products.Count} products for store {store.StoreId}.");
            });

            return data;
        }

        private async Task<Dictionary<StoreInfo, List<PriceInfo>>> _crawlAndProcess(
            DateTime? date = null,
            CancellationToken cancellationToken = default,
            Action<StoreInfoDto, List<ProductInfoDto>>? onStoreProcessed = null)
        {
            var crawlDate = date ?? DateTime.UtcNow.Date;
            var result = new Dictionary<StoreInfo, List<PriceInfo>>();

            var csvUrls = await GetIndexUrls(crawlDate);
            if (!csvUrls.Any())
            {
                Console.WriteLine($"No price list found for {crawlDate:yyyy-MM-dd}");
                return new Dictionary<StoreInfo, List<PriceInfo>>();
            }

            foreach (var url in csvUrls)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    StoreInfoDto store = ParseStoreInfo(url);
                    List<ProductInfoDto> products = null;

                    var storeFolder = Path.Combine(cacheFolder, CHAIN);
                    var fileName = $"{store.StoreId}-{date:yyyy-MM-dd}";
                    var filePath = Path.Combine(storeFolder, fileName + _cache.Extension);
                    if (_cache.Exists(filePath))
                    {
                        // If file already exists, read from it
                        Console.WriteLine($"Using cached data for store {store.StoreId} from {filePath}");
                        products = await readStorePricesCsv(filePath);
                        transformToResult(result, store, products);

                        continue;
                    }
                    else
                    {
                        // Otherwise, fetch from the URL
                        Console.WriteLine($"Fetching data for store {store.StoreId} from {url}");
                        products = await GetStorePrices(url);
                    }

                    // Adding the store and products to the result dictionary
                    transformToResult(result, store, products);

                    Console.WriteLine($"Read {products.Count} products for store {store.StoreId}.");
                    if (onStoreProcessed != null)
                        onStoreProcessed(store, products);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error processing {url}: {ex.Message}");
                }
            }

            return result;
        }

        private void transformToResult(Dictionary<StoreInfo, List<PriceInfo>> result, 
            StoreInfoDto store, List<ProductInfoDto> products)
        {
            result.Add(
                new StoreInfo
                {
                    Name = store.Name,
                    StreetAddress = store.StreetAddress,
                    Zipcode = store.Zipcode,
                    City = store.City
                },
                products.Select(p => (PriceInfo)p).ToList()
            );
        }

        private async Task<List<ProductInfoDto>> readStorePricesCsv(string filePath)
        {
            if (!_cache.Exists(filePath))
            {
                throw new FileNotFoundException($"CSV file not found: {filePath}");
            }

            // Read from cache
            var results = await _cache.ReadAsync<ProductInfoDto>(filePath); // Ensure cache is read
            return results.ToList();
        }

        private async Task<List<string>> GetIndexUrls(DateTime date)
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

        private async Task<List<ProductInfoDto>> GetStorePrices(string csvUrl)
        {
            var csvText = await FetchTextAsync(csvUrl);
            using var reader = new StringReader(csvText);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { MissingFieldFound = null, BadDataFound = null });

            var results = new List<ProductInfoDto>();
            await csv.ReadAsync();
            csv.ReadHeader();

            while (await csv.ReadAsync())
            {
                var p = new ProductInfoDto
                {
                    Product = csv.GetField("NAZIV PROIZVODA"),
                    ProductId = csv.GetField("ŠIFRA PROIZVODA"),
                    Brand = csv.GetField("MARKA PROIZVODA"),
                    Quantity = csv.GetField("NETO KOLIČINA"),
                    Unit = csv.GetField("JEDINICA MJERE"),
                    Barcode = csv.GetField("BARKOD"),
                    Category = csv.GetField("KATEGORIJA PROIZVODA"),
                    Price = csv.TryGetField("MALOPRODAJNA CIJENA", out string pr) 
                        ? pr 
                        : csv.TryGetField("MPC ZA VRIJEME POSEBNOG OBLIKA PRODAJE", out string mpcpob) ? mpcpob 
                        : string.Empty,
                    UnitPrice = csv.GetField("CIJENA ZA JEDINICU MJERE"),
                    SpecialPrice = csv.GetField("MPC ZA VRIJEME POSEBNOG OBLIKA PRODAJE"),
                    BestPrice30 = csv.GetField("NAJNIŽA CIJENA U POSLJEDNIH 30 DANA"),
                    AnchorPrice = csv.GetField("SIDRENA CIJENA NA 2.5.2025")
                };
                results.Add(p);
            }
            return results;
        }
    }
}
