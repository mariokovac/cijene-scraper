using System.Reflection;
using Parquet;
using Parquet.Data;
using Parquet.Schema; 

namespace CijeneScraper.Services.Caching
{
    public class ParquetCacheProvider : BaseCacheProvider
    {
        public override string Extension => ".parquet";

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

            // build Parquet schema from T’s public properties
            var props = typeof(T)
                         .GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var fields = props
                         .Select(p => new DataField(p.Name, p.PropertyType))
                         .ToArray();

            var schema = new ParquetSchema(fields);

            // create writer
            await using var writer = await ParquetWriter.CreateAsync(schema, stream, null, false, ct);

            // write a single row‐group
            using var rowGroup = writer.CreateRowGroup();
            for (int colIdx = 0; colIdx < props.Length; colIdx++)
            {
                var prop = props[colIdx];
                var field = fields[colIdx];

                // create a strongly‐typed array, e.g. string[], decimal[], int?[], etc.
                var dataArray = Array.CreateInstance(prop.PropertyType, list.Count);

                for (int i = 0; i < list.Count; i++)
                {
                    // grab the value (already the right CLR type or null)
                    var value = prop.GetValue(list[i]);
                    dataArray.SetValue(value, i);
                }

                // now Parquet sees exactly the type it expects
                await rowGroup.WriteColumnAsync(
                    new DataColumn(field, dataArray),
                    ct
                );
            }
        }

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

                // read each column into a CLR array
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
