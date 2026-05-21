using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Sharpy.Compiler.Text;
using Xunit;
using Xunit.Abstractions;

using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// File-based integration tests that discover .spy test files and their expected outputs.
///
/// Test files are organized in the TestFixtures directory:
///
/// SINGLE-FILE TESTS:
///   TestFixtures/
///     feature_name/
///       test_name.spy          - Sharpy source code
///       test_name.expected     - Expected stdout output
///       test_name.expected.cs  - (optional) Expected generated C# snapshot
///       test_name.error        - (optional) Expected compilation error substring
///       test_name.runtime-error - (optional) Expected runtime error substring in stderr
///       test_name.warning      - (optional) Expected compilation warning substring
///
/// MULTI-FILE TESTS (for imports):
///   TestFixtures/
///     feature_name/
///       test_name/             - Subdirectory containing multiple .spy files
///         main.spy             - Entry point (or test_name.spy)
///         module1.spy          - Additional module
///         module2.spy          - Additional module
///         main.expected        - Expected stdout output (same base name as entry point)
///         main.expected.cs     - (optional) Expected generated C# snapshot
///         main.error           - (optional) Expected compilation error substring
///         main.runtime-error   - (optional) Expected runtime error substring in stderr
///         main.warning         - (optional) Expected compilation warning substring
///
/// A test passes if:
///   - The .spy file(s) compile and execute successfully
///   - The stdout matches the contents of .expected exactly
///
/// Error tests (when .error file exists):
///   - The .spy file(s) should fail to compile
///   - The error message should contain the text in .error
///
/// Runtime error tests (when .runtime-error file exists):
///   - The .spy file(s) should compile successfully
///   - Execution should fail with a non-zero exit code
///   - Each non-empty, non-comment line in .runtime-error is a case-insensitive
///     substring that must appear in stderr
///
/// Warning tests (when .warning file exists):
///   - Compilation should succeed (warnings don't cause failure)
///   - Each non-empty, non-comment line in .warning must appear in compilation warnings
///   - An empty .warning file (or one with only comments) asserts zero warnings
///   - Can be combined with .expected for output verification, or stand alone
/// </summary>
[Collection("HeavyCompilation")]
public class FileBasedIntegrationTests : IntegrationTestBase
{
    private static readonly string FixturesPath = FixtureDiscoveryHelper.FixturesPath;

