using CijeneScraper.Models.Crawler;
using CijeneScraper.Utility;
using CsvHelper.Configuration.Attributes;
using System.Globalization;

namespace CijeneScraper.Services.Crawlers.Chains.Spar
{
    /// <summary>
    /// Represents a CSV record from Spar price lists.
    /// CSV format: "naziv;šifra;marka;neto koli?ina;jedinica mjere;MPC (EUR);cijena za jedinicu mjere (EUR);MPC za vrijeme posebnog oblika prodaje (EUR);Najniža cijena u posljednjih 30 dana (EUR);sidrena cijena na 2.5.2025. (EUR);barkod;kategorija proizvoda"
    /// </summary>
    public class SparCsvRecord
    {
        [Name("naziv")]
        [Index(0)]
        public string Product { get; set; } = string.Empty;

        [Name("šifra")]
        [Index(1)]
        public string ProductCode { get; set; } = string.Empty;

        [Name("marka")]
        [Index(2)]
        public string Brand { get; set; } = string.Empty;

        [Name("neto koli?ina")]
        [Index(3)]
        public string Quantity { get; set; } = string.Empty;

        [Name("jedinica mjere")]
        [Index(4)]
        public string UOM { get; set; } = string.Empty;

        [Name("MPC (EUR)")]
        [Index(5)]
        public string Price { get; set; } = string.Empty;

        [Name("cijena za jedinicu mjere (EUR)")]
        [Index(6)]
        public string PricePerUnit { get; set; } = string.Empty;

        [Name("MPC za vrijeme posebnog oblika prodaje (EUR)")]
        [Index(7)]
        public string SpecialPrice { get; set; } = string.Empty;

        [Name("Najniža cijena u posljednjih 30 dana (EUR)")]
        [Index(8)]
        public string BestPrice30 { get; set; } = string.Empty;

        [Name("sidrena cijena na 2.5.2025. (EUR)")]
        [Index(9)]
        public string AnchorPrice { get; set; } = string.Empty;

        [Name("barkod")]
        [Index(10)]
        public string Barcode { get; set; } = string.Empty;

        [Name("kategorija proizvoda")]
        [Index(11)]
        public string Category { get; set; } = string.Empty;

        public static NumberFormatInfo NumberFormatInfo = new NumberFormatInfo()
        {
            NumberDecimalSeparator = ","
        };

        public static explicit operator PriceInfo(SparCsvRecord p)
        {
            return new PriceInfo
            {
                ProductCode = p.ProductCode,
                Barcode = p.Barcode.NormalizeBarcode(p.ProductCode),
                Name = p.Product.Trim(),
                Price = decimal.TryParse(p.Price, NumberStyles.Any, NumberFormatInfo, out var price) ? price : (decimal?)null,
                Brand = p.Brand?.Trim(),
                UOM = p.UOM?.Trim(),
                Quantity = p.Quantity?.Trim(),
                PricePerUnit = decimal.TryParse(p.PricePerUnit, NumberStyles.Any, NumberFormatInfo, out var unitPrice) ? unitPrice : (decimal?)null,
                SpecialPrice = decimal.TryParse(p.SpecialPrice, NumberStyles.Any, NumberFormatInfo, out var specialPrice) ? specialPrice : (decimal?)null,
                BestPrice30 = decimal.TryParse(p.BestPrice30, NumberStyles.Any, NumberFormatInfo, out var bestPrice30) ? bestPrice30 : (decimal?)null,
                AnchorPrice = decimal.TryParse(p.AnchorPrice, NumberStyles.Any, NumberFormatInfo, out var anchorPrice) ? anchorPrice : (decimal?)null
            };
        }
    }
}