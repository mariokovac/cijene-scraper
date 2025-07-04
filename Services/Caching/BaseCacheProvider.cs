namespace CijeneScraper.Services.Caching
{
    public abstract class BaseCacheProvider : ICacheProvider
    {
        public bool Exists(string folder, string fileName)
        {
            var path = GetFilePath(folder, fileName);
            return File.Exists(path);
        }

        public bool Exists(string filePath)
        {
            return File.Exists(filePath);
        }

        public abstract Task SaveAsync<T>(string folder, string fileName, IEnumerable<T> records, CancellationToken ct = default);

        public virtual Task<IEnumerable<T>> ReadAsync<T>(string folder, string fileName, CancellationToken ct = default) where T : new()
        {
            var path = GetFilePath(folder, fileName);
            return ReadAsync<T>(path, ct);
        }

        public abstract Task<IEnumerable<T>> ReadAsync<T>(string filePath, CancellationToken ct = default) where T : new();

        /// <summary>
        /// Svaki provider definira svoju ekstenziju (npr. “.csv” ili “.parquet”)
        /// </summary>
        public abstract string Extension { get; }

        protected string GetFilePath(string folder, string fileName)
        {
            var dir = Path.Combine(folder);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            return Path.Combine(dir, $"{fileName}{Extension}");
        }
    }
}
