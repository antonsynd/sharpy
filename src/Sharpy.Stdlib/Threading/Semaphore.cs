using System;
using System.Threading;

namespace Sharpy
{
    /// <summary>
    /// A counting semaphore, similar to Python's <c>threading.Semaphore</c>.
    /// Wraps <see cref="SemaphoreSlim"/>.
    /// </summary>
    [SharpyModuleType("threading", "Semaphore")]
    public class Semaphore : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;

        /// <summary>
        /// Create a new Semaphore with the given initial count.
        /// </summary>
        /// <param name="value">The initial count (default 1).</param>
        public Semaphore(int value = 1)
        {
            if (value < 0)
            {
                throw new ValueError("semaphore initial value must be >= 0");
            }
            _semaphore = new SemaphoreSlim(value, int.MaxValue);
        }

        /// <summary>
        /// Acquire the semaphore (decrement the counter).
        /// </summary>
        /// <param name="blocking">If true (default), block until acquired.</param>
        /// <param name="timeout">Optional timeout in seconds. -1 means wait forever.</param>
        /// <returns>True if acquired, false otherwise.</returns>
        public bool Acquire(bool blocking = true, double timeout = -1)
        {
            if (!blocking)
            {
                return _semaphore.Wait(0);
            }

            if (timeout < 0)
            {
                _semaphore.Wait();
                return true;
            }

            int ms = (int)(timeout * 1000);
            return _semaphore.Wait(ms);
        }

        /// <summary>
        /// Release the semaphore (increment the counter).
        /// </summary>
        public void Release()
        {
            _semaphore.Release();
        }

        /// <summary>
        /// Context manager entry — acquires the semaphore.
        /// </summary>
        /// <returns>This semaphore instance.</returns>
        public Semaphore __enter__()
        {
            Acquire();
            return this;
        }

        /// <summary>
        /// Context manager exit — releases the semaphore.
        /// </summary>
        public void __exit__(object? excType = null, object? excVal = null, object? excTb = null)
        {
            Release();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _semaphore.Dispose();
        }
    }
}
