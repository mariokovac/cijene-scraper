using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CijeneScraper.Models.Database
{
    [Table("ScrapingJobs")]
    [Index(nameof(ChainID), nameof(Date), IsUnique = true)]
    public class ScrapingJob
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        public int ChainID { get; set; }

        [Required]
        public DateOnly Date { get; set; }

        public DateTime CompletedAt { get; set; }

        [ForeignKey(nameof(ChainID))]
        public virtual Chain Chain { get; set; } = null!;
    }
}