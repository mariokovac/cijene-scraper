using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CijeneScraper.Models.Database
{
    [Table("Prices")]

    #region Indexes
    // Index to speed up queries filtering by date
    [Index(nameof(Date), Name = "IX_Prices_Date")]

    // Unique index to ensure no duplicate prices for the same product in the same store on the same date
    [Index(
        nameof(ChainProductId), nameof(StoreId), nameof(Date),
        IsUnique = true,
        Name = "UX_Prices_Product_Store_Date"
    )]

    [Index(nameof(StoreId), Name = "IX_Prices_StoreId")]
    [Index(nameof(ChainProductId), Name = "IX_Prices_ChainProductId")]

    // Optimized DELETE
    [Index(nameof(Date), nameof(StoreId), Name = "IX_Prices_Date_StoreId")]
    #endregion
    public class Price
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        public long ChainProductId { get; set; }

        public long StoreId { get; set; }

        public DateOnly Date { get; set; }

        public decimal? MPC { get; set; }

        public decimal? PricePerUnit { get; set; }

        public decimal? SpecialPrice { get; set; }

        public decimal? BestPrice30 { get; set; }

        public decimal? AnchorPrice { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(ChainProductId))]
        public virtual ChainProduct ChainProduct { get; set; } = null!;

        [ForeignKey(nameof(StoreId))]
        public virtual Store Store { get; set; } = null!;

    }
}
