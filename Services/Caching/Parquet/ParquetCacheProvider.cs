using System.Reflection;
using Parquet;
using Parquet.Data;
using Parquet.Schema;

namespace CijeneScraper.Services.Caching
{
    /// <summary>
    /// Provides a cache provider implementation for storing and retrieving data in Parquet file format.
    /// </summary>
    public class ParquetCacheProvider : FileBasedCacheProvider
    {
        /// <summary>
        /// Gets the file extension used by this cache provider (".parquet").
        /// </summary>
        public override string Extension => ".parquet";

        /// <summary>
        /// Saves the specified records to a Parquet file asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of records to save.</typeparam>
        /// <param name="folder">The folder where the Parquet file will be saved.</param>
        /// <param name="fileName">The name of the Parquet file (without extension).</param>
        /// <param name="records">The records to save.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>A task representing the asynchronous save operation.</returns>
        public override async Task SaveAsync<T>(
            string folder,
            string fileName,
            IEnumerable<T> records,
            CancellationToken ct = default)
        {
            var list = records.ToList();
            if (!list.Any()) return;

            var path = GetFilePath(folder, fileName);
            await using var stream = File.Create(path);

            // Build Parquet schema from T's public properties
            var props = typeof(T)
                         .GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var fields = props
                         .Select(p => new DataField(p.Name, p.PropertyType))
                         .ToArray();

            var schema = new ParquetSchema(fields);

            // Create Parquet writer
            await using var writer = await ParquetWriter.CreateAsync(schema, stream, null, false, ct);

            // Write a single row group
            using var rowGroup = writer.CreateRowGroup();
            for (int colIdx = 0; colIdx < props.Length; colIdx++)
            {
                var prop = props[colIdx];
                var field = fields[colIdx];

                // Create a strongly-typed array, e.g. string[], decimal[], int?[], etc.
                var dataArray = Array.CreateInstance(prop.PropertyType, list.Count);

                for (int i = 0; i < list.Count; i++)
                {
                    // Get the value (already the right CLR type or null)
                    var value = prop.GetValue(list[i]);
                    dataArray.SetValue(value, i);
                }

                // Parquet expects the exact type
                await rowGroup.WriteColumnAsync(
                    new DataColumn(field, dataArray),
                    ct
                );
            }
        }

        /// <summary>
        /// Reads records from a Parquet file asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of records to read.</typeparam>
        /// <param name="filePath">The full path to the Parquet file.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>A task representing the asynchronous read operation, containing the records.</returns>
        public override async Task<IEnumerable<T>> ReadAsync<T>(
            string filePath,
            CancellationToken ct = default)
        {
            await using var stream = File.OpenRead(filePath);
            using var reader = await ParquetReader.CreateAsync(stream, parquetOptions: null, true, ct);

            var schema = reader.Schema;
            var dataFields = schema.GetDataFields();
            var props = typeof(T)
                             .GetProperties(BindingFlags.Public | BindingFlags.Instance);

            var results = new List<T>();

            for (int g = 0; g < reader.RowGroupCount; g++)
            {
                using var rg = reader.OpenRowGroupReader(g);
                var columns = new Dictionary<string, Array>();

                // Read each column into a CLR array
                foreach (var df in dataFields)
                {
                    var col = await rg.ReadColumnAsync(df, ct);
                    columns[df.Name] = col.Data;
                }

                int rowCount = columns[dataFields[0].Name].Length;
                for (int i = 0; i < rowCount; i++)
                {
                    var obj = new T();
                    foreach (var df in dataFields)
                    {
                        var pi = props.FirstOrDefault(p => p.Name == df.Name);
                        if (pi is null) continue;

                        var raw = columns[df.Name].GetValue(i);
                        if (raw is null) continue;

                        var val = Convert.ChangeType(raw, pi.PropertyType);
                        pi.SetValue(obj, val);
                    }
                    results.Add(obj);
                }
            }

            return results;
        }
    }
}