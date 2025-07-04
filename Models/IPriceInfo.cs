using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CijeneScraper.Models
{
    public interface IPriceInfo
    {
        string Barcode { get; set; }
        string Name { get; set; }
        decimal Price { get; set; }
    }
}
