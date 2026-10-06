using System;
using System.Threading;

namespace Sharpy
{
    /// <summary>
    /// A thread synchronization event, similar to Python's <c>threading.Event</c>.
    /// </summary>
    [SharpyModuleType("threading", "Event")]
    public class Event : IDisposable
    {
        private readonly ManualResetEventSlim _event = new ManualResetEventSlim(false);

        public void Set()
        {
            _event.Set();
        }

        public void Clear()
        {
            _event.Reset();
        }

        public bool IsSet()
        {
            return _event.IsSet;
        }

        /// <summary>
        /// Wait until the flag is set, or until <paramref name="timeout"/> seconds pass, and return
        /// the flag. A set flag returns <c>True</c> whatever the timeout; a timeout at or below zero
        /// does not wait.
        /// </summary>
        public bool Wait(double? timeout = null)
        {
            // python's Event.wait converts the timeout only when it would wait, so a set flag never
            // raises for a timeout python cannot represent (#2263).
            if (_event.IsSet)
            {
                return true;
            }
            if (timeout == null)
            {
                _event.Wait();
                return true;
            }
            if (!(timeout.Value > 0))
            {
                return _event.IsSet;
            }
            WaitTimeout.CheckRepresentable(timeout.Value);
            return _event.Wait(WaitTimeout.ToMilliseconds(timeout.Value));
        }

        public void Dispose()
        {
            _event.Dispose();
        }
    }
}
