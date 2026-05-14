using Sharpy.Compiler.Formatting;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// File-based integration tests for the formatter (<see cref="FormatterService"/>).
///
/// Test fixtures live in <c>Integration/TestFixtures/Formatting/</c>. Each fixture is a
/// pair of files:
///
///   <c>{name}.spy</c>        — unformatted input source
///   <c>{name}.formatted</c>  — expected output after running <see cref="FormatterService.Format"/>
///
/// A test passes when:
///   1. The formatter output for the <c>.spy</c> input matches the <c>.formatted</c> file
///      byte-for-byte.
///   2. Re-formatting the produced output yields the same result (idempotence).
/// </summary>
public class FormatterIntegrationTests
{
    private static readonly string FixturesPath = Path.Combine(
        FixtureDiscoveryHelper.FixturesPath,
        "Formatting");

    private readonly ITestOutputHelper _output;

    public FormatterIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Discovers all paired <c>.spy</c>/<c>.formatted</c> fixtures in the Formatting
    /// fixtures directory. Returns the test name plus absolute paths to both files.
    /// </summary>
    public static IEnumerable<object[]> GetFormatterFixtures()
    {
        if (!Directory.Exists(FixturesPath))
        {
            yield break;
        }

        foreach (var spyFile in Directory.EnumerateFiles(FixturesPath, "*.spy", SearchOption.AllDirectories)
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            var formattedFile = Path.ChangeExtension(spyFile, ".formatted");
            if (!File.Exists(formattedFile))
            {
                continue;
            }

            var formatSkipFile = Path.ChangeExtension(spyFile, ".format-skip");
            if (File.Exists(formatSkipFile))
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(FixturesPath, spyFile);
            var testName = Path.ChangeExtension(relativePath, null)
                .Replace(Path.DirectorySeparatorChar, '/');

            yield return new object[] { testName, spyFile, formattedFile };
        }
    }

    [Theory]
    [MemberData(nameof(GetFormatterFixtures))]
    public void FormatterFixture_ProducesExpectedOutput(string testName, string spyFile, string formattedFile)
    {
        _output.WriteLine($"Running formatter fixture: {testName}");
        _output.WriteLine($"Input: {spyFile}");
        _output.WriteLine($"Expected: {formattedFile}");

        var input = File.ReadAllText(spyFile);
        var expected = File.ReadAllText(formattedFile);

        // Normalize line endings on the expected text so fixtures created on Windows
        // still compare correctly against the formatter's LF output.
        expected = expected.Replace("\r\n", "\n");

        _output.WriteLine("=== Input ===");
        _output.WriteLine(input);
        _output.WriteLine("=============");

        var result = FormatterService.Format(input, FormatOptions.Default, spyFile);

        _output.WriteLine("=== Diagnostics ===");
        foreach (var diag in result.Diagnostics)
        {
            _output.WriteLine(diag.ToString() ?? "<null>");
        }
        _output.WriteLine("===================");

        _output.WriteLine("=== Actual ===");
        _output.WriteLine(result.FormattedText);
        _output.WriteLine("==============");

        _output.WriteLine("=== Expected ===");
        _output.WriteLine(expected);
        _output.WriteLine("================");

        Assert.Empty(result.Diagnostics);
        Assert.Equal(expected, result.FormattedText);

        // Idempotence: formatting an already-formatted output must produce the same text.
        var second = FormatterService.Format(result.FormattedText, FormatOptions.Default, spyFile);
        Assert.Equal(result.FormattedText, second.FormattedText);
        Assert.False(second.HasChanges,
            "Formatter is not idempotent: re-formatting the output reported HasChanges=true.");
    }

    /// <summary>
    /// Sanity check that the Formatting fixtures directory exists and is non-empty.
    /// </summary>
    [Fact]
    public void FormattingFixturesDirectory_Exists()
    {
        _output.WriteLine($"Looking for fixtures in: {FixturesPath}");
        Assert.True(Directory.Exists(FixturesPath),
            $"Formatting fixtures directory not found at: {FixturesPath}");

        var spyFiles = Directory.GetFiles(FixturesPath, "*.spy", SearchOption.AllDirectories);
        Assert.NotEmpty(spyFiles);
    }
}
