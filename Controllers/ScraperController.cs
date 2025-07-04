using CijeneScraper.Crawler;
using CijeneScraper.Crawler.Chains;
using CijeneScraper.Services;
using Microsoft.AspNetCore.Mvc;

namespace CijeneScraper.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScraperController : ControllerBase
    {
        private readonly ScrapingQueue _queue;
        private readonly ILogger<ScraperController> _logger;
        private readonly Dictionary<string, ICrawler> _crawlers;
        private const string outputFolder = "ScrapedData";

        public ScraperController(ScrapingQueue queue, 
            ILogger<ScraperController> logger, 
            IEnumerable<ICrawler> crawlers)
        {
            _queue = queue;
            _logger = logger;
            _crawlers = crawlers.ToDictionary(
                c => c.Chain,
                StringComparer.OrdinalIgnoreCase);
        }

        [HttpPost("start/{chain}")]
        public IActionResult StartScraping(string chain, DateTime? date = null)
        {
            _logger.LogInformation("Zahtjev za scrapanje primljen.");

            if (!_crawlers.TryGetValue(chain, out var crawler))
            {
                _logger.LogError("Nepoznat lanac: {chain}", chain);
                return BadRequest($"Nepoznat lanac: {chain}");
            }

            if(date == null)
            {
                date = DateTime.Now;
            }

            _queue.Enqueue(token =>
                crawler.CrawlAsync(outputFolder, date, token));

            //_queue.Enqueue(async token =>
            //{
            //    // Ovamo stavi stvarni scraper
            //    await Task.Delay(3000, token); // simulacija scrapanja
            //    Console.WriteLine($"Scrapanje obavljeno u {DateTime.Now}");
            //});

            //_queue.Enqueue(token => crawler.CrawlAsync(configuredOutput, DateTime.Now, token));

            return Accepted("Scrapanje dodano u red.");
        }
    }
}
