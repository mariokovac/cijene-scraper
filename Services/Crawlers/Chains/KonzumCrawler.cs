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

namespace CijeneScraper.Crawler.Chains
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
            this.cacheFolder = Path.Combine(outputFolder);

            var masterFileName = $"{CHAIN}-master-{date:yyyy-MM-dd}";
            string masterFilePath = Path.Combine(cacheFolder, masterFileName + _cache.Extension);

            // if master file already exists, skip processing and read from it
            if (_cache.Exists(masterFilePath))
            {
                Console.WriteLine($"Master file already exists: {masterFileName}");
                return await readChainMasterCsv(masterFilePath);
            }

            var data = await _crawlAndProcess(date, cancellationToken, async (store, products) =>
            {
                var storeFolder = Path.Combine(cacheFolder, CHAIN);
                var fileName = $"{store.StoreId}-{date:yyyy-MM-dd}";

                await _cache.SaveAsync(storeFolder, fileName, products, cancellationToken);

                //var rows = new List<string[]>();
                //rows.Add(new[] { 
                //    nameof(ParsedProductInfo.Product),
                //    nameof(ParsedProductInfo.ProductId),
                //    nameof(ParsedProductInfo.Brand),
                //    nameof(ParsedProductInfo.Quantity),
                //    nameof(ParsedProductInfo.Unit),
                //    nameof(ParsedProductInfo.Barcode),
                //    nameof(ParsedProductInfo.Category),
                //    nameof(ParsedProductInfo.Price),
                //    nameof(ParsedProductInfo.UnitPrice),
                //    nameof(ParsedProductInfo.SpecialPrice),
                //    nameof(ParsedProductInfo.BestPrice30),
                //    nameof(ParsedProductInfo.AnchorPrice)
                //    });
                //rows.AddRange(products.Select(p => new[] { p.Product, p.ProductId, p.Brand, p.Quantity, p.Unit, p.Barcode, p.Category, p.Price, p.UnitPrice, p.SpecialPrice, p.BestPrice30, p.AnchorPrice }));

                //SaveCsv(storeFolder, fileName, rows);
                Console.WriteLine($"Saved {products.Count} products for store {store.StoreId}.");
            });

            // create master CSV file with all stores and their products
            var masterCsvRows = new List<string[]>();
            masterCsvRows.Add(new[] { 
                nameof(FlattenInfo.Store), 
                nameof(FlattenInfo.StreetAddress),
                nameof(FlattenInfo.Zipcode),
                nameof(FlattenInfo.City),
                nameof(FlattenInfo.Product),
                nameof(FlattenInfo.Barcode),
                nameof(FlattenInfo.Price)
            });
            foreach (var kvp in data)
            {
                var store = kvp.Key;
                var products = kvp.Value;
                foreach (var product in products)
                {
                    masterCsvRows.Add(new[]
                    {
                        store.Name, store.StreetAddress, store.Zipcode, store.City,
                        product.Name, product.Barcode, product.Price.ToString(CultureInfo.InvariantCulture)
                    });
                }
            }
            SaveCsv(outputFolder, masterFileName, masterCsvRows);
            Console.WriteLine($"Saved master CSV with {masterCsvRows.Count - 1} products for all stores.");

            return data;
        }

        private async Task<Dictionary<StoreInfo, List<PriceInfo>>> _crawlAndProcess(
            DateTime? date = null,
            CancellationToken cancellationToken = default,
            Action<ParsedStoreInfo, List<ParsedProductInfo>>? onProcessed = null)
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
                    ParsedStoreInfo store = ParseStoreInfo(url);
                    List<ParsedProductInfo> products = null;

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
                    if (onProcessed != null)
                        onProcessed(store, products);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error processing {url}: {ex.Message}");
                }
            }

            return result;
        }

        private void transformToResult(Dictionary<StoreInfo, List<PriceInfo>> result, 
            ParsedStoreInfo store, List<ParsedProductInfo> products)
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

        private async Task<List<ParsedProductInfo>> readStorePricesCsv(string filePath)
        {
            if (!_cache.Exists(filePath))
            {
                throw new FileNotFoundException($"CSV file not found: {filePath}");
            }

            //var result = new List<ParsedProductInfo>();
            // Read from cache
            var results = await _cache.ReadAsync<ParsedProductInfo>(filePath); // Ensure cache is read
            return results.ToList();

            //using var reader = new StreamReader(filePath);
            //using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { MissingFieldFound = null, BadDataFound = null });

            //var results = new List<ParsedProductInfo>();
            //await csv.ReadAsync();
            //csv.ReadHeader();

            //while (await csv.ReadAsync())
            //{
            //    var p = new ParsedProductInfo
            //    {
            //        Product = csv.GetField(nameof(ParsedProductInfo.Product)),
            //        ProductId = csv.GetField(nameof(ParsedProductInfo.ProductId)),
            //        Brand = csv.GetField(nameof(ParsedProductInfo.Brand)),
            //        Quantity = csv.GetField(nameof(ParsedProductInfo.Quantity)),
            //        Unit = csv.GetField(nameof(ParsedProductInfo.Unit)),
            //        Barcode = csv.GetField(nameof(ParsedProductInfo.Barcode)),
            //        Category = csv.GetField(nameof(ParsedProductInfo.Category)),
            //        Price = csv.TryGetField(nameof(ParsedProductInfo.Price), out string price) ? price : string.Empty,
            //        UnitPrice = csv.GetField(nameof(ParsedProductInfo.UnitPrice)),
            //        SpecialPrice = csv.GetField(nameof(ParsedProductInfo.SpecialPrice)),
            //        BestPrice30 = csv.GetField(nameof(ParsedProductInfo.BestPrice30)),
            //        AnchorPrice = csv.GetField(nameof(ParsedProductInfo.AnchorPrice))
            //    };
            //    results.Add(p);
            //}
            //return results;
        }

        private async Task<Dictionary<StoreInfo, List<PriceInfo>>> readChainMasterCsv(string filePath)
        {

            if (!_cache.Exists(filePath))
            {
                throw new FileNotFoundException($"Master CSV file not found: {filePath}");
            }
            var results = await _cache.ReadAsync<FlattenInfo>(filePath); // Ensure cache is read

            //using var reader = new StreamReader(filePath);
            //using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { MissingFieldFound = null, BadDataFound = null });

            //var results = new List<FlattenInfo>();
            //await csv.ReadAsync();
            //csv.ReadHeader();

            //while (await csv.ReadAsync())
            //{
            //    var p = new FlattenInfo
            //    {
            //        Store = csv.GetField(nameof(FlattenInfo.Store)),
            //        StreetAddress = csv.GetField(nameof(FlattenInfo.StreetAddress)),
            //        Zipcode = csv.GetField(nameof(FlattenInfo.Zipcode)),
            //        City = csv.GetField(nameof(FlattenInfo.City)),
            //        Product = csv.GetField(nameof(FlattenInfo.Product)),
            //        Barcode = csv.GetField(nameof(FlattenInfo.Barcode)),
            //        Price = csv.TryGetField<decimal>(nameof(FlattenInfo.Price), out decimal price) ? price : 0m
            //    };
            //    results.Add(p);
            //}

            // Convert flat result to dictionary of StoreInfo and List<PriceInfo>
            return results
                .GroupBy(f => new { f.Store, f.StreetAddress, f.Zipcode, f.City })
                .ToDictionary(
                    grp => new StoreInfo
                    {
                        Name = grp.Key.Store,
                        StreetAddress = grp.Key.StreetAddress,
                        Zipcode = grp.Key.Zipcode,
                        City = grp.Key.City
                    },
                    grp => grp.Select(f => new PriceInfo
                    {
                        Name = f.Product,
                        Barcode = f.Barcode,
                        Price = f.Price
                    }).ToList()
                );
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

        private ParsedStoreInfo ParseStoreInfo(string url)
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

            return new ParsedStoreInfo
            {
                StoreId = storeId,
                StoreType = storeType,
                Name = $"{CHAIN} {city}",
                StreetAddress = street,
                Zipcode = zipcode,
                City = city
            };
        }

        private async Task<List<ParsedProductInfo>> GetStorePrices(string csvUrl)
        {
            var csvText = await FetchTextAsync(csvUrl);
            using var reader = new StringReader(csvText);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { MissingFieldFound = null, BadDataFound = null });

            var results = new List<ParsedProductInfo>();
            await csv.ReadAsync();
            csv.ReadHeader();

            while (await csv.ReadAsync())
            {
                var p = new ParsedProductInfo
                {
                    Product = csv.GetField("NAZIV PROIZVODA"),
                    ProductId = csv.GetField("ŠIFRA PROIZVODA"),
                    Brand = csv.GetField("MARKA PROIZVODA"),
                    Quantity = csv.GetField("NETO KOLIČINA"),
                    Unit = csv.GetField("JEDINICA MJERE"),
                    Barcode = csv.GetField("BARKOD"),
                    Category = csv.GetField("KATEGORIJA PROIZVODA"),
                    Price = csv.TryGetField("MALOPRODAJNA CIJENA", out string pr) ? pr : string.Empty,
                    UnitPrice = csv.GetField("CIJENA ZA JEDINICU MJERE"),
                    SpecialPrice = csv.GetField("MPC ZA VRIJEME POSEBNOG OBLIKA PRODAJE"),
                    BestPrice30 = csv.GetField("NAJNIŽA CIJENA U POSLJEDNIH 30 DANA"),
                    AnchorPrice = csv.GetField("SIDRENA CIJENA NA 2.5.2025")
                };
                results.Add(p);
            }
            return results;
        }

        private class ParsedStoreInfo
        {
            public string StoreId { get; set; }
            public string StoreType { get; set; }
            public string Name { get; set; }
            public string StreetAddress { get; set; }
            public string Zipcode { get; set; }
            public string City { get; set; }
        }

        private class ParsedProductInfo
        {
            public string Product { get; set; }
            public string ProductId { get; set; }
            public string Brand { get; set; }
            public string Quantity { get; set; }
            public string Unit { get; set; }
            public string Barcode { get; set; }
            public string Category { get; set; }
            public string Price { get; set; }
            public string UnitPrice { get; set; }
            public string SpecialPrice { get; set; }
            public string BestPrice30 { get; set; }
            public string AnchorPrice { get; set; }

            // Explicit operator to convert to IPriceInfo
            public static explicit operator PriceInfo(ParsedProductInfo p)
            {
                return new PriceInfo
                {
                    Barcode = p.Barcode,
                    Name = p.Product,
                    Price = decimal.TryParse(p.Price, NumberStyles.Any, CultureInfo.InvariantCulture, out var price) ? price : 0m
                };
            }
        }

        private class FlattenInfo
        {
            public string Store, StreetAddress, Zipcode, City, Product, Barcode;
            public decimal Price;

            public FlattenInfo() { }
            public FlattenInfo(StoreInfo store, PriceInfo product)
            {
                Store = store.Name;
                StreetAddress = store.StreetAddress;
                Zipcode = store.Zipcode;
                City = store.City;
                Product = product.Name;
                Barcode = product.Barcode;
                Price = product.Price;
            }
        }
    }
}
