using System.Globalization;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.TestInfrastructure.Integration;

/// <summary>
/// The output an <see cref="IntegrationTestBase"/> test writes through. xUnit keeps a test's output in memory until the
/// test ends and vstest then serializes it as one string; a sweep that left every compile's generated C# there exhausted
/// the testhost's heap (#2438). So the compile path can be muted, and every test has a ceiling on what it leaves in its
/// result.
/// </summary>
/// <remarks>
/// Passing the ceiling fails the test when it ends, never at the write: the write that crosses it usually runs inside
/// the compile path, whose <c>catch</c> would read the throw as a failed compile. Writes past the ceiling are dropped,
/// because the point of the ceiling is that the heap stays bounded until the verdict arrives.
/// </remarks>
public sealed class TestResultOutput : ITestOutputHelper
{
    /// <summary>The most characters one test may write to its result, newlines included.</summary>
    public const long CharacterCeiling = 1_000_000;

    private readonly ITestOutputHelper _inner;
    private long _characters;
    private int _dropNoted;

    public TestResultOutput(ITestOutputHelper inner)
    {
        _inner = inner;
        CompilePath = new CompilePathWriter(this);
    }

    /// <summary>While set, writes through <see cref="CompilePath"/> are neither printed nor counted.</summary>
    public bool MuteCompilePath { get; set; }

    /// <summary>Where the compile path writes: this output, unless <see cref="MuteCompilePath"/> is set.</summary>
    public ITestOutputHelper CompilePath { get; }

    /// <summary>How many characters this test wrote, including any the ceiling dropped.</summary>
    public long CharactersWritten => Interlocked.Read(ref _characters);

    public void WriteLine(string message)
    {
        long written = Interlocked.Add(ref _characters, message.Length + Environment.NewLine.Length);
        if (written <= CharacterCeiling)
            _inner.WriteLine(message);
        else if (Interlocked.Exchange(ref _dropNoted, 1) == 0)
            _inner.WriteLine($"*** {nameof(TestResultOutput)}: past the {CharacterCeiling}-character ceiling; further output is dropped and the test will fail. ***");
    }

    public void WriteLine(string format, params object[] args) => WriteLine(string.Format(CultureInfo.InvariantCulture, format, args));

    /// <summary>Fails the running test when it wrote more than <see cref="CharacterCeiling"/> characters.</summary>
    public void FailIfOverCeiling()
    {
        long written = CharactersWritten;
        if (written > CharacterCeiling)
        {
            Assert.Fail(
                $"This test wrote {written} characters to its output and the per-result ceiling is {CharacterCeiling} (#2438). "
                + "If the writes are the compile path's, set MuteCompilePath in the test class's constructor. "
                + "If they are the test's own, print a summary and capped failures. Do not raise the ceiling.");
        }
    }

    private sealed class CompilePathWriter : ITestOutputHelper
    {
        private readonly TestResultOutput _owner;

        public CompilePathWriter(TestResultOutput owner) => _owner = owner;

        public void WriteLine(string message)
        {
            if (!_owner.MuteCompilePath)
                _owner.WriteLine(message);
        }

        public void WriteLine(string format, params object[] args)
        {
            if (!_owner.MuteCompilePath)
                _owner.WriteLine(format, args);
        }
    }
}
