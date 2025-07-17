using System.Text.Json.Serialization;

namespace CijeneScraper.Services.Crawlers.Chains.Spar
{
    /// <summary>
    /// Represents a file entry in the Spar price list JSON.
    /// </summary>
    public class SparFileInfo
    {
        /// <summary>
        /// Name of the CSV file.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// URL to download the CSV file.
        /// </summary>
        [JsonPropertyName("URL")]
        public string URL { get; set; } = string.Empty;

        /// <summary>
        /// SHA hash of the file.
        /// </summary>
        [JsonPropertyName("SHA")]
        public string SHA { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents the root structure of the Spar price list JSON.
    /// </summary>
    public class SparPriceListJson
    {
        /// <summary>
        /// List of CSV files available for download.
        /// </summary>
        [JsonPropertyName("files")]
        public List<SparFileInfo> Files { get; set; } = new List<SparFileInfo>();
    }
}