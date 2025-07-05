using System.Diagnostics;

namespace CijeneScraper.Utility
{
    /// <summary>
    /// Represents a timed operation that logs the start and end (with duration) of a code block.
    /// Use with a using statement to automatically log the duration when disposed.
    /// </summary>
    public class TimedOperation : IDisposable
    {
        /// <summary>
        /// Stopwatch used to measure the duration of the operation.
        /// </summary>
        private readonly Stopwatch _stopwatch;

        /// <summary>
        /// Logger used to log operation start and completion messages.
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Name of the operation being timed.
        /// </summary>
        private readonly string _operationName;

        /// <summary>
        /// Initializes a new instance of the <see cref="TimedOperation"/> class and starts timing.
        /// Logs the start of the operation.
        /// </summary>
        /// <param name="logger">The logger to use for logging messages.</param>
        /// <param name="operationName">The name of the operation being timed.</param>
        public TimedOperation(ILogger logger, string operationName)
        {
            _logger = logger;
            _operationName = operationName;
            _stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("⏱️ Started: {OperationName}", operationName);
        }

        /// <summary>
        /// Stops the timer and logs the completion of the operation with the elapsed duration.
        /// </summary>
        public void Dispose()
        {
            _stopwatch.Stop();
            _logger.LogInformation("✅ Completed: {OperationName} in {Duration:hh\\:mm\\:ss\\.fff}",
                _operationName, _stopwatch.Elapsed);
        }
    }
}