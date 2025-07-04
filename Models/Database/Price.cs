using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CijeneScraper.Models.Database
{
    [Table("Prices")]
    public class Price
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        public long ChainProductId { get; set; }

        public long StoreId { get; set; }

        public DateTime Date { get; set; }

        public decimal? MPC { get; set; }

        public decimal? PricePerUnit { get; set; }

        public decimal? SpecialPrice { get; set; }

        public decimal? BestPrice30 { get; set; }

        public decimal? AnchorPrice { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("ChainProductId")]
        public virtual ChainProduct ChainProduct { get; set; } = null!;

        [ForeignKey("StoreId")]
        public virtual Store Store { get; set; } = null!;

    }
}
