using System;

namespace Sharpy
{
    /// <summary>
    /// A thread that executes a function after a specified interval,
    /// similar to Python's <c>threading.Timer</c>. A <c>Timer</c> is a <see cref="Thread"/>:
    /// <c>start()</c> starts it, <c>join()</c> waits for it, and <c>cancel()</c> stops it
    /// from calling the function while it is still waiting.
    /// </summary>
    [SharpyModuleType("threading", "Timer")]
    public sealed class Timer : Thread
    {
        private readonly double _interval;
        private readonly Action _function;
        private readonly Event _finished = new Event();

        public Timer(double interval, Action function)
        {
            _interval = interval;
            _function = function ?? throw new ValueError("function must not be null");
        }

        /// <summary>
        /// Stop the timer, and cancel the execution of the timer's action. This only works
        /// if the timer is still in its waiting stage.
        /// </summary>
        public void Cancel()
        {
            _finished.Set();
        }

        /// <summary>
        /// Wait for the interval (or until cancelled), then call the function unless cancelled.
        /// </summary>
        public override void Run()
        {
            _finished.Wait(_interval);
            if (!_finished.IsSet())
            {
                _function();
            }
            _finished.Set();
        }
    }
}
