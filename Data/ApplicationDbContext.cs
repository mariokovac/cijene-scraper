using CijeneScraper.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace CijeneScraper.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Chain> Chains { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ChainProduct> ChainProducts { get; set; }
        public DbSet<Store> Stores { get; set; }
        public DbSet<Price> Prices { get; set; }
        public DbSet<ScrapingJob> ScrapingJobs { get; set; }
        public DbSet<ScrapingJobLog> ScrapingJobLogs { get; set; }
        public DbSet<ApplicationLog> ApplicationLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure ScrapingJobLog
            modelBuilder.Entity<ScrapingJobLog>(entity =>
            {
                entity.Property(e => e.Status)
                    .HasDefaultValue(ScrapingJobStatus.Running);
            });

            // Configure ApplicationLog
            modelBuilder.Entity<ApplicationLog>(entity =>
            {
            });

            // Configure ScrapingJob
            modelBuilder.Entity<ScrapingJob>(entity =>
            {
                entity.Property(e => e.PriceChanges)
                    .HasDefaultValue(0);
                    
                entity.Property(e => e.IsForced)
                    .HasDefaultValue(false);
            });
        }
    }
}
