using CijeneScraper.Models.Crawler;
using CijeneScraper.Utility;
using System.Globalization;
using System.Xml.Serialization;

namespace CijeneScraper.Services.Crawlers.Chains.Studenac
{
    /// <summary>
    /// Represents the root XML structure from Studenac price files.
    /// </summary>
    [XmlRoot("Proizvodi")]
    public class StudenacProizvodiXml
    {
        [XmlElement("ProdajniObjekt")]
        public StudenacProdajniObjekt ProdajniObjekt { get; set; } = new();
    }

    /// <summary>
    /// Represents a store (ProdajniObjekt) in the XML structure.
    /// </summary>
    public class StudenacProdajniObjekt
    {
        [XmlElement("Oblik")]
        public string Oblik { get; set; } = string.Empty;

        [XmlElement("Oznaka")]
        public string Oznaka { get; set; } = string.Empty;

        [XmlElement("Adresa")]
        public string Adresa { get; set; } = string.Empty;

        [XmlElement("BrojPohrane")]
        public string BrojPohrane { get; set; } = string.Empty;

        [XmlArray("Proizvodi")]
        [XmlArrayItem("Proizvod")]
        public List<StudenacXmlRecord> Proizvodi { get; set; } = new();
    }

    /// <summary>
    /// Represents a product record from Studenac XML files.
    /// XML structure: NazivProizvoda, SifraProizvoda, MarkaProizvoda, NetoKolicina, JedinicaMjere,
    /// MaloprodajnaCijena, CijenaZaJedinicuMjere, MaloprodajnaCijenaAkcija, NajnizaCijena, SidrenaCijena, Barkod, KategorijeProizvoda
    /// </summary>
    public class StudenacXmlRecord
    {
        [XmlElement("NazivProizvoda")]
        public string Product { get; set; } = string.Empty;

        [XmlElement("SifraProizvoda")]
        public string ProductCode { get; set; } = string.Empty;

        [XmlElement("MarkaProizvoda")]
        public string Brand { get; set; } = string.Empty;

        [XmlElement("NetoKolicina")]
        public string Quantity { get; set; } = string.Empty;

        [XmlElement("JedinicaMjere")]
        public string UOM { get; set; } = string.Empty;

        [XmlElement("MaloprodajnaCijena")]
        public string Price { get; set; } = string.Empty;

        [XmlElement("CijenaZaJedinicuMjere")]
        public string PricePerUnit { get; set; } = string.Empty;

        [XmlElement("MaloprodajnaCijenaAkcija")]
        public string SpecialPrice { get; set; } = string.Empty;

        [XmlElement("NajnizaCijena")]
        public string BestPrice30 { get; set; } = string.Empty;

        [XmlElement("SidrenaCijena")]
        public string AnchorPrice { get; set; } = string.Empty;

        [XmlElement("Barkod")]
        public string Barcode { get; set; } = string.Empty;

        [XmlElement("KategorijeProizvoda")]
        public string Category { get; set; } = string.Empty;

        public static NumberFormatInfo NumberFormatInfo = new NumberFormatInfo()
        {
            NumberDecimalSeparator = "."
        };

        public PriceInfo ToPriceInfo()
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