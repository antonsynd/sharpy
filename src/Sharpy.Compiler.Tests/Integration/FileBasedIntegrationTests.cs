using System.Reflection;
using Xunit;
using Xunit.Abstractions;

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
///       test_name.error        - (optional) Expected compilation error substring
///
/// MULTI-FILE TESTS (for imports):
///   TestFixtures/
///     feature_name/
///       test_name/             - Subdirectory containing multiple .spy files
///         main.spy             - Entry point (or test_name.spy)
///         module1.spy          - Additional module
///         module2.spy          - Additional module
///         main.expected        - Expected stdout output (same base name as entry point)
///         main.error           - (optional) Expected compilation error substring
///
/// A test passes if:
///   - The .spy file(s) compile and execute successfully
///   - The stdout matches the contents of .expected exactly
///
/// Error tests (when .error file exists):
///   - The .spy file(s) should fail to compile
///   - The error message should contain the text in .error
/// </summary>
public class FileBasedIntegrationTests : IntegrationTestBase
{
    private static readonly string FixturesPath;

    static FileBasedIntegrationTests()
    {
        // Find the TestFixtures directory relative to the test assembly
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var assemblyDir = Path.GetDirectoryName(assemblyLocation)!;

        // Navigate from bin/Debug/net10.0 to the source directory
        FixturesPath = Path.GetFullPath(Path.Combine(
            assemblyDir, "..", "..", "..", "Integration", "TestFixtures"));
    }

    public FileBasedIntegrationTests(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>
    /// Discovers all test fixtures by scanning the TestFixtures directory.
    /// Supports both single-file tests and multi-file tests.
    ///
    /// Multi-file test detection:
    /// A directory is considered a multi-file test if it contains multiple .spy files
    /// AND at least one of these is true:
    /// 1. There's a main.spy file (indicates entry point)
    /// 2. There's a main.expected or main.error file
    /// 3. The directory contains only .spy, .expected, and .error files (no nested dirs of tests)
    ///
    /// This prevents directories like "errors/" with multiple single-file error tests
    /// from being mistakenly treated as multi-file projects.
    /// </summary>
    public static IEnumerable<object[]> GetTestFixtures()
    {
        if (!Directory.Exists(FixturesPath))
        {
            yield break;
        }

        // Track directories we've already processed as multi-file tests
        var processedDirectories = new HashSet<string>();

        // Find all .spy files recursively
        foreach (var spyFile in Directory.EnumerateFiles(FixturesPath, "*.spy", SearchOption.AllDirectories))
        {
            var spyDir = Path.GetDirectoryName(spyFile)!;

            // Check if this directory contains multiple .spy files
            var spyFilesInDir = Directory.GetFiles(spyDir, "*.spy");

            // A directory is a multi-file test if:
            // - It has multiple .spy files AND
            // - It has main.spy or main.expected/main.error (explicit entry point marker)
            var hasMainSpy = File.Exists(Path.Combine(spyDir, "main.spy"));
            var hasMainExpected = File.Exists(Path.Combine(spyDir, "main.expected"));
            var hasMainError = File.Exists(Path.Combine(spyDir, "main.error"));
            var isMultiFileTest = spyFilesInDir.Length > 1 && (hasMainSpy || hasMainExpected || hasMainError);

            if (isMultiFileTest)
            {
                // Multi-file test - only process once per directory
                if (processedDirectories.Contains(spyDir))
                {
                    continue;
                }
                processedDirectories.Add(spyDir);

                // Skip tests with .skip files (tests pending fixes)
                if (File.Exists(Path.Combine(spyDir, "main.skip")))
                {
                    continue;
                }

                // Use the directory path as the test identifier
                var relativePath = Path.GetRelativePath(FixturesPath, spyDir);
                var testName = relativePath.Replace(Path.DirectorySeparatorChar, '/');

                // Return the directory path with a marker
                yield return new object[] { testName, spyDir, true /* isMultiFile */ };
            }
            else
            {
                // Skip tests with .skip files (tests pending fixes)
                if (File.Exists(Path.ChangeExtension(spyFile, ".skip")))
                {
                    continue;
                }

                // Single-file test
                var relativePath = Path.GetRelativePath(FixturesPath, spyFile);
                var testName = Path.ChangeExtension(relativePath, null).Replace(Path.DirectorySeparatorChar, '/');
                yield return new object[] { testName, spyFile, false /* isMultiFile */ };
            }
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

        if (isMultiFile)
        {
            // Multi-file test: path is a directory containing multiple .spy files
            var projectDir = path;

            // Find the entry point file
            var entryPointFile = FindEntryPoint(projectDir);
            Output.WriteLine($"Entry point: {entryPointFile}");

            // List all source files
            var sourceFiles = Directory.GetFiles(projectDir, "*.spy");
            Output.WriteLine("=== Source Files ===");
            foreach (var sourceFile in sourceFiles)
            {
                Output.WriteLine($"--- {Path.GetFileName(sourceFile)} ---");
                Output.WriteLine(File.ReadAllText(sourceFile));
            }
            Output.WriteLine("====================");

            // Check for error/expected files (based on entry point name)
            var entryPointBaseName = Path.GetFileNameWithoutExtension(entryPointFile);
            errorFilePath = Path.Combine(projectDir, $"{entryPointBaseName}.error");
            expectedFilePath = Path.Combine(projectDir, $"{entryPointBaseName}.expected");

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

            // Execute the test
            result = CompileAndExecute(source, Path.GetFileName(spyFilePath));
        }

        // Check if this is an error test
        var isErrorTest = File.Exists(errorFilePath);

        if (isErrorTest)
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
            var expectedLines = expectedErrorContent
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith('#'))
                .ToList();

            foreach (var expectedLine in expectedLines)
            {
                Assert.Contains(expectedLine, actualErrors, StringComparison.OrdinalIgnoreCase);
            }
        }
        else
        {
            // Success test: compilation should succeed and output should match
            Assert.True(File.Exists(expectedFilePath),
                $"Missing expected output file: {expectedFilePath}");

            var expectedOutput = File.ReadAllText(expectedFilePath);

            Assert.True(result.Success,
                $"Compilation failed: {string.Join("\n", result.CompilationErrors)}");

            Assert.Equal(expectedOutput, result.StandardOutput);
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
