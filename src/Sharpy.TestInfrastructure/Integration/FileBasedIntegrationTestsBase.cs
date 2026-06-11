using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Sharpy.Compiler.Text;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.TestInfrastructure.Integration;

public abstract class FileBasedIntegrationTestsBase : IntegrationTestBase
{
    protected abstract string FixturesPath { get; }

    protected FileBasedIntegrationTestsBase(ITestOutputHelper output) : base(output)
    {
    }

    public static IEnumerable<object[]> DiscoverTestFixtures(string fixturesPath)
    {
        foreach (var fixture in FixtureDiscoveryHelper.DiscoverFixtures(fixturesPath))
        {
            yield return new object[] { fixture.TestName, fixture.SpyFilePath, fixture.IsMultiFile };
        }
    }

    protected void RunTestFixtureImpl(string testName, string path, bool isMultiFile)
    {
        Output.WriteLine($"Running test: {testName}");
        Output.WriteLine($"Test type: {(isMultiFile ? "Multi-file project" : "Single file")}");
        Output.WriteLine($"Path: {path}");

        ExecutionResult result;
        string errorFilePath;
        string expectedFilePath;
        string? sourceTextContent = null;

        if (isMultiFile)
        {
            var projectDir = path;
            var entryPointFile = FindEntryPoint(projectDir);
            Output.WriteLine($"Entry point: {entryPointFile}");

            var sourceFiles = Directory.GetFiles(projectDir, "*.spy", SearchOption.AllDirectories);
            Output.WriteLine("=== Source Files ===");
            foreach (var sourceFile in sourceFiles)
            {
                var relativePath = Path.GetRelativePath(projectDir, sourceFile);
                Output.WriteLine($"--- {relativePath} ---");
                Output.WriteLine(File.ReadAllText(sourceFile));
            }
            Output.WriteLine("====================");

            var entryPointBaseName = Path.GetFileNameWithoutExtension(entryPointFile);
            errorFilePath = Path.Combine(projectDir, $"{entryPointBaseName}.error");
            expectedFilePath = Path.Combine(projectDir, $"{entryPointBaseName}.expected");
            sourceTextContent = File.ReadAllText(Path.Combine(projectDir, entryPointFile));
            result = CompileAndExecuteProject(projectDir, entryPointFile);
        }
        else
        {
            var spyFilePath = path;
            var source = File.ReadAllText(spyFilePath);
            Output.WriteLine("=== Sharpy Source ===");
            Output.WriteLine(source);
            Output.WriteLine("=====================");

            errorFilePath = Path.ChangeExtension(spyFilePath, ".error");
            expectedFilePath = Path.ChangeExtension(spyFilePath, ".expected");
            sourceTextContent = source;
            result = CompileAndExecute(source, Path.GetFileName(spyFilePath));
        }

        var isErrorTest = File.Exists(errorFilePath);

        var runtimeErrorFilePath = isMultiFile
            ? Path.Combine(path, $"{Path.GetFileNameWithoutExtension(FindEntryPoint(path))}.runtime-error")
            : path.Replace(".spy", ".runtime-error", StringComparison.Ordinal);
        var isRuntimeErrorTest = File.Exists(runtimeErrorFilePath);

        var warningFilePath = Path.ChangeExtension(errorFilePath, ".warning");
        var hasWarningFile = File.Exists(warningFilePath);

        if (isRuntimeErrorTest)
        {
            var expectedRuntimeErrorContent = File.ReadAllText(runtimeErrorFilePath).Trim();
            Output.WriteLine($"Expected runtime error patterns:\n{expectedRuntimeErrorContent}");

            Assert.True(result.GeneratedCSharp != null,
                $"Expected compilation to succeed for runtime error test, but no C# was generated. " +
                $"Compilation errors: {string.Join("\n", result.CompilationErrors)}");

            Assert.False(result.Success,
                $"Expected runtime error but program exited successfully. Output: {result.StandardOutput}");

            Output.WriteLine($"Actual stderr:\n{result.StandardError}");

            var expectedLines = expectedRuntimeErrorContent
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith('#'))
                .ToList();

            foreach (var expectedLine in expectedLines)
            {
                Assert.Contains(expectedLine, result.StandardError, StringComparison.OrdinalIgnoreCase);
            }
        }
        else if (isErrorTest)
        {
            var expectedErrorContent = File.ReadAllText(errorFilePath).Trim();
            Output.WriteLine($"Expected error patterns:\n{expectedErrorContent}");

            Assert.False(result.Success,
                $"Expected compilation to fail, but it succeeded. Output: {result.StandardOutput}");

            var actualErrors = string.Join("\n", result.CompilationErrors);
            Output.WriteLine($"Actual errors:\n{actualErrors}");

            var expectedLines = expectedErrorContent
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith('#'))
                .ToList();

