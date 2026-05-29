using System;
using System.Threading;
using SysThread = System.Threading.Thread;

namespace Sharpy
{
    /// <summary>
    /// A reentrant mutual exclusion lock, similar to Python's <c>threading.RLock</c>.
    /// The same thread may acquire it multiple times without deadlocking.
    /// </summary>
    [SharpyModuleType("threading", "RLock")]
    public class RLock : IDisposable
    {
        private readonly object _lock = new object();
        private int _owner = -1;
        private int _count = 0;

        /// <summary>
        /// Acquire the lock. The same thread may acquire it multiple times.
        /// </summary>
        /// <param name="blocking">If true (default), block until the lock is acquired.</param>
        /// <param name="timeout">Optional timeout in seconds (only used when blocking is true). -1 means wait forever.</param>
        /// <returns>True if the lock was acquired, false otherwise.</returns>
        public bool Acquire(bool blocking = true, double timeout = -1)
        {
            int currentId = SysThread.CurrentThread.ManagedThreadId;

            lock (_lock)
            {
                if (_owner == currentId)
                {
                    _count++;
                    return true;
                }
            }

            bool acquired;
            if (!blocking)
            {
                acquired = Monitor.TryEnter(_lock, 0);
            }
            else if (timeout < 0)
            {
                Monitor.Enter(_lock);
                acquired = true;
            }
            else
            {
                int ms = (int)(timeout * 1000);
                acquired = Monitor.TryEnter(_lock, ms);
            }

            if (acquired)
            {
                _owner = currentId;
                _count = 1;
            }

            return acquired;
        }

        /// <summary>
        /// Release the lock (decrementing the recursion count).
        /// </summary>
        /// <exception cref="RuntimeError">If the lock is not owned by the current thread.</exception>
        public void Release()
        {
            int currentId = SysThread.CurrentThread.ManagedThreadId;
            if (_owner != currentId)
            {
                throw new RuntimeError("cannot release un-acquired lock");
            }

            _count--;
            if (_count == 0)
            {
                _owner = -1;
                Monitor.Exit(_lock);
            }
        }

        /// <summary>
        /// Context manager entry — acquires the lock.
        /// </summary>
        /// <returns>This lock instance.</returns>
        public RLock __enter__()
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
            // No unmanaged resources — provided for pattern consistency.
        }
    }
}
