using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Xml.Linq;

namespace CijeneScraper.Models.Database
{
    [Table("ChainProducts")]
    [Index(nameof(Barcode), Name = "IX_ChainProducts_Barcode")]
    public class ChainProduct
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        public int ChainId { get; set; }

        [Required]
        public string Code { get; set; } = null!;

        public string? Barcode { get; set; } = null!;

        [Required]
        public string Name { get; set; } = null!;

        public string? Brand { get; set; } = null!;

        public string? UOM { get; set; }

        public string? Quantity { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("ChainId")]
        public Chain Chain { get; set; } = null!;

        [InverseProperty("ChainProduct")]
        public virtual ICollection<Price> Prices { get; set; } = new List<Price>();
    }
}
