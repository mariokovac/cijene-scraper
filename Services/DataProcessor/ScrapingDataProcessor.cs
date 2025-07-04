using CijeneScraper.Crawler;
using CijeneScraper.Data;
using CijeneScraper.Models;
using CijeneScraper.Models.Database;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace CijeneScraper.Services.DataProcessor
{
    public class ScrapingDataProcessor : IScrapingDataProcessor
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ScrapingDataProcessor> _logger;

        public ScrapingDataProcessor(
            IServiceScopeFactory scopeFactory,
            ILogger<ScrapingDataProcessor> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task ProcessScrapingResultsAsync(
            ICrawler crawler, 
            Dictionary<StoreInfo, List<PriceInfo>> results,
            DateOnly date, 
            CancellationToken token)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            using var transaction = await dbContext.Database.BeginTransactionAsync(token);
            try
            {
                var sw = Stopwatch.StartNew();
                sw.Start();

                _logger.LogInformation("┌───────────────────────────────────────────────────────────────");
                _logger.LogInformation("│ ▶️\tStarting DB update for chain: {ChainName} on date: {Date:yyyy-MM-dd}", crawler.Chain, date);
                _logger.LogInformation("├───────────────────────────────────────────────────────────────");

                // 1. Ensure chain exists
                var chain = await EnsureChainExistsAsync(dbContext, crawler.Chain, token);
                _logger.LogInformation("│   ├─✅\tEnsured chain exists: {ChainName}", chain.Name);

                // 2. Process stores
                await ProcessStoresAsync(dbContext, chain, results.Keys, token);
                _logger.LogInformation("│   ├─🏬\tProcessed stores for chain: {ChainName}", chain.Name);

                // 3. Process products
                await ProcessProductsAsync(dbContext, chain, results.Values.SelectMany(p => p), token);
                _logger.LogInformation("│   ├─📦\tProcessed products for chain: {ChainName}", chain.Name);

                // 4. Process prices
                var changesCount = await ProcessPricesAsync(dbContext, chain, results, date, token);
                _logger.LogInformation("│   └─💲\tProcessed prices for chain: {ChainName}", crawler.Chain);

                await transaction.CommitAsync(token);

                sw.Stop();
                _logger.LogInformation("├───────────────────────────────────────────────────────────────");
                _logger.LogInformation("│ ✅\tDB update completed for chain: {ChainName} on date: {Date:yyyy-MM-dd}. Time taken: {ElapsedMilliseconds}",
                    crawler.Chain, date, sw.Elapsed.ToString(@"hh\:mm\:ss"));
                _logger.LogInformation("└───────────────────────────────────────────────────────────────");
            }
            catch
            {
                await transaction.RollbackAsync(token);
                throw;
            }
            finally
            {
                var memoryBefore = GC.GetTotalMemory(false);
                _logger.LogInformation("Memory usage at {Stage}: {Memory:N0} bytes", "Before", memoryBefore);

                results.Clear(); // Clear results to free memory
                results = null; // Allow GC to collect
                dbContext.ChangeTracker.Clear();

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect(); // Collect again to ensure all memory is freed

                var memoryAfter = GC.GetTotalMemory(false);
                _logger.LogInformation("Memory usage at {Stage}: {Memory:N0} bytes", "After", memoryAfter);
            }
        }

        private async Task<Chain> EnsureChainExistsAsync(ApplicationDbContext dbContext, string chainName, CancellationToken token)
        {
            var chain = await dbContext.Chains
                .FirstOrDefaultAsync(c => c.Name == chainName, token);

            if (chain == null)
            {
                chain = new Chain { Name = chainName };
                dbContext.Chains.Add(chain);
                await dbContext.SaveChangesAsync(token);
            }

            return chain;
        }

        private async Task ProcessStoresAsync(ApplicationDbContext dbContext, Chain chain, IEnumerable<StoreInfo> storeInfos, CancellationToken token)
        {
            var storeCodes = storeInfos.Select(s => s.Code).ToHashSet();

            var existingStores = await dbContext.Stores
                .Where(s => s.ChainId == chain.Id && storeCodes.Contains(s.Code))
                .ToDictionaryAsync(s => s.Code, token);

            var newStores = new List<Store>();

            foreach (var storeInfo in storeInfos)
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
                }
                else
                {
                    // Update existing store
                    store.Address = storeInfo.StreetAddress;
                    store.City = storeInfo.City;
                    store.PostalCode = storeInfo.PostalCode;
                }
            }

            if (newStores.Any())
            {
                dbContext.Stores.AddRange(newStores);
                await dbContext.SaveChangesAsync(token);
            }
        }

        private async Task ProcessProductsAsync(ApplicationDbContext dbContext, Chain chain, IEnumerable<PriceInfo> priceInfos, CancellationToken token)
        {
            var productCodes = priceInfos.Select(p => p.ProductCode).ToHashSet();

            var existingChainProducts = await dbContext.ChainProducts
                .Where(cp => cp.ChainId == chain.Id && productCodes.Contains(cp.Code))
                .ToDictionaryAsync(cp => cp.Code, token);

            var newChainProducts = new List<ChainProduct>();

            foreach (var priceInfo in priceInfos.GroupBy(p => p.ProductCode))
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

            if (newChainProducts.Any())
            {
                dbContext.ChainProducts.AddRange(newChainProducts);
                await dbContext.SaveChangesAsync(token);
            }
        }

        private async Task<int> ProcessPricesAsync(ApplicationDbContext dbContext, Chain chain, Dictionary<StoreInfo, List<PriceInfo>> results, DateOnly date, CancellationToken token)
        {
            var storeCodes = results.Keys.Select(s => s.Code).ToHashSet();
            var productCodes = results.Values.SelectMany(p => p.Select(pr => pr.ProductCode)).ToHashSet();

            // Load existing entities
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
                           p.Date == date)
                .Select(p => new { StoreCode = p.Store.Code, ProductCode = p.ChainProduct.Code, Price = p })
                .ToDictionaryAsync(x => $"{x.StoreCode}_{x.ProductCode}", x => x.Price, token);

            var newPrices = new List<Price>();

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
                            Date = date,
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
                        // Update existing price
                        existingPrice.MPC = priceInfo.Price;
                        existingPrice.PricePerUnit = priceInfo.PricePerUnit;
                        existingPrice.SpecialPrice = priceInfo.SpecialPrice;
                        existingPrice.BestPrice30 = priceInfo.BestPrice30;
                        existingPrice.AnchorPrice = priceInfo.AnchorPrice;
                    }
                }
            }

            if (newPrices.Any())
            {
                dbContext.Prices.AddRange(newPrices);
            }

            return await dbContext.SaveChangesAsync(token);
        }
    }
}
