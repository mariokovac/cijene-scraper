using CijeneScraper.Crawler;
using CijeneScraper.Data;
using CijeneScraper.Models;
using CijeneScraper.Models.Database;
using CijeneScraper.Utility;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace CijeneScraper.Services.DataProcessor
{
    /// <summary>
    /// Processes and persists scraping results into the database, handling chains, stores, products, and prices.
    /// </summary>
    public class ScrapingDataProcessor : IScrapingDataProcessor
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ScrapingDataProcessor> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScrapingDataProcessor"/> class.
        /// </summary>
        /// <param name="scopeFactory">The service scope factory for dependency injection.</param>
        /// <param name="logger">The logger instance.</param>
        public ScrapingDataProcessor(
            IServiceScopeFactory scopeFactory,
            ILogger<ScrapingDataProcessor> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <summary>
        /// Processes the results of a scraping operation and updates the database accordingly.
        /// </summary>
        /// <param name="crawler">The crawler that produced the results.</param>
        /// <param name="results">A dictionary mapping store information to lists of price information.</param>
        /// <param name="date">The date for which the prices were scraped.</param>
        /// <param name="token">A cancellation token.</param>
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
                using var timer = new TimedOperation(_logger, "DB update");

                _logger.LogInformation("┌───────────────────────────────────────────────────────────────");
                _logger.LogInformation("│ ▶️\tStarting DB update for chain: {ChainName} on date: {Date:yyyy-MM-dd}", crawler.Chain, date);
                _logger.LogInformation("├───────────────────────────────────────────────────────────────");

                // 1. Delete old prices for the date
                _logger.LogInformation("│   ├─🗑️\t[1/5]\tDeleting old prices for date: {Date:yyyy-MM-dd}", date);
                await DeletePricesForDate(dbContext, date, crawler.Chain, token);

                // 2. Ensure chain exists
                _logger.LogInformation("│   ├─✅\t[2/5]\tEnsuring chain exists: {ChainName}", crawler.Chain);
                var chain = await EnsureChainExistsAsync(dbContext, crawler.Chain, token);

                // 3. Process stores
                _logger.LogInformation("│   ├─🏬\t[3/5]\tProcessing stores for chain: {ChainName}", crawler.Chain);
                await ProcessStoresAsync(dbContext, chain, results.Keys, token);

                // 4. Process products
                _logger.LogInformation("│   ├─📦\t[4/5]\tProcessing products for chain: {ChainName}", crawler.Chain);
                await ProcessProductsAsync(dbContext, chain, results.Values.SelectMany(p => p), token);

                // 5. Process prices
                _logger.LogInformation("│   └─💲\t[5/5]\tProcessing prices for chain: {ChainName}", crawler.Chain);
                var changesCount = await ProcessPricesAsync(dbContext, chain, results, date, token);

                _logger.LogInformation("│   └─✅\tCommiting transaction");
                await transaction.CommitAsync(token);

                _logger.LogInformation("├───────────────────────────────────────────────────────────────");
                _logger.LogInformation("│ ✅\tDB update completed for chain: {ChainName} on date: {Date:yyyy-MM-dd} (Changes: {ChangesCount}).",
                    crawler.Chain, date, changesCount);
                _logger.LogInformation("└───────────────────────────────────────────────────────────────");
            }
            catch
            {
                await transaction.RollbackAsync(token);
                throw;
            }
            finally
            {
                // Clear results to free memory
                results.Clear();
                results = null; // Allow garbage collection
                dbContext.ChangeTracker.Clear();

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect(); // Collect again to ensure all memory is freed
            }
        }

        /// <summary>
        /// Deletes all price records for a specific date and chain from the database.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="date">The date for which to delete prices.</param>
        /// <param name="chainName">The name of the chain.</param>
        /// <param name="token">A cancellation token.</param>
        private async Task DeletePricesForDate(ApplicationDbContext dbContext, DateOnly date, string chainName, CancellationToken token)
        {
            await dbContext.Prices
                .Where(p => p.Date == date && p.Store.Chain.Name == chainName)
                .ExecuteDeleteAsync(token);
        }

        /// <summary>
        /// Ensures that a chain with the specified name exists in the database, creating it if necessary.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="chainName">The name of the chain.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>The <see cref="Chain"/> entity.</returns>
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

        /// <summary>
        /// Processes and updates store information for a chain, adding new stores or updating existing ones.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="chain">The chain entity.</param>
        /// <param name="storeInfos">A collection of store information.</param>
        /// <param name="token">A cancellation token.</param>
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
                    // Update existing store with new address, city, and postal code
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

        /// <summary>
        /// Processes and updates product information for a chain, adding new products as needed.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="chain">The chain entity.</param>
        /// <param name="priceInfos">A collection of price information containing product data.</param>
        /// <param name="token">A cancellation token.</param>
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

        /// <summary>
        /// Processes and updates price information for a chain, adding new prices or updating existing ones.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="chain">The chain entity.</param>
        /// <param name="results">A dictionary mapping store information to lists of price information.</param>
        /// <param name="date">The date for which the prices are being processed.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>The number of state entries written to the database.</returns>
        private async Task<int> ProcessPricesAsync(ApplicationDbContext dbContext, Chain chain, Dictionary<StoreInfo, List<PriceInfo>> results, DateOnly date, CancellationToken token)
        {
            var storeCodes = results.Keys.Select(s => s.Code).ToHashSet();
            var productCodes = results.Values.SelectMany(p => p.Select(pr => pr.ProductCode)).ToHashSet();

            // Load existing stores for the chain
            var existingStores = await dbContext.Stores
                .Where(s => s.ChainId == chain.Id && storeCodes.Contains(s.Code))
                .ToDictionaryAsync(s => s.Code, token);

            // Load existing products for the chain
            var existingChainProducts = await dbContext.ChainProducts
                .Where(cp => cp.ChainId == chain.Id && productCodes.Contains(cp.Code))
                .ToDictionaryAsync(cp => cp.Code, token);

            // Load existing prices for the given date, stores, and products
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
                        // Add new price entry
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
                        // Update existing price entry
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