using CijeneScraper.Crawler;
using CijeneScraper.Data;
using CijeneScraper.Models;
using CijeneScraper.Models.Database;
using CijeneScraper.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CijeneScraper.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScraperController : ControllerBase
    {
        private readonly ScrapingQueue _queue;
        private readonly ILogger<ScraperController> _logger;
        private readonly Dictionary<string, ICrawler> _crawlers;
        private readonly ApplicationDbContext _dbContext;
        private readonly IServiceScopeFactory _scopeFactory;

        private const string outputFolder = "ScrapedData";

        /// <summary>
        /// Initializes a new instance of the <see cref="ScraperController"/> class.
        /// </summary>
        /// <param name="queue">The scraping queue for managing scraping tasks.</param>
        /// <param name="logger">The logger instance for logging information and errors.</param>
        /// <param name="crawlers">A collection of available crawlers, mapped by chain name.</param>
        public ScraperController(ScrapingQueue queue,
            ILogger<ScraperController> logger,
            IEnumerable<ICrawler> crawlers,
            ApplicationDbContext dbContext,
            IServiceScopeFactory scopeFactory
            )
        {
            _queue = queue;
            _logger = logger;
            _crawlers = crawlers.ToDictionary(
                c => c.Chain,
                StringComparer.OrdinalIgnoreCase);
            _dbContext = dbContext;
            _scopeFactory = scopeFactory;
        }

        /// <summary>
        /// Starts a scraping job for the specified chain and optional date.
        /// </summary>
        /// <param name="chain">The name of the chain to scrape.</param>
        /// <param name="date">The date for which to scrape data. If null, the current date is used.</param>
        /// <returns>
        /// Returns 202 Accepted if the scraping job is added to the queue,
        /// 400 Bad Request if the chain is unknown.
        /// </returns>
        [HttpPost("start/{chain}")]
        public IActionResult StartScraping(string chain, DateTime? date = null)
        {
            _logger.LogInformation("Scraping request received.");

            if (!_crawlers.TryGetValue(chain, out var crawler))
            {
                _logger.LogError("Unknown chain: {chain}", chain);
                return BadRequest($"Unknown chain: {chain}");
            }

            if (date == null)
            {
                date = DateTime.Now;
            }

            _queue.Enqueue(async token =>
            {
                var results = await crawler.CrawlAsync(outputFolder, date, token);

                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                DateTime isoDate = date.Value.Date.ToUniversalTime();

                using var transaction = await dbContext.Database.BeginTransactionAsync(token);
                try
                {
                    // 1. Učitaj ili stvori chain
                    var chain = await dbContext.Chains
                        .FirstOrDefaultAsync(c => c.Name == crawler.Chain, token)
                        ?? dbContext.Chains.Add(new Chain { Name = crawler.Chain }).Entity;

                    await dbContext.SaveChangesAsync(token); // Potrebno za chain.Id

                    // 2. Pripremi kolekcije za bulk operacije
                    var storeCodes = results.Keys.Select(s => s.Code).ToHashSet();
                    var productCodes = results.Values.SelectMany(p => p.Select(pr => pr.ProductCode)).ToHashSet();

                    // 3. Učitaj postojeće entitete odjednom
                    var existingStores = await dbContext.Stores
                        .Where(s => s.ChainId == chain.Id && storeCodes.Contains(s.Code))
                        .ToDictionaryAsync(s => s.Code, token);

                    var existingChainProducts = await dbContext.ChainProducts
                        .Where(cp => cp.ChainId == chain.Id && productCodes.Contains(cp.Code))
                        .ToDictionaryAsync(cp => cp.Code, token);

                    var existingPrices = await dbContext.Prices
                        .Where(p => p.Store.ChainId == chain.Id &&
                                   storeCodes.Contains(p.Store.Code) &&
                                   productCodes.Contains(p.ChainProduct.Code) &&
                                   p.Date == isoDate)
                        .Select(p => new { StoreCode = p.Store.Code, ProductCode = p.ChainProduct.Code, Price = p })
                        .ToDictionaryAsync(x => $"{x.StoreCode}_{x.ProductCode}", x => x.Price, token);

                    // 4. Pripremi nove entitete
                    var newStores = new List<Store>();
                    var newChainProducts = new List<ChainProduct>();
                    var newPrices = new List<Price>();
                    var updatedPrices = new List<Price>();

                    // 5. Obradi stores
                    foreach (var storeInfo in results.Keys)
                    {
                        if (!existingStores.TryGetValue(storeInfo.Code, out var store))
                        {
                            store = new Store
                            {
                                Chain = chain,
                                Code = storeInfo.Code,
                                Address = storeInfo.StreetAddress,
                                City = storeInfo.City,
                                PostalCode = storeInfo.PostalCode
                            };
                            newStores.Add(store);
                            existingStores[storeInfo.Code] = store;
                        }
                        else
                        {
                            // Ažuriraj postojeći store
                            store.Address = storeInfo.StreetAddress;
                            store.City = storeInfo.City;
                            store.PostalCode = storeInfo.PostalCode;
                        }
                    }

                    // 6. Dodaj nove stores
                    if (newStores.Any())
                    {
                        dbContext.Stores.AddRange(newStores);
                        await dbContext.SaveChangesAsync(token); // Potrebno za store.Id
                    }

                    // 7. Obradi chain products
                    foreach (var priceInfo in results.Values.SelectMany(p => p).GroupBy(p => p.ProductCode))
                    {
                        var productCode = priceInfo.Key;
                        var firstPrice = priceInfo.First();

                        if (!existingChainProducts.ContainsKey(productCode))
                        {
                            var chainProduct = new ChainProduct
                            {
                                Chain = chain,
                                Code = productCode,
                                Name = firstPrice.Name,
                                Barcode = firstPrice.Barcode,
                                Brand = firstPrice.Brand,
                                UOM = firstPrice.UOM,
                                Quantity = firstPrice.Quantity
                            };
                            newChainProducts.Add(chainProduct);
                            existingChainProducts[productCode] = chainProduct;
                        }
                    }

                    // 8. Dodaj nove chain products
                    if (newChainProducts.Any())
                    {
                        dbContext.ChainProducts.AddRange(newChainProducts);
                        await dbContext.SaveChangesAsync(token); // Potrebno za chainProduct.Id
                    }

                    // 9. Obradi prices
                    foreach (var storeInfo in results.Keys)
                    {
                        var store = existingStores[storeInfo.Code];

                        foreach (var priceInfo in results[storeInfo])
                        {
                            var chainProduct = existingChainProducts[priceInfo.ProductCode];
                            var priceKey = $"{store.Code}_{chainProduct.Code}";

                            if (!existingPrices.TryGetValue(priceKey, out var existingPrice))
                            {
                                var newPrice = new Price
                                {
                                    Store = store,
                                    ChainProduct = chainProduct,
                                    Date = isoDate,
                                    MPC = priceInfo.Price,
                                    PricePerUnit = priceInfo.PricePerUnit,
                                    SpecialPrice = priceInfo.SpecialPrice,
                                    BestPrice30 = priceInfo.BestPrice30,
                                    AnchorPrice = priceInfo.AnchorPrice
                                };
                                newPrices.Add(newPrice);
                            }
                            else
                            {
                                // Ažuriraj postojeći price
                                existingPrice.MPC = priceInfo.Price;
                                existingPrice.PricePerUnit = priceInfo.PricePerUnit;
                                existingPrice.SpecialPrice = priceInfo.SpecialPrice;
                                existingPrice.BestPrice30 = priceInfo.BestPrice30;
                                existingPrice.AnchorPrice = priceInfo.AnchorPrice;
                                updatedPrices.Add(existingPrice);
                            }
                        }
                    }

                    // 10. Dodaj nove prices
                    if (newPrices.Any())
                    {
                        dbContext.Prices.AddRange(newPrices);
                    }

                    // 11. Spremi sve promjene odjednom
                    int changes = await dbContext.SaveChangesAsync(token);
                    await transaction.CommitAsync(token);

                    _logger.LogInformation("Scraping completed successfully. Changes made: {changes}", changes);
                }
                catch
                {
                    await transaction.RollbackAsync(token);
                    throw;
                }
            });

            return Accepted("Scraping job added to the queue.");
        }
    }
}