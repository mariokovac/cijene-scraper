namespace CijeneScraper.Models.Response.Price
{
    public class PriceByBarcode
    {
        public DateOnly Date { get; set; }
        public string ChainName { get; set; }
        public string StoreName { get; set; }
        public string ProductName { get; set; }
        public decimal Price { get; set; }
    }
}
