using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Sharpy.Compiler.Tests.Infrastructure;

/// <summary>
/// Roslyn semantic scan that finds every <c>ObjectCreationExpressionSyntax</c> and
/// <c>ImplicitObjectCreationExpressionSyntax</c> whose bound type lives in namespace
/// <c>Sharpy.Compiler.Parser.Ast</c>. The companion census test uses these sites to
/// verify that every concrete AST kind is constructed somewhere in the parser (or is
/// in a checked roster of kinds synthesized outside the parser).
/// </summary>
public static class AstConstructionScan
{
    public record ConstructionSite(string TypeName, string File, int Line);

    public record ScanResult(
        IReadOnlyDictionary<string, IReadOnlyList<ConstructionSite>> SitesByType,
        int UnresolvedCount,
        int TotalCreationCount);

    private const string AstNamespace = "Sharpy.Compiler.Parser.Ast";

    public static ScanResult Scan(
        string projectDir,
        string projectCsproj,
        Dictionary<string, string>? aliasOverrides = null,
        bool suppressGlobalUsings = false)
    {
        var repoRoot = DispatchSiteScan.FindRepoRoot();
        var fullProjectDir = Path.GetFullPath(Path.Combine(repoRoot, projectDir));
        var fullCsprojPath = Path.GetFullPath(Path.Combine(repoRoot, projectCsproj));

        var sourceFiles = Directory.GetFiles(fullProjectDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .ToList();

        var syntaxTrees = sourceFiles
            .Select(f => CSharpSyntaxTree.ParseText(
                File.ReadAllText(f),
                CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                path: f))
            .ToList();

        if (!suppressGlobalUsings)
        {
            var globalUsingsSource = DispatchSiteScan.BuildGlobalUsingsSource(fullCsprojPath);
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(
                globalUsingsSource,
                CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                path: "GlobalUsings.synthesized.cs"));
        }

        var aliasMap = aliasOverrides ?? DispatchSiteScan.ReadAliasMap(fullCsprojPath);
        var projectAssemblyName = DispatchSiteScan.GetAssemblyName(fullCsprojPath);
        var references = DispatchSiteScan.BuildReferences(aliasMap, projectAssemblyName);

        var compilation = CSharpCompilation.Create(
            $"{projectAssemblyName}.AstConstructionScan",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        var sitesByType = new Dictionary<string, List<ConstructionSite>>();
        int unresolvedCount = 0;
        int totalCreationCount = 0;

        foreach (var tree in syntaxTrees)
        {
            if (tree.FilePath is "GlobalUsings.synthesized.cs")
                continue;

            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetCompilationUnitRoot();
            var relativePath = Path.GetRelativePath(fullProjectDir, tree.FilePath)
                .Replace('\\', '/');

            var creations = root.DescendantNodes()
                .Where(n => n is ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax);

            foreach (var creation in creations)
            {
                totalCreationCount++;

                var typeInfo = model.GetTypeInfo(creation);
                var type = typeInfo.Type;

                if (type == null || type.TypeKind == TypeKind.Error)
                {
                    unresolvedCount++;
                    continue;
                }

                var ns = type.ContainingNamespace?.ToDisplayString();
                if (ns != AstNamespace)
                    continue;

                var typeName = type.Name;
                var line = creation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                if (!sitesByType.TryGetValue(typeName, out var list))
                {
                    list = new List<ConstructionSite>();
                    sitesByType[typeName] = list;
                }
                list.Add(new ConstructionSite(typeName, relativePath, line));
            }
        }

        var resultDict = sitesByType.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<ConstructionSite>)kvp.Value.AsReadOnly());

        return new ScanResult(resultDict, unresolvedCount, totalCreationCount);
    }
}
