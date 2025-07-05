using CijeneScraper.Crawler;
using CijeneScraper.Data;
using CijeneScraper.Data.Repository;
using CijeneScraper.Services;
using CijeneScraper.Services.Caching;
using CijeneScraper.Services.Crawlers.Chains.Kaufland;
using CijeneScraper.Services.Crawlers.Chains.Konzum;
using CijeneScraper.Services.DataProcessor;
using Microsoft.EntityFrameworkCore;

namespace CijeneScraper
{
    /// <summary>
    /// Entry point for the CijeneScraper application.
    /// Configures and starts the web application and its services.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Main method. Configures services and starts the web application.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add EF Core DbContext
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(
                    builder.Configuration.GetConnectionString("DefaultConnection")
                )
            );
            builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

            // Register application services and dependencies
            builder.Services.AddControllers();
            builder.Services.AddSingleton<ScrapingQueue>();
            builder.Services.AddHostedService<ScrapingWorker>();
            builder.Services.AddSingleton<HttpClient>();
            builder.Services.AddSingleton<ICacheProvider, ParquetCacheProvider>();

            #region Crawlers
            builder.Services.AddTransient<ICrawler, KonzumCrawler>();
            builder.Services.AddTransient<ICrawler, KauflandCrawler>();
            // TODO
            //builder.Services.AddTransient<ICrawler, IntersparCrawler>();
            //builder.Services.AddTransient<ICrawler, LidlCrawler>();
            //...
            #endregion

            builder.Services.AddScoped<IScrapingDataProcessor, ScrapingDataProcessor>();

            // Register OpenAPI/Swagger services
            builder.Services.AddOpenApiDocument();

            var app = builder.Build();

            // Enable OpenAPI and Swagger UI
            app.UseOpenApi();
            app.UseSwaggerUi(c =>
            {
                c.Path = "/swagger";
                c.DocumentPath = "/swagger/v1/swagger.json";
            });

            app.UseHttpsRedirection();
            app.UseAuthorization();

            // Map OpenAPI and controller endpoints
            app.MapOpenApi();
            app.MapControllers();

            // Apply migrations at startup
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Database.Migrate();
            }

            app.Run();
        }
    }
}