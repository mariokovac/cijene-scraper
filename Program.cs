using CijeneScraper.Crawler;
using CijeneScraper.Data;
using CijeneScraper.Data.Repository;
using CijeneScraper.Extensions;
using CijeneScraper.Models;
using CijeneScraper.Services;
using CijeneScraper.Services.Caching;
using CijeneScraper.Services.Caching.CSV;
using CijeneScraper.Services.Crawlers.Chains.Kaufland;
using CijeneScraper.Services.Crawlers.Chains.Konzum;
using CijeneScraper.Services.DataProcessor;
using CijeneScraper.Services.Geocoding;
using CijeneScraper.Services.Logging;
using CijeneScraper.Services.Notification;
using CijeneScraper.Services.Scrape;
using CijeneScraper.Services.Security;
using Microsoft.EntityFrameworkCore;
using System.Text;

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
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var builder = WebApplication.CreateBuilder(args);
            var cachingEngine = builder.Configuration["Caching:Engine"]?.ToLowerInvariant() ?? "parquet";

            builder.Services.AddApiKeyAuthentication(builder.Configuration);

            // Add EF Core DbContext
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(
                    builder.Configuration.GetConnectionString("DefaultConnection")
                )
            );
            builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

            // Add database logging
            builder.Logging.AddDatabaseLogging(options =>
            {
                options.MinLevel = LogLevel.Information;
                options.BufferSize = 50;
                options.FlushIntervalSeconds = 15;
            });

            // Register logging services
            builder.Services.AddScrapingJobLogging();

            // Register the geocoding service
            builder.Services.AddScoped<IGeocodingService, GeocodingService>();
            builder.Services.AddHttpClient("GoogleGeocoding", client =>
            {
                client.BaseAddress = new Uri("https://maps.googleapis.com/");
            });

            // Register application services and dependencies
            builder.Services.AddControllers();

            builder.Services.Configure<MailSettings>(builder.Configuration.GetSection("MailSettings"));
            builder.Services.AddScoped<IEmailNotificationService, EmailNotificationService>();
            builder.Services.AddScoped<IScrapingJobService, ScrapingJobService>();

            builder.Services.AddSingleton<ScrapingQueue>();
            builder.Services.AddHostedService<ScrapingWorker>();
            builder.Services.AddSingleton<HttpClient>();

            if(cachingEngine == "csv")
                builder.Services.AddSingleton<ICacheProvider, CsvCacheProvider>();
            else
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

            // Add CORS policy
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy
                        .AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
            });

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

            app.UseCors("AllowAll");

            // Map OpenAPI and controller endpoints
            app.MapOpenApi();
            app.MapControllers();

            // Apply migrations at startup
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                try
                {
                    db.Database.Migrate();
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("Database migrations applied successfully");
                }
                catch (Exception ex)
                {
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "Failed to apply database migrations");
                    throw;
                }
            }

            app.Run();
        }
    }
}