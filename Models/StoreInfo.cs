using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CijeneScraper.Models
{
    public class StoreInfo
    {
        public string Name { get; internal set; }
        public string StreetAddress { get; internal set; }
        public string Zipcode { get; internal set; }
        public string City { get; internal set; }
    }
}
