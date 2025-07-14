using CijeneScraper.Data;
using CijeneScraper.Models.Database;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;

namespace CijeneScraper.Services.Logging
{
    /// <summary>
    /// Configuration options for database logging
    /// </summary>
    public class DatabaseLoggerOptions
    {
        /// <summary>
        /// Minimum log level to write to database
        /// </summary>
        public Microsoft.Extensions.Logging.LogLevel MinLevel { get; set; } = Microsoft.Extensions.Logging.LogLevel.Information;

        /// <summary>
        /// Maximum number of log entries to keep in memory before flushing to database
        /// </summary>
        public int BufferSize { get; set; } = 100;

        /// <summary>
        /// Maximum time to wait before flushing buffered entries to database (in seconds)
        /// </summary>
        public int FlushIntervalSeconds { get; set; } = 30;

        /// <summary>
        /// Categories to exclude from database logging (to prevent recursive logging)
        /// </summary>
        public string[] ExcludedCategories { get; set; } = new[]
        {
            "Microsoft.EntityFrameworkCore",
            "Microsoft.EntityFrameworkCore.Database.Command",
            "Microsoft.EntityFrameworkCore.Infrastructure",
            "Microsoft.EntityFrameworkCore.Migrations"
        };
    }

    /// <summary>
    /// Database logger provider that writes log entries to the database
    /// </summary>
    public class DatabaseLoggerProvider : ILoggerProvider
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly DatabaseLoggerOptions _options;
        private readonly ConcurrentDictionary<string, DatabaseLogger> _loggers = new();
        private readonly Timer _flushTimer;
        private readonly ConcurrentQueue<ApplicationLog> _logBuffer = new();

        public DatabaseLoggerProvider(IServiceProvider serviceProvider, IOptions<DatabaseLoggerOptions> options)
        {
            _serviceProvider = serviceProvider;
            _options = options.Value;
            
            // Create timer to periodically flush logs to database
            _flushTimer = new Timer(FlushLogs, null, TimeSpan.FromSeconds(_options.FlushIntervalSeconds), 
                                   TimeSpan.FromSeconds(_options.FlushIntervalSeconds));
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new DatabaseLogger(name, this, _options));
        }

        internal void AddLogEntry(ApplicationLog logEntry)
        {
            _logBuffer.Enqueue(logEntry);
            
            // If buffer is full, trigger immediate flush
            if (_logBuffer.Count >= _options.BufferSize)
            {
                _ = Task.Run(() => FlushLogs(null));
            }
        }

        private async void FlushLogs(object? state)
        {
            if (_logBuffer.IsEmpty) return;

            var logsToFlush = new List<ApplicationLog>();
            
            // Dequeue all pending logs
            while (_logBuffer.TryDequeue(out var log) && logsToFlush.Count < _options.BufferSize * 2)
            {
                logsToFlush.Add(log);
            }

            if (logsToFlush.Count == 0) return;

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                
                await dbContext.ApplicationLogs.AddRangeAsync(logsToFlush);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log to console/file as fallback - avoid recursive database logging
                Console.WriteLine($"Failed to flush logs to database: {ex.Message}");
            }
        }

        public void Dispose()
        {
            // Flush any remaining logs before disposal
            FlushLogs(null);
            _flushTimer?.Dispose();
            _loggers.Clear();
        }
    }

    /// <summary>
    /// Database logger implementation
    /// </summary>
    internal class DatabaseLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly DatabaseLoggerProvider _provider;
        private readonly DatabaseLoggerOptions _options;

        public DatabaseLogger(string categoryName, DatabaseLoggerProvider provider, DatabaseLoggerOptions options)
        {
            _categoryName = categoryName;
            _provider = provider;
            _options = options;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
        {
            // Check if logging is enabled for this level and category
            if (logLevel < _options.MinLevel) return false;
            
            // Exclude certain categories to prevent recursive logging
            return !_options.ExcludedCategories.Any(excluded => 
                _categoryName.StartsWith(excluded, StringComparison.OrdinalIgnoreCase));
        }

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);
            if (string.IsNullOrEmpty(message)) return;

            var logEntry = new ApplicationLog
            {
                Timestamp = DateTime.UtcNow,
                Level = logLevel.ToString(),
                Category = _categoryName,
                Message = message,
                Exception = exception?.ToString(),
                EventId = eventId.Id != 0 ? eventId.Id : null,
                Properties = SerializeProperties(state)
            };

            _provider.AddLogEntry(logEntry);
        }

        private string? SerializeProperties<TState>(TState state)
        {
            try
            {
                if (state is IEnumerable<KeyValuePair<string, object?>> properties)
                {
                    var dict = properties
                        .Where(p => p.Key != "{OriginalFormat}")
                        .ToDictionary(p => p.Key, p => p.Value);
                        
                    if (dict.Count > 0)
                    {
                        return JsonSerializer.Serialize(dict);
                    }
                }
            }
            catch
            {
                // Ignore serialization errors
            }
            return null;
        }
    }
}