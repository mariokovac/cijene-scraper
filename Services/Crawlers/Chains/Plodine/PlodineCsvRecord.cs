using CijeneScraper.Models.Crawler;
using CijeneScraper.Services.Crawlers.Common;
using CijeneScraper.Utility;
using CsvHelper.Configuration.Attributes;
using System.Globalization;

namespace CijeneScraper.Services.Crawlers.Chains.Plodine
{
    /// <summary>
    /// Represents a CSV record from Plodine price lists.
    /// Based on the field mapping from the Python crawler.
    /// </summary>
    public class PlodineCsvRecord : CsvRecordBase
    {
        [Name("Naziv proizvoda")]
        [Index(0)]
        public override string Product { get; set; } = string.Empty;

        [Name("Sifra proizvoda")]
        [Index(1)]
        public override string ProductCode { get; set; } = string.Empty;

        [Name("Marka proizvoda")]
        [Index(2)]
        public override string Brand { get; set; } = string.Empty;

        [Name("Neto kolicina")]
        [Index(3)]
        public override string Quantity { get; set; } = string.Empty;

        [Name("Jedinica mjere")]
        [Index(4)]
        public override string UOM { get; set; } = string.Empty;

        [Name("Barkod")]
        [Index(5)]
        public override string Barcode { get; set; } = string.Empty;

        [Name("Kategorija proizvoda")]
        [Index(6)]
        public string Category { get; set; } = string.Empty;

        [Name("Maloprodajna cijena")]
        [Index(7)]
        public override string Price { get; set; } = string.Empty;

        [Name("Cijena po JM")]
        [Index(8)]
        public override string PricePerUnit { get; set; } = string.Empty;

        [Name("MPC za vrijeme posebnog oblika prodaje")]
        [Index(9)]
        public override string SpecialPrice { get; set; } = string.Empty;

        [Name("Najniza cijena u poslj. 30 dana")]
        [Index(10)]
        public override string BestPrice30 { get; set; } = string.Empty;

        [Name("Sidrena cijena na 2.5.2025")]
        [Index(11)]
        public override string AnchorPrice { get; set; } = string.Empty;

        public static NumberFormatInfo NumberFormatInfo = new NumberFormatInfo()
        {
            NumberDecimalSeparator = ","
        };

        public override PriceInfo ToPriceInfo()
        {
            return new PriceInfo
            {
                ProductCode = ProductCode,
                Barcode = Barcode.NormalizeBarcode(ProductCode),
                Name = Product.Trim(),
                Price = decimal.TryParse(Price, NumberStyles.Any, NumberFormatInfo, out var price) ? price : (decimal?)null,
                Brand = Brand,
                UOM = UOM,
                Quantity = Quantity?.Trim(),
                PricePerUnit = decimal.TryParse(PricePerUnit, NumberStyles.Any, NumberFormatInfo, out var unitPrice) ? unitPrice : (decimal?)null,
                SpecialPrice = decimal.TryParse(SpecialPrice, NumberStyles.Any, NumberFormatInfo, out var specialPrice) ? specialPrice : (decimal?)null,
                BestPrice30 = decimal.TryParse(BestPrice30, NumberStyles.Any, NumberFormatInfo, out var bestPrice30) ? bestPrice30 : (decimal?)null,
                AnchorPrice = decimal.TryParse(AnchorPrice, NumberStyles.Any, NumberFormatInfo, out var anchorPrice) ? anchorPrice : (decimal?)null
            };
        }
    }
}