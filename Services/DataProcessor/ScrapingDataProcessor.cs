using CijeneScraper.Crawler;
using CijeneScraper.Data;
using CijeneScraper.Models.Crawler;
using CijeneScraper.Models.Database;
using CijeneScraper.Services.Geocoding;
using CijeneScraper.Utility;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace CijeneScraper.Services.DataProcessor
{
    /// <summary>
    /// Processes and persists scraping results into the database, handling chains, stores, products, and prices.
    /// </summary>
    public class ScrapingDataProcessor : IScrapingDataProcessor
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ScrapingDataProcessor> _logger;
        private readonly IGeocodingService _geocodingService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScrapingDataProcessor"/> class.
        /// </summary>
        /// <param name="scopeFactory">The service scope factory for dependency injection.</param>
        /// <param name="logger">The logger instance.</param>
        public ScrapingDataProcessor(
            IServiceScopeFactory scopeFactory,
            ILogger<ScrapingDataProcessor> logger,
            IGeocodingService geocodingService)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _geocodingService = geocodingService;
        }

        /// <summary>
        /// Processes the results of a scraping operation and updates the database accordingly.
        /// </summary>
        /// <param name="crawler">The crawler that produced the results.</param>
        /// <param name="results">A dictionary mapping store information to lists of price information.</param>
        /// <param name="date">The date for which the prices were scraped.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>The number of state entries written to the database as a result of the operation.</returns>
        public async Task<int> ProcessScrapingResultsAsync(
            ICrawler crawler,
            Dictionary<StoreInfo, List<PriceInfo>> results,
            DateOnly date,
            CancellationToken token)
        {
            int changesCount = 0;
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            using var transaction = await dbContext.Database.BeginTransactionAsync(token);
            try
            {
                var sw = Stopwatch.StartNew();

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
                changesCount = await ProcessPricesAsyncUltraFast(dbContext, chain, results, date, token);

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

            return changesCount;
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
        private async Task ProcessStoresAsync(
            ApplicationDbContext dbContext, 
            Chain chain, 
            IEnumerable<StoreInfo> storeInfos, 
            CancellationToken token)
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
                    var fullAddress = $"{storeInfo.Chain} {storeInfo.StreetAddress}, {storeInfo.PostalCode} {storeInfo.City}";
                    var geocodingResult = await _geocodingService.GeocodeAsync(fullAddress, token);

                    var geocodingResultCity = geocodingResult?.AddressComponents?.FirstOrDefault(ac => ac.Types.Contains("locality"))?.LongName;
                    var geocofingResultPostalCode = geocodingResult?.AddressComponents?.FirstOrDefault(ac => ac.Types.Contains("postal_code"))?.LongName;
                    //var geocodingAddress = geocodingResult?.AddressComponents?.FirstOrDefault(ac => ac.Types.Contains("route"))?.LongName;
                    //var geocodingAddressNumber = geocodingResult?.AddressComponents?.FirstOrDefault(ac => ac.Types.Contains("street_number"))?.LongName;
                    //geocodingAddress = geocodingAddressNumber != null 
                    //    ? $"{geocodingAddress} {geocodingAddressNumber}" 
                    //    : geocodingAddress;

                    store = new Store
                    {
                        Chain = chain,
                        Code = storeInfo.Code,
                        Address = storeInfo.StreetAddress,
                        City = geocodingResultCity ?? storeInfo.City,
                        PostalCode = geocofingResultPostalCode ?? storeInfo.PostalCode,
                        Latitude = geocodingResult?.Geometry.Location.Lat ?? 0.0,
                        Longitude = geocodingResult?.Geometry.Location.Lng ?? 0.0
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
        /// Ensures that a global Product exists for each unique product code/barcode, and links ChainProduct to Product.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="chain">The chain entity.</param>
        /// <param name="priceInfos">A collection of price information containing product data.</param>
        /// <param name="token">A cancellation token.</param>
        private async Task ProcessProductsAsync(
            ApplicationDbContext dbContext, 
            Chain chain, 
            IEnumerable<PriceInfo> priceInfos, 
            CancellationToken token)
        {
            var productCodes = priceInfos.Select(p => p.ProductCode).ToHashSet();

            // 1. Load existing global products by code or barcode
            var existingProducts = await dbContext.Products
                .Where(p => p.Barcode != null)
                .ToDictionaryAsync(p => p.Barcode!, token);

            // 2. Prepare new global products
            var newProducts = new List<Product>();
            foreach (var group in priceInfos.GroupBy(p => p.Barcode))
            {
                var barcode = group.Key;
                var first = group.First();

                if (!existingProducts.ContainsKey(barcode))
                {
                    var product = new Product
                    {
                        Barcode = barcode,
                        Name = StringHelpers.TrimToMaxLength(first.Name, 300),
                        Brand = first.Brand,
                        UOM = first.UOM,
                        Quantity = first.Quantity,
                        Category = null // TODO: Handle category
                    };
                    newProducts.Add(product);
                    existingProducts[barcode] = product;
                }
            }

            if (newProducts.Any())
            {
                dbContext.Products.AddRange(newProducts);
                await dbContext.SaveChangesAsync(token);
            }

            // 3. Load existing chain products
            var existingChainProducts = await dbContext.ChainProducts
                .Where(cp => cp.ChainId == chain.Id && productCodes.Contains(cp.Code))
                .ToDictionaryAsync(cp => cp.Code, token);

            var newChainProducts = new List<ChainProduct>();
            foreach (var group in priceInfos.GroupBy(p => p.ProductCode))
            {
                var productCode = group.Key;
                var first = group.First();

                if (!existingChainProducts.ContainsKey(productCode))
                {
                    var chainProduct = new ChainProduct
                    {
                        Chain = chain,
                        Code = productCode,
                        Name = first.Name,
                        Barcode = first.Barcode,
                        Brand = first.Brand,
                        UOM = first.UOM,
                        Quantity = first.Quantity,
                        ProductId = existingProducts[first.Barcode].Id // Poveži na Product
                    };
                    newChainProducts.Add(chainProduct);
                    existingChainProducts[productCode] = chainProduct;
                }
                else
                {
                    // Update existing chain product and ensure ProductId is set
                    var cp = existingChainProducts[productCode];
                    cp.Name = first.Name;
                    cp.Barcode = first.Barcode;
                    cp.Brand = first.Brand;
                    cp.UOM = first.UOM;
                    cp.Quantity = first.Quantity;
                    cp.ProductId = existingProducts[first.Barcode].Id;
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
        /// Optimized for large datasets with batch processing and minimal memory usage.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="chain">The chain entity.</param>
        /// <param name="results">A dictionary mapping store information to lists of price information.</param>
        /// <param name="date">The date for which the prices are being processed.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>The number of state entries written to the database.</returns>
        [Obsolete]
        private async Task<int> ProcessPricesAsync(ApplicationDbContext dbContext, Chain chain, Dictionary<StoreInfo, List<PriceInfo>> results, DateOnly date, CancellationToken token)
        {
            const int batchSize = 5000; // Increased batch size since we're only inserting

            var storeCodes = results.Keys.Select(s => s.Code).ToHashSet();
            var productCodes = results.Values.SelectMany(p => p.Select(pr => pr.ProductCode)).ToHashSet();

            // Load existing stores and products with single optimized queries
            var existingStores = await dbContext.Stores
                .AsNoTracking()
                .Where(s => s.ChainId == chain.Id && storeCodes.Contains(s.Code))
                .Select(s => new { s.Id, s.Code })
                .ToDictionaryAsync(s => s.Code, s => s.Id, token);

            var existingChainProducts = await dbContext.ChainProducts
                .AsNoTracking()
                .Where(cp => cp.ChainId == chain.Id && productCodes.Contains(cp.Code))
                .Select(cp => new { cp.Id, cp.Code })
                .ToDictionaryAsync(cp => cp.Code, cp => cp.Id, token);

            // Prepare all price entries for batch processing
            var allPriceEntries = new List<Price>();
            var skippedCount = 0;

            foreach (var storeInfo in results.Keys)
            {
                if (!existingStores.TryGetValue(storeInfo.Code, out var storeId))
                {
                    skippedCount++;
                    continue;
                }

                foreach (var priceInfo in results[storeInfo])
                {
                    if (!existingChainProducts.TryGetValue(priceInfo.ProductCode, out var chainProductId))
                    {
                        skippedCount++;
                        continue;
                    }

                    allPriceEntries.Add(new Price
                    {
                        StoreId = storeId,
                        ChainProductId = chainProductId,
                        Date = date,
                        MPC = priceInfo.Price,
                        PricePerUnit = priceInfo.PricePerUnit,
                        SpecialPrice = priceInfo.SpecialPrice,
                        BestPrice30 = priceInfo.BestPrice30,
                        AnchorPrice = priceInfo.AnchorPrice,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            if (skippedCount > 0)
            {
                _logger.LogWarning("│     ├─⚠️\tSkipped {SkippedCount} price entries due to missing stores or products", skippedCount);
            }

            _logger.LogInformation("│     ├─📊\tPrepared {TotalEntries} price entries for bulk insert", allPriceEntries.Count);

            if (allPriceEntries.Count == 0)
            {
                _logger.LogWarning("│     └─❌\tNo valid price entries to process");
                return 0;
            }

            // Process in batches for better memory management and progress tracking
            var totalChanges = 0;
            var processedCount = 0;
            var totalBatches = (allPriceEntries.Count + batchSize - 1) / batchSize;

            for (int i = 0; i < allPriceEntries.Count; i += batchSize)
            {
                var batch = allPriceEntries.Skip(i).Take(batchSize).ToList();

                // Bulk insert the batch
                dbContext.Prices.AddRange(batch);
                var batchChanges = await dbContext.SaveChangesAsync(token);

                totalChanges += batchChanges;
                processedCount += batch.Count;

                _logger.LogInformation("│     ├─⚡\tProcessed batch {CurrentBatch}/{TotalBatches} ({ProcessedCount}/{TotalCount} entries, {BatchChanges} changes)",
                    (i / batchSize) + 1, totalBatches, processedCount, allPriceEntries.Count, batchChanges);

                // Clear change tracker to free memory after each batch
                dbContext.ChangeTracker.Clear();

                // Optional: Brief pause every 10 batches to allow other operations
                if (i % (batchSize * 10) == 0 && i > 0)
                {
                    await Task.Delay(50, token);
                }
            }

            _logger.LogInformation("│     └─✅\tCompleted price processing: {TotalChanges} total changes", totalChanges);
            return totalChanges;
        }

        /// <summary>
        /// Ultra-fast version using bulk insert with raw SQL, respecting PostgreSQL parameter limits
        /// This is the recommended approach for cross-network scenarios
        /// </summary>
        private async Task<int> ProcessPricesAsyncUltraFast(ApplicationDbContext dbContext, Chain chain, Dictionary<StoreInfo, List<PriceInfo>> results, DateOnly date, CancellationToken token)
        {
            const int maxParametersPerStatement = 65000; // Leave some margin below 65535
            const int parametersPerRow = 9; // ChainProductId, StoreId, Date, MPC, PricePerUnit, SpecialPrice, BestPrice30, AnchorPrice, CreatedAt
            const int maxRowsPerBatch = maxParametersPerStatement / parametersPerRow; // ~7222 rows per batch

            var storeCodes = results.Keys.Select(s => s.Code).ToHashSet();
            var productCodes = results.Values.SelectMany(p => p.Select(pr => pr.ProductCode)).ToHashSet();

            // Load stores and products
            var existingStores = await dbContext.Stores
                .AsNoTracking()
                .Where(s => s.ChainId == chain.Id && storeCodes.Contains(s.Code))
                .Select(s => new { s.Id, s.Code })
                .ToDictionaryAsync(s => s.Code, s => s.Id, token);

            var existingChainProducts = await dbContext.ChainProducts
                .AsNoTracking()
                .Where(cp => cp.ChainId == chain.Id && productCodes.Contains(cp.Code))
                .Select(cp => new { cp.Id, cp.Code })
                .ToDictionaryAsync(cp => cp.Code, cp => cp.Id, token);

            // Prepare all price data first
            var allPriceData = new List<(long ChainProductId, long StoreId, DateOnly Date, decimal? MPC, decimal? PricePerUnit, decimal? SpecialPrice, decimal? BestPrice30, decimal? AnchorPrice, DateTime CreatedAt)>();

            foreach (var storeInfo in results.Keys)
            {
                if (!existingStores.TryGetValue(storeInfo.Code, out var storeId))
                    continue;

                foreach (var priceInfo in results[storeInfo])
                {
                    if (!existingChainProducts.TryGetValue(priceInfo.ProductCode, out var chainProductId))
                        continue;

                    allPriceData.Add((chainProductId, storeId, date, priceInfo.Price, priceInfo.PricePerUnit,
                        priceInfo.SpecialPrice, priceInfo.BestPrice30, priceInfo.AnchorPrice, DateTime.UtcNow));
                }
            }

            if (allPriceData.Count == 0)
            {
                _logger.LogWarning("│     └─❌\tNo valid price entries to process");
                return 0;
            }

            _logger.LogInformation("│     ├─📊\tPrepared {TotalEntries} price entries for bulk insert in batches of {MaxRows}",
                allPriceData.Count, maxRowsPerBatch);

            var totalChanges = 0;
            var processedCount = 0;
            var totalBatches = (allPriceData.Count + maxRowsPerBatch - 1) / maxRowsPerBatch;

            for (int i = 0; i < allPriceData.Count; i += maxRowsPerBatch)
            {
                var batch = allPriceData.Skip(i).Take(maxRowsPerBatch).ToList();

                // Prepare batch SQL with parameters
                var values = new List<string>();
                var parameters = new List<object?>();

                for (int j = 0; j < batch.Count; j++)
                {
                    var paramIndex = j * parametersPerRow;
                    values.Add($"(@p{paramIndex}, @p{paramIndex + 1}, @p{paramIndex + 2}, @p{paramIndex + 3}, @p{paramIndex + 4}, @p{paramIndex + 5}, @p{paramIndex + 6}, @p{paramIndex + 7}, @p{paramIndex + 8})");

                    var row = batch[j];
                    parameters.AddRange(new object?[] {
                        row.ChainProductId,   // @p{paramIndex}
                        row.StoreId,          // @p{paramIndex + 1}
                        row.Date,             // @p{paramIndex + 2}
                        row.MPC ?? (object?)DBNull.Value,              // @p{paramIndex + 3}
                        row.PricePerUnit ?? (object?)DBNull.Value,     // @p{paramIndex + 4}
                        row.SpecialPrice ?? (object?)DBNull.Value,     // @p{paramIndex + 5}
                        row.BestPrice30 ?? (object?)DBNull.Value,      // @p{paramIndex + 6}
                        row.AnchorPrice ?? (object?)DBNull.Value,      // @p{paramIndex + 7}
                        row.CreatedAt         // @p{paramIndex + 8}
                    });
                }

                // Execute batch insert
                var sql = $@"
                    INSERT INTO ""Prices"" (""ChainProductId"", ""StoreId"", ""Date"", ""MPC"", ""PricePerUnit"", ""SpecialPrice"", ""BestPrice30"", ""AnchorPrice"", ""CreatedAt"")
                    VALUES {string.Join(",", values)}";

                var batchChanges = await dbContext.Database.ExecuteSqlRawAsync(sql, parameters.ToArray(), token);
                totalChanges += batchChanges;
                processedCount += batch.Count;

                _logger.LogInformation("│     ├─⚡\tProcessed batch {CurrentBatch}/{TotalBatches} ({ProcessedCount}/{TotalCount} entries, {BatchChanges} changes)",
                    (i / maxRowsPerBatch) + 1, totalBatches, processedCount, allPriceData.Count, batchChanges);

                // Brief pause every 5 batches to allow other operations and reduce memory pressure
                if (i % (maxRowsPerBatch * 5) == 0 && i > 0)
                {
                    await Task.Delay(50, token);
                }
            }

            _logger.LogInformation("│     └─✅\tCompleted bulk insert: {TotalChanges} rows inserted in {TotalBatches} batches", totalChanges, totalBatches);
            return totalChanges;
        }
    }
}