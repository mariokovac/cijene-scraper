using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Xml.Linq;

namespace CijeneScraper.Models.Database
{
    [Index(nameof(ChainId), Name = "IX_Stores_ChainId")]
    [Index(nameof(Code), nameof(ChainId), Name = "IX_Stores_Code_ChainId")]
    public class Store
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        public int ChainId { get; set; }

        [Required]
        public string Code { get; set; } = null!;

        public string? Address { get; set; } = null!;

        public string? City { get; set; } = null!;

        public string? PostalCode { get; set; } = null!;

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("ChainId")]
        public virtual Chain Chain { get; set; } = null!;
    }
}
