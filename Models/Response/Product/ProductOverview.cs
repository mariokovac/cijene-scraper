namespace CijeneScraper.Models.Response.Product
{
    public class ProductOverview
    {
        public string? Barcode { get; set; }
        public string? Brand { get; set; }
        public string Name { get; set; }
        public List<ProductChainInfo> Chains { get; set; }
    }
}
