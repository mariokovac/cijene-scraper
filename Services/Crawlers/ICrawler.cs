using CijeneScraper.Models.Crawler;
using System.Text;

namespace CijeneScraper.Crawler
{
    /// <summary>
    /// Represents a contract for a crawler that retrieves price information from stores.
    /// </summary>
    public interface ICrawler
    {
        /// <summary>
        /// Crawls all stores and retrieves price information for the specified date.
        /// </summary>
        /// <param name="date">The date for which to retrieve prices. If null, uses the current date.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// A dictionary mapping <see cref="StoreInfo"/> to a list of <see cref="PriceInfo"/> objects.
        /// </returns>
        Task<Dictionary<StoreInfo, List<PriceInfo>>> Crawl(
            DateOnly date,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously crawls all stores and saves the results to the specified output folder.
        /// </summary>
        /// <param name="outputFolder">The folder where the results will be saved.</param>
        /// <param name="date">The date for which to retrieve prices. If null, uses the current date.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// A dictionary mapping <see cref="StoreInfo"/> to a list of <see cref="PriceInfo"/> objects.
        /// </returns>
        Task<Dictionary<StoreInfo, List<PriceInfo>>> CrawlAsync(
            string outputFolder,
            DateOnly date,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously fetches the raw text content from the specified URL.
        /// This method ensures the HTTP request is successful and attempts to decode the content using the provided encodings.
        /// If no encodings are specified, UTF-8 is used by default.
        /// </summary>
        /// <param name="url">The URL to fetch content from.</param>
        /// <param name="encodings">
        /// An optional array of encodings to attempt when decoding the content.
        /// If null, the default encoding is UTF-8.
        /// </param>
        /// <returns>
        /// The raw text content of the page, decoded using the most suitable encoding.
        /// </returns>
        Task<string> FetchTextAsync(string url, Encoding[]? encodings = null);

        /// <summary>
        /// Gets the name of the store chain this crawler is associated with.
        /// </summary>
        string Chain { get; }

        Task ClearCacheAsync(
            string outputFolder,
            DateOnly date,
            CancellationToken cancellationToken = default);
    }
}