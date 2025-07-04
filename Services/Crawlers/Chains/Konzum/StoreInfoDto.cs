namespace CijeneScraper.Services.Crawlers.Chains.Konzum
{
    /// <summary>
    /// Data transfer object representing store information scraped from Konzum.
    /// </summary>
    public class StoreInfoDto
    {
        /// <summary>
        /// Unique identifier for the store.
        /// </summary>
        public string StoreId { get; set; }

        /// <summary>
        /// Type of the store (e.g., supermarket, mini, etc.).
        /// </summary>
        public string StoreType { get; set; }

        /// <summary>
        /// Name of the store.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Street address of the store.
        /// </summary>
        public string StreetAddress { get; set; }

        /// <summary>
        /// Postal code of the store location.
        /// </summary>
        public string Zipcode { get; set; }

        /// <summary>
        /// City where the store is located.
        /// </summary>
        public string City { get; set; }
    }
}