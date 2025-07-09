using CijeneScraper.Models.Google;
using Newtonsoft.Json.Linq;
using System.Text.Json;
using static System.Net.WebRequestMethods;

namespace CijeneScraper.Services.Geocoding
{
    public class GeocodingService : IGeocodingService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GeocodingService> _logger;

        public GeocodingService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<GeocodingService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<GeocodingResult?> GeocodeAsync(string address, CancellationToken cancellationToken = default)
        {
            var apiKey = _configuration["Google:GeocodingApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("Google Geocoding API key is not configured.");
                return null;
            }

            var client = _httpClientFactory.CreateClient("GoogleGeocoding");
            var requestUri = $"maps/api/geocode/json?address={Uri.EscapeDataString(address)}&key={apiKey}";

            try
            {
                var response = await client.GetAsync(requestUri, cancellationToken);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var geocodeResponse = JsonSerializer.Deserialize<GeocodingResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (geocodeResponse?.Status == "OK" && geocodeResponse.Results.Any())
                {
                    //var location = geocodeResponse.Results.First().Geometry.Location;
                    return geocodeResponse.Results.First();
                }

                _logger.LogWarning("Geocoding for address '{Address}' failed with status: {Status}", address, geocodeResponse?.Status);
                return null;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error calling Google Geocoding API for address: {Address}", address);
                return null;
            }
        }
    }
}