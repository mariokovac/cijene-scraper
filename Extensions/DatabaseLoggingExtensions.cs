using CijeneScraper.Services.Logging;
using Microsoft.Extensions.Options;

namespace CijeneScraper.Extensions
{
    /// <summary>
    /// Extension methods for configuring database logging
    /// </summary>
    public static class DatabaseLoggingExtensions
    {
        /// <summary>
        /// Adds database logging to the logging builder
        /// </summary>
        public static ILoggingBuilder AddDatabaseLogging(this ILoggingBuilder builder, Action<DatabaseLoggerOptions>? configure = null)
        {
            if (configure != null)
            {
                builder.Services.Configure(configure);
            }
            else
            {
                builder.Services.Configure<DatabaseLoggerOptions>(options => { });
            }

            builder.Services.AddSingleton<ILoggerProvider, DatabaseLoggerProvider>();
            return builder;
        }

        /// <summary>
        /// Adds database logging with default configuration
        /// </summary>
        public static ILoggingBuilder AddDatabaseLogging(this ILoggingBuilder builder, DatabaseLoggerOptions options)
        {
            builder.Services.Configure<DatabaseLoggerOptions>(opt =>
            {
                opt.MinLevel = options.MinLevel;
                opt.BufferSize = options.BufferSize;
                opt.FlushIntervalSeconds = options.FlushIntervalSeconds;
                opt.ExcludedCategories = options.ExcludedCategories;
            });

            builder.Services.AddSingleton<ILoggerProvider, DatabaseLoggerProvider>();
            return builder;
        }

        /// <summary>
        /// Adds scraping job logging service
        /// </summary>
        public static IServiceCollection AddScrapingJobLogging(this IServiceCollection services)
        {
            services.AddScoped<IScrapingJobLogService, ScrapingJobLogService>();
            return services;
        }
    }
}