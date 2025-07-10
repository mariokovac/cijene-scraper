using CijeneScraper.Models;
using CijeneScraper.Utility;
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CijeneScraper.Services.Crawlers.Chains.Konzum
{
    public class KonzumCsvRecord
    {
        [Name("NAZIV PROIZVODA")]
        public string Product { get; set; }

        [Name("ŠIFRA PROIZVODA")]
        public string ProductCode { get; set; }

        [Name("MARKA PROIZVODA")]
        public string Brand { get; set; }

        [Name("NETO KOLIČINA")]
        public string Quantity { get; set; }

        [Name("JEDINICA MJERE")]
        public string Unit { get; set; }

        [Name("BARKOD")]
        public string Barcode { get; set; }

        [Name("KATEGORIJA PROIZVODA")]
        public string Category { get; set; }

        [Name("MALOPRODAJNA CIJENA")]
        public string Price { get; set; }

        [Name("CIJENA ZA JEDINICU MJERE")]
        public string UnitPrice { get; set; }

        [Name("MPC ZA VRIJEME POSEBNOG OBLIKA PRODAJE")]
        public string SpecialPrice { get; set; }

        [Name("NAJNIŽA CIJENA U POSLJEDNIH 30 DANA")]
        public string BestPrice30 { get; set; }

        [Name("SIDRENA CIJENA NA 2.5.2025")]
        public string AnchorPrice { get; set; }

        private static Regex _uomCleanupRegex = new Regex(@"[^\d\.]", RegexOptions.Compiled);

        /// <summary>
        /// Explicit conversion operator to <see cref="PriceInfo"/>.
        /// Converts only relevant fields: Barcode, Name, and Price.
        /// </summary>
        /// <param name="p">The <see cref="KonzumCsvRecord"/> instance.</param>
        public static explicit operator PriceInfo(KonzumCsvRecord p)
        {
            return new PriceInfo
            {
                ProductCode = p.ProductCode,
                Barcode = p.Barcode.NormalizeBarcode(p.ProductCode),
                Name = p.Product.Trim(),
                Price = decimal.TryParse(p.Price, NumberStyles.Any, CultureInfo.InvariantCulture, out var price) ? price : (decimal?)null,
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
