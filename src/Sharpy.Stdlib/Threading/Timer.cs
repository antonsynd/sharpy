using System;

namespace Sharpy
{
    /// <summary>
    /// A timer that executes a function after a specified interval,
    /// similar to Python's <c>threading.Timer</c>.
    /// </summary>
    [SharpyModuleType("threading", "Timer")]
    public sealed class Timer : IDisposable
    {
        private readonly double _interval;
        private readonly Action _function;
        private System.Threading.Timer? _timer;
        private volatile bool _finished;

        public Timer(double interval, Action function)
        {
            _interval = interval;
            _function = function ?? throw new ValueError("function must not be null");
        }

        public bool IsAlive => _timer != null && !_finished;

        public void Start()
        {
            if (_timer != null)
            {
                throw new RuntimeError("timer can only be started once");
            }
            _timer = new System.Threading.Timer(_ =>
            {
                _finished = true;
                _function();
            }, null, System.TimeSpan.FromSeconds(_interval), System.Threading.Timeout.InfiniteTimeSpan);
        }

        public void Cancel()
        {
            _timer?.Dispose();
            _finished = true;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
