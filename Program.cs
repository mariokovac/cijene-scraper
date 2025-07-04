using CijeneScraper.Crawler;
using CijeneScraper.Services;
using CijeneScraper.Services.Caching;
using CijeneScraper.Services.Crawlers.Chains.Konzum;

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

            // Register application services and dependencies
            builder.Services.AddControllers();
            builder.Services.AddSingleton<ScrapingQueue>();
            builder.Services.AddHostedService<ScrapingWorker>();

            builder.Services.AddSingleton<HttpClient>();
            builder.Services.AddSingleton<ICacheProvider, ParquetCacheProvider>();
            builder.Services.AddTransient<ICrawler, KonzumCrawler>();
            // TODO
            // To enable other crawlers, uncomment the following lines:
            // builder.Services.AddTransient<ICrawler, IntersparCrawler>();
            // builder.Services.AddTransient<ICrawler, LidlCrawler>();

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

            app.Run();
        }
    }
}