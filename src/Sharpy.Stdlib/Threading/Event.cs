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

        public void Dispose()
        {
            _event.Dispose();
        }
    }
}
