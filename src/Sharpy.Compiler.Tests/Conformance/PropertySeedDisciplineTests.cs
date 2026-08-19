using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Guards that every property test under Properties/ derives its randomness from the CsCheck
/// seed. An unseeded <c>new Random()</c> (parameterless) means the printed CsCheck seed cannot
/// reproduce the corruption/injection point, and re-running it only reproduces the module —
/// measured at 3-of-8 on a fixed seed (#1439, #1522).
/// </summary>
public class PropertySeedDisciplineTests
{
    [Fact]
    public void NoUnseededRandom_InPropertyTests()
    {
        var propertiesDir = FindSourceDir("Sharpy.Compiler.Tests");
        var propertiesPath = Path.Combine(propertiesDir, "Properties");
        Assert.True(Directory.Exists(propertiesPath), $"Properties dir not found: {propertiesPath}");

        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(propertiesPath, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                continue;

            var text = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(text);
            var root = tree.GetRoot();

            foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                if (creation.Type.ToString() != "Random")
                    continue;

                if (creation.ArgumentList == null || creation.ArgumentList.Arguments.Count == 0)
                {
                    var lineSpan = creation.GetLocation().GetLineSpan();
                    violations.Add(
                        $"  {Path.GetFileName(file)}:{lineSpan.StartLinePosition.Line + 1} — " +
                        "new Random() with no seed");
                }
            }

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression.ToString().EndsWith("GetHashCode") &&
                    invocation.ArgumentList.Arguments.Count == 0)
                {
                    var lineSpan = invocation.GetLocation().GetLineSpan();
                    violations.Add(
                        $"  {Path.GetFileName(file)}:{lineSpan.StartLinePosition.Line + 1} — " +
                        "GetHashCode() (process-randomized in .NET; use the CsCheck seed)");
                }
            }
        }

        Assert.True(violations.Count == 0,
            $"Unseeded RNG found in Properties/:\n{string.Join("\n", violations)}\n\n" +
            "Derive the seed from the CsCheck generator — see ModuleWithSeed in " +
            "ParserFromStringPropertyTests.");
    }

    private static string FindSourceDir(string project)
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var path = Path.Combine(current, "src", project);
            if (Directory.Exists(path))
                return path;
            current = Directory.GetParent(current)?.FullName;
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", project));
    }
}
