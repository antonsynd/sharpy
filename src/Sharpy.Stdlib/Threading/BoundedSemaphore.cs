using System;
using System.Threading;

namespace Sharpy
{
    /// <summary>
    /// A bounded semaphore that checks that the counter never exceeds its initial value,
    /// similar to Python's <c>threading.BoundedSemaphore</c>.
    /// </summary>
    [SharpyModuleType("threading", "BoundedSemaphore")]
    public class BoundedSemaphore : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly int _maxValue;
        private int _currentValue;
        private readonly object _countLock = new object();

        public BoundedSemaphore(int value = 1)
        {
            if (value < 0)
            {
                throw new ValueError("semaphore initial value must be >= 0");
            }
            _maxValue = value;
            _currentValue = value;
            _semaphore = new SemaphoreSlim(value, value);
        }

        public bool Acquire(bool blocking = true, double timeout = -1)
        {
            bool acquired;
            if (!blocking)
            {
                acquired = _semaphore.Wait(0);
            }
            else if (timeout < 0)
            {
                _semaphore.Wait();
                acquired = true;
            }
            else
            {
                acquired = _semaphore.Wait((int)(timeout * 1000));
            }

            if (acquired)
            {
                Interlocked.Decrement(ref _currentValue);
            }
            return acquired;
        }

        public void Release()
        {
            lock (_countLock)
            {
                if (_currentValue >= _maxValue)
                {
                    throw new ValueError("Semaphore released too many times");
                }
                _currentValue++;
            }
            _semaphore.Release();
        }

        public BoundedSemaphore __enter__()
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
