using System;
using SysBarrier = System.Threading.Barrier;

namespace Sharpy
{
    /// <summary>
    /// A barrier synchronization primitive, similar to Python's <c>threading.Barrier</c>.
    /// </summary>
    [SharpyModuleType("threading", "Barrier")]
    public class Barrier : IDisposable
    {
        private readonly SysBarrier _barrier;

        public Barrier(int parties)
        {
            if (parties < 1)
            {
                throw new ValueError("barrier requires at least 1 party");
            }
            _barrier = new SysBarrier(parties);
        }

        public int Parties => _barrier.ParticipantCount;

        public int NWaiting
        {
            get
            {
                return _barrier.ParticipantCount - _barrier.ParticipantsRemaining;
            }
        }

        public void Wait(double? timeout = null)
        {
            try
            {
                if (timeout == null)
                {
                    _barrier.SignalAndWait(System.Threading.Timeout.InfiniteTimeSpan);
                    return;
                }

                int ms = (int)(timeout.Value * 1000);
                bool signaled = _barrier.SignalAndWait(System.TimeSpan.FromMilliseconds(ms));
                if (!signaled)
                {
                    throw new RuntimeError("barrier wait timed out");
                }
            }
            catch (System.Threading.BarrierPostPhaseException ex)
            {
                throw new RuntimeError("barrier is broken: " + ex.Message);
            }
        }

        public void Dispose()
        {
            _barrier.Dispose();
        }
    }
}
