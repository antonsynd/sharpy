using System.Globalization;
using SysInterlocked = System.Threading.Interlocked;
using SysThread = System.Threading.Thread;

namespace Sharpy
{
    /// <summary>
    /// The threading module's process-wide state, modelled on CPython 3.12's <c>threading.py</c>
    /// (#2261 part 2): <c>_counter</c> numbers the default names <c>Thread-N</c> and
    /// <c>Dummy-N</c>; <c>_active</c> (with <c>_limbo</c>) holds every thread that
    /// <c>enumerate()</c> and <c>active_count()</c> report; <c>_main_thread</c> is the thread on
    /// which the module was first used.
    /// <para>Like CPython's <c>_main_thread = _MainThread()</c>, which runs on the thread that imports
    /// <c>threading</c>, <see cref="Main"/> is the thread that first reaches this class: every
    /// <see cref="Thread"/> constructor and every module function touches it, so in a Sharpy program
    /// it is the thread <c>main()</c> runs on. The explicit static constructor makes that first
    /// touch exact (no <c>beforefieldinit</c>).</para>
    /// <para>Thread safety: <c>_active</c> is read and written only under <see cref="ActiveLock"/>,
    /// as CPython reads and writes it only under <c>_active_limbo_lock</c>; <c>_counter</c> is
    /// incremented atomically, as <c>itertools.count.__next__</c> is one call under the GIL. Both
    /// are rows of StdlibModuleGlobalRosterTests (Stdlib.Tests).</para>
    /// </summary>
    internal static class ThreadRegistry
    {
        private static readonly object ActiveLock = new object();

        // CPython's _active ∪ _limbo: the main thread, every started Thread that has not finished,
        // and every dummy, in the order they were added (enumerate()'s order). A Thread is added by
        // start() before its .NET thread starts and removed on its own thread as its body ends, so
        // it is present while it runs and gone once join() returns. Guarded by ActiveLock.
        private static readonly System.Collections.Generic.List<Thread> _active = new System.Collections.Generic.List<Thread>();

        // CPython's _counter = itertools.count(1): Thread-N and Dummy-N share it; a named thread
        // and the main thread do not consume a number. Incremented with Interlocked only.
        private static int _counter;

        /// <summary>CPython's <c>_main_thread</c>: named <c>MainThread</c>, not a daemon, created started.</summary>
        internal static readonly Thread Main = RegisterMain();

        static ThreadRegistry()
        {
        }

        private static Thread RegisterMain()
        {
            var main = new Thread(SysThread.CurrentThread, "MainThread", daemon: false);
            lock (ActiveLock)
            {
                _active.Add(main);
            }
            return main;
        }

        /// <summary>
        /// python's <c>name or _newname("Thread-%d")</c>: an empty or absent name takes the next
        /// <c>Thread-N</c>. Always touches this class, so <see cref="Main"/> is captured first.
        /// </summary>
        internal static string NameOrDefault(string? name)
        {
            return string.IsNullOrEmpty(name) ? NewName("Thread-") : name!;
        }

        private static string NewName(string prefix)
        {
            return prefix + SysInterlocked.Increment(ref _counter).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Adds a thread that is about to start (python's <c>_limbo[self] = self</c>).</summary>
        internal static void Register(Thread thread)
        {
            lock (ActiveLock)
            {
                _active.Add(thread);
            }
        }

        /// <summary>Removes a thread whose body has ended (python's <c>_delete()</c>).</summary>
        internal static void Unregister(Thread thread)
        {
            lock (ActiveLock)
            {
                _active.Remove(thread);
            }
        }

        /// <summary>
        /// python's <c>current_thread()</c>: the <see cref="Thread"/> object whose .NET thread is the
        /// caller's — the same object <c>start()</c> was called on, or <see cref="Main"/>. A thread
        /// the threading module did not start gets a <c>Dummy-N</c> daemon thread that stays in
        /// <c>enumerate()</c>, as python's <c>_DummyThread</c> does; one whose ident a dead dummy
        /// still holds replaces that dummy.
        /// </summary>
        internal static Thread Current()
        {
            var current = SysThread.CurrentThread;
            lock (ActiveLock)
            {
                for (var i = 0; i < _active.Count; i++)
                {
                    if (_active[i].NativeThread == current)
                    {
                        return _active[i];
                    }
                }

                var dummy = new Thread(current, NewName("Dummy-"), daemon: true);
                _active.RemoveAll(t => t.Ident == dummy.Ident);
                _active.Add(dummy);
                return dummy;
            }
        }

        /// <summary>python's <c>active_count()</c>: <c>len(_active) + len(_limbo)</c>.</summary>
        internal static int Count()
        {
            lock (ActiveLock)
            {
                return _active.Count;
            }
        }

        /// <summary>python's <c>enumerate()</c>: a snapshot of every thread <see cref="Count"/> counts.</summary>
        internal static Thread[] Snapshot()
        {
            lock (ActiveLock)
            {
                return _active.ToArray();
            }
        }
    }
}
