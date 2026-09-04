using FluentAssertions;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Guards the store-conversion seam invariant (plan-14853b Phase 2 Task 3):
/// <list type="bullet">
///   <item><c>ImplicitConversions.*</c> callers in <c>src/Sharpy.Compiler</c> are in the allowlist —
///   new callers must route through the store seam or the argument-assignability seam.</item>
///   <item><c>IsArgumentAssignable</c> callers outside <c>TypeChecker.Utilities.cs</c> are in the
///   allowlist — new resolution routes must go through the existing seam.</item>
/// </list>
/// </summary>
public class StoreSeamConformanceTests
{
    private static readonly HashSet<string> ImplicitConversionsAllowedFiles = new()
    {
        "TypeChecker.StoreConversion.cs",
        "TypeChecker.Utilities.cs",
        "TypeChecker.Expressions.Operators.cs",
        "TypeChecker.Statements.cs",
        "TypeChecker.Expressions.Access.Calls.cs",
    };

    private static readonly HashSet<string> IsArgumentAssignableAllowedFiles = new()
    {
        "TypeChecker.Utilities.cs",
        "TypeChecker.Expressions.Access.Calls.cs",
        "TypeChecker.Expressions.Access.Calls.Overloads.cs",
        "TypeChecker.Expressions.Access.Calls.Construction.cs",
        "TypeChecker.cs",
    };

    [Fact]
    public void ImplicitConversions_CallersAreInAllowlist()
    {
        var compilerDir = FindCompilerSemanticDirectory();
        Directory.Exists(compilerDir).Should().BeTrue(
            $"compiler Semantic directory not found at {compilerDir}");

        var files = Directory.GetFiles(compilerDir, "*.cs", SearchOption.AllDirectories);
        var violations = new List<string>();

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);

            if (fileName == "ImplicitConversions.cs")
                continue;

            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("ImplicitConversions."))
                    continue;

                if (lines[i].TrimStart().StartsWith("//") || lines[i].TrimStart().StartsWith("///"))
                    continue;

                if (!ImplicitConversionsAllowedFiles.Contains(fileName))
                    violations.Add($"{fileName}:{i + 1}: {lines[i].Trim()}");
            }
        }

        violations.Should().BeEmpty(
            "ImplicitConversions.* callers must be in the store-conversion or argument-assignability seam. " +
            "New conversion logic should route through ClassifyStore or IsArgumentAssignable.");
    }

    [Fact]
    public void IsArgumentAssignable_CallersAreInAllowlist()
    {
        var compilerDir = FindCompilerSemanticDirectory();
        Directory.Exists(compilerDir).Should().BeTrue(
            $"compiler Semantic directory not found at {compilerDir}");

        var files = Directory.GetFiles(compilerDir, "*.cs", SearchOption.AllDirectories);
        var violations = new List<string>();

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var lines = File.ReadAllLines(file);

            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("IsArgumentAssignable"))
                    continue;

                if (lines[i].TrimStart().StartsWith("//") || lines[i].TrimStart().StartsWith("///"))
                    continue;

                if (lines[i].Contains("private bool IsArgumentAssignable"))
                    continue;

                if (!IsArgumentAssignableAllowedFiles.Contains(fileName))
                    violations.Add($"{fileName}:{i + 1}: {lines[i].Trim()}");
            }
        }

        violations.Should().BeEmpty(
            "IsArgumentAssignable callers outside its allowlist must route through the existing " +
            "argument-assignability seam in TypeChecker.Utilities.cs.");
    }

    private static string FindCompilerSemanticDirectory()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var semanticPath = Path.Combine(current, "src", "Sharpy.Compiler", "Semantic");
            if (Directory.Exists(semanticPath))
                return semanticPath;
            current = Directory.GetParent(current)?.FullName;
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Sharpy.Compiler", "Semantic"));
    }
}
