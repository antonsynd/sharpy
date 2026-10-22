using System;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// Thread-based parallelism module, similar to Python's <c>threading</c> module.
    /// Unlike CPython, .NET has no GIL — threads run with true parallelism.
    /// </summary>
    public static partial class ThreadingModule
    {
        /// <summary>
        /// The largest timeout, in seconds, that a blocking call accepts; a larger one raises
        /// OverflowError. A wait longer than about 24.8 days is cut short at 24.8 days.
        /// </summary>
        // python's threading.TIMEOUT_MAX; the 24.8-day cap is the deviation row
        // stdlib-timeout-wait-saturates (R-FH, #2263).
        public const double TIMEOUT_MAX = 9223372036.0;

        /// <summary>
        /// Return the Thread object of the calling thread: the object <c>start()</c> was called
        /// on, or the main thread. A thread the threading module did not start gets a daemon
        /// <c>Dummy-N</c> thread, which stays in <c>enumerate()</c>.
        /// </summary>
        public static Thread CurrentThread()
        {
            return ThreadRegistry.Current();
        }

        /// <summary>
        /// Return the number of Thread objects currently alive: the length of <c>enumerate()</c>.
        /// </summary>
        public static int ActiveCount()
        {
            return ThreadRegistry.Count();
        }

        /// <summary>
        /// Return the main Thread object, named <c>MainThread</c>: the thread that first used the
        /// threading module, which in a Sharpy program is the thread <c>main()</c> runs on.
        /// </summary>
        public static Thread MainThread()
        {
            return ThreadRegistry.Main;
        }

        /// <summary>
        /// Return a list of every Thread object currently alive: the main thread, every started
        /// thread that has not finished, and the dummy threads <c>current_thread()</c> created.
        /// </summary>
        public static List<Thread> Enumerate()
        {
            var result = new List<Thread>();
            foreach (var thread in ThreadRegistry.Snapshot())
            {
                result.Add(thread);
            }
            return result;
        }

        public static Lock Lock()
        {
            return new Lock();
        }

        public static RLock RLock()
        {
            return new RLock();
        }

        public static Event Event()
        {
            return new Event();
        }

        public static Semaphore Semaphore(int value = 1)
        {
            return new Semaphore(value);
        }

        public static BoundedSemaphore BoundedSemaphore(int value = 1)
        {
            return new BoundedSemaphore(value);
        }

        public static Barrier Barrier(int parties, Action? action = null, double? timeout = null)
        {
            return new Barrier(parties, action, timeout);
        }

        public static Timer Timer(double interval, Action function)
        {
            return new Timer(interval, function);
        }
    }
}
