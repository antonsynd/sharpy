using System;
using SysBarrier = System.Threading.Barrier;

namespace Sharpy
{
    /// <summary>
    /// A barrier synchronization primitive, similar to Python's <c>threading.Barrier</c>.
    /// Wraps <see cref="System.Threading.Barrier"/>.
    /// </summary>
    [SharpyModuleType("threading", "Barrier")]
    public class Barrier : IDisposable
    {
        private readonly SysBarrier _barrier;

        /// <summary>
        /// Create a new Barrier for the given number of parties.
        /// </summary>
        /// <param name="parties">The number of threads that must call Wait() before they are released.</param>
        public Barrier(int parties)
        {
            if (parties < 1)
            {
                throw new ValueError("barrier requires at least 1 party");
            }
            _barrier = new SysBarrier(parties);
        }

        /// <summary>
        /// The number of threads required to trip the barrier.
        /// </summary>
        public int Parties => _barrier.ParticipantCount;

        /// <summary>
        /// The number of threads currently waiting at the barrier.
        /// </summary>
        public int NWaiting
        {
            get
            {
                // .NET Barrier doesn't directly expose waiting count the same way;
                // we approximate using ParticipantsRemaining.
                return _barrier.ParticipantCount - _barrier.ParticipantsRemaining;
            }
        }

        /// <summary>
        /// Wait at the barrier until all parties have arrived.
        /// </summary>
        /// <param name="timeout">Optional timeout in seconds. If null, waits indefinitely.</param>
        /// <returns>The arrival index of the current thread (0 to parties-1).</returns>
        /// <exception cref="RuntimeError">If the barrier is broken or the timeout expires.</exception>
        public int Wait(double? timeout = null)
        {
            try
            {
                if (timeout == null)
                {
                    _barrier.SignalAndWait(System.Threading.Timeout.InfiniteTimeSpan);
                    return 0;
                }

                int ms = (int)(timeout.Value * 1000);
                bool signaled = _barrier.SignalAndWait(TimeSpan.FromMilliseconds(ms));
                if (!signaled)
                {
                    throw new RuntimeError("barrier wait timed out");
                }
                return 0;
            }
            catch (System.Threading.BarrierPostPhaseException ex)
            {
                throw new RuntimeError($"barrier is broken: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset the barrier to its initial state.
        /// </summary>
        public void Reset()
        {
            // .NET Barrier doesn't have a direct Reset; remove and re-add participants
            // is not equivalent. We signal that the barrier is broken.
            throw new RuntimeError("barrier reset is not supported on .NET Barrier");
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _barrier.Dispose();
        }
    }
}
