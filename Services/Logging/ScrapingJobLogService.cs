using CijeneScraper.Data;
using CijeneScraper.Models.Database;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CijeneScraper.Services.Logging
{
    /// <summary>
    /// Service for managing scraping job logs with detailed tracking
    /// </summary>
    public interface IScrapingJobLogService
    {
        /// <summary>
        /// Start logging a new scraping job
        /// </summary>
        Task<ScrapingJobLog> StartJobAsync(string chainName, DateOnly date, string? initiatedBy = null, 
            string? requestSource = null, bool isForced = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update job progress
        /// </summary>
        Task UpdateProgressAsync(long jobLogId, int? storesProcessed = null, int? productsFound = null, 
            int? priceChanges = null, string? metadata = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Complete a job successfully
        /// </summary>
        Task CompleteJobAsync(long jobLogId, int totalPriceChanges, string? successMessage = null, 
            object? metadata = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Mark a job as failed
        /// </summary>
        Task FailJobAsync(long jobLogId, string errorMessage, string? stackTrace = null, 
            object? metadata = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Mark a job as cancelled
        /// </summary>
        Task CancelJobAsync(long jobLogId, string? reason = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get recent job logs with filtering
        /// </summary>
        Task<List<ScrapingJobLog>> GetRecentJobsAsync(string? chainName = null, int take = 50, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get job statistics
        /// </summary>
        Task<ScrapingJobStatistics> GetStatisticsAsync(DateOnly? fromDate = null, DateOnly? toDate = null, 
            string? chainName = null, CancellationToken cancellationToken = default);
    }

    public class ScrapingJobStatistics
    {
        public int TotalJobs { get; set; }
        public int SuccessfulJobs { get; set; }
        public int FailedJobs { get; set; }
        public int CancelledJobs { get; set; }
        public double SuccessRate { get; set; }
        public TimeSpan AverageDuration { get; set; }
        public int TotalPriceChanges { get; set; }
        public DateTime? LastSuccessfulRun { get; set; }
        public DateTime? LastFailedRun { get; set; }
    }

    public class ScrapingJobLogService : IScrapingJobLogService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<ScrapingJobLogService> _logger;

        public ScrapingJobLogService(ApplicationDbContext dbContext, ILogger<ScrapingJobLogService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<ScrapingJobLog> StartJobAsync(string chainName, DateOnly date, string? initiatedBy = null, 
            string? requestSource = null, bool isForced = false, CancellationToken cancellationToken = default)
        {
            try
            {
                // Get chain ID
                var chain = await _dbContext.Chains.FirstOrDefaultAsync(c => c.Name == chainName, cancellationToken);
                if (chain == null)
                {
                    throw new ArgumentException($"Chain '{chainName}' not found", nameof(chainName));
                }

                var jobLog = new ScrapingJobLog
                {
                    ChainID = chain.Id,
                    Date = date,
                    StartedAt = DateTime.UtcNow,
                    Status = ScrapingJobStatus.Running,
                    InitiatedBy = initiatedBy,
                    RequestSource = requestSource ?? RequestSource.System,
                    IsForced = isForced
                };

                _dbContext.ScrapingJobLogs.Add(jobLog);
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Started scraping job log {JobLogId} for chain {ChainName} on {Date}", 
                    jobLog.Id, chainName, date);

                return jobLog;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start scraping job log for chain {ChainName} on {Date}", chainName, date);
                throw;
            }
        }

        public async Task UpdateProgressAsync(long jobLogId, int? storesProcessed = null, int? productsFound = null, 
            int? priceChanges = null, string? metadata = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var jobLog = await _dbContext.ScrapingJobLogs.FindAsync(new object[] { jobLogId }, cancellationToken);
                if (jobLog == null)
                {
                    _logger.LogWarning("Scraping job log {JobLogId} not found for progress update", jobLogId);
                    return;
                }

                if (storesProcessed.HasValue) jobLog.StoresProcessed = storesProcessed.Value;
                if (productsFound.HasValue) jobLog.ProductsFound = productsFound.Value;
                if (priceChanges.HasValue) jobLog.PriceChanges = priceChanges.Value;
                if (!string.IsNullOrEmpty(metadata)) jobLog.Metadata = metadata;

                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update progress for scraping job log {JobLogId}", jobLogId);
            }
        }

        public async Task CompleteJobAsync(long jobLogId, int totalPriceChanges, string? successMessage = null, 
            object? metadata = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var jobLog = await _dbContext.ScrapingJobLogs.FindAsync(new object[] { jobLogId }, cancellationToken);
                if (jobLog == null)
                {
                    _logger.LogWarning("Scraping job log {JobLogId} not found for completion", jobLogId);
                    return;
                }

                var completedAt = DateTime.UtcNow;
                jobLog.CompletedAt = completedAt;
                jobLog.Status = ScrapingJobStatus.Completed;
                jobLog.PriceChanges = totalPriceChanges;
                jobLog.DurationMs = (long)(completedAt - jobLog.StartedAt).TotalMilliseconds;
                jobLog.SuccessMessage = successMessage;

                if (metadata != null)
                {
                    jobLog.Metadata = JsonSerializer.Serialize(metadata);
                }

                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Completed scraping job log {JobLogId} with {PriceChanges} price changes in {Duration}ms", 
                    jobLogId, totalPriceChanges, jobLog.DurationMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to complete scraping job log {JobLogId}", jobLogId);
                throw;
            }
        }

        public async Task FailJobAsync(long jobLogId, string errorMessage, string? stackTrace = null, 
            object? metadata = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var jobLog = await _dbContext.ScrapingJobLogs.FindAsync(new object[] { jobLogId }, cancellationToken);
                if (jobLog == null)
                {
                    _logger.LogWarning("Scraping job log {JobLogId} not found for failure", jobLogId);
                    return;
                }

                var completedAt = DateTime.UtcNow;
                jobLog.CompletedAt = completedAt;
                jobLog.Status = ScrapingJobStatus.Failed;
                jobLog.DurationMs = (long)(completedAt - jobLog.StartedAt).TotalMilliseconds;
                jobLog.ErrorMessage = errorMessage;
                jobLog.ErrorStackTrace = stackTrace;

                if (metadata != null)
                {
                    jobLog.Metadata = JsonSerializer.Serialize(metadata);
                }

                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogError("Failed scraping job log {JobLogId} after {Duration}ms: {ErrorMessage}", 
                    jobLogId, jobLog.DurationMs, errorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark scraping job log {JobLogId} as failed", jobLogId);
                throw;
            }
        }

        public async Task CancelJobAsync(long jobLogId, string? reason = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var jobLog = await _dbContext.ScrapingJobLogs.FindAsync(new object[] { jobLogId }, cancellationToken);
                if (jobLog == null)
                {
                    _logger.LogWarning("Scraping job log {JobLogId} not found for cancellation", jobLogId);
                    return;
                }

                var completedAt = DateTime.UtcNow;
                jobLog.CompletedAt = completedAt;
                jobLog.Status = ScrapingJobStatus.Cancelled;
                jobLog.DurationMs = (long)(completedAt - jobLog.StartedAt).TotalMilliseconds;
                jobLog.ErrorMessage = reason ?? "Job was cancelled";

                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Cancelled scraping job log {JobLogId} after {Duration}ms: {Reason}", 
                    jobLogId, jobLog.DurationMs, reason ?? "No reason provided");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel scraping job log {JobLogId}", jobLogId);
                throw;
            }
        }

        public async Task<List<ScrapingJobLog>> GetRecentJobsAsync(string? chainName = null, int take = 50, 
            CancellationToken cancellationToken = default)
        {
            var query = _dbContext.ScrapingJobLogs
                .Include(j => j.Chain)
                .AsQueryable();

            if (!string.IsNullOrEmpty(chainName))
            {
                query = query.Where(j => j.Chain.Name == chainName);
            }

            return await query
                .OrderByDescending(j => j.StartedAt)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public async Task<ScrapingJobStatistics> GetStatisticsAsync(DateOnly? fromDate = null, DateOnly? toDate = null, 
            string? chainName = null, CancellationToken cancellationToken = default)
        {
            var query = _dbContext.ScrapingJobLogs
                .Include(j => j.Chain)
                .AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(j => j.Date >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(j => j.Date <= toDate.Value);

            if (!string.IsNullOrEmpty(chainName))
                query = query.Where(j => j.Chain.Name == chainName);

            var completedJobs = await query.Where(j => j.CompletedAt != null).ToListAsync(cancellationToken);

            var totalJobs = completedJobs.Count;
            var successfulJobs = completedJobs.Count(j => j.Status == ScrapingJobStatus.Completed);
            var failedJobs = completedJobs.Count(j => j.Status == ScrapingJobStatus.Failed);
            var cancelledJobs = completedJobs.Count(j => j.Status == ScrapingJobStatus.Cancelled);

            var successRate = totalJobs > 0 ? (double)successfulJobs / totalJobs * 100 : 0;
            var avgDuration = completedJobs.Any(j => j.DurationMs.HasValue) 
                ? TimeSpan.FromMilliseconds(completedJobs.Where(j => j.DurationMs.HasValue).Average(j => j.DurationMs!.Value))
                : TimeSpan.Zero;

            return new ScrapingJobStatistics
            {
                TotalJobs = totalJobs,
                SuccessfulJobs = successfulJobs,
                FailedJobs = failedJobs,
                CancelledJobs = cancelledJobs,
                SuccessRate = successRate,
                AverageDuration = avgDuration,
                TotalPriceChanges = completedJobs.Where(j => j.PriceChanges.HasValue).Sum(j => j.PriceChanges!.Value),
                LastSuccessfulRun = completedJobs
                    .Where(j => j.Status == ScrapingJobStatus.Completed)
                    .Max(j => j.CompletedAt),
                LastFailedRun = completedJobs
                    .Where(j => j.Status == ScrapingJobStatus.Failed)
                    .Max(j => j.CompletedAt)
            };
        }
    }
}