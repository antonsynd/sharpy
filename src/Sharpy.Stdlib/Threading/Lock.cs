using System;
using System.Threading;

namespace Sharpy
{
    /// <summary>
    /// A non-reentrant mutual exclusion lock, similar to Python's <c>threading.Lock</c>.
    /// </summary>
    [SharpyModuleType("threading", "Lock")]
    public class Lock : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

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

        public bool Locked()
        {
            return _semaphore.CurrentCount == 0;
        }

        public Lock __enter__()
        {
            Acquire();
            return this;
        }

        public void __exit__(object? excType = null, object? excVal = null, object? excTb = null)
        {
            Release();
        }

        public void Dispose()
        {
            _semaphore.Dispose();
        }
    }
}
