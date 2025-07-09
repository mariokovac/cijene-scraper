using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CijeneScraper.Models.Google
{
    public class GeocodingResponse
    {
        [JsonPropertyName("results")]
        public List<GeocodingResult> Results { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("error_message")]
        public string ErrorMessage { get; set; }

        [JsonPropertyName("plus_code")]
        public PlusCode PlusCode { get; set; }
    }

    public class GeocodingResult
    {
        [JsonPropertyName("address_components")]
        public List<AddressComponent> AddressComponents { get; set; }

        [JsonPropertyName("formatted_address")]
        public string FormattedAddress { get; set; }

        [JsonPropertyName("geometry")]
        public Geometry Geometry { get; set; }

        [JsonPropertyName("place_id")]
        public string PlaceId { get; set; }

        [JsonPropertyName("types")]
        public List<string> Types { get; set; }

        [JsonPropertyName("plus_code")]
        public PlusCode PlusCode { get; set; }

        [JsonPropertyName("postcode_localities")]
        public List<string> PostcodeLocalities { get; set; }

        [JsonPropertyName("partial_match")]
        public bool PartialMatch { get; set; }
    }

    public class AddressComponent
    {
        [JsonPropertyName("long_name")]
        public string LongName { get; set; }

        [JsonPropertyName("short_name")]
        public string ShortName { get; set; }

        [JsonPropertyName("types")]
        public List<string> Types { get; set; }
    }

    public class Geometry
    {
        [JsonPropertyName("location")]
        public Location Location { get; set; }

        [JsonPropertyName("location_type")]
        public string LocationType { get; set; }

        [JsonPropertyName("viewport")]
        public Viewport Viewport { get; set; }

        [JsonPropertyName("bounds")]
        public Viewport Bounds { get; set; }
    }

    public class Location
    {
        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lng")]
        public double Lng { get; set; }
    }

    public class Viewport
    {
        [JsonPropertyName("northeast")]
        public Location Northeast { get; set; }

        [JsonPropertyName("southwest")]
        public Location Southwest { get; set; }
    }

    public class PlusCode
    {
        [JsonPropertyName("compound_code")]
        public string CompoundCode { get; set; }

        [JsonPropertyName("global_code")]
        public string GlobalCode { get; set; }
    }
}