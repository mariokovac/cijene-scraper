using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace CijeneScraper.Services
{
    /// <summary>
    /// Thread-safe queue for managing scraping tasks.
    /// </summary>
    public class ScrapingQueue
    {
        private readonly object _lock = new();
        private Task? _currentTask;
        private CancellationTokenSource? _cts;

        /// <summary>
        /// Internal concurrent queue holding scraping tasks.
        /// </summary>
        private readonly ConcurrentQueue<Func<CancellationToken, Task>> _tasks = new();

        /// <summary>
        /// Indicates whether a scraping task is currently running.
        /// </summary>
        public bool IsRunning
        {
            get
            {
                lock (_lock)
                {
                    return _currentTask != null && !_currentTask.IsCompleted;
                }
            }
        }

        /// <summary>
        /// Enqueues a new scraping task to the queue.
        /// </summary>
        /// <param name="task">A function representing the scraping task, accepting a <see cref="CancellationToken"/>.</param>
        public void Enqueue(Func<CancellationToken, Task> task)
        {
            lock (_lock)
            {
                // Cancel current task if running
                if (IsRunning)
                {
                    _cts?.Cancel();

                    // Clear the queue of pending tasks
                    while (_tasks.TryDequeue(out _)) { }
                }

                // Add new task to queue
                _tasks.Enqueue(task);

                // Start processing if not already running
                if (!IsRunning)
                {
                    _ = Task.Run(ProcessQueueAsync);
                }
            }
        }

        /// <summary>
        /// Attempts to dequeue a scraping task from the queue.
        /// </summary>
        /// <param name="task">The dequeued task if available; otherwise, null.</param>
        /// <returns>True if a task was dequeued; otherwise, false.</returns>
        public bool TryDequeue(out Func<CancellationToken, Task>? task)
        {
            return _tasks.TryDequeue(out task);
        }

        /// <summary>
        /// Cancels the currently running scraping task.
        /// </summary>
        public void CancelCurrent()
        {
            lock (_lock)
            {
                _cts?.Cancel();
            }
        }

        /// <summary>
        /// Processes tasks from the queue sequentially.
        /// </summary>
        private async Task ProcessQueueAsync()
        {
            while (_tasks.TryDequeue(out var task))
            {
                lock (_lock)
                {
                    _cts?.Dispose();
                    _cts = new CancellationTokenSource();
                }

                try
                {
                    _currentTask = task(_cts.Token);
                    await _currentTask;
                }
                catch (OperationCanceledException)
                {
                    // Task was cancelled, continue to next task
                }
                catch (Exception)
                {
                    // Log exception if needed, but continue processing
                    throw;
                }
            }

            lock (_lock)
            {
                _currentTask = null;
                _cts?.Dispose();
                _cts = null;
            }
        }
    }
}