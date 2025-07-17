namespace CijeneScraper.Services.Crawlers.Chains.Spar
{
    /// <summary>
    /// Data transfer object representing store information scraped from Spar.
    /// </summary>
    public class StoreInfoDto
    {
        /// <summary>
        /// Unique identifier for the store.
        /// </summary>
        public string StoreId { get; set; } = string.Empty;

        /// <summary>
        /// Type of the store (e.g., supermarket, hipermarket).
        /// </summary>
        public string StoreType { get; set; } = string.Empty;

        /// <summary>
        /// Name of the store.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Street address of the store.
        /// </summary>
        public string StreetAddress { get; set; } = string.Empty;

        /// <summary>
        /// Postal code of the store location.
        /// </summary>
        public string Zipcode { get; set; } = string.Empty;

        /// <summary>
        /// City where the store is located.
        /// </summary>
        public string City { get; set; } = string.Empty;
    }
}