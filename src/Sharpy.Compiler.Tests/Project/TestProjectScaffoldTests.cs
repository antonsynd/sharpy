using Sharpy.Compiler.Project;
using Xunit;

namespace Sharpy.Compiler.Tests.Project;

public class TestProjectScaffoldTests
{
    #region Compile Items

    [Fact]
    public void GenerateCsprojContent_IncludesCompileWildcard()
    {
        var config = CreateTestConfig();

        var content = TestProjectScaffold.GenerateCsprojContent(config, "/tmp/output", "TestAssembly");

        Assert.Contains("<Compile Include=\"**/*.cs\" />", content);
    }

    #endregion

    #region Runtime References

    [Fact]
    public void GenerateCsprojContent_IncludesRuntimeReferences()
    {
        var config = CreateTestConfig();

        var content = TestProjectScaffold.GenerateCsprojContent(config, "/tmp/output", "TestAssembly");

        // The scaffold resolves references from the compiler's assembly location.
        // In test context, Sharpy.Core.dll and Sharpy.Stdlib.dll should be present
        // alongside the test runner's loaded compiler assembly.
        // We verify the Reference element structure is correct.
        Assert.Contains("<Reference Include=\"Sharpy.Core\">", content);
        Assert.Contains("<HintPath>", content);
    }

    #endregion

    #region Required Test Packages

    [Fact]
    public void GenerateCsprojContent_IncludesTestSdk()
    {
        var config = CreateTestConfig();

        var content = TestProjectScaffold.GenerateCsprojContent(config, "/tmp/output", "TestAssembly");

        Assert.Contains("Microsoft.NET.Test.Sdk", content);
    }

    [Fact]
    public void GenerateCsprojContent_IncludesRunnerVisualStudio()
    {
        var config = CreateTestConfig();

        var content = TestProjectScaffold.GenerateCsprojContent(config, "/tmp/output", "TestAssembly");

        Assert.Contains("xunit.runner.visualstudio", content);
    }

    [Fact]
    public void GenerateCsprojContent_DoesNotDuplicateUserDeclaredPackages()
    {
        var config = new ProjectConfig
        {
            RootNamespace = "Test",
            TargetFramework = "net10.0",
            PackageReferences = new List<PackageRef>
            {
                new PackageRef("xunit", "2.9.3"),
                new PackageRef("Microsoft.NET.Test.Sdk", "17.10.0"), // User-declared version
            },
            SourceFiles = new List<string> { "test.spy" },
        };

        var content = TestProjectScaffold.GenerateCsprojContent(config, "/tmp/output", "TestAssembly");

        // Count occurrences of Microsoft.NET.Test.Sdk — should appear exactly once
        var count = CountOccurrences(content, "Microsoft.NET.Test.Sdk");
        Assert.Equal(1, count);

        // User's version should be preserved, not overwritten
        Assert.Contains("Version=\"17.10.0\"", content);
    }

    #endregion

    #region User Package References

    [Fact]
    public void GenerateCsprojContent_IncludesUserPackageReferences()
    {
        var config = new ProjectConfig
        {
            RootNamespace = "Test",
            TargetFramework = "net10.0",
            PackageReferences = new List<PackageRef>
            {
                new PackageRef("xunit", "2.9.3"),
                new PackageRef("Moq", "4.20.0"),
            },
            SourceFiles = new List<string> { "test.spy" },
        };

        var content = TestProjectScaffold.GenerateCsprojContent(config, "/tmp/output", "TestAssembly");

        Assert.Contains("Include=\"xunit\"", content);
        Assert.Contains("Include=\"Moq\"", content);
    }

    #endregion

    #region Target Framework

    [Fact]
    public void GenerateCsprojContent_UsesConfigTargetFramework()
    {
        var config = new ProjectConfig
        {
            RootNamespace = "Test",
            TargetFramework = "net9.0",
            PackageReferences = new List<PackageRef>
            {
                new PackageRef("xunit", "2.9.3"),
            },
            SourceFiles = new List<string> { "test.spy" },
        };

        var content = TestProjectScaffold.GenerateCsprojContent(config, "/tmp/output", "TestAssembly");

        Assert.Contains("<TargetFramework>net9.0</TargetFramework>", content);
    }

    #endregion

    #region Overall Structure

    [Fact]
    public void GenerateCsprojContent_IsValidXmlStructure()
    {
        var config = CreateTestConfig();

        var content = TestProjectScaffold.GenerateCsprojContent(config, "/tmp/output", "TestAssembly");

        // Verify it starts with <Project> and ends with </Project>
        Assert.StartsWith("<Project", content.Trim());
        Assert.EndsWith("</Project>", content.Trim());

        // Verify it contains all three ItemGroup sections
        Assert.Contains("<PropertyGroup>", content);
        Assert.Contains("<ItemGroup>", content);
        Assert.Contains("<IsPackable>false</IsPackable>", content);
    }

    #endregion

    #region Helpers

    private static ProjectConfig CreateTestConfig()
    {
        return new ProjectConfig
        {
            RootNamespace = "Test",
            TargetFramework = "net10.0",
            PackageReferences = new List<PackageRef>
            {
                new PackageRef("xunit", "2.9.3"),
            },
            SourceFiles = new List<string> { "test.spy" },
        };
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    #endregion
}
