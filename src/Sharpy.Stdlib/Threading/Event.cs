using System;
using System.Threading;

namespace Sharpy
{
    /// <summary>
    /// A thread synchronization event, similar to Python's <c>threading.Event</c>.
    /// Wraps <see cref="ManualResetEventSlim"/>.
    /// </summary>
    [SharpyModuleType("threading", "Event")]
    public class Event : IDisposable
    {
        private readonly ManualResetEventSlim _event = new ManualResetEventSlim(false);

        /// <summary>
        /// Set the internal flag to true. All threads waiting for it to become true are awakened.
        /// </summary>
        public void Set()
        {
            _event.Set();
        }

        /// <summary>
        /// Reset the internal flag to false.
        /// </summary>
        public void Clear()
        {
            _event.Reset();
        }

        /// <summary>
        /// Return true if and only if the internal flag is true.
        /// </summary>
        public bool IsSet()
        {
            return _event.IsSet;
        }

        /// <summary>
        /// Block until the internal flag is true.
        /// </summary>
        /// <param name="timeout">Optional timeout in seconds. If null, blocks indefinitely.</param>
        /// <returns>True if the event was set before timeout, false otherwise.</returns>
        public bool Wait(double? timeout = null)
        {
            if (timeout == null)
            {
                _event.Wait();
                return true;
            }

            int ms = (int)(timeout.Value * 1000);
            return _event.Wait(ms);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _event.Dispose();
        }
    }
}
