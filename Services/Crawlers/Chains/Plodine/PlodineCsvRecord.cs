using CijeneScraper.Models.Crawler;
using CijeneScraper.Utility;
using CsvHelper.Configuration.Attributes;
using System.Globalization;

namespace CijeneScraper.Services.Crawlers.Chains.Plodine
{
    /// <summary>
    /// Represents a CSV record from Plodine price lists.
    /// Based on the field mapping from the Python crawler.
    /// </summary>
    public class PlodineCsvRecord
    {
        [Name("Naziv proizvoda")]
        [Index(0)]
        public string Product { get; set; } = string.Empty;

        [Name("Sifra proizvoda")]
        [Index(1)]
        public string ProductCode { get; set; } = string.Empty;

        [Name("Marka proizvoda")]
        [Index(2)]
        public string Brand { get; set; } = string.Empty;

        [Name("Neto kolicina")]
        [Index(3)]
        public string Quantity { get; set; } = string.Empty;

        [Name("Jedinica mjere")]
        [Index(4)]
        public string Unit { get; set; } = string.Empty;

        [Name("Barkod")]
        [Index(5)]
        public string Barcode { get; set; } = string.Empty;

        [Name("Kategorija proizvoda")]
        [Index(6)]
        public string Category { get; set; } = string.Empty;

        [Name("Maloprodajna cijena")]
        [Index(7)]
        public string Price { get; set; } = string.Empty;

        [Name("Cijena po JM")]
        [Index(8)]
        public string UnitPrice { get; set; } = string.Empty;

        [Name("MPC za vrijeme posebnog oblika prodaje")]
        [Index(9)]
        public string SpecialPrice { get; set; } = string.Empty;

        [Name("Najniza cijena u poslj. 30 dana")]
        [Index(10)]
        public string BestPrice30 { get; set; } = string.Empty;

        [Name("Sidrena cijena na 2.5.2025")]
        [Index(11)]
        public string AnchorPrice { get; set; } = string.Empty;

        public static NumberFormatInfo NumberFormatInfo = new NumberFormatInfo()
        {
            NumberDecimalSeparator = ","
        };

        /// <summary>
        /// Explicit conversion operator to <see cref="PriceInfo"/>.
        /// </summary>
        /// <param name="p">The <see cref="PlodineCsvRecord"/> instance.</param>
        public static explicit operator PriceInfo(PlodineCsvRecord p)
        {
            return new PriceInfo
            {
                ProductCode = p.ProductCode,
                Barcode = p.Barcode.NormalizeBarcode(p.ProductCode),
                Name = p.Product.Trim(),
                Price = decimal.TryParse(p.Price, NumberStyles.Any, NumberFormatInfo, out var price) ? price : (decimal?)null,
                Brand = p.Brand,
                UOM = p.Unit,
                Quantity = p.Quantity?.Trim(),
                PricePerUnit = decimal.TryParse(p.UnitPrice, NumberStyles.Any, NumberFormatInfo, out var unitPrice) ? unitPrice : (decimal?)null,
                SpecialPrice = decimal.TryParse(p.SpecialPrice, NumberStyles.Any, NumberFormatInfo, out var specialPrice) ? specialPrice : (decimal?)null,
                BestPrice30 = decimal.TryParse(p.BestPrice30, NumberStyles.Any, NumberFormatInfo, out var bestPrice30) ? bestPrice30 : (decimal?)null,
                AnchorPrice = decimal.TryParse(p.AnchorPrice, NumberStyles.Any, NumberFormatInfo, out var anchorPrice) ? anchorPrice : (decimal?)null
            };
        }
    }
}