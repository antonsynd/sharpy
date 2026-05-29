using System;
using SysThread = System.Threading.Thread;

namespace Sharpy
{
    /// <summary>
    /// Represents a thread of control, similar to Python's <c>threading.Thread</c>.
    /// Wraps <see cref="System.Threading.Thread"/>.
    /// </summary>
    /// <remarks>
    /// Unlike CPython, .NET has no GIL — threads run with true parallelism.
    /// </remarks>
    [SharpyModuleType("threading", "Thread")]
    public class Thread
    {
        private readonly SysThread _thread;

        /// <summary>
        /// Create a new Thread that wraps an existing <see cref="System.Threading.Thread"/>.
        /// </summary>
        internal Thread(SysThread thread)
        {
            _thread = thread;
        }

        /// <summary>
        /// Create a new Thread with a target callable.
        /// </summary>
        /// <param name="target">The callable to invoke when the thread starts.</param>
        /// <param name="daemon">Whether the thread is a daemon thread.</param>
        /// <param name="name">Optional thread name.</param>
        public Thread(Action target, bool daemon = false, string? name = null)
        {
            _thread = new SysThread(() => target());
            _thread.IsBackground = daemon;
            if (name != null)
            {
                _thread.Name = name;
            }
        }

        /// <summary>
        /// The thread's name.
        /// </summary>
        public string? Name
        {
            get => _thread.Name;
            set => _thread.Name = value;
        }

        /// <summary>
        /// Whether the thread is a daemon thread.
        /// A daemon thread does not prevent the process from exiting.
        /// Maps to <see cref="System.Threading.Thread.IsBackground"/>.
        /// </summary>
        public bool Daemon
        {
            get => _thread.IsBackground;
            set => _thread.IsBackground = value;
        }

        /// <summary>
        /// Whether the thread is alive (has been started and has not yet terminated).
        /// </summary>
        public bool IsAlive => _thread.IsAlive;

        /// <summary>
        /// The thread's identifier.
        /// </summary>
        public int Ident => _thread.ManagedThreadId;

        /// <summary>
        /// Start the thread's activity.
        /// </summary>
        /// <exception cref="RuntimeError">If the thread has already been started.</exception>
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

        /// <summary>
        /// Wait until the thread terminates.
        /// </summary>
        /// <param name="timeout">Optional timeout in seconds. If null, blocks indefinitely.</param>
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
    }
}
