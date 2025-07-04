using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CijeneScraper.Models
{
    public class PriceInfo : IPriceInfo
    {
        public string Barcode { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
    }
}
