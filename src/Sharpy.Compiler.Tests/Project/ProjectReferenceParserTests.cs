using FluentAssertions;
using Xunit;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// The .spyproj <c>&lt;Reference&gt;</c> rule (R-EK, #2223) in the one parser,
/// <see cref="ProjectFileParser.Load"/>: a non-empty <c>&lt;HintPath&gt;</c> child is the
/// reference's file — absolute, or relative to the project directory, normalized to one full
/// spelling — and <c>Include</c> is only its assembly name; without a HintPath, <c>Include</c> is
/// the path exactly as before.
/// </summary>
/// <remarks>
/// Cells: the spelling {Include=path absolute, Include=path relative, Include=name + HintPath
/// absolute, Include=name + HintPath relative (through <c>..</c>), HintPath only, Include + empty
/// HintPath} × the file {exists, missing}. The parser does not consult the disk, so both states
/// must hold the same path; a missing file is reported later as SPY0305 by the registry.
/// </remarks>
public sealed class ProjectReferenceParserTests : IDisposable
{
    private readonly string _root;
    private readonly string _projectDir;
    private readonly string _libDir;

    public ProjectReferenceParserTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "spy-refparse-" + Guid.NewGuid().ToString("N"));
        _projectDir = Path.Combine(_root, "proj");
        _libDir = Path.Combine(_root, "libs");
        Directory.CreateDirectory(_projectDir);
        Directory.CreateDirectory(_libDir);
        Directory.CreateDirectory(Path.Combine(_projectDir, "refs"));
        File.WriteAllText(Path.Combine(_projectDir, "main.spy"), "print(1)\n");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public static TheoryData<string, bool> Cells()
    {
        var data = new TheoryData<string, bool>();
        var spellings = new[]
        {
            "IncludeAbsolute", "IncludeRelative", "HintPathAbsolute",
            "HintPathRelative", "HintPathOnly", "IncludeWithEmptyHintPath",
        };
        foreach (var spelling in spellings)
        {
            data.Add(spelling, true);
            data.Add(spelling, false);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void Reference_ResolvesToThePathTheRuleNames(string spelling, bool fileExists)
    {
        var libAbsolute = Path.Combine(_libDir, "Greeter.dll");
        var localRelative = Path.Combine("refs", "Local.dll");

        var (item, expected) = spelling switch
        {
            "IncludeAbsolute" => (
                $"<Reference Include=\"{libAbsolute}\" />",
                libAbsolute),
            "IncludeRelative" => (
                $"<Reference Include=\"{localRelative}\" />",
                // Today's Include rule, unchanged: joined to the project directory, not normalized.
                Path.Combine(_projectDir, localRelative)),
            "HintPathAbsolute" => (
                $"<Reference Include=\"Greeter\"><HintPath>{libAbsolute}</HintPath></Reference>",
                libAbsolute),
            "HintPathRelative" => (
                "<Reference Include=\"Greeter\"><HintPath>../libs/Greeter.dll</HintPath></Reference>",
                Path.GetFullPath(Path.Combine(_root, "libs", "Greeter.dll"))),
            "HintPathOnly" => (
                "<Reference><HintPath>../libs/Greeter.dll</HintPath></Reference>",
                Path.GetFullPath(Path.Combine(_root, "libs", "Greeter.dll"))),
            "IncludeWithEmptyHintPath" => (
                $"<Reference Include=\"{localRelative}\"><HintPath>  </HintPath></Reference>",
                Path.Combine(_projectDir, localRelative)),
            _ => throw new ArgumentOutOfRangeException(nameof(spelling), spelling, null),
        };

        if (fileExists)
        {
            File.WriteAllBytes(expected, new byte[] { 0 });
        }

        var projectFile = Path.Combine(_projectDir, "app.spyproj");
        File.WriteAllText(projectFile,
            "<Project>\n"
            + "  <PropertyGroup><RootNamespace>App</RootNamespace><OutputType>exe</OutputType></PropertyGroup>\n"
            + "  <ItemGroup>\n"
            + "    <SpyFile Include=\"main.spy\" />\n"
            + $"    {item}\n"
            + "  </ItemGroup>\n"
            + "</Project>\n");

        var config = ProjectFileParser.Load(projectFile);

        File.Exists(expected).Should().Be(fileExists, "the cell's disk state is what it says");
        config.References.Should().Equal(
            new[] { expected },
            $"[{spelling}, exists={fileExists}] {item} names exactly this path");
    }
}
