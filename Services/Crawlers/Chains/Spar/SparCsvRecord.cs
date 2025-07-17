using CijeneScraper.Models.Crawler;
using CijeneScraper.Services.Crawlers.Common;
using CijeneScraper.Utility;
using CsvHelper.Configuration.Attributes;
using System.Globalization;

namespace CijeneScraper.Services.Crawlers.Chains.Spar
{
    /// <summary>
    /// Represents a CSV record from Spar price lists.
    /// CSV format: "naziv;šifra;marka;neto količina;jedinica mjere;MPC (EUR);cijena za jedinicu mjere (EUR);MPC za vrijeme posebnog oblika prodaje (EUR);Najniža cijena u posljednjih 30 dana (EUR);sidrena cijena na 2.5.2025. (EUR);barkod;kategorija proizvoda"
    /// </summary>
    public class SparCsvRecord : CsvRecordBase
    {
        [Name("naziv")]
        [Index(0)]
        public override string Product { get; set; } = string.Empty;

        [Name("šifra")]
        [Index(1)]
        public override string ProductCode { get; set; } = string.Empty;

        [Name("marka")]
        [Index(2)]
        public override string Brand { get; set; } = string.Empty;

        [Name("neto količina")]
        [Index(3)]
        public override string Quantity { get; set; } = string.Empty;

        [Name("jedinica mjere")]
        [Index(4)]
        public override string UOM { get; set; } = string.Empty;

        [Name("MPC (EUR)")]
        [Index(5)]
        public override string Price { get; set; } = string.Empty;

        [Name("cijena za jedinicu mjere (EUR)")]
        [Index(6)]
        public override string PricePerUnit { get; set; } = string.Empty;

        [Name("MPC za vrijeme posebnog oblika prodaje (EUR)")]
        [Index(7)]
        public override string SpecialPrice { get; set; } = string.Empty;

        [Name("Najniža cijena u posljednjih 30 dana (EUR)")]
        [Index(8)]
        public override string BestPrice30 { get; set; } = string.Empty;

        [Name("sidrena cijena na 2.5.2025. (EUR)")]
        [Index(9)]
        public override string AnchorPrice { get; set; } = string.Empty;

        [Name("barkod")]
        [Index(10)]
        public override string Barcode { get; set; } = string.Empty;

        [Name("kategorija proizvoda")]
        [Index(11)]
        public string Category { get; set; } = string.Empty;

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
                Brand = Brand?.Trim(),
                UOM = UOM?.Trim(),
                Quantity = Quantity?.Trim(),
                PricePerUnit = decimal.TryParse(PricePerUnit, NumberStyles.Any, NumberFormatInfo, out var unitPrice) ? unitPrice : (decimal?)null,
                SpecialPrice = decimal.TryParse(SpecialPrice, NumberStyles.Any, NumberFormatInfo, out var specialPrice) ? specialPrice : (decimal?)null,
                BestPrice30 = decimal.TryParse(BestPrice30, NumberStyles.Any, NumberFormatInfo, out var bestPrice30) ? bestPrice30 : (decimal?)null,
                AnchorPrice = decimal.TryParse(AnchorPrice, NumberStyles.Any, NumberFormatInfo, out var anchorPrice) ? anchorPrice : (decimal?)null
            };
        }
    }
}