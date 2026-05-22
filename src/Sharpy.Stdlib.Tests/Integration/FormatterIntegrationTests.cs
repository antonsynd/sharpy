using Sharpy.Compiler.Formatting;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;
using IOPath = System.IO.Path;

namespace Sharpy.Stdlib.Tests.Integration;

public class FormatterIntegrationTests
{
    private static readonly string FixturesPath = IOPath.Combine(
        FixtureDiscoveryHelper.FixturesPath,
        "Formatting");

    private readonly ITestOutputHelper _output;

    public FormatterIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static IEnumerable<object[]> GetFormatterFixtures()
    {
        if (!Directory.Exists(FixturesPath))
        {
            yield break;
        }

        foreach (var spyFile in Directory.EnumerateFiles(FixturesPath, "*.spy", SearchOption.AllDirectories)
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            var formattedFile = IOPath.ChangeExtension(spyFile, ".formatted");
            if (!File.Exists(formattedFile))
            {
                continue;
            }

            var formatSkipFile = IOPath.ChangeExtension(spyFile, ".format-skip");
            if (File.Exists(formatSkipFile))
            {
                continue;
            }

            var relativePath = IOPath.GetRelativePath(FixturesPath, spyFile);
            var testName = IOPath.ChangeExtension(relativePath, null)
                .Replace(IOPath.DirectorySeparatorChar, '/');

            yield return new object[] { testName, spyFile, formattedFile };
        }
    }

    [Theory]
    [MemberData(nameof(GetFormatterFixtures))]
    public void FormatterFixture_ProducesExpectedOutput(string testName, string spyFile, string formattedFile)
    {
        _output.WriteLine($"Running formatter fixture: {testName}");

        var input = File.ReadAllText(spyFile);
        var expected = File.ReadAllText(formattedFile).Replace("\r\n", "\n");

        var result = FormatterService.Format(input, FormatOptions.Default, spyFile);

        _output.WriteLine("=== Actual ===");
        _output.WriteLine(result.FormattedText);
        _output.WriteLine("=== Expected ===");
        _output.WriteLine(expected);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(expected, result.FormattedText);

        var second = FormatterService.Format(result.FormattedText, FormatOptions.Default, spyFile);
        Assert.Equal(result.FormattedText, second.FormattedText);
        Assert.False(second.HasChanges,
            "Formatter is not idempotent: re-formatting the output reported HasChanges=true.");
    }

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
