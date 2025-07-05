using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CijeneScraper.Services.Caching
{
    /// <summary>
    /// Provides a base implementation for cache providers that handle storing and retrieving cached data.
    /// </summary>
    public abstract class BaseCacheProvider : ICacheProvider
    {
        /// <summary>
        /// Checks if a cache file exists in the specified folder with the given file name.
        /// </summary>
        /// <param name="folder">The folder to check for the cache file.</param>
        /// <param name="fileName">The name of the cache file (without extension).</param>
        /// <returns>True if the cache file exists; otherwise, false.</returns>
        public bool Exists(string folder, string fileName)
        {
            var path = GetFilePath(folder, fileName);
            return File.Exists(path);
        }

        /// <summary>
        /// Checks if a cache file exists at the specified file path.
        /// </summary>
        /// <param name="filePath">The full path to the cache file.</param>
        /// <returns>True if the cache file exists; otherwise, false.</returns>
        public bool Exists(string filePath)
        {
            return File.Exists(filePath);
        }

        /// <summary>
        /// Saves the specified records to the cache asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of records to save.</typeparam>
        /// <param name="folder">The folder where the cache file will be saved.</param>
        /// <param name="fileName">The name of the cache file (without extension).</param>
        /// <param name="records">The records to save.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>A task representing the asynchronous save operation.</returns>
        public abstract Task SaveAsync<T>(string folder, string fileName, IEnumerable<T> records, CancellationToken ct = default);

        /// <summary>
        /// Reads records from the cache asynchronously using the specified folder and file name.
        /// </summary>
        /// <typeparam name="T">The type of records to read.</typeparam>
        /// <param name="folder">The folder where the cache file is located.</param>
        /// <param name="fileName">The name of the cache file (without extension).</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>A task representing the asynchronous read operation, containing the records.</returns>
        public virtual Task<IEnumerable<T>> ReadAsync<T>(string folder, string fileName, CancellationToken ct = default) where T : new()
        {
            var path = GetFilePath(folder, fileName);
            return ReadAsync<T>(path, ct);
        }

        /// <summary>
        /// Reads records from the cache asynchronously using the specified file path.
        /// </summary>
        /// <typeparam name="T">The type of records to read.</typeparam>
        /// <param name="filePath">The full path to the cache file.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>A task representing the asynchronous read operation, containing the records.</returns>
        public abstract Task<IEnumerable<T>> ReadAsync<T>(string filePath, CancellationToken ct = default) where T : new();

        /// <summary>
        /// Each provider defines its own file extension (e.g., ".csv" or ".parquet").
        /// </summary>
        public abstract string Extension { get; }

        /// <summary>
        /// Gets the full file path for the cache file, creating the directory if it does not exist.
        /// </summary>
        /// <param name="folder">The folder where the cache file will be stored.</param>
        /// <param name="fileName">The name of the cache file (without extension).</param>
        /// <returns>The full file path including the extension.</returns>
        protected string GetFilePath(string folder, string fileName)
        {
            var dir = Path.Combine(folder);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            return Path.Combine(dir, $"{fileName}{Extension}");
        }

        /// <summary>
        /// Clears the cache for the specified output folder and date.
        /// </summary>
        /// <param name="outputFolder">The folder whose cache should be cleared.</param>
        /// <param name="date">The date for which the cache should be cleared.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task representing the asynchronous clear operation.</returns>
        public abstract Task ClearAsync(
            string outputFolder, 
            DateOnly date, 
            CancellationToken cancellationToken = default);
    }
}