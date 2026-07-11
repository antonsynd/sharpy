using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Text;
using Xunit;

namespace Sharpy.Compiler.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="CrashBundleWriter"/> (SPY0909 minimal-repro bundles). These run entirely
/// against a temp directory and never invoke the compiler.
/// </summary>
public sealed class CrashBundleWriterTests : IDisposable
{
    private readonly string _tempDir;

    public CrashBundleWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_crash_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    [Fact]
    public void TryWrite_WritesBundleUnderCrashRoot_AndReturnsPath()
    {
        var request = new CrashBundleRequest
        {
            Exception = new InvalidOperationException("boom"),
            Phase = CompilerPhase.CodeGeneration,
            Producer = null,
            Component = "TestEmitter",
            SourceFiles = new Dictionary<string, string> { ["main.spy"] = "x: int = 1\n" }
        };

        var bundlePath = CrashBundleWriter.TryWrite(_tempDir, request);

        Assert.NotNull(bundlePath);
        Assert.True(Directory.Exists(bundlePath));
        // Bundle lands under <output>/.sharpy-crash/<timestamp>/
        var crashRoot = Path.Combine(_tempDir, CrashBundleWriter.CrashRootDirectoryName);
        Assert.StartsWith(crashRoot, bundlePath!, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(bundlePath!, "report.md")));
    }

    [Fact]
    public void TryWrite_ReportContainsExceptionPhaseComponentAndVersion()
    {
        var request = new CrashBundleRequest
        {
            Exception = new InvalidOperationException("the-unique-boom-message"),
            Phase = CompilerPhase.TypeChecking,
            Producer = "ProtocolValidator",
            Component = "TypeChecker"
        };

        var bundlePath = CrashBundleWriter.TryWrite(_tempDir, request);
        Assert.NotNull(bundlePath);

        var report = File.ReadAllText(Path.Combine(bundlePath!, "report.md"));
        Assert.Contains("SPY0909", report, StringComparison.Ordinal);
        Assert.Contains("the-unique-boom-message", report, StringComparison.Ordinal);
        Assert.Contains("TypeChecking", report, StringComparison.Ordinal);
        Assert.Contains("ProtocolValidator", report, StringComparison.Ordinal);
        Assert.Contains("TypeChecker", report, StringComparison.Ordinal);
        Assert.Contains("Compiler version:", report, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException", report, StringComparison.Ordinal);
    }

    [Fact]
    public void TryWrite_EmbedsSourceFiles()
    {
        var request = new CrashBundleRequest
        {
            Exception = new InvalidOperationException("boom"),
            SourceFiles = new Dictionary<string, string>
            {
                ["/proj/main.spy"] = "print('hello from main')\n",
                ["/proj/lib.spy"] = "def helper() -> int:\n    return 1\n"
            }
        };

        var bundlePath = CrashBundleWriter.TryWrite(_tempDir, request);
        Assert.NotNull(bundlePath);

        var sourcesDir = Path.Combine(bundlePath!, "sources");
        Assert.True(Directory.Exists(sourcesDir));
        Assert.Contains("hello from main", File.ReadAllText(Path.Combine(sourcesDir, "main.spy")), StringComparison.Ordinal);
        Assert.Contains("def helper", File.ReadAllText(Path.Combine(sourcesDir, "lib.spy")), StringComparison.Ordinal);
    }

    [Fact]
    public void TryWrite_IncludesSourceExcerptAroundCrashLine()
    {
        var source = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"line_{i} = {i}")) + "\n";
        var request = new CrashBundleRequest
        {
            Exception = new InvalidOperationException("boom"),
            SourceFiles = new Dictionary<string, string> { ["main.spy"] = source },
            SpanFilePath = "main.spy",
            Line = 10
        };

        var bundlePath = CrashBundleWriter.TryWrite(_tempDir, request);
        Assert.NotNull(bundlePath);

        var report = File.ReadAllText(Path.Combine(bundlePath!, "report.md"));
        Assert.Contains("Source excerpt", report, StringComparison.Ordinal);
        Assert.Contains("line_10", report, StringComparison.Ordinal);
        // The crash line is marked with '>'.
        Assert.Contains("> ", report, StringComparison.Ordinal);
    }

    [Fact]
    public void TryWrite_NeverThrows_OnBadOutputDirectory()
    {
        // A path containing an invalid character (NUL) cannot be created; the writer must swallow.
        var request = new CrashBundleRequest { Exception = new InvalidOperationException("boom") };
        var badDir = "\0::invalid::";

        var result = CrashBundleWriter.TryWrite(badDir, request);

        Assert.Null(result);
    }

    [Fact]
    public void DescribeSpan_RendersFileLineColumnAndOffset()
    {
        var span = new TextSpan(5, 3);
        var descriptor = CrashBundleWriter.DescribeSpan(span, line: 2, column: 4, filePath: "main.spy");

        Assert.NotNull(descriptor);
        Assert.Contains("main.spy", descriptor!, StringComparison.Ordinal);
        Assert.Contains("line 2, col 4", descriptor, StringComparison.Ordinal);
        Assert.Contains("offset 5..8", descriptor, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeSpan_ReturnsNull_WhenNoLocationInfo()
    {
        Assert.Null(CrashBundleWriter.DescribeSpan(span: null, line: null, column: null, filePath: null));
    }
}
