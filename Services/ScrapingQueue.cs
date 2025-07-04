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
        /// <summary>
        /// Internal concurrent queue holding scraping tasks.
        /// </summary>
        private readonly ConcurrentQueue<Func<CancellationToken, Task>> _tasks = new();

        /// <summary>
        /// Enqueues a new scraping task to the queue.
        /// </summary>
        /// <param name="task">A function representing the scraping task, accepting a <see cref="CancellationToken"/>.</param>
        public void Enqueue(Func<CancellationToken, Task> task)
        {
            _tasks.Enqueue(task);
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
    }
}