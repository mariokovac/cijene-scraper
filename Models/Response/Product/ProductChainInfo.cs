using CijeneScraper.Models.Response.Price;

namespace CijeneScraper.Models.Response.Product
{
    public class ProductChainInfo
    {
        public string Chain { get; set; }
        public string StoreProductCode { get; set; }
        public string Name { get; set; }
        public string? Brand { get; set; }
        public string? Category { get; set; }
        public PriceStatistics PriceStatistics { get; set; }
    }
}
