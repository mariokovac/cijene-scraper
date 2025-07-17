using CijeneScraper.Models.Crawler;
using CijeneScraper.Services.Crawlers.Chains.Spar;
using CijeneScraper.Utility;

namespace CijeneScraper.Services.Crawlers.Common
{
    public abstract class CsvRecordBase
    {
        public abstract string ProductCode { get; set; }
        public abstract string Barcode { get; set; }
        public abstract string Product { get; set; }
        public abstract string Price { get; set; }
        public abstract string Brand { get; set; }
        public abstract string UOM { get; set; }
        public abstract string Quantity { get; set; }
        public abstract string PricePerUnit { get; set; }
        public abstract string SpecialPrice { get; set; }
        public abstract string BestPrice30 { get; set; }
        public abstract string AnchorPrice { get; set; }

        /// <summary>
        /// Converts CSV record to PriceInfo using consistent parsing logic
        /// </summary>
        /// <returns>PriceInfo object</returns>
        public abstract PriceInfo ToPriceInfo();
    }
}
