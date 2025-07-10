using CijeneScraper.Data;
using CijeneScraper.Models.Response.Price;
using CijeneScraper.Models.Response.Product;
using CijeneScraper.Models.Response.Store;
using CijeneScraper.Services.Geocoding;
using CijeneScraper.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CijeneScraper.Controllers
{
    /// <summary>
    /// Controller for handling price-related API endpoints.
    /// </summary>
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class PricesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IGeocodingService _geocodingService;

        /// <summary>
        /// Initializes a new instance of the <see cref="PricesController"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="geocodingService">The geocoding service.</param>
        public PricesController(ApplicationDbContext context, IGeocodingService geocodingService)
        {
            _context = context;
            _geocodingService = geocodingService;
        }

        /// <summary>
        /// Retrieves a list of prices for the specified dates and optional chain.
        /// </summary>
        /// <param name="dates">Array of dates to filter prices (required).</param>
        /// <param name="take">Maximum number of results to return (default: 100).</param>
        /// <param name="chain">Optional chain name to filter results.</param>
        /// <returns>A list of <see cref="PriceInfo"/> objects matching the criteria.</returns>
        // GET: api/Prices
        public async Task<ActionResult<IEnumerable<PriceInfo>>> GetPrices(
            [FromQuery] DateOnly[] dates,
            int take = 100,
            string chain = null)
        {
            // Validate the dates parameter
            if (dates == null || dates.Length == 0)
            {
                return BadRequest("Dates parameter is required.");
            }
            // Ensure dates are distinct
            dates = dates.Distinct().ToArray();

            return await _context.Prices
                .Include(p => p.ChainProduct)
                .Include(p => p.Store)
                .Where(o => dates.Contains(o.Date))
                .Where(o => string.IsNullOrEmpty(chain) || o.ChainProduct.Chain.Name == chain)
                .Select(o => new PriceInfo
                {
                    Date = o.Date,
                    ChainName = o.ChainProduct.Chain.Name,
                    StoreName = o.Store.Address + ", " + o.Store.PostalCode + " " + o.Store.City,
                    ProductName = o.ChainProduct.Name,
                    Price = o.MPC ?? o.SpecialPrice ?? 0m
                })
                .Take(take)
                .ToListAsync();
        }

        /// <summary>
        /// Retrieves a list of prices for a specific product (by barcode) on a given date across all stores and chains.
        /// </summary>
        /// <param name="barcode">The product barcode (required).</param>
        /// <param name="date">
        /// The date for which to retrieve prices (optional, defaults to today if not provided).
        /// </param>
        /// <returns>
        /// A list of <see cref="PriceByBarcode"/> objects
        /// Returns <c>400 Bad Request</c> if the barcode parameter is missing.
        /// </returns>
        [HttpGet("ByBarcode")]
        public async Task<ActionResult<IEnumerable<PriceByBarcode>>> GetPricesByBarcodeForDay(
            [FromQuery] string barcode,
            [FromQuery] DateOnly? date = null)
        {
            // Validate the barcode parameter
            if (string.IsNullOrEmpty(barcode))
            {
                return BadRequest("Barcode parameter is required.");
            }
            // Use today's date if not provided
            if (date == null)
                date = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            var prices = await _context.Prices
                .Include(p => p.ChainProduct)
                .Include(p => p.Store)
                .Where(p => p.ChainProduct.Barcode == barcode && p.Date == date.Value)
                .Select(p => new PriceByBarcode
                {
                    Date = p.Date,
                    ChainName = p.ChainProduct.Chain.Name,
                    StoreName = p.Store.Address + ", " + p.Store.PostalCode + " " + p.Store.City,
                    ProductName = p.ChainProduct.Name,
                    Price = p.MPC ?? p.SpecialPrice ?? 0m
                })
                .Where(o => o.Price > 0)
                .ToListAsync();
            return prices;
        }

        /// <summary>
        /// Retrieves the locations with the lowest price for a product by barcode on a specific date.
        /// </summary>
        /// <param name="barcode">The product barcode (required).</param>
        /// <param name="date">The date to search for prices (optional, defaults to today).</param>
        /// <returns>A list of <see cref="CheapestStoreInfo"/> for the lowest price locations.</returns>
        // GET: api/Prices/CheapestLocation?barcode=1234567890123&date=2025-07-15
        [HttpGet("CheapestLocation")]
        public async Task<ActionResult<IEnumerable<CheapestStoreInfo>>> GetCheapestLocation(string barcode, DateOnly? date = null)
        {
            // Validate the barcode parameter
            if (barcode == null)
            {
                return BadRequest("Barcode parameter is required.");
            }

            // Use today's date if not provided
            if (date == null)
                date = DateOnly.FromDateTime(DateTime.UtcNow.Date);

            var prices = await _context.Prices
                .Include(p => p.ChainProduct)
                .Include(p => p.Store)
                .Include(p => p.Store.Chain)
                .Where(p => p.ChainProduct.Barcode == barcode && p.Date == date.Value)
                .OrderBy(p => p.MPC ?? p.SpecialPrice)
                .Select(p => new CheapestStoreInfo
                {
                    Chain = p.Store.Chain.Name,
                    Date = p.Date,
                    ProductName = p.ChainProduct.Name,
                    Address = p.Store.Address,
                    PostalCode = p.Store.PostalCode,
                    City = p.Store.City,
                    Price = p.MPC ?? p.SpecialPrice ?? 0m
                })
                .ToListAsync();

            // Find the lowest price
            var lowestPrice = prices.First().Price;

            // Return all locations with the lowest price
            return prices.Where(p => p.Price == lowestPrice).ToList();
        }

        /// <summary>
        /// Retrieves prices for the specified product names grouped by chain and product name for today's date,
        /// limiting the results to 5 stores per product unless a city is specified.
        /// </summary>
        /// <param name="productNames">List of product names (required).</param>
        /// <param name="city">Optional city name to filter results.</param>
        /// <returns>A dictionary where the key is the chain name, and the value is another dictionary with product names as keys and lists of prices as values.</returns>
        [HttpGet("ByProductNamesGrouped")]
        public async Task<ActionResult<Dictionary<string, Dictionary<string, List<PriceInfo>>>>> GetPricesByProductNamesGrouped(
            CancellationToken cancellationToken,
            [FromQuery] List<string> productNames,
            [FromQuery] string? city = null)
        {
            // Validate the productNames parameter
            if (productNames == null || productNames.Count == 0)
            {
                return BadRequest("ProductNames parameter is required.");
            }
        
            // Get today's date
            var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        
            // Convert product names to lowercase for case-insensitive comparison
            var lowerCaseProductNames = productNames.Select(name => name.ToLower()).ToList();

            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadUncommitted);

            // Query prices for the specified product names on today's date
            var pricesQuery = _context.Prices
                .Include(p => p.ChainProduct)
                .Include(p => p.Store)
                .AsNoTracking()
                .Where(p => lowerCaseProductNames.Any(name => p.ChainProduct.Name.ToLower().Contains(name)) && p.Date == today);
        
            // Apply city filter if provided
            if (!string.IsNullOrEmpty(city))
            {
                pricesQuery = pricesQuery.Where(p => p.Store.City.ToLower() == city.ToLower());
            }

            var q = pricesQuery
                .Select(p => new
                {
                    ChainName = p.ChainProduct.Chain.Name,
                    ProductName = p.ChainProduct.Name,
                    PriceViewModel = new PriceInfo
                    {
                        Date = p.Date,
                        ChainName = p.ChainProduct.Chain.Name,
                        StoreName = p.Store.Address + ", " + p.Store.PostalCode + " " + p.Store.City,
                        ProductName = p.ChainProduct.Name,
                        Price = p.MPC ?? p.SpecialPrice ?? 0m
                    }
                })
                .Where(p => p.PriceViewModel.Price > 0) // Filter prices with Amount > 0
                .OrderBy(p => p.PriceViewModel.Price); // Sort by Amount in ascending order;

            var prices = await q
                .ToListAsync(cancellationToken);
        
            // Group prices by ChainName and then by ProductName
            var groupedPrices = prices
                .GroupBy(p => p.ChainName)
                .ToDictionary(
                    chainGroup => chainGroup.Key,
                    chainGroup => chainGroup
                        .GroupBy(p => p.ProductName)
                        .ToDictionary(
                            productGroup => productGroup.Key,
                            productGroup => string.IsNullOrEmpty(city)
                                ? productGroup.Take(5).Select(p => p.PriceViewModel).ToList() // Apply limit if city is not provided
                                : productGroup.Select(p => p.PriceViewModel).ToList() // Ignore limit if city is provided
                        )
                );
        
            return groupedPrices;
        }

        public class PriceNearbyViewModel : PriceInfo
        {
            public double DistanceKm { get; set; }
        }

        /// <summary>
        /// Retrieves prices for specified item codes from stores near the provided GPS coordinates.
        /// </summary>
        /// <param name="codes">A list of item codes (Product.Id) to find prices for.</param>
        /// <param name="latitude">The user's latitude.</param>
        /// <param name="longitude">The user's longitude.</param>
        /// <param name="radiusKm">The search radius in kilometers (default: 5.0).</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A list of prices from nearby stores, ordered by distance.</returns>
        [HttpGet("ByCodesNearby")]
        public async Task<ActionResult<IEnumerable<PriceNearbyViewModel>>> GetPricesByCodesNearby(
            [FromQuery] List<long> codes,
            [FromQuery] double latitude,
            [FromQuery] double longitude,
            [FromQuery] double radiusKm = 5.0,
            CancellationToken cancellationToken = default)
        {
            if (codes == null || codes.Count == 0)
            {
                return BadRequest("Item codes parameter is required.");
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

            // This part of the query is executed in-memory after fetching all stores,
            // as distance calculation cannot be translated to SQL by default.
            // For larger datasets, consider a database with spatial support (e.g., PostGIS).
            var allStores = await _context.Stores.AsNoTracking().ToListAsync(cancellationToken);

            // Geocode stores with missing coordinates
            foreach (var store in allStores.Where(s => (s.Latitude == 0 || s.Longitude == 0) && !string.IsNullOrWhiteSpace(s.Address)))
            {
                var fullAddress = $"{store.Address}, {store.PostalCode} {store.City}";
                var geocodeResult = await _geocodingService.GeocodeAsync(fullAddress, cancellationToken);
                if (geocodeResult != null)
                {
                    store.Latitude = geocodeResult.Geometry.Location.Lat;
                    store.Longitude = geocodeResult.Geometry.Location.Lng;
                    _context.Update(store);
                }
            }
            await _context.SaveChangesAsync(cancellationToken);

            var nearbyStores = allStores
                .Select(s => new
                {
                    StoreId = s.Id,
                    Distance = GeoHelpers.CalculateDistance(latitude, longitude, s.Latitude, s.Longitude)
                })
                .Where(s => s.Distance <= radiusKm)
                .OrderBy(s => s.Distance)
                .ToList();

            var nearbyStoreIds = nearbyStores.Select(s => s.StoreId).ToList();
            if (nearbyStoreIds.Count == 0)
            {
                return Ok(new List<PriceNearbyViewModel>());
            }

            var prices = await _context.Prices
                .AsNoTracking()
                .Include(p => p.ChainProduct)
                .Include(p => p.ChainProduct.Chain)
                .Include(p => p.Store)
                .Where(p => p.Date == today &&
                            nearbyStoreIds.Contains(p.StoreId) &&
                            codes.Contains(p.ChainProduct.ProductId))
                .Select(p => new
                {
                    Price = p,
                    StoreId = p.StoreId
                })
                .ToListAsync(cancellationToken);

            var result = prices
                .Join(nearbyStores,
                      priceInfo => priceInfo.StoreId,
                      storeInfo => storeInfo.StoreId,
                      (priceInfo, storeInfo) => new PriceNearbyViewModel
                      {
                          Date = priceInfo.Price.Date,
                          ChainName = priceInfo.Price.ChainProduct.Chain.Name,
                          StoreName = priceInfo.Price.Store.Address + ", " + priceInfo.Price.Store.PostalCode + " " + priceInfo.Price.Store.City,
                          ProductName = priceInfo.Price.ChainProduct.Name,
                          Price = priceInfo.Price.MPC ?? priceInfo.Price.SpecialPrice ?? 0m,
                          DistanceKm = storeInfo.Distance
                      })
                .Where(p => p.Price > 0)
                .OrderBy(p => p.DistanceKm)
                .ThenBy(p => p.Price)
                .ToList();

            return Ok(result);
        }

        /// <summary>
        /// Searches for products and returns detailed information including price statistics.
        /// </summary>
        /// <param name="q">The search query to filter products by name.</param>
        /// <param name="datum">Optional date for price statistics, defaults to today.</param>
        /// <param name="chains">Optional list of chain names to filter results.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A list of products with their details and price statistics across specified chains.</returns>
        [HttpGet("SearchProducts")]
        public async Task<ActionResult<IEnumerable<ProductOverview>>> SearchProducts(
            [FromQuery] string q,
            [FromQuery] DateOnly? datum = null,
            [FromQuery] List<string>? chains = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest("Search query parameter 'q' is required.");
            }

            var searchDate = datum ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
            var lowerCaseQuery = q.ToLower();

            var query = _context.Products.AsNoTracking()
                .Where(p => p.Name.ToLower().Contains(lowerCaseQuery) || (p.Brand != null && p.Brand.ToLower().Contains(lowerCaseQuery)));

            var products = await query
                .Select(p => new
                {
                    Product = p,
                    ChainProducts = p.ChainProducts
                        .Where(cp => (chains == null || chains.Count == 0 || chains.Contains(cp.Chain.Name)))
                        .Select(cp => new
                        {
                            ChainProduct = cp,
                            Chain = cp.Chain,
                            Prices = cp.Prices
                                .Where(price => price.Date == searchDate && (price.MPC.HasValue || price.SpecialPrice.HasValue))
                                .Select(price => price.MPC ?? price.SpecialPrice ?? 0)
                        })
                        .Where(cp => cp.Prices.Any())
                })
                .Where(p => p.ChainProducts.Any())
                .ToListAsync(cancellationToken);

            var result = products.Select(a => new ProductOverview
            {
                Barcode = a.Product.Barcode,
                Brand = a.Product.Brand,
                Name = a.Product.Name,
                Chains = a.ChainProducts.Select(cp => new ProductChainInfo
                {
                    Chain = cp.Chain.Name,
                    StoreProductCode = cp.ChainProduct.Code,
                    Name = cp.ChainProduct.Name,
                    Brand = cp.ChainProduct.Brand,
                    Category = a.Product.Category,
                    PriceStatistics = new PriceStatistics
                    {
                        MinPrice = cp.Prices.Min(),
                        MaxPrice = cp.Prices.Max(),
                        AvgPrice = cp.Prices.Average()
                    }
                }).ToList()
            }).ToList();

            return Ok(result);
        }
    }
}
