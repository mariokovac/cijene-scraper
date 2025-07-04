using Microsoft.AspNetCore.Mvc;

namespace CijeneScraper.Controllers
{
    [ApiController]
    public class HealthController : ControllerBase
    {
        [Route("health")]
        public IActionResult Index()
        {
            return Content("OK");
        }
    }
}
