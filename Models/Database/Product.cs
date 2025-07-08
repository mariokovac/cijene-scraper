using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CijeneScraper.Models.Database
{
    /// <summary>
    /// Represents the main product entity, containing global product information
    /// shared across all chains and stores.
    /// </summary>
    [Table("Products")]
    public class Product
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        /// <summary>
        /// EAN/UPC barcode.
        /// </summary>
        [MaxLength(50)]
        public string? Barcode { get; set; }

        /// <summary>
        /// Official product name.
        /// </summary>
        [Required]
        [MaxLength(300)]
        public string Name { get; set; } = null!;

        /// <summary>
        /// Brand or manufacturer.
        /// </summary>
        [MaxLength(100)]
        public string? Brand { get; set; }

        /// <summary>
        /// Unit of measure (e.g. "kg", "l", "kom").
        /// </summary>
        [MaxLength(20)]
        public string? UOM { get; set; }

        /// <summary>
        /// Net quantity (e.g. "1", "0.5", "100").
        /// </summary>
        [MaxLength(50)]
        public string? Quantity { get; set; }

        /// <summary>
        /// Product category or group (optional).
        /// </summary>
        [MaxLength(100)]
        public string? Category { get; set; }

        /// <summary>
        /// Date and time when the product was created in the system.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Navigation property for all chain-specific product mappings.
        /// </summary>
        public virtual ICollection<ChainProduct> ChainProducts { get; set; } = new List<ChainProduct>();
    }
}
