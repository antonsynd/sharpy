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
            return _semaphore.Wait(AcquireMilliseconds(blocking, timeout));
        }

        /// <summary>
        /// python's argument check for <c>Lock.acquire</c> and <c>RLock.acquire</c> (CPython's
        /// <c>lock_acquire_parse_args</c>), as the milliseconds to wait: <c>-1</c> (wait forever) for a
        /// blocking call with the default <c>timeout=-1</c>, <c>0</c> for a non-blocking call. python
        /// converts the timeout first, so a value it cannot represent raises even on a non-blocking
        /// call; then a timeout on a non-blocking call, and a negative timeout other than <c>-1</c>,
        /// are <see cref="ValueError"/> (#2263).
        /// </summary>
        internal static int AcquireMilliseconds(bool blocking, double timeout)
        {
            WaitTimeout.CheckRepresentable(timeout);
            if (!blocking)
            {
                if (timeout != -1)
                {
                    throw new ValueError("can't specify a timeout for a non-blocking call");
                }
                return 0;
            }
            if (timeout == -1)
            {
                return System.Threading.Timeout.Infinite;
            }
            if (timeout < 0)
            {
                throw new ValueError("timeout value must be positive");
            }
            return WaitTimeout.ToMilliseconds(timeout);
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

        // Context-manager protocol. The Sharpy emitter detects __enter__/__exit__ on the
        // discovered type symbol and lowers `with lk:` to calls to Enter()/Exit(); the dunder
        // names below are the discovery markers that route to the real Enter()/Exit() methods.
        public Lock Enter()
        {
            Acquire();
            return this;
        }

        public void Exit()
        {
            Release();
        }

        public Lock __enter__() => Enter();

        public void __exit__(object? excType = null, object? excVal = null, object? excTb = null) => Exit();

        public void Dispose()
        {
            _semaphore.Dispose();
        }
    }
}
