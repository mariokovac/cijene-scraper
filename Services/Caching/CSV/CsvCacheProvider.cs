using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CijeneScraper.Services.Caching.CSV
{
    public class CsvCacheProvider : FileBasedCacheProvider
    {
        public override string Extension => ".csv";

        private readonly CsvConfiguration _cfg = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            MissingFieldFound = null,
            BadDataFound = null
        };

        public override async Task SaveAsync<T>(string folder, string fileName, IEnumerable<T> records, CancellationToken ct = default)
        {
            var path = GetFilePath(folder, fileName);
            await using var writer = new StreamWriter(path);
            await using var csv = new CsvWriter(writer, _cfg);
            await csv.WriteRecordsAsync(records, ct);
        }

        public override async Task<IEnumerable<T>> ReadAsync<T>(string filePath, CancellationToken ct = default)
        {
            using StreamReader reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, _cfg);

            var results = new List<T>();
            await foreach (var item in csv.GetRecordsAsync<T>().WithCancellation(ct))
            {
                results.Add(item);
            }

            return results;
        }

        public override async Task ClearAsync(
            string outputFolder, 
            DateOnly date, 
            CancellationToken cancellationToken = default)
        {
            var fileName = $"{date:yyyy-MM-dd}";
            var filePath = GetFilePath(outputFolder, fileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
