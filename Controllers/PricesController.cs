using CijeneScraper.Models.Database;
using CijeneScraper.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CijeneScraper.Models.ViewModel;

namespace CijeneScraper.Controllers
{
    /// <summary>
    /// Controller for handling price-related API endpoints.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class PricesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="PricesController"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        public PricesController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Retrieves a list of prices for the specified dates and optional chain.
        /// </summary>
        /// <param name="dates">Array of dates to filter prices (required).</param>
        /// <param name="take">Maximum number of results to return (default: 100).</param>
        /// <param name="chain">Optional chain name to filter results.</param>
        /// <returns>A list of <see cref="PriceViewModel"/> objects matching the criteria.</returns>
        // GET: api/Prices
        public async Task<ActionResult<IEnumerable<PriceViewModel>>> GetPrices(
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
                .Select(o => new PriceViewModel
                {
                    Date = o.Date,
                    ChainName = o.ChainProduct.Chain.Name,
                    StoreName = o.Store.Address + ", " + o.Store.PostalCode + " " + o.Store.City,
                    ProductName = o.ChainProduct.Name,
                    Amount = o.MPC ?? o.SpecialPrice ?? 0m
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
        /// A list of <see cref="PriceByBarcodeViewModel"/> objects
        /// Returns <c>400 Bad Request</c> if the barcode parameter is missing.
        /// </returns>
        public async Task<ActionResult<IEnumerable<PriceByBarcodeViewModel>>> GetPricesByBarcodeForDay(
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
                .Select(p => new PriceByBarcodeViewModel
                {
                    Date = p.Date,
                    ChainName = p.ChainProduct.Chain.Name,
                    StoreName = p.Store.Address + ", " + p.Store.PostalCode + " " + p.Store.City,
                    ProductName = p.ChainProduct.Name,
                    Price = p.MPC ?? p.SpecialPrice ?? 0m
                })
                .ToListAsync();
            return prices;
        }

        /// <summary>
        /// Retrieves the locations with the lowest price for a product by barcode on a specific date.
        /// </summary>
        /// <param name="barcode">The product barcode (required).</param>
        /// <param name="date">The date to search for prices (optional, defaults to today).</param>
        /// <returns>A list of <see cref="CheapestLocationViewModel"/> for the lowest price locations.</returns>
        // GET: api/Prices/CheapestLocation?barcode=1234567890123&date=2025-07-15
        [HttpGet("CheapestLocation")]
        public async Task<ActionResult<IEnumerable<CheapestLocationViewModel>>> GetCheapestLocation(string barcode, DateOnly? date = null)
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
                .Select(p => new CheapestLocationViewModel
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
    }
}