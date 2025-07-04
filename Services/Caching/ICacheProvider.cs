namespace CijeneScraper.Services.Caching
{
    public interface ICacheProvider
    {
        /// <summary>
        /// Provjerava postoji li “cache” file.
        /// </summary>
        bool Exists(string folder, string fileName);

        bool Exists(string filePath);

        string Extension { get; }

        /// <summary>
        /// Sprema podatke u cache.
        /// </summary>
        Task SaveAsync<T>(string folder, string fileName, IEnumerable<T> records, CancellationToken ct = default); 

        /// <summary>
        /// Čita podatke iz cache.
        /// </summary>
        Task<IEnumerable<T>> ReadAsync<T>(string folder, string fileName, CancellationToken ct = default) where T : new();

        Task<IEnumerable<T>> ReadAsync<T>(string filePath, CancellationToken ct = default) where T : new();
    }
}
