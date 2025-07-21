using CijeneScraper.Models.Crawler;
using CijeneScraper.Services.Crawlers.Common;
using CijeneScraper.Utility;
using CsvHelper.Configuration.Attributes;
using System.Globalization;

namespace CijeneScraper.Services.Crawlers.Chains.Lidl
{
    /// <summary>
    /// Represents a CSV record from Lidl price lists.
    /// CSV columns: NAZIV, ŠIFRA, NETO_KOLIČINA, JEDINICA_MJERE, MARKA, MALOPRODAJNA_CIJENA, 
    /// MPC_ZA_VRIJEME_POSEBNOG_OBLIKA_PRODAJE, NAJNIZA_CIJENA_U_POSLJ._30_DANA, 
    /// CIJENA_ZA_JEDINICU_MJERE, BARKOD, KATEGORIJA_PROIZVODA, Sidrena_cijena_na_02.05.2025
    /// </summary>
    public class LidlCsvRecord : CsvRecordBase
    {
        [Name("NAZIV")]
        [Index(0)]
        public override string Product { get; set; } = string.Empty;

        [Name("ŠIFRA")]
        [Index(1)]
        public override string ProductCode { get; set; } = string.Empty;

        [Name("NETO_KOLIČINA")]
        [Index(2)]
        public override string Quantity { get; set; } = string.Empty;

        [Name("JEDINICA_MJERE")]
        [Index(3)]
        public override string UOM { get; set; } = string.Empty;

        [Name("MARKA")]
        [Index(4)]
        public override string Brand { get; set; } = string.Empty;

        [Name("MALOPRODAJNA_CIJENA")]
        [Index(5)]
        public override string Price { get; set; } = string.Empty;

        [Name("MPC_ZA_VRIJEME_POSEBNOG_OBLIKA_PRODAJE")]
        [Index(6)]
        public override string SpecialPrice { get; set; } = string.Empty;

        [Name("NAJNIZA_CIJENA_U_POSLJ._30_DANA")]
        [Index(7)]
        public override string BestPrice30 { get; set; } = string.Empty;

        [Name("CIJENA_ZA_JEDINICU_MJERE")]
        [Index(8)]
        public override string PricePerUnit { get; set; } = string.Empty;

        [Name("BARKOD")]
        [Index(9)]
        public override string Barcode { get; set; } = string.Empty;

        [Name("KATEGORIJA_PROIZVODA")]
        [Index(10)]
        public string Category { get; set; } = string.Empty;

        [Name("Sidrena_cijena_na_02.05.2025")]
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
                UOM = UOM?.TrimToMaxLength(20),
                Quantity = Quantity?.Trim(),
                PricePerUnit = decimal.TryParse(PricePerUnit, NumberStyles.Any, NumberFormatInfo, out var unitPrice) ? unitPrice : (decimal?)null,
                SpecialPrice = decimal.TryParse(SpecialPrice, NumberStyles.Any, NumberFormatInfo, out var specialPrice) ? specialPrice : (decimal?)null,
                BestPrice30 = decimal.TryParse(BestPrice30, NumberStyles.Any, NumberFormatInfo, out var bestPrice30) ? bestPrice30 : (decimal?)null,
                AnchorPrice = decimal.TryParse(AnchorPrice, NumberStyles.Any, NumberFormatInfo, out var anchorPrice) ? anchorPrice : (decimal?)null
            };
        }
    }
}