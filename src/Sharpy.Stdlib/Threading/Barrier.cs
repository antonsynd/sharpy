using System;
using System.Diagnostics;
using System.Threading;

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
        // CPython's algorithm (Lib/threading.py), on one monitor: Wait returns the party's
        // ARRIVAL index in the current phase (0 for the first, `parties - 1` for the last), and the
        // barrier is a four-state machine — filling, draining (the last party released the others),
        // resetting, broken — so a reset, an abort, a timeout or a failing action wakes every waiting
        // party with BrokenBarrierError. Built on `System.Threading.Barrier` before
        // #2265, every party got the .NET phase NUMBER (1, 2, … for all of them, so
        // `if b.wait() == 0:` picked no thread), and an abort or a reset disposed the barrier under
        // a waiting party, whose `ObjectDisposedException` ended the process.

        private const int Filling = 0;
        private const int Draining = 1;
        private const int Resetting = -1;
        private const int Broken_ = -2;

        private readonly object _cond = new object();
        private readonly int _parties;
        private readonly Action? _action;
        private readonly double? _timeout;
        private int _state = Filling;
        private int _count;

        /// <summary>Create a barrier for <paramref name="parties"/> threads.</summary>
        /// <param name="parties">The number of threads that must call <see cref="Wait"/> to pass.</param>
        /// <param name="action">Called by one of the threads, before any is released, once per phase.</param>
        /// <param name="timeout">The default timeout, in seconds, of every <see cref="Wait"/> that gives none.</param>
        public Barrier(int parties, Action? action = null, double? timeout = null)
        {
            if (parties < 1)
            {
                throw new ValueError("parties must be >= 1");
            }
            _parties = parties;
            _action = action;
            _timeout = timeout;
        }

        /// <summary>The number of threads required to pass the barrier.</summary>
        public int Parties => _parties;

        /// <summary>The number of threads currently waiting at the barrier.</summary>
        public int NWaiting => CountWaiting();

        /// <summary>True when the barrier is broken.</summary>
        public bool Broken => IsBroken();

        /// <summary>
        /// Wait until all parties have called <c>wait()</c>, and return this party's arrival index in
        /// the phase, from 0 to <c>parties - 1</c>.
        /// </summary>
        /// <param name="timeout">Seconds to wait; the barrier's own timeout when omitted.</param>
        public int Wait(double? timeout = null)
        {
            timeout ??= _timeout;
            lock (_cond)
            {
                Enter();
                int index = _count;
                _count++;
                try
                {
                    if (index + 1 == _parties)
                    {
                        Release();
                    }
                    else
                    {
                        AwaitRelease(timeout);
                    }
                    return index;
                }
                finally
                {
                    _count--;
                    Exit();
                }
            }
        }

        /// <summary>Return the barrier to its initial state; any thread waiting raises <see cref="BrokenBarrierError"/>.</summary>
        public void Reset()
        {
            lock (_cond)
            {
                if (_count > 0)
                {
                    // A draining phase finishes first; a filling or broken one makes its waiters raise.
                    if (_state == Filling || _state == Broken_)
                    {
                        _state = Resetting;
                    }
                }
                else
                {
                    _state = Filling;
                }
                Monitor.PulseAll(_cond);
            }
        }

        /// <summary>Put the barrier into the broken state; any waiting or later <c>wait()</c> raises.</summary>
        public void Abort()
        {
            lock (_cond)
            {
                Break();
            }
        }

        /// <summary>Nothing to release: the barrier holds no OS resource.</summary>
        public void Dispose()
        {
        }

        // Wait while the previous phase drains or a reset completes; a broken barrier refuses.
        private void Enter()
        {
            while (_state == Resetting || _state == Draining)
            {
                Monitor.Wait(_cond);
            }
            if (_state < 0)
            {
                throw new BrokenBarrierError("barrier is broken");
            }
        }

        // The last party: run the action, then release the phase. A failing action breaks it.
        private void Release()
        {
            try
            {
                _action?.Invoke();
                _state = Draining;
                Monitor.PulseAll(_cond);
            }
            catch
            {
                Break();
                throw;
            }
        }

        // Any other party: wait for the release. python's last party never reads its timeout; any
        // other gives up at once on a timeout at or below zero (or NaN), which breaks the barrier, and
        // a timeout python cannot represent raises before any wait (#2263) — the finally in Wait
        // uncounts the party, so the barrier is left as it was.
        private void AwaitRelease(double? timeout)
        {
            if (timeout == null)
            {
                while (_state == Filling)
                {
                    Monitor.Wait(_cond);
                }
            }
            else
            {
                if (timeout.Value > 0)
                {
                    WaitTimeout.CheckRepresentable(timeout.Value);
                }
                long budget = WaitTimeout.ToMilliseconds(timeout.Value);
                var clock = Stopwatch.StartNew();
                while (_state == Filling)
                {
                    long remaining = budget - clock.ElapsedMilliseconds;
                    if (remaining <= 0)
                    {
                        Break();
                        throw new BrokenBarrierError("barrier wait timed out");
                    }
                    Monitor.Wait(_cond, (int)remaining);
                }
            }
            if (_state < 0)
            {
                throw new BrokenBarrierError("barrier is broken");
            }
        }

        // The last party out of a draining or resetting phase opens the barrier for the next one.
        private void Exit()
        {
            if (_count == 0 && (_state == Resetting || _state == Draining))
            {
                _state = Filling;
                Monitor.PulseAll(_cond);
            }
        }

        private int CountWaiting()
        {
            lock (_cond)
            {
                return _state == Filling ? _count : 0;
            }
        }

        private bool IsBroken()
        {
            lock (_cond)
            {
                return _state == Broken_;
            }
        }

        private void Break()
        {
            _state = Broken_;
            Monitor.PulseAll(_cond);
        }
    }
}
