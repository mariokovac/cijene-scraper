using CijeneScraper.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CijeneScraper.Services.Crawlers.Chains.Konzum
{
    /// <summary>
    /// Data transfer object representing product information scraped from Konzum.
    /// </summary>
    internal class ProductInfoDto
    {
        /// <summary>Product name.</summary>
        public string Product { get; set; }

        /// <summary>Unique product identifier.</summary>
        public string ProductCode { get; set; }

        /// <summary>Brand of the product.</summary>
        public string Brand { get; set; }

        /// <summary>Quantity of the product (e.g., "1", "500").</summary>
        public string Quantity { get; set; }

        /// <summary>Unit of measure (e.g., "g", "ml", "kom").</summary>
        public string Unit { get; set; }

        /// <summary>Product barcode.</summary>
        public string Barcode { get; set; }

        /// <summary>Product category.</summary>
        public string Category { get; set; }

        /// <summary>Product price as string (should be parseable to decimal).</summary>
        public string Price { get; set; }

        /// <summary>Unit price as string (should be parseable to decimal).</summary>
        public string UnitPrice { get; set; }

        /// <summary>Special price as string (should be parseable to decimal).</summary>
        public string SpecialPrice { get; set; }

        /// <summary>Best price in the last 30 days as string (should be parseable to decimal).</summary>
        public string BestPrice30 { get; set; }

        /// <summary>Anchor price as string (should be parseable to decimal).</summary>
        public string AnchorPrice { get; set; }

        private static Regex _uomCleanupRegex = new Regex(@"[^\d\.]", RegexOptions.Compiled);

        /// <summary>
        /// Explicit conversion operator to <see cref="PriceInfo"/>.
        /// Converts only relevant fields: Barcode, Name, and Price.
        /// </summary>
        /// <param name="p">The <see cref="ProductInfoDto"/> instance.</param>
        public static explicit operator PriceInfo(ProductInfoDto p)
        {
            return new PriceInfo
            {
                ProductCode = p.ProductCode,
                Barcode = p.Barcode,
                Name = p.Product,
                Price = decimal.TryParse(p.Price, NumberStyles.Any, CultureInfo.InvariantCulture, out var price) ? price : 0m,
                Brand = p.Brand,
                UOM = p.Unit,
                Quantity = _uomCleanupRegex.Replace(p.Quantity, "").Trim(),
                PricePerUnit = decimal.TryParse(p.UnitPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out var unitPrice) ? unitPrice : (decimal?)null,
                SpecialPrice = decimal.TryParse(p.SpecialPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out var specialPrice) ? specialPrice : (decimal?)null,
                BestPrice30 = decimal.TryParse(p.BestPrice30, NumberStyles.Any, CultureInfo.InvariantCulture, out var bestPrice30) ? bestPrice30 : (decimal?)null,
                AnchorPrice = decimal.TryParse(p.AnchorPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out var anchorPrice) ? anchorPrice : (decimal?)null
            };
        }
    }
}
