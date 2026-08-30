using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Shared;
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

            var sourceFiles = SourceGlob.EnumerateSourceFiles(projectDir, "*.spy", SearchOption.AllDirectories)
                .ToArray();
            Output.WriteLine("=== Source Files ===");
            foreach (var sourceFile in sourceFiles)
            {
                var relativePath = Path.GetRelativePath(projectDir, sourceFile);
                Output.WriteLine($"--- {relativePath} ---");
                Output.WriteLine(File.ReadAllText(sourceFile));
            }
            Output.WriteLine("====================");

            errorFilePath = MultiFileSidecar(projectDir, ".error");
            expectedFilePath = MultiFileSidecar(projectDir, ".expected");
            sourceTextContent = File.ReadAllText(Path.Combine(projectDir, entryPointFile));

            // A `.features` sidecar next to the entry point enables experimental features
            // (e.g. matmul, defer) compilation-wide for the whole fixture project. Unknown
            // names throw loudly here rather than silently disabling the feature.
            var projectFeatures = ReadFixtureFeatures(MultiFileSidecar(projectDir, ".features"));
            result = CompileAndExecuteProject(projectDir, entryPointFile, features: projectFeatures);
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

            // A `<stem>.features` sidecar enables experimental features (e.g. matmul, defer)
            // for this single-file fixture. Unknown names throw loudly here.
            var fileFeatures = ReadFixtureFeatures(Path.ChangeExtension(spyFilePath, ".features"));
            result = CompileAndExecute(source, Path.GetFileName(spyFilePath), features: fileFeatures);
        }

        var runtimeErrorFilePath = isMultiFile
            ? MultiFileSidecar(path, ".runtime-error")
            : path.Replace(".spy", ".runtime-error", StringComparison.Ordinal);
        var snapshotFilePath = isMultiFile
            ? MultiFileSidecar(path, ".expected.cs")
            : Path.ChangeExtension(path, ".expected.cs");

        AssertFixtureOutcome(result, errorFilePath, expectedFilePath, runtimeErrorFilePath,
            snapshotFilePath, sourceTextContent);
    }

    /// <summary>
    /// Asserts a fixture's outcome against its sidecars: <c>.runtime-error</c> (compiles, then fails
    /// at runtime), <c>.error</c> (compilation must fail, optionally at a stated
    /// <c>@line:col</c>), else <c>.expected</c> stdout — plus the <c>.expected.cs</c> snapshot and
    /// <c>.warning</c> checks. Every harness that drives fixtures shares this one method so the arms
    /// cannot drift in what "passing" means (#1171); an arm differs only in how it compiles.
    /// </summary>
    /// <param name="verifyCSharpSnapshot">
    /// When false, the <c>.expected.cs</c> comparison is skipped. Set by arms whose generated C# is
    /// legitimately shaped differently from the snapshot's owning arm (e.g. a different root
    /// namespace), where the snapshot would compare two different-by-design outputs.
    /// </param>
    protected void AssertFixtureOutcome(
        ExecutionResult result,
        string errorFilePath,
        string expectedFilePath,
        string runtimeErrorFilePath,
        string snapshotFilePath,
        string? sourceTextContent,
        bool verifyCSharpSnapshot = true)
    {
        var isErrorTest = File.Exists(errorFilePath);
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
                var expectation = ParseExpectation(expectedLine, runtimeErrorFilePath);
                switch (expectation.Kind)
                {
                    case ExpectationKind.Negative:
                        Assert.DoesNotContain(expectation.Text, result.StandardError,
                            StringComparison.OrdinalIgnoreCase);
                        break;
                    case ExpectationKind.Count:
                        Assert.Fail(
                            $"'!count' is only meaningful in a .error sidecar, where diagnostics are "
                            + $"counted; {Path.GetFileName(runtimeErrorFilePath)} matches against "
                            + "process stderr, which is one stream and not a diagnostic list.");
                        break;
                    default:
                        Assert.Contains(expectation.Text, result.StandardError,
                            StringComparison.OrdinalIgnoreCase);
                        break;
                }
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

            foreach (var rawExpectedLine in expectedLines)
            {
                var expectation = ParseExpectation(rawExpectedLine, errorFilePath);

                if (expectation.Kind == ExpectationKind.Negative)
                {
                    Assert.DoesNotContain(expectation.Text, actualErrors, StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (expectation.Kind == ExpectationKind.Count)
                {
                    Assert.True(result.CompilationErrors.Count == expectation.Count,
                        $"Expected exactly {expectation.Count} error diagnostic(s), got "
                        + $"{result.CompilationErrors.Count}:\n{actualErrors}");
                    continue;
                }

                var expectedLine = expectation.Text;
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
        if (verifyCSharpSnapshot && !isErrorTest && result.Success && result.GeneratedCSharp != null)
        {
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

    /// <summary>What one expectation line in a <c>.error</c>/<c>.runtime-error</c> sidecar asserts.</summary>
    private enum ExpectationKind
    {
        /// <summary>A substring that MUST appear in the diagnostics (the historical behaviour).</summary>
        Positive,

        /// <summary><c>!substring</c> — a substring that must NOT appear.</summary>
        Negative,

        /// <summary><c>!count N</c> — exactly N error diagnostics.</summary>
        Count,
    }

    private readonly record struct DiagnosticExpectation(ExpectationKind Kind, string Text, int Count);

    /// <summary>
    /// Classifies one expectation line (#1457).
    ///
    /// <para>Before this, a sidecar could only say "this substring appears somewhere in the
    /// diagnostics". It could not say <b>this is the only diagnostic</b> or <b>this diagnostic is
    /// absent</b>, so a fixture that emitted the right error plus three wrong ones was
    /// indistinguishable from one that emitted only the right error — and with 646 of the 724
    /// sidecars carrying a single line, 91 of them matching on 20 characters or fewer (the
    /// shortest being the 4-character <c>type</c>), "some diagnostic mentions this" is a very
    /// weak claim. Two forms close that:</para>
    /// <list type="bullet">
    ///   <item><c>!some text</c> — that text must NOT appear in any diagnostic.</item>
    ///   <item><c>!count 1</c> — exactly one error diagnostic, no more.</item>
    /// </list>
    ///
    /// <para>The <c>!</c> sigil was verified unused across every existing <c>.error</c> and
    /// <c>.runtime-error</c> sidecar before it was chosen, so no fixture changes meaning. A line
    /// after <c>!</c> is read as the count directive only when it is literally <c>count</c>
    /// followed by an integer; anything else is a negative pattern.</para>
    /// </summary>
    private static DiagnosticExpectation ParseExpectation(string line, string sidecarPath)
    {
        if (!line.StartsWith('!'))
            return new DiagnosticExpectation(ExpectationKind.Positive, line, 0);

        var rest = line[1..].Trim();

        var countMatch = Regex.Match(rest, @"^count\s+(\d+)$");
        if (countMatch.Success)
        {
            return new DiagnosticExpectation(ExpectationKind.Count, line,
                int.Parse(countMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        // An empty negative pattern would assert that the diagnostics do not contain the empty
        // string, which is false for every input — a fixture that can never pass. Fail on the
        // sidecar rather than on the compiler.
        Assert.True(rest.Length > 0,
            $"{Path.GetFileName(sidecarPath)} has a bare '!' line. A negative expectation needs "
            + "the text that must be absent (e.g. '!SPY0220'), and '!count N' asserts a count.");

        return new DiagnosticExpectation(ExpectationKind.Negative, rest, 0);
    }

    /// <summary>
    /// Reads a fixture's <c>.features</c> sidecar (via <see cref="FixtureDiscoveryHelper.ReadFeaturesFile"/>)
    /// and folds the declared names into a <see cref="FeatureFlags"/> set. Returns
    /// <see cref="FeatureFlags.None"/> when the sidecar is absent. Unknown feature names throw
    /// loudly from the discovery helper, naming the file and the bad name.
    /// </summary>
    protected static FeatureFlags ReadFixtureFeatures(string featuresFilePath)
    {
        var names = FixtureDiscoveryHelper.ReadFeaturesFile(featuresFilePath);
        return names.Count == 0 ? FeatureFlags.None : FeatureFlags.None.Enable(names);
    }

    /// <summary>
    /// The path of a multi-file fixture's sidecar with the given extension — the entry point's base
    /// name plus <paramref name="extension"/> inside <paramref name="projectDir"/> (e.g.
    /// <c>main.expected</c>). One definition so every arm and every sidecar kind agree on where a
    /// fixture's expectations live.
    /// </summary>
    public static string MultiFileSidecar(string projectDir, string extension)
        => Path.Combine(projectDir,
            Path.GetFileNameWithoutExtension(FindEntryPoint(projectDir)) + extension);

    /// <summary>
    /// The entry file of a multi-file fixture directory: <c>main.spy</c>, else a <c>.spy</c> named
    /// after the directory, else the first <c>.spy</c> in name order. Public so harnesses that drive
    /// multi-file fixtures without deriving from this class (e.g. the metamorphic corpus sweep) share
    /// one definition of "the entry point" with the fixture runner.
    /// </summary>
    public static string FindEntryPoint(string projectDir)
    {
        var dirName = Path.GetFileName(projectDir);

        if (File.Exists(Path.Combine(projectDir, "main.spy")))
            return "main.spy";

        if (File.Exists(Path.Combine(projectDir, $"{dirName}.spy")))
            return $"{dirName}.spy";

        var spyFiles = SourceGlob.EnumerateSourceFiles(projectDir, "*.spy", SearchOption.TopDirectoryOnly).OrderBy(f => f).ToList();
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
