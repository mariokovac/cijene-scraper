using CijeneScraper.Models.Crawler;
using CijeneScraper.Services.Crawlers.Common;
using CijeneScraper.Utility;
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CijeneScraper.Services.Crawlers.Chains.Konzum
{
    public class KonzumCsvRecord : CsvRecordBase
    {
        [Name("NAZIV PROIZVODA")]
        public override string Product { get; set; }

        [Name("ŠIFRA PROIZVODA")]
        public override string ProductCode { get; set; }

        [Name("MARKA PROIZVODA")]
        public override string Brand { get; set; }

        [Name("NETO KOLIČINA")]
        public override string Quantity { get; set; }

        [Name("JEDINICA MJERE")]
        public override string UOM { get; set; }

        [Name("BARKOD")]
        public override string Barcode { get; set; }

        [Name("KATEGORIJA PROIZVODA")]
        public string Category { get; set; }

        [Name("MALOPRODAJNA CIJENA")]
        public override string Price { get; set; }

        [Name("CIJENA ZA JEDINICU MJERE")]
        public override string PricePerUnit { get; set; }

        [Name("MPC ZA VRIJEME POSEBNOG OBLIKA PRODAJE")]
        public override string SpecialPrice { get; set; }

        [Name("NAJNIŽA CIJENA U POSLJEDNIH 30 DANA")]
        public override string BestPrice30 { get; set; }

        [Name("SIDRENA CIJENA NA 2.5.2025")]
        public override string AnchorPrice { get; set; }

        private static Regex _uomCleanupRegex = new Regex(@"[^\d\.]", RegexOptions.Compiled);

        public override PriceInfo ToPriceInfo()
        {
            return new PriceInfo
            {
                ProductCode = ProductCode,
                Barcode = Barcode.NormalizeBarcode(ProductCode),
                Name = Product.Trim(),
                Price = decimal.TryParse(Price, NumberStyles.Any, CultureInfo.InvariantCulture, out var price) ? price : (decimal?)null,
                Brand = Brand,
                UOM = UOM,
                Quantity = _uomCleanupRegex.Replace(Quantity, "").Trim(),
                PricePerUnit = decimal.TryParse(PricePerUnit, NumberStyles.Any, CultureInfo.InvariantCulture, out var unitPrice) ? unitPrice : (decimal?)null,
                SpecialPrice = decimal.TryParse(SpecialPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out var specialPrice) ? specialPrice : (decimal?)null,
                BestPrice30 = decimal.TryParse(BestPrice30, NumberStyles.Any, CultureInfo.InvariantCulture, out var bestPrice30) ? bestPrice30 : (decimal?)null,
                AnchorPrice = decimal.TryParse(AnchorPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out var anchorPrice) ? anchorPrice : (decimal?)null
            };
        }
    }
}
