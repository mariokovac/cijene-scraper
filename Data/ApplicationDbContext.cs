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
        public DbSet<ChainProduct> ChainProducts { get; set; }
        public DbSet<Store> Stores { get; set; }
        public DbSet<Price> Prices { get; set; }
    }
}
