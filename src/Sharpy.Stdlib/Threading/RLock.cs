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
            // python checks the arguments before it looks at the owner, so the owning thread's
            // re-entrant acquire refuses a bad timeout too.
            int ms = Lock.AcquireMilliseconds(blocking, timeout);
            bool acquired = Monitor.TryEnter(_lock, ms);
            if (acquired)
            {
                _owner = SysThread.CurrentThread.ManagedThreadId;
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

        // Context-manager protocol. The Sharpy emitter detects __enter__/__exit__ on the
        // discovered type symbol and lowers `with rl:` to calls to Enter()/Exit(); the dunder
        // names below are the discovery markers that route to the real Enter()/Exit() methods.
        public RLock Enter()
        {
            Acquire();
            return this;
        }

        public void Exit()
        {
            Release();
        }

        public RLock __enter__() => Enter();

        public void __exit__(object? excType = null, object? excVal = null, object? excTb = null) => Exit();

        public void Dispose()
        {
        }
    }
}
