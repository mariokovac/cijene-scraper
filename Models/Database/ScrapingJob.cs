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

        /// <summary>
        /// When the scraping job was started
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// When the scraping job was completed
        /// </summary>
        public DateTime CompletedAt { get; set; }

        /// <summary>
        /// User or system that initiated the scraping
        /// </summary>
        [MaxLength(100)]
        public string? InitiatedBy { get; set; }

        /// <summary>
        /// Whether this was a forced execution
        /// </summary>
        public bool IsForced { get; set; }

        /// <summary>
        /// Number of price changes detected during this job
        /// </summary>
        public int PriceChanges { get; set; }

        /// <summary>
        /// Reference to the detailed log entry
        /// </summary>
        public long? ScrapingJobLogId { get; set; }

        [ForeignKey(nameof(ChainID))]
        public virtual Chain Chain { get; set; } = null!;

        [ForeignKey(nameof(ScrapingJobLogId))]
        public virtual ScrapingJobLog? ScrapingJobLog { get; set; }
    }
}