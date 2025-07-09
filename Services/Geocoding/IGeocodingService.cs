using CijeneScraper.Models.Google;
using static CijeneScraper.Services.Geocoding.GeocodingService;

namespace CijeneScraper.Services.Geocoding
{
    public interface IGeocodingService
    {
        Task<GeocodingResult?> GeocodeAsync(string address, CancellationToken cancellationToken = default);
    }
}