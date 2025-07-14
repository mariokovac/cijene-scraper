using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CijeneScraper.Models.Database
{
    /// <summary>
    /// Detailed logging of scraping job execution with start/end times, user info, and status tracking
    /// </summary>
    [Table("ScrapingJobLogs")]
    [Index(nameof(ChainID), nameof(Date), nameof(StartedAt))]
    [Index(nameof(Status), nameof(StartedAt))]
    public class ScrapingJobLog
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
        [Required]
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// When the scraping job completed (success or failure)
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Current status of the scraping job
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = null!; // Running, Completed, Failed, Cancelled

        /// <summary>
        /// User or system that initiated the scraping (IP address, user ID, etc.)
        /// </summary>
        [MaxLength(100)]
        public string? InitiatedBy { get; set; }

        /// <summary>
        /// Source of the request (API, Scheduled, Manual, etc.)
        /// </summary>
        [MaxLength(50)]
        public string? RequestSource { get; set; }

        /// <summary>
        /// Whether this was a forced execution (bypassing duplicate checks)
        /// </summary>
        public bool IsForced { get; set; }

        /// <summary>
        /// Total number of stores processed
        /// </summary>
        public int? StoresProcessed { get; set; }

        /// <summary>
        /// Total number of products found
        /// </summary>
        public int? ProductsFound { get; set; }

        /// <summary>
        /// Total number of price changes detected
        /// </summary>
        public int? PriceChanges { get; set; }

        /// <summary>
        /// Duration of the scraping job in milliseconds
        /// </summary>
        public long? DurationMs { get; set; }

        /// <summary>
        /// Success message if completed successfully
        /// </summary>
        [MaxLength(500)]
        public string? SuccessMessage { get; set; }

        /// <summary>
        /// Error message if failed
        /// </summary>
        [MaxLength(1000)]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Detailed error stack trace for debugging
        /// </summary>
        [Column(TypeName = "text")]
        public string? ErrorStackTrace { get; set; }

        /// <summary>
        /// Additional metadata in JSON format
        /// </summary>
        [Column(TypeName = "jsonb")]
        public string? Metadata { get; set; }

        [ForeignKey(nameof(ChainID))]
        public virtual Chain Chain { get; set; } = null!;
    }

    /// <summary>
    /// Constants for ScrapingJobLog status values
    /// </summary>
    public static class ScrapingJobStatus
    {
        public const string Running = "Running";
        public const string Completed = "Completed";
        public const string Failed = "Failed";
        public const string Cancelled = "Cancelled";
    }

    /// <summary>
    /// Constants for request source values
    /// </summary>
    public static class RequestSource
    {
        public const string API = "API";
        public const string Scheduled = "Scheduled";
        public const string Manual = "Manual";
        public const string System = "System";
    }
}