            foreach (var expectedLine in expectedLines)
            {
                var locationMatch = Regex.Match(expectedLine, @"^(.+?)\s+@(\d+):(\d+)$");
                if (locationMatch.Success)
                {
                    var messagePattern = locationMatch.Groups[1].Value.Trim();
                    var expectedLineNum = int.Parse(locationMatch.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                    var expectedColumn = int.Parse(locationMatch.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);

                    Assert.Contains(messagePattern, actualErrors, StringComparison.OrdinalIgnoreCase);

                    var matchingDiag = result.RawDiagnostics.FirstOrDefault(d =>
                        d.Message.Contains(messagePattern, StringComparison.OrdinalIgnoreCase));

                    Assert.True(matchingDiag != null,
                        $"No raw diagnostic found matching '{messagePattern}'. " +
                        $"RawDiagnostics count: {result.RawDiagnostics.Count}");

                    int? actualLine = null;
                    int? actualColumn = null;
                    if (matchingDiag!.Span.HasValue && sourceTextContent != null)
                    {
                        var st = new SourceText(sourceTextContent);
                        var pos = st.GetLineAndColumn(matchingDiag.Span.Value.Start);
                        actualLine = pos.Line;
                        actualColumn = pos.Column;
                    }
                    else if (matchingDiag.Line.HasValue)
                    {
                        actualLine = matchingDiag.Line;
                        actualColumn = matchingDiag.Column;
                    }

                    Assert.True(actualLine.HasValue,
                        $"Diagnostic '{messagePattern}' has no location information (no Span or Line). " +
                        $"Diagnostic: {matchingDiag}");

                    Assert.Equal(expectedLineNum, actualLine!.Value);
                    Assert.Equal(expectedColumn, actualColumn ?? 0);
                }
                else
                {
                    Assert.Contains(expectedLine, actualErrors, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        else
        {
            Assert.True(result.Success,
                $"Compilation failed: {string.Join("\n", result.CompilationErrors)}");

            if (File.Exists(expectedFilePath))
            {
                var expectedOutput = File.ReadAllText(expectedFilePath);
                Assert.Equal(expectedOutput, result.StandardOutput);
            }
            else if (!hasWarningFile)
            {
                Assert.Fail($"Missing expected output file: {expectedFilePath}");
            }
        }

        // C# snapshot verification
        if (!isErrorTest && result.Success && result.GeneratedCSharp != null)
        {
            var snapshotFilePath = isMultiFile
                ? Path.Combine(path, $"{Path.GetFileNameWithoutExtension(FindEntryPoint(path))}.expected.cs")
                : Path.ChangeExtension(path, ".expected.cs");

            var updateSnapshots = Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "true";

            if (updateSnapshots && File.Exists(snapshotFilePath))
            {
                var normalized = NormalizeCSharp(result.GeneratedCSharp);
                var existingContent = File.ReadAllText(snapshotFilePath);
                var snapshotComment = ExtractSnapshotComment(existingContent);
                var contentToWrite = snapshotComment != null
                    ? snapshotComment + "\n" + normalized
                    : normalized;
                File.WriteAllText(snapshotFilePath, contentToWrite);
                Output.WriteLine($"Updated snapshot: {snapshotFilePath}");
            }
            else if (File.Exists(snapshotFilePath))
            {
                var expectedCSharp = StripSnapshotComment(File.ReadAllText(snapshotFilePath));
                var actualNormalized = NormalizeCSharp(result.GeneratedCSharp);
                var expectedNormalized = NormalizeCSharp(expectedCSharp);

                Output.WriteLine("=== Generated C# (normalized) ===");
                Output.WriteLine(actualNormalized);
                Output.WriteLine("=================================");

                Assert.Equal(expectedNormalized, actualNormalized);
            }
        }

        // Warning verification
        if (hasWarningFile)
        {
            var expectedWarningContent = File.ReadAllText(warningFilePath).Trim();
            Output.WriteLine($"Expected warning patterns:\n{expectedWarningContent}");

            var actualWarnings = string.Join("\n", result.CompilationWarnings);
            Output.WriteLine($"Actual warnings:\n{actualWarnings}");

            var expectedWarningLines = expectedWarningContent
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith('#'))
                .ToList();

            if (expectedWarningLines.Count == 0)
            {
                Assert.Empty(result.CompilationWarnings);
            }
            else
            {
                foreach (var expectedWarningLine in expectedWarningLines)
                {
                    Assert.Contains(expectedWarningLine, actualWarnings, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }

    protected static string FindEntryPoint(string projectDir)
    {
        var dirName = Path.GetFileName(projectDir);

        if (File.Exists(Path.Combine(projectDir, "main.spy")))
            return "main.spy";

        if (File.Exists(Path.Combine(projectDir, $"{dirName}.spy")))
            return $"{dirName}.spy";

        var spyFiles = Directory.GetFiles(projectDir, "*.spy").OrderBy(f => f).ToList();
        if (spyFiles.Count > 0)
            return Path.GetFileName(spyFiles[0]);

        throw new InvalidOperationException($"No .spy files found in {projectDir}");
    }

    protected static string NormalizeCSharp(string csharpCode)
    {
        var normalizedInput = csharpCode.Replace("\r\n", "\n", StringComparison.Ordinal);
        var tree = CSharpSyntaxTree.ParseText(normalizedInput);
        var root = tree.GetRoot();

        using var workspace = new AdhocWorkspace();
        var formatted = Formatter.Format(root, workspace);
        var text = formatted.ToFullString().Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";

        // Make multi-file snapshots machine-independent: in project compilations
        // each #line directive embeds the absolute source path, which differs per
        // checkout (developer machine vs CI). Reduce the path in any #line
        // directive to its bare file name. Single-file fixtures already emit bare
        // file names, so this is a no-op for them.
        text = Regex.Replace(
            text,
            "(#line\\b[^\"\n]*?)\"[^\"\n]*[/\\\\]([^\"/\\\\\n]+)\"",
            "$1\"$2\"");

        return text;
    }

    private static string? ExtractSnapshotComment(string content)
    {
        if (content.StartsWith("// Snapshot:", StringComparison.Ordinal))
        {
            var newlineIndex = content.IndexOf('\n', StringComparison.Ordinal);
            return newlineIndex >= 0 ? content.Substring(0, newlineIndex).TrimEnd('\r') : content;
        }

        return null;
    }

    private static string StripSnapshotComment(string content)
    {
        if (content.StartsWith("// Snapshot:", StringComparison.Ordinal))
        {
            var newlineIndex = content.IndexOf('\n', StringComparison.Ordinal);
            return newlineIndex >= 0 ? content.Substring(newlineIndex + 1) : string.Empty;
        }

        return content;
    }

    [Fact]
    public void TestFixturesDirectory_Exists()
    {
        Output.WriteLine($"Looking for fixtures in: {FixturesPath}");
        Assert.True(Directory.Exists(FixturesPath),
            $"TestFixtures directory not found at: {FixturesPath}");
    }
}
