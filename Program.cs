
using CijeneScraper.Crawler;
using CijeneScraper.Crawler.Chains;
using CijeneScraper.Services;
using CijeneScraper.Services.Caching;
using CijeneScraper.Services.Caching.CSV;

namespace CijeneScraper
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddSingleton<ScrapingQueue>();
            builder.Services.AddHostedService<ScrapingWorker>();

            builder.Services.AddSingleton<HttpClient>();
            builder.Services.AddSingleton<ICacheProvider, ParquetCacheProvider>();
            builder.Services.AddTransient<ICrawler, KonzumCrawler>();
            //builder.Services.AddTransient<ICrawler, IntersparCrawler>();
            //builder.Services.AddTransient<ICrawler, LidlCrawler>();

            builder.Services.AddOpenApiDocument();

            var app = builder.Build();

            app.UseOpenApi();

            app.UseSwaggerUi(c =>
            {
                c.Path = "/swagger";
                c.DocumentPath = "/swagger/v1/swagger.json";
            });

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapOpenApi();

            app.MapControllers();

            app.Run();
        }
    }
}
