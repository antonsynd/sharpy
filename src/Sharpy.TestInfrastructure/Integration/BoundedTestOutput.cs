using System.Globalization;
using Xunit.Abstractions;

namespace Sharpy.TestInfrastructure.Integration;

/// <summary>
/// The test output <see cref="IntegrationTestBase"/> writes through (#2438). xUnit keeps a test's output in memory
/// until the test ends and vstest then serializes it as one string for the result send; a sweep writing millions of
/// characters (each compile's generated C#) exhausted the testhost's heap and turned a passing run into
/// "Test Run Failed" with 0 failed tests. This wrapper does two things:
/// <list type="bullet">
/// <item><see cref="CompilePath"/> is the view the base class's compile helpers write through; it drops every write
/// while <see cref="MuteCompilePath"/> is set, so a sweep's thousands of compiles buffer nothing.</item>
/// <item>Every forwarded write — compile path and the test's own — is counted against <see cref="MaxCharsPerTest"/>.
/// The write that would cross it is dropped, a one-line notice is forwarded instead, and
/// <see cref="TestOutputCeilingExceededException"/> is thrown; every later write is dropped and throws again, and
/// <see cref="ThrowIfCeilingExceeded"/> re-raises it outside any catch the compile helpers own. The result can
/// therefore never carry more than the ceiling, and the test goes red instead.</item>
/// </list>
/// </summary>
public sealed class BoundedTestOutput : ITestOutputHelper
{
    /// <summary>The per-test stdout ceiling, in characters (R-KR a). The largest result #2438's trx recorded was
    /// 8,145,515 chars; a muted sweep writes a few hundred.</summary>
    public const int MaxCharsPerTest = 1_000_000;

    private readonly ITestOutputHelper _inner;
    private readonly int _maxChars;
    private readonly object _gate = new();
    private long _written;
    private long _dropped;
    private bool _exceeded;

    public BoundedTestOutput(ITestOutputHelper inner) : this(inner, MaxCharsPerTest) { }

    /// <summary>A smaller ceiling is for the wrapper's own unit tests; the base class always uses
    /// <see cref="MaxCharsPerTest"/>.</summary>
    public BoundedTestOutput(ITestOutputHelper inner, int maxChars)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        if (maxChars <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxChars), maxChars, "the ceiling must be positive");
        _maxChars = maxChars;
        CompilePath = new CompilePathView(this);
    }

    /// <summary>While set, writes through <see cref="CompilePath"/> are dropped (not forwarded, not counted).</summary>
    public bool MuteCompilePath { get; set; }

    /// <summary>The output the compile helpers write each compile's generated C# and logs through.</summary>
    public ITestOutputHelper CompilePath { get; }

    /// <summary>Characters forwarded to the test result so far, each line's newline included.</summary>
    public long CharsWritten
    {
        get { lock (_gate) return _written; }
    }

    /// <summary>True once a write was refused by the ceiling.</summary>
    public bool CeilingExceeded
    {
        get { lock (_gate) return _exceeded; }
    }

    public void WriteLine(string message) => Write(message ?? string.Empty);

    public void WriteLine(string format, params object[] args) => Write(string.Format(CultureInfo.InvariantCulture, format, args));

    /// <summary>Throws <see cref="TestOutputCeilingExceededException"/> when the ceiling was crossed — the base class
    /// calls it after each compile, outside the compile helpers' own catch blocks, so a crossing inside a compile
    /// cannot be swallowed into an <c>ExecutionResult</c>.</summary>
    public void ThrowIfCeilingExceeded()
    {
        lock (_gate)
        {
            if (_exceeded)
                throw Exceeded();
        }
    }

    private void Write(string message)
    {
        lock (_gate)
        {
            // xUnit appends a newline to every line, so a line costs its length plus one newline: the count is what
            // the result's stdout holds.
            long cost = message.Length + Environment.NewLine.Length;
            if (!_exceeded && _written + cost <= _maxChars)
            {
                _written += cost;
                _inner.WriteLine(message);
                return;
            }

            _dropped += cost;
            if (!_exceeded)
            {
                _exceeded = true;
                _inner.WriteLine($"[test output ceiling: {_written} chars written, the next write of {cost} "
                    + $"chars would pass {_maxChars}; this and every later write are dropped and the test fails (#2438)]");
            }
            throw Exceeded();
        }
    }

    private TestOutputCeilingExceededException Exceeded()
        => new($"The test wrote more than {_maxChars} characters of output ({_written} kept, at least {_dropped} "
            + "dropped). vstest serializes a result's output as one string, and output this large has exhausted the "
            + "testhost's heap (#2438). Mute the compile path (MuteCompilePath = true in the constructor) or print less.");

    private sealed class CompilePathView(BoundedTestOutput owner) : ITestOutputHelper
    {
        public void WriteLine(string message)
        {
            if (!owner.MuteCompilePath)
                owner.WriteLine(message);
        }

        public void WriteLine(string format, params object[] args)
        {
            if (!owner.MuteCompilePath)
                owner.WriteLine(format, args);
        }
    }
}

/// <summary>A test wrote past <see cref="BoundedTestOutput.MaxCharsPerTest"/> (#2438).</summary>
public sealed class TestOutputCeilingExceededException(string message) : Exception(message);
