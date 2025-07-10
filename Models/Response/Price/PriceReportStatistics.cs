namespace CijeneScraper.Models.Response.Price
{
    public class PriceReportStatistics
    {
        public string ChainName { get; set; }
        public DateOnly Date { get; set; }
        public int NumPrices { get; set; }
    }
}
