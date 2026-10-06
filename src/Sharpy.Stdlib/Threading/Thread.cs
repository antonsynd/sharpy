using System;
using SysThread = System.Threading.Thread;
using SysVolatile = System.Threading.Volatile;
using SysInterlocked = System.Threading.Interlocked;

namespace Sharpy
{
    /// <summary>
    /// Represents a thread of control, similar to Python's <c>threading.Thread</c>.
    /// Unlike CPython, .NET has no GIL — threads run with true parallelism.
    /// <para>Misuse that depends on the thread's state raises <see cref="RuntimeError"/>, as in python:
    /// <c>start()</c> twice, <c>join()</c> before <c>start()</c> or on the current thread, and setting
    /// <c>daemon</c> once started.</para>
    /// </summary>
    [SharpyModuleType("threading", "Thread")]
    public class Thread
    {
        private readonly SysThread _thread;
        // The state checks read these flags, not the .NET thread's state: SysThread.IsBackground and
        // Join throw ThreadStateException on an unstarted or dead thread, and that type is not one a
        // Sharpy `except` clause names (#2261). The current_thread() wrapper is created started.
        private int _started;
        private bool _daemon;

        internal Thread(SysThread thread)
        {
            _thread = thread;
            _started = 1;
            _daemon = thread.IsBackground;
        }

        public Thread(Action? target = null, bool daemon = false, string? name = null)
        {
            _thread = new SysThread(() =>
            {
                if (target != null)
                {
                    target();
                }
                else
                {
                    Run();
                }
            });
            _thread.IsBackground = daemon;
            _daemon = daemon;
            if (name != null)
            {
                _thread.Name = name;
            }
        }

        public string? Name
        {
            get => _thread.Name;
            set => _thread.Name = value;
        }

        public bool Daemon
        {
            get => _daemon;
            set
            {
                if (SysVolatile.Read(ref _started) != 0)
                {
                    throw new RuntimeError("cannot set daemon status of active thread");
                }
                _thread.IsBackground = value;
                _daemon = value;
            }
        }

        public bool IsAlive()
        {
            return _thread.IsAlive;
        }

        public int Ident => _thread.ManagedThreadId;

        public void Start()
        {
            if (SysInterlocked.Exchange(ref _started, 1) != 0)
            {
                throw new RuntimeError("threads can only be started once");
            }
            _thread.Start();
        }

        public void Join(double? timeout = null)
        {
            if (SysVolatile.Read(ref _started) == 0)
            {
                throw new RuntimeError("cannot join thread before it is started");
            }
            if (_thread == SysThread.CurrentThread)
            {
                throw new RuntimeError("cannot join current thread");
            }
            if (timeout == null)
            {
                _thread.Join();
            }
            else
            {
                int ms = (int)(timeout.Value * 1000);
                _thread.Join(ms);
            }
        }

        /// <summary>
        /// Override this method when subclassing Thread instead of passing a target callable.
        /// </summary>
        public virtual void Run()
        {
        }
    }
}
