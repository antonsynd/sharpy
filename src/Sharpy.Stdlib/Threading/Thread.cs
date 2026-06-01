using System;
using SysThread = System.Threading.Thread;

namespace Sharpy
{
    /// <summary>
    /// Represents a thread of control, similar to Python's <c>threading.Thread</c>.
    /// Unlike CPython, .NET has no GIL — threads run with true parallelism.
    /// </summary>
    [SharpyModuleType("threading", "Thread")]
    public class Thread
    {
        private readonly SysThread _thread;

        internal Thread(SysThread thread)
        {
            _thread = thread;
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
            get => _thread.IsBackground;
            set => _thread.IsBackground = value;
        }

        public bool IsAlive => _thread.IsAlive;

        public int Ident => _thread.ManagedThreadId;

        public void Start()
        {
            try
            {
                _thread.Start();
            }
            catch (System.Threading.ThreadStateException)
            {
                throw new RuntimeError("threads can only be started once");
            }
        }

        public void Join(double? timeout = null)
        {
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
