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

        public bool Acquire(bool blocking = true, double? timeout = null)
        {
            return AcquireSlim(_semaphore, blocking, timeout);
        }

        /// <summary>
        /// python's <c>Semaphore.acquire</c>, which <c>BoundedSemaphore</c> inherits there: a timeout
        /// on a non-blocking call is <see cref="ValueError"/>; a free slot is taken whatever the
        /// timeout; otherwise a timeout at or below zero gives up at once, and python converts the
        /// timeout only when it waits, so a value it cannot represent raises only then (#2263).
        /// </summary>
        internal static bool AcquireSlim(SemaphoreSlim semaphore, bool blocking, double? timeout)
        {
            if (!blocking && timeout != null)
            {
                throw new ValueError("can't specify timeout for non-blocking acquire");
            }
            if (semaphore.Wait(0))
            {
                return true;
            }
            if (!blocking)
            {
                return false;
            }
            if (timeout == null)
            {
                semaphore.Wait();
                return true;
            }
            if (timeout.Value <= 0)
            {
                return false;
            }
            WaitTimeout.CheckRepresentable(timeout.Value);
            return semaphore.Wait(WaitTimeout.ToMilliseconds(timeout.Value));
        }

        public void Release()
        {
            _semaphore.Release();
        }

        // Context-manager protocol. The Sharpy emitter detects __enter__/__exit__ on the
        // discovered type symbol and lowers `with sem:` to calls to Enter()/Exit(); the dunder
        // names below are the discovery markers that route to the real Enter()/Exit() methods.
        public Semaphore Enter()
        {
            Acquire();
            return this;
        }

        public void Exit()
        {
            Release();
        }

        public Semaphore __enter__() => Enter();

        public void __exit__(object? excType = null, object? excVal = null, object? excTb = null) => Exit();

        public void Dispose()
        {
            _semaphore.Dispose();
        }
    }
}
