using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CijeneScraper.Models.Database
{
    /// <summary>
    /// General application event logging for all system activities
    /// </summary>
    [Table("ApplicationLogs")]
    [Index(nameof(Level), nameof(Timestamp))]
    [Index(nameof(Category), nameof(Timestamp))]
    [Index(nameof(Timestamp))]
    public class ApplicationLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        /// <summary>
        /// When the log entry was created
        /// </summary>
        [Required]
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Log level (Information, Warning, Error, Critical, Debug, Trace)
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Level { get; set; } = null!;

        /// <summary>
        /// Logger category (usually the class name)
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Category { get; set; } = null!;

        /// <summary>
        /// Log message
        /// </summary>
        [Required]
        [Column(TypeName = "text")]
        public string Message { get; set; } = null!;

        /// <summary>
        /// Exception details if applicable
        /// </summary>
        [Column(TypeName = "text")]
        public string? Exception { get; set; }

        /// <summary>
        /// Event ID for categorizing log entries
        /// </summary>
        public int? EventId { get; set; }

        /// <summary>
        /// Additional structured data in JSON format
        /// </summary>
        [Column(TypeName = "jsonb")]
        public string? Properties { get; set; }

        /// <summary>
        /// Correlation ID for tracking related operations
        /// </summary>
        [MaxLength(100)]
        public string? CorrelationId { get; set; }

        /// <summary>
        /// User or system that generated the log
        /// </summary>
        [MaxLength(100)]
        public string? UserId { get; set; }

        /// <summary>
        /// Source IP address if applicable
        /// </summary>
        [MaxLength(45)]
        public string? IpAddress { get; set; }

        /// <summary>
        /// User agent string if from HTTP request
        /// </summary>
        [MaxLength(500)]
        public string? UserAgent { get; set; }
    }

    /// <summary>
    /// Constants for log levels
    /// </summary>
    public static class LogLevel
    {
        public const string Trace = "Trace";
        public const string Debug = "Debug";
        public const string Information = "Information";
        public const string Warning = "Warning";
        public const string Error = "Error";
        public const string Critical = "Critical";
    }
}