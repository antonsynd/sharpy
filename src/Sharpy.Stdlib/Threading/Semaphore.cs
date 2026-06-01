using System;
using System.Threading;

namespace Sharpy
{
    /// <summary>
    /// A counting semaphore, similar to Python's <c>threading.Semaphore</c>.
    /// </summary>
    [SharpyModuleType("threading", "Semaphore")]
    public class Semaphore : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;

        public Semaphore(int value = 1)
        {
            if (value < 0)
            {
                throw new ValueError("semaphore initial value must be >= 0");
            }
            _semaphore = new SemaphoreSlim(value, int.MaxValue);
        }

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
            _semaphore.Release();
        }

        public Semaphore __enter__()
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
