using CijeneScraper.Models.Database;
using CijeneScraper.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CijeneScraper.Models.ViewModel;

namespace CijeneScraper.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PricesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PricesController(ApplicationDbContext context)
        {
            _context = context;
        }

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
            // Ensure dates are in UTC and distinct
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

        public class CheapestLocationViewModel
        {
            public string ProductName { get; set; }
            public string Address { get; set; }
            public string PostalCode { get; set; }
            public string City { get; set; }
            public decimal Price { get; set; }
            public DateOnly Date { get; set; }
        }

        // GET: api/Prices/CheapestLocation?barcode=1234567890123&date=2025-07-15
        [HttpGet("CheapestLocation")]
        public async Task<ActionResult<IEnumerable<CheapestLocationViewModel>>> GetCheapestLocation(string barcode, DateOnly? date = null)
        {
            if(barcode == null)
            {
                return BadRequest("Barcode parameter is required.");
            }

            if(date == null)
                date = DateOnly.FromDateTime(DateTime.UtcNow.Date);

            var prices = await _context.Prices
                .Include(p => p.ChainProduct)
                .Include(p => p.Store)
                .Where(p => p.ChainProduct.Barcode == barcode && p.Date == date.Value)
                .OrderBy(p => p.MPC ?? p.SpecialPrice)
                .Select(p => new CheapestLocationViewModel
                {
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

        // GET: api/Prices/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Price>> GetPrice(long id)
        {
            var price = await _context.Prices
                .Include(p => p.ChainProduct)
                .Include(p => p.Store)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (price == null)
            {
                return NotFound();
            }

            return price;
        }
    }
}