    public FileBasedIntegrationTests(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>
    /// Discovers all test fixtures by scanning the TestFixtures directory.
    /// Delegates to <see cref="FixtureDiscoveryHelper.DiscoverFixtures"/> and converts
    /// results to the object[] format required by xUnit MemberData.
    /// </summary>
    public static IEnumerable<object[]> GetTestFixtures()
    {
        foreach (var fixture in FixtureDiscoveryHelper.DiscoverFixtures())
        {
            yield return new object[] { fixture.TestName, fixture.SpyFilePath, fixture.IsMultiFile };
        }
    }

    [Theory]
    [MemberData(nameof(GetTestFixtures))]
    public void RunTestFixture(string testName, string path, bool isMultiFile)
    {
        Output.WriteLine($"Running test: {testName}");
        Output.WriteLine($"Test type: {(isMultiFile ? "Multi-file project" : "Single file")}");
        Output.WriteLine($"Path: {path}");

        ExecutionResult result;
        string errorFilePath;
        string expectedFilePath;
        string? sourceTextContent = null; // For location assertions in error tests

        if (isMultiFile)
        {
            // Multi-file test: path is a directory containing multiple .spy files
            var projectDir = path;

            // Find the entry point file
            var entryPointFile = FindEntryPoint(projectDir);
            Output.WriteLine($"Entry point: {entryPointFile}");

            // List all source files (including subdirectories for packages)
            var sourceFiles = Directory.GetFiles(projectDir, "*.spy", SearchOption.AllDirectories);
            Output.WriteLine("=== Source Files ===");
            foreach (var sourceFile in sourceFiles)
            {
                var relativePath = Path.GetRelativePath(projectDir, sourceFile);
                Output.WriteLine($"--- {relativePath} ---");
                Output.WriteLine(File.ReadAllText(sourceFile));
            }
            Output.WriteLine("====================");

            // Check for error/expected files (based on entry point name)
            var entryPointBaseName = Path.GetFileNameWithoutExtension(entryPointFile);
            errorFilePath = Path.Combine(projectDir, $"{entryPointBaseName}.error");
            expectedFilePath = Path.Combine(projectDir, $"{entryPointBaseName}.expected");

            // Read the entry point source for location assertions
            sourceTextContent = File.ReadAllText(Path.Combine(projectDir, entryPointFile));

            // Execute the multi-file test
            result = CompileAndExecuteProject(projectDir, entryPointFile);
        }
        else
        {
            // Single-file test: path is a .spy file
            var spyFilePath = path;

            // Read the source file
            var source = File.ReadAllText(spyFilePath);
            Output.WriteLine("=== Sharpy Source ===");
            Output.WriteLine(source);
            Output.WriteLine("=====================");

            errorFilePath = Path.ChangeExtension(spyFilePath, ".error");
            expectedFilePath = Path.ChangeExtension(spyFilePath, ".expected");
            sourceTextContent = source;

            // Execute the test
            result = CompileAndExecute(source, Path.GetFileName(spyFilePath));
        }

        // Check if this is an error test
        var isErrorTest = File.Exists(errorFilePath);

        // Check for runtime-error file (.runtime-error) - same base name as .error/.expected
        var runtimeErrorFilePath = isMultiFile
            ? Path.Combine(path, $"{Path.GetFileNameWithoutExtension(FindEntryPoint(path))}.runtime-error")
            : path.Replace(".spy", ".runtime-error");
        var isRuntimeErrorTest = File.Exists(runtimeErrorFilePath);

        // Check for warning file (.warning) - same base name as .error/.expected
        var warningFilePath = Path.ChangeExtension(errorFilePath, ".warning");
        var hasWarningFile = File.Exists(warningFilePath);

        if (isRuntimeErrorTest)
        {
            // Runtime error test: compilation should succeed but execution should fail
            var expectedRuntimeErrorContent = File.ReadAllText(runtimeErrorFilePath).Trim();
            Output.WriteLine($"Expected runtime error patterns:\n{expectedRuntimeErrorContent}");

            // Verify compilation succeeded (generated C# was produced)
            Assert.True(result.GeneratedCSharp != null,
                $"Expected compilation to succeed for runtime error test, but no C# was generated. " +
                $"Compilation errors: {string.Join("\n", result.CompilationErrors)}");

            // Verify execution failed (non-zero exit code)
            Assert.False(result.Success,
                $"Expected runtime error but program exited successfully. Output: {result.StandardOutput}");

            Output.WriteLine($"Actual stderr:\n{result.StandardError}");

            // Match each non-empty, non-comment line as a case-insensitive substring against stderr
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
            // Error test: compilation should fail
            var expectedErrorContent = File.ReadAllText(errorFilePath).Trim();
            Output.WriteLine($"Expected error patterns:\n{expectedErrorContent}");

            Assert.False(result.Success,
                $"Expected compilation to fail, but it succeeded. Output: {result.StandardOutput}");

            var actualErrors = string.Join("\n", result.CompilationErrors);
            Output.WriteLine($"Actual errors:\n{actualErrors}");

            // Each non-empty, non-comment line in the .error file is checked
            // independently as a substring that must appear in the actual errors.
            // This allows verifying that error recovery produces multiple distinct errors.
            //
            // Location assertions: lines ending with @line:column verify that the
            // matching diagnostic points to the correct source location. Format:
            //   message substring @3:5
            // The message part is still checked as a substring. The @line:column part
            // asserts the diagnostic's line and column. Lines without @line:column
            // continue to work as substring-only matches (backward compatible).
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
                    // Location-aware assertion: check both message and location
                    var messagePattern = locationMatch.Groups[1].Value.Trim();
                    var expectedLineNum = int.Parse(locationMatch.Groups[2].Value);
                    var expectedColumn = int.Parse(locationMatch.Groups[3].Value);

                    // First verify the message substring exists in the errors
                    Assert.Contains(messagePattern, actualErrors, StringComparison.OrdinalIgnoreCase);

                    // Then verify a matching diagnostic has the correct location
                    var matchingDiag = result.RawDiagnostics.FirstOrDefault(d =>
                        d.Message.Contains(messagePattern, StringComparison.OrdinalIgnoreCase));

                    Assert.True(matchingDiag != null,
                        $"No raw diagnostic found matching '{messagePattern}'. " +
                        $"RawDiagnostics count: {result.RawDiagnostics.Count}");

                    // Check location: derive line/column from Span (via SourceText)
                    // or fall back to the diagnostic's Line/Column fields
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
                    // Plain substring match (backward compatible)
                    Assert.Contains(expectedLine, actualErrors, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        else
        {
            // Success test: compilation should succeed
            Assert.True(result.Success,
                $"Compilation failed: {string.Join("\n", result.CompilationErrors)}");

            // If there's an expected output file, verify stdout matches
            if (File.Exists(expectedFilePath))
            {
                var expectedOutput = File.ReadAllText(expectedFilePath);
                Assert.Equal(expectedOutput, result.StandardOutput);
            }
            else if (!hasWarningFile)
            {
                // Neither .expected nor .warning file found - test fixture is incomplete
                Assert.Fail($"Missing expected output file: {expectedFilePath}");
            }
        }

        // C# snapshot verification (when .expected.cs file exists)
        if (!isErrorTest && result.Success && result.GeneratedCSharp != null)
        {
            var snapshotFilePath = isMultiFile
                ? Path.Combine(path, $"{Path.GetFileNameWithoutExtension(FindEntryPoint(path))}.expected.cs")
                : Path.ChangeExtension(path, ".expected.cs");

            var updateSnapshots = Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "true";

            if (updateSnapshots && File.Exists(snapshotFilePath))
            {
                // Regenerate existing snapshot (does not create new snapshot files)
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

        // Warning verification (can apply to both error tests and success tests)
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
                // Empty .warning file (or only comments) means "no warnings expected"
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

    /// <summary>
    /// Finds the entry point file in a multi-file test directory.
    /// Priority: main.spy > directory_name.spy > first .spy file alphabetically
    /// </summary>
    private static string FindEntryPoint(string projectDir)
    {
        var dirName = Path.GetFileName(projectDir);

        // Priority 1: main.spy
        var mainSpy = Path.Combine(projectDir, "main.spy");
        if (File.Exists(mainSpy))
        {
            return "main.spy";
        }

        // Priority 2: directory_name.spy
        var dirNameSpy = Path.Combine(projectDir, $"{dirName}.spy");
        if (File.Exists(dirNameSpy))
        {
            return $"{dirName}.spy";
        }

        // Priority 3: First .spy file alphabetically
        var spyFiles = Directory.GetFiles(projectDir, "*.spy").OrderBy(f => f).ToList();
        if (spyFiles.Count > 0)
        {
            return Path.GetFileName(spyFiles[0]);
        }

        throw new InvalidOperationException($"No .spy files found in {projectDir}");
    }

    /// <summary>
    /// Normalizes generated C# code for snapshot comparison.
    /// Uses Roslyn formatting to ensure consistent whitespace regardless of emitter formatting changes.
    /// </summary>
    private static string NormalizeCSharp(string csharpCode)
    {
        // Normalize line endings to LF before parsing so Roslyn produces consistent output
        var normalizedInput = csharpCode.Replace("\r\n", "\n");
        var tree = CSharpSyntaxTree.ParseText(normalizedInput);
        var root = tree.GetRoot();

        // Use Roslyn's built-in formatter to normalize whitespace
        using var workspace = new AdhocWorkspace();
        var formatted = Formatter.Format(root, workspace);
        return formatted.ToFullString().Replace("\r\n", "\n").TrimEnd() + "\n";
    }

    /// <summary>
    /// Extracts the "// Snapshot: ..." comment line from the beginning of a snapshot file, if present.
    /// Returns the comment line (without trailing newline) or null if not found.
    /// </summary>
    private static string? ExtractSnapshotComment(string content)
    {
        if (content.StartsWith("// Snapshot:", StringComparison.Ordinal))
        {
            var newlineIndex = content.IndexOf('\n');
            return newlineIndex >= 0 ? content.Substring(0, newlineIndex).TrimEnd('\r') : content;
        }

        return null;
    }

    /// <summary>
    /// Strips the "// Snapshot: ..." comment line from the beginning of a snapshot file, if present.
    /// Returns the content without the comment line.
    /// </summary>
    private static string StripSnapshotComment(string content)
    {
        if (content.StartsWith("// Snapshot:", StringComparison.Ordinal))
        {
            var newlineIndex = content.IndexOf('\n');
            return newlineIndex >= 0 ? content.Substring(newlineIndex + 1) : string.Empty;
        }

        return content;
    }

    /// <summary>
    /// Verifies that the test fixtures directory exists and contains at least one test.
    /// This is a sanity check to ensure the test discovery is working.
    /// </summary>
    [Fact]
    public void TestFixturesDirectory_Exists()
    {
        Output.WriteLine($"Looking for fixtures in: {FixturesPath}");
        Assert.True(Directory.Exists(FixturesPath),
            $"TestFixtures directory not found at: {FixturesPath}");
    }
}
