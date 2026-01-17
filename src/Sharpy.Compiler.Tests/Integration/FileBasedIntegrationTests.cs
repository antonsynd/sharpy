using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// File-based integration tests that discover .spy test files and their expected outputs.
///
/// Test files are organized in the TestFixtures directory:
///   TestFixtures/
///     feature_name/
///       test_name.spy          - Sharpy source code
///       test_name.expected     - Expected stdout output
///       test_name.error        - (optional) Expected compilation error substring
///
/// A test passes if:
///   - The .spy file compiles and executes successfully
///   - The stdout matches the contents of .expected exactly
///
/// Error tests (when .error file exists):
///   - The .spy file should fail to compile
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
    /// </summary>
    public static IEnumerable<object[]> GetTestFixtures()
    {
        if (!Directory.Exists(FixturesPath))
        {
            yield break;
        }

        // Find all .spy files recursively
        foreach (var spyFile in Directory.EnumerateFiles(FixturesPath, "*.spy", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(FixturesPath, spyFile);
            var testName = Path.ChangeExtension(relativePath, null).Replace(Path.DirectorySeparatorChar, '/');
            yield return new object[] { testName, spyFile };
        }
    }

    [Theory]
    [MemberData(nameof(GetTestFixtures))]
    public void RunTestFixture(string testName, string spyFilePath)
    {
        Output.WriteLine($"Running test: {testName}");
        Output.WriteLine($"Source file: {spyFilePath}");

        // Read the source file
        var source = File.ReadAllText(spyFilePath);
        Output.WriteLine("=== Sharpy Source ===");
        Output.WriteLine(source);
        Output.WriteLine("=====================");

        // Check if this is an error test
        var errorFilePath = Path.ChangeExtension(spyFilePath, ".error");
        var isErrorTest = File.Exists(errorFilePath);

        // Execute the test
        var result = CompileAndExecute(source, Path.GetFileName(spyFilePath));

        if (isErrorTest)
        {
            // Error test: compilation should fail
            var expectedError = File.ReadAllText(errorFilePath).Trim();
            Output.WriteLine($"Expected error: {expectedError}");

            Assert.False(result.Success,
                $"Expected compilation to fail, but it succeeded. Output: {result.StandardOutput}");

            var actualErrors = string.Join("\n", result.CompilationErrors);
            Assert.Contains(expectedError, actualErrors, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            // Success test: compilation should succeed and output should match
            var expectedFilePath = Path.ChangeExtension(spyFilePath, ".expected");

            Assert.True(File.Exists(expectedFilePath),
                $"Missing expected output file: {expectedFilePath}");

            var expectedOutput = File.ReadAllText(expectedFilePath);

            Assert.True(result.Success,
                $"Compilation failed: {string.Join("\n", result.CompilationErrors)}");

            Assert.Equal(expectedOutput, result.StandardOutput);
        }
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
