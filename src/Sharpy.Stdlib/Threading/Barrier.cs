using System;
using SysBarrier = System.Threading.Barrier;

namespace Sharpy
{
    [SharpyModuleType("threading", "BrokenBarrierError")]
    public class BrokenBarrierError : Exception
    {
        public BrokenBarrierError(string message) : base(message) { }
        public BrokenBarrierError(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// A barrier synchronization primitive, similar to Python's <c>threading.Barrier</c>.
    /// </summary>
    [SharpyModuleType("threading", "Barrier")]
    public class Barrier : IDisposable
    {
        private SysBarrier _barrier;
        private readonly int _parties;
        private readonly Action? _action;
        private volatile bool _broken;

        public Barrier(int parties, Action? action = null)
        {
            if (parties < 1)
            {
                throw new ValueError("barrier requires at least 1 party");
            }
            _parties = parties;
            _action = action;
            _barrier = new SysBarrier(parties, action != null ? _ => action() : null);
        }

        public int Parties => _parties;

        public int NWaiting
        {
            get
            {
                return _barrier.ParticipantCount - _barrier.ParticipantsRemaining;
            }
        }

        public bool Broken => _broken;

        public int Wait(double? timeout = null)
        {
            if (_broken)
                throw new BrokenBarrierError("barrier is broken");

            try
            {
                if (timeout == null)
                {
                    _barrier.SignalAndWait(System.Threading.Timeout.InfiniteTimeSpan);
                }
                else
                {
                    int ms = (int)(timeout.Value * 1000);
                    bool signaled = _barrier.SignalAndWait(System.TimeSpan.FromMilliseconds(ms));
                    if (!signaled)
                    {
                        _broken = true;
                        throw new BrokenBarrierError("barrier wait timed out");
                    }
                }
                return (int)_barrier.CurrentPhaseNumber;
            }
            catch (System.Threading.BarrierPostPhaseException ex)
            {
                _broken = true;
                throw new BrokenBarrierError("barrier is broken: " + ex.Message, ex);
            }
        }

        public void Reset()
        {
            _barrier.Dispose();
            _barrier = new SysBarrier(_parties, _action != null ? _ => _action() : null);
            _broken = false;
        }

        public void Abort()
        {
            _broken = true;
            _barrier.Dispose();
        }

        public void Dispose()
        {
            _barrier.Dispose();
        }
    }
}
