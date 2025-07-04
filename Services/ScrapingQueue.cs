using System.Collections.Concurrent;

namespace CijeneScraper.Services
{
    public class ScrapingQueue
    {
        private readonly ConcurrentQueue<Func<CancellationToken, Task>> _tasks = new();

        public void Enqueue(Func<CancellationToken, Task> task)
        {
            _tasks.Enqueue(task);
        }

        public bool TryDequeue(out Func<CancellationToken, Task>? task)
        {
            return _tasks.TryDequeue(out task);
        }
    }
}
