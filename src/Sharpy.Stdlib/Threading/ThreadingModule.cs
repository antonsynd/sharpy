using System;
using System.Collections.Generic;
using System.Linq;
using SysThread = System.Threading.Thread;

namespace Sharpy
{
    /// <summary>
    /// Thread-based parallelism module, similar to Python's <c>threading</c> module.
    /// Unlike CPython, .NET has no GIL — threads run with true parallelism.
    /// </summary>
    public static partial class ThreadingModule
    {
        public static Thread CurrentThread()
        {
            return new Thread(SysThread.CurrentThread);
        }

        public static int ActiveCount()
        {
            return System.Diagnostics.Process.GetCurrentProcess().Threads.Count;
        }

        public static Thread MainThread()
        {
            return new Thread(SysThread.CurrentThread);
        }

        public static List<Thread> Enumerate()
        {
            var result = new List<Thread>();
            result.Add(CurrentThread());
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

        public static Barrier Barrier(int parties)
        {
            return new Barrier(parties);
        }

        public static Timer Timer(double interval, Action function)
        {
            return new Timer(interval, function);
        }
    }
}
