using System;
using System.Collections.Generic;
using System.Linq;
using SysThread = System.Threading.Thread;

namespace Sharpy
{
    /// <summary>
    /// Thread-based parallelism module, similar to Python's <c>threading</c> module.
    /// </summary>
    /// <remarks>
    /// Unlike CPython, .NET has no GIL — threads run with true parallelism.
    /// This means race conditions are possible without explicit synchronization.
    /// </remarks>
    public static partial class ThreadingModule
    {
        /// <summary>
        /// Return the current Thread object corresponding to the caller's thread of control.
        /// </summary>
        /// <returns>The current <see cref="Thread"/> wrapper.</returns>
        public static Thread CurrentThread()
        {
            return new Thread(SysThread.CurrentThread);
        }

        /// <summary>
        /// Return the number of Thread objects currently alive.
        /// </summary>
        /// <returns>The count of active threads.</returns>
        public static int ActiveCount()
        {
            return System.Diagnostics.Process.GetCurrentProcess().Threads.Count;
        }

        /// <summary>
        /// Create a new <see cref="Lock"/> (non-reentrant mutex).
        /// </summary>
        /// <returns>A new Lock instance.</returns>
        public static Lock Lock()
        {
            return new Lock();
        }

        /// <summary>
        /// Create a new <see cref="RLock"/> (reentrant mutex).
        /// </summary>
        /// <returns>A new RLock instance.</returns>
        public static RLock RLock()
        {
            return new RLock();
        }

        /// <summary>
        /// Create a new <see cref="Event"/> for thread signaling.
        /// </summary>
        /// <returns>A new Event instance.</returns>
        public static Event Event()
        {
            return new Event();
        }

        /// <summary>
        /// Create a new <see cref="Semaphore"/> with the given initial value.
        /// </summary>
        /// <param name="value">The initial semaphore count (default 1).</param>
        /// <returns>A new Semaphore instance.</returns>
        public static Semaphore Semaphore(int value = 1)
        {
            return new Semaphore(value);
        }

        /// <summary>
        /// Create a new <see cref="Barrier"/> for the given number of parties.
        /// </summary>
        /// <param name="parties">The number of threads that must call wait() before they are all released.</param>
        /// <returns>A new Barrier instance.</returns>
        public static Barrier Barrier(int parties)
        {
            return new Barrier(parties);
        }
    }
}
