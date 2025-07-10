using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CijeneScraper.Models.Crawler
{
    public class PriceInfo
    {
        public string ProductCode { get; set; } = null!;
        public string Barcode { get; set; } = null!;
        public string Name { get; set; } = null!;
        public decimal? Price { get; set; }
        public string? Brand { get; set; }
        public string? UOM { get; set; }
        public string? Quantity { get; set; }
        public decimal? PricePerUnit { get; set; }
        public decimal? SpecialPrice { get; set; }
        public decimal? BestPrice30 { get; set; }
        public decimal? AnchorPrice { get; set; }
    }
}
