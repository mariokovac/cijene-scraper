using CijeneScraper.Models;
using CsvHelper.Configuration.Attributes;
using System.Globalization;

namespace CijeneScraper.Services.Crawlers.Chains.Kaufland
{
    /// <summary>
    /// Represents a store information data transfer object for Kaufland.
    /// "naziv proizvoda	šifra proizvoda	marka proizvoda	neto količina(KG)	jedinica mjere	maloprod.cijena(EUR)	akc.cijena, A=akcija	kol.jed.mj.	jed.mj. (1 KOM/L/KG)	cijena jed.mj.(EUR)	MPC poseb.oblik prod	Najniža MPC u 30dana	Sidrena cijena	barkod	WG"
    /// </summary>
    public class KauflandCsvRecord
    {
        [Name("naziv proizvoda")]
        [Index(0)]
        public string Product { get; set; }

        [Name("šifra proizvoda")]
        [Index(1)]
        public string ProductCode { get; set; }

        [Name("marka proizvoda")]
        [Index(2)]
        public string Brand { get; set; }

        [Name("neto količina(KG)")]
        [Index(3)]
        public string Quantity { get; set; }

        [Name("jedinica mjere")]
        [Index(4)]
        public string UOM { get; set; }

        [Name("maloprod.cijena(EUR)")]
        [Index(5)]
        public string Price { get; set; }

        [Name("akc.cijena, A=akcija")]
        [Index(6)]
        public string Action { get; set; }

        [Name("kol.jed.mj.")]
        [Index(7)]
        public string SingleUnitQty { get; set; }

        [Name("jed.mj. (1 KOM/L/KG)")]
        [Index(8)]
        public string SingleUnitUOM { get; set; }

        [Name("cijena jed.mj.(EUR)")]
        [Index(9)]
        public string PricePerUnit { get; set; }

        [Name("MPC poseb.oblik prod")]
        [Index(10)]
        public string SpecialPrice { get; set; }

        [Name("Najniža MPC u 30dana")]
        [Index(11)]
        public string BestPrice30 { get; set; }

        [Name("Sidrena cijena")]
        [Index(12)]
        public string AnchorPrice { get; set; }

        [Name("barkod")]
        [Index(13)]
        public string Barcode { get; set; }

        [Name("WG")]
        [Index(14)]
        public string Category { get; set; }

        public static NumberFormatInfo NumberFormatInfo = new NumberFormatInfo()
        {
            NumberDecimalSeparator = ","
        };

        public static explicit operator PriceInfo(KauflandCsvRecord p)
        {
            return new PriceInfo
            {
                ProductCode = p.ProductCode,
                Barcode = p.Barcode,
                Name = p.Product,
                Price = decimal.TryParse(p.Price, NumberStyles.Any, NumberFormatInfo, out var price) ? price : 0m,
                Brand = p.Brand,
                UOM = p.UOM,
                Quantity = p.Quantity?.Trim(),
                PricePerUnit = decimal.TryParse(p.PricePerUnit, NumberStyles.Any, NumberFormatInfo, out var unitPrice) ? unitPrice : (decimal?)null,
                SpecialPrice = decimal.TryParse(p.SpecialPrice, NumberStyles.Any, NumberFormatInfo, out var specialPrice) ? specialPrice : (decimal?)null,
                BestPrice30 = decimal.TryParse(p.BestPrice30, NumberStyles.Any, NumberFormatInfo, out var bestPrice30) ? bestPrice30 : (decimal?)null,
                AnchorPrice = decimal.TryParse(p.AnchorPrice, NumberStyles.Any, NumberFormatInfo, out var anchorPrice) ? anchorPrice : (decimal?)null
            };
        }
    }
}
