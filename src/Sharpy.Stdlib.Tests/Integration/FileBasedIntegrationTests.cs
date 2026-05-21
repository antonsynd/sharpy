using System.Text.RegularExpressions;
using Sharpy.TestInfrastructure.Integration;
using Sharpy.Compiler.Text;
using Xunit;
using Xunit.Abstractions;
using IOPath = System.IO.Path;

namespace Sharpy.Stdlib.Tests.Integration;

[Collection("HeavyCompilation")]
public class FileBasedIntegrationTests : StdlibIntegrationTestBase
{
    private static readonly string FixturesPath = IOPath.GetFullPath(IOPath.Combine(
        IOPath.GetDirectoryName(typeof(FileBasedIntegrationTests).Assembly.Location)!,
        "Integration", "TestFixtures"));

    public FileBasedIntegrationTests(ITestOutputHelper output) : base(output)
    {
    }

    public static IEnumerable<object[]> GetTestFixtures()
    {
        foreach (var fixture in FixtureDiscoveryHelper.DiscoverFixtures(FixturesPath))
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
                var relativePath = IOPath.GetRelativePath(projectDir, sourceFile);
                Output.WriteLine($"--- {relativePath} ---");
                Output.WriteLine(File.ReadAllText(sourceFile));
            }
            Output.WriteLine("====================");

            var entryPointBaseName = IOPath.GetFileNameWithoutExtension(entryPointFile);
            errorFilePath = IOPath.Combine(projectDir, $"{entryPointBaseName}.error");
            expectedFilePath = IOPath.Combine(projectDir, $"{entryPointBaseName}.expected");
            sourceTextContent = File.ReadAllText(IOPath.Combine(projectDir, entryPointFile));
            result = CompileAndExecuteProject(projectDir, entryPointFile);
        }
        else
        {
            var spyFilePath = path;
            var source = File.ReadAllText(spyFilePath);
            Output.WriteLine("=== Sharpy Source ===");
            Output.WriteLine(source);
            Output.WriteLine("=====================");

            errorFilePath = IOPath.ChangeExtension(spyFilePath, ".error");
            expectedFilePath = IOPath.ChangeExtension(spyFilePath, ".expected");
            sourceTextContent = source;
            result = CompileAndExecute(source, IOPath.GetFileName(spyFilePath));
        }

        var isErrorTest = File.Exists(errorFilePath);
        var runtimeErrorFilePath = isMultiFile
            ? IOPath.Combine(path, $"{IOPath.GetFileNameWithoutExtension(FindEntryPoint(path))}.runtime-error")
            : path.Replace(".spy", ".runtime-error");
        var isRuntimeErrorTest = File.Exists(runtimeErrorFilePath);
        var warningFilePath = IOPath.ChangeExtension(errorFilePath, ".warning");
        var hasWarningFile = File.Exists(warningFilePath);

        if (isRuntimeErrorTest)
        {
            var expectedRuntimeErrorContent = File.ReadAllText(runtimeErrorFilePath).Trim();
            Assert.True(result.GeneratedCSharp != null,
                $"Expected compilation to succeed but no C# was generated. Errors: {string.Join("\n", result.CompilationErrors)}");
            Assert.False(result.Success,
                $"Expected runtime error but program succeeded. Output: {result.StandardOutput}");

            var expectedLines = expectedRuntimeErrorContent
                .Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')).ToList();
            foreach (var line in expectedLines)
                Assert.Contains(line, result.StandardError, StringComparison.OrdinalIgnoreCase);
        }
        else if (isErrorTest)
        {
            var expectedErrorContent = File.ReadAllText(errorFilePath).Trim();
            Assert.False(result.Success, $"Expected failure but succeeded. Output: {result.StandardOutput}");
            var actualErrors = string.Join("\n", result.CompilationErrors);

            var expectedLines = expectedErrorContent
                .Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')).ToList();
            foreach (var line in expectedLines)
            {
                var locationMatch = Regex.Match(line, @"^(.+?)\s+@(\d+):(\d+)$");
                if (locationMatch.Success)
                {
                    var messagePattern = locationMatch.Groups[1].Value.Trim();
                    var expectedLineNum = int.Parse(locationMatch.Groups[2].Value);
                    var expectedColumn = int.Parse(locationMatch.Groups[3].Value);
                    Assert.Contains(messagePattern, actualErrors, StringComparison.OrdinalIgnoreCase);
                    var matchingDiag = result.RawDiagnostics.FirstOrDefault(d =>
                        d.Message.Contains(messagePattern, StringComparison.OrdinalIgnoreCase));
                    Assert.True(matchingDiag != null, $"No diagnostic matching '{messagePattern}'");
                    int? actualLine = null, actualColumn = null;
                    if (matchingDiag!.Span.HasValue && sourceTextContent != null)
                    {
                        var st = new SourceText(sourceTextContent);
                        var pos = st.GetLineAndColumn(matchingDiag.Span.Value.Start);
                        actualLine = pos.Line;
                        actualColumn = pos.Column;
                    }
                    else if (matchingDiag.Line.HasValue)
                    { actualLine = matchingDiag.Line; actualColumn = matchingDiag.Column; }
                    Assert.True(actualLine.HasValue, $"Diagnostic '{messagePattern}' has no location");
                    Assert.Equal(expectedLineNum, actualLine!.Value);
                    Assert.Equal(expectedColumn, actualColumn ?? 0);
                }
                else
                {
                    Assert.Contains(line, actualErrors, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        else
        {
            Assert.True(result.Success, $"Compilation failed: {string.Join("\n", result.CompilationErrors)}");
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

        if (hasWarningFile)
        {
            var expectedWarningContent = File.ReadAllText(warningFilePath).Trim();
            var actualWarnings = string.Join("\n", result.CompilationWarnings);
            var expectedWarningLines = expectedWarningContent
                .Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')).ToList();
            if (expectedWarningLines.Count == 0)
                Assert.Empty(result.CompilationWarnings);
            else
                foreach (var line in expectedWarningLines)
                    Assert.Contains(line, actualWarnings, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string FindEntryPoint(string projectDir)
    {
        var dirName = IOPath.GetFileName(projectDir);
        if (File.Exists(IOPath.Combine(projectDir, "main.spy")))
            return "main.spy";
        if (File.Exists(IOPath.Combine(projectDir, $"{dirName}.spy")))
            return $"{dirName}.spy";
        var spyFiles = Directory.GetFiles(projectDir, "*.spy").OrderBy(f => f).ToList();
        if (spyFiles.Count > 0)
            return IOPath.GetFileName(spyFiles[0]);
        throw new InvalidOperationException($"No .spy files found in {projectDir}");
    }
}
