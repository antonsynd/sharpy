using FluentAssertions;
using Sharpy.Compiler.Formatting;
using Xunit;

namespace Sharpy.Compiler.Tests.Formatting;

/// <summary>
/// Tests for <see cref="FormatRunner"/>, the engine that backs the
/// <c>sharpyc format</c> CLI command. These exercise the format logic
/// programmatically without invoking the CLI binary.
/// </summary>
public class FormatCommandTests : IDisposable
{
    private readonly string _tempDir;

    public FormatCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sharpy-format-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup; ignore failures so tests don't fail in CI on locked files.
        }
    }

    private string WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_tempDir, relativePath);
        var dir = Path.GetDirectoryName(fullPath)!;
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    // -----------------------------------------------------------------------
    // FormatFile (single file content)
    // -----------------------------------------------------------------------

    [Fact]
    public void FormatFile_FormatsAndOverwritesInPlace()
    {
        var path = WriteFile("a.spy", "def foo():\n    pass\ndef bar():\n    pass\n");

        var outcome = FormatRunner.FormatFile(path, new FormatRunnerOptions { Mode = FormatMode.Write });

        outcome.Changed.Should().BeTrue();
        outcome.Wrote.Should().BeTrue();
        outcome.HasError.Should().BeFalse();
        File.ReadAllText(path).Should().Contain("pass\n\n\ndef bar():");
    }

    [Fact]
    public void FormatFile_AlreadyFormatted_LeavesFileUnchanged()
    {
        var source = "x = 1\n";
        var path = WriteFile("a.spy", source);

        var outcome = FormatRunner.FormatFile(path, new FormatRunnerOptions { Mode = FormatMode.Write });

        outcome.Changed.Should().BeFalse();
        outcome.Wrote.Should().BeFalse();
        File.ReadAllText(path).Should().Be(source);
    }

    [Fact]
    public void FormatFile_WritesToOutputPathInsteadOfOverwriting()
    {
        var source = "def foo():\n    pass\ndef bar():\n    pass\n";
        var inputPath = WriteFile("input.spy", source);
        var outputPath = Path.Combine(_tempDir, "out.spy");

        var outcome = FormatRunner.FormatFile(inputPath, new FormatRunnerOptions
        {
            Mode = FormatMode.Write,
            OutputPath = outputPath,
        });

        outcome.Changed.Should().BeTrue();
        outcome.Wrote.Should().BeTrue();
        File.ReadAllText(inputPath).Should().Be(source); // original untouched
        File.ReadAllText(outputPath).Should().Contain("pass\n\n\ndef bar():");
    }

    [Fact]
    public void FormatFile_SyntaxErrors_ReportsErrorWithoutWriting()
    {
        var source = "def foo(\n";
        var path = WriteFile("broken.spy", source);

        var outcome = FormatRunner.FormatFile(path, new FormatRunnerOptions { Mode = FormatMode.Write });

        outcome.HasError.Should().BeTrue();
        outcome.Diagnostics.Should().NotBeEmpty();
        outcome.Wrote.Should().BeFalse();
        File.ReadAllText(path).Should().Be(source);
    }

    // -----------------------------------------------------------------------
    // Check mode
    // -----------------------------------------------------------------------

    [Fact]
    public void Run_CheckMode_AlreadyFormatted_ExitsZero()
    {
        var path = WriteFile("a.spy", "x = 1\n");

        var result = FormatRunner.Run(path, new FormatRunnerOptions { Mode = FormatMode.Check });

        result.ExitCode.Should().Be(0);
        result.ChangedCount.Should().Be(0);
        File.ReadAllText(path).Should().Be("x = 1\n");
    }

    [Fact]
    public void Run_CheckMode_UnformattedFile_ExitsOneAndLeavesFileUnchanged()
    {
        var source = "def foo():\n    pass\ndef bar():\n    pass\n";
        var path = WriteFile("a.spy", source);

        var result = FormatRunner.Run(path, new FormatRunnerOptions { Mode = FormatMode.Check });

        result.ExitCode.Should().Be(1);
        result.ChangedCount.Should().Be(1);
        result.Outcomes.Should().HaveCount(1);
        result.Outcomes[0].Changed.Should().BeTrue();
        result.Outcomes[0].Wrote.Should().BeFalse();
        File.ReadAllText(path).Should().Be(source); // never modified
    }

    [Fact]
    public void Run_CheckMode_ErrorsExitTwo()
    {
        var path = WriteFile("a.spy", "def foo(\n");

        var result = FormatRunner.Run(path, new FormatRunnerOptions { Mode = FormatMode.Check });

        result.ExitCode.Should().Be(2);
        result.ErrorCount.Should().Be(1);
    }

    [Fact]
    public void Run_NonexistentPath_ExitsTwoWithError()
    {
        var path = Path.Combine(_tempDir, "does-not-exist.spy");

        var result = FormatRunner.Run(path, new FormatRunnerOptions { Mode = FormatMode.Check });

        result.ExitCode.Should().Be(2);
        result.ErrorCount.Should().Be(1);
        result.Outcomes[0].HasError.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Directory recursive formatting
    // -----------------------------------------------------------------------

    [Fact]
    public void FindSharpyFiles_RecursivelyDiscoversSpyFiles()
    {
        WriteFile("a.spy", "x = 1\n");
        WriteFile("nested/b.spy", "y = 2\n");
        WriteFile("nested/deep/c.spy", "z = 3\n");
        WriteFile("not-included.txt", "ignored\n");

        var files = FormatRunner.FindSharpyFiles(_tempDir);

        files.Should().HaveCount(3);
        files.Should().OnlyContain(p => p.EndsWith(".spy"));
        files.Should().Contain(p => p.EndsWith("a.spy"));
        files.Should().Contain(p => p.EndsWith("b.spy"));
        files.Should().Contain(p => p.EndsWith("c.spy"));
    }

    [Fact]
    public void Run_DirectoryMode_FormatsEveryFile()
    {
        var unformatted = "def foo():\n    pass\ndef bar():\n    pass\n";
        var fileA = WriteFile("a.spy", unformatted);
        var fileB = WriteFile("nested/b.spy", unformatted);
        var alreadyFormatted = WriteFile("c.spy", "x = 1\n");

        var result = FormatRunner.Run(_tempDir, new FormatRunnerOptions { Mode = FormatMode.Write });

        result.ExitCode.Should().Be(0);
        result.ChangedCount.Should().Be(2);
        result.Outcomes.Should().HaveCount(3);

        File.ReadAllText(fileA).Should().Contain("pass\n\n\ndef bar():");
        File.ReadAllText(fileB).Should().Contain("pass\n\n\ndef bar():");
        File.ReadAllText(alreadyFormatted).Should().Be("x = 1\n");
    }

    [Fact]
    public void Run_DirectoryMode_CheckReportsAllUnformatted()
    {
        WriteFile("a.spy", "def foo():\n    pass\ndef bar():\n    pass\n");
        WriteFile("b.spy", "def foo():\n    pass\ndef bar():\n    pass\n");
        WriteFile("c.spy", "x = 1\n");

        var result = FormatRunner.Run(_tempDir, new FormatRunnerOptions { Mode = FormatMode.Check });

        result.ExitCode.Should().Be(1);
        result.ChangedCount.Should().Be(2);
        result.Outcomes.Should().HaveCount(3);
    }

    [Fact]
    public void Run_DirectoryMode_IgnoresOutputPathOption()
    {
        // OutputPath only makes sense for single-file input; for directory it
        // would be ambiguous which file the output applies to. The runner
        // should silently ignore it rather than write every file to the same
        // location.
        WriteFile("a.spy", "x = 1\n");
        WriteFile("b.spy", "y = 2\n");
        var phantomOutput = Path.Combine(_tempDir, "phantom-output.spy");

        var result = FormatRunner.Run(_tempDir, new FormatRunnerOptions
        {
            Mode = FormatMode.Write,
            OutputPath = phantomOutput,
        });

        result.ExitCode.Should().Be(0);
        File.Exists(phantomOutput).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // Comment preservation
    // -----------------------------------------------------------------------

    [Fact]
    public void FormatFile_PreservesLeadingCommentsThroughFormatting()
    {
        var source = "# module header\n# second line\nx = 1\n";
        var path = WriteFile("a.spy", source);

        var outcome = FormatRunner.FormatFile(path, new FormatRunnerOptions { Mode = FormatMode.Write });

        var roundtripped = File.ReadAllText(path);
        roundtripped.Should().Contain("# module header");
        roundtripped.Should().Contain("# second line");
        outcome.HasError.Should().BeFalse();
    }

    [Fact]
    public void FormatFile_PreservesInlineCommentsThroughFormatting()
    {
        var source = "x = 1  # inline comment\ny = 2\n";
        var path = WriteFile("a.spy", source);

        FormatRunner.FormatFile(path, new FormatRunnerOptions { Mode = FormatMode.Write });

        File.ReadAllText(path).Should().Contain("# inline comment");
    }

    // -----------------------------------------------------------------------
    // Format options
    // -----------------------------------------------------------------------

    [Fact]
    public void FormatFile_UsesCustomIndentSize()
    {
        var path = WriteFile("a.spy", "def foo():\n    pass\n");

        FormatRunner.FormatFile(path, new FormatRunnerOptions
        {
            Mode = FormatMode.Write,
            FormatOptions = FormatOptions.Default with { IndentSize = 2 },
        });

        var formatted = File.ReadAllText(path);
        formatted.Should().Contain("  pass");
        formatted.Should().NotContain("    pass");
    }

    [Fact]
    public void FormatFile_UsesTabsWhenRequested()
    {
        var path = WriteFile("a.spy", "def foo():\n    pass\n");

        FormatRunner.FormatFile(path, new FormatRunnerOptions
        {
            Mode = FormatMode.Write,
            FormatOptions = FormatOptions.Default with { UseTabs = true },
        });

        File.ReadAllText(path).Should().Contain("\tpass");
    }

    // -----------------------------------------------------------------------
    // Diff mode
    // -----------------------------------------------------------------------

    [Fact]
    public void Run_DiffMode_ProducesUnifiedDiffWithoutModifyingFile()
    {
        var source = "def foo():\n    pass\ndef bar():\n    pass\n";
        var path = WriteFile("a.spy", source);

        var result = FormatRunner.Run(path, new FormatRunnerOptions { Mode = FormatMode.Diff });

        result.ExitCode.Should().Be(0);
        result.Outcomes[0].Changed.Should().BeTrue();
        result.Outcomes[0].Diff.Should().NotBeNullOrEmpty();
        result.Outcomes[0].Diff.Should().Contain("--- ");
        result.Outcomes[0].Diff.Should().Contain("+++ ");
        File.ReadAllText(path).Should().Be(source); // unchanged on disk
    }

    [Fact]
    public void Run_DiffMode_FormattedFile_ProducesNoDiff()
    {
        var path = WriteFile("a.spy", "x = 1\n");

        var result = FormatRunner.Run(path, new FormatRunnerOptions { Mode = FormatMode.Diff });

        result.ExitCode.Should().Be(0);
        result.Outcomes[0].Changed.Should().BeFalse();
        result.Outcomes[0].Diff.Should().BeNull();
    }

    [Fact]
    public void ComputeUnifiedDiff_EmitsHeaderAndChangedLines()
    {
        var diff = FormatRunner.ComputeUnifiedDiff("a\nb\nc\n", "a\nB\nc\n", "/tmp/x.spy");

        diff.Should().Contain("--- /tmp/x.spy");
        diff.Should().Contain("+++ /tmp/x.spy");
        diff.Should().Contain("-b");
        diff.Should().Contain("+B");
        diff.Should().Contain(" a");
        diff.Should().Contain(" c");
    }
}
