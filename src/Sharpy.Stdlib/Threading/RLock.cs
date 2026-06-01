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

        public bool Acquire(bool blocking = true, double timeout = -1)
        {
            int currentId = SysThread.CurrentThread.ManagedThreadId;

            if (!blocking)
            {
                bool entered = Monitor.TryEnter(_lock, 0);
                if (entered)
                {
                    _owner = currentId;
                    _count++;
                }
                return entered;
            }

            if (timeout < 0)
            {
                Monitor.Enter(_lock);
                _owner = currentId;
                _count++;
                return true;
            }

            int ms = (int)(timeout * 1000);
            bool acquired = Monitor.TryEnter(_lock, ms);
            if (acquired)
            {
                _owner = currentId;
                _count++;
            }
            return acquired;
        }

        public void Release()
        {
            if (_owner != SysThread.CurrentThread.ManagedThreadId)
            {
                throw new RuntimeError("cannot release un-acquired lock");
            }

            _count--;
            if (_count == 0)
            {
                _owner = -1;
            }
            Monitor.Exit(_lock);
        }

        public RLock __enter__()
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
        }
    }
}
