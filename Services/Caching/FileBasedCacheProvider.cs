
namespace CijeneScraper.Services.Caching
{
    public class FileBasedCacheProvider : BaseCacheProvider
    {
        public override string Extension => throw new NotImplementedException();

        /// <inheritdoc />
        public override async Task ClearAsync(
            string outputFolder, 
            DateOnly date, 
            CancellationToken cancellationToken = default)
        {
            if(Directory.Exists(outputFolder) == false)
            {
                return; // nothing to clear
            }

            // remove all files that filename ends with the date
            var files = Directory.GetFiles(outputFolder);
            foreach (var file in files)
            {
                if (Exists(file))
                {
                    File.Delete(file);
                }
            }
        }

        public override Task<IEnumerable<T>> ReadAsync<T>(string filePath, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public override Task SaveAsync<T>(string folder, string fileName, IEnumerable<T> records, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }
}
