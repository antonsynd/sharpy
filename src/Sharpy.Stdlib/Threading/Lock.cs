using System;
using System.Threading;

namespace Sharpy
{
    /// <summary>
    /// A non-reentrant mutual exclusion lock, similar to Python's <c>threading.Lock</c>.
    /// Implements <see cref="IDisposable"/> for context manager (with-statement) support.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="SemaphoreSlim"/> internally to provide non-reentrant behavior.
    /// </remarks>
    [SharpyModuleType("threading", "Lock")]
    public class Lock : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Acquire the lock, blocking until it becomes available.
        /// </summary>
        /// <param name="blocking">If true (default), block until the lock is acquired. If false, return immediately.</param>
        /// <param name="timeout">Optional timeout in seconds (only used when blocking is true). -1 means wait forever.</param>
        /// <returns>True if the lock was acquired, false otherwise.</returns>
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
        /// Release the lock.
        /// </summary>
        /// <exception cref="RuntimeError">If the lock is not currently acquired.</exception>
        public void Release()
        {
            try
            {
                _semaphore.Release();
            }
            catch (SemaphoreFullException)
            {
                throw new RuntimeError("release unlocked lock");
            }
        }

        /// <summary>
        /// Whether the lock is currently held.
        /// </summary>
        public bool Locked()
        {
            return _semaphore.CurrentCount == 0;
        }

        /// <summary>
        /// Context manager entry — acquires the lock.
        /// </summary>
        /// <returns>This lock instance.</returns>
        public Lock __enter__()
        {
            Acquire();
            return this;
        }

        /// <summary>
        /// Context manager exit — releases the lock.
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
