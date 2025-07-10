namespace CijeneScraper.Models.Response.Store
{
    public class CheapestStoreInfo
    {
        public string Chain { get; set; }
        public string ProductName { get; set; }
        public string Address { get; set; }
        public string PostalCode { get; set; }
        public string City { get; set; }
        public decimal Price { get; set; }
        public DateOnly Date { get; set; }
    }
}
