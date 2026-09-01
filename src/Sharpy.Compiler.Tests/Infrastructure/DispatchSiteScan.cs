using System.Collections.Immutable;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Sharpy.Compiler.Tests.Infrastructure;

/// <summary>
/// Compilation-level typed-scrutinee census: builds a Roslyn <see cref="CSharpCompilation"/>
/// from a project's source files, resolves every switch scrutinee's type, and reports
/// those whose type derives from <c>Sharpy.Compiler.Parser.Ast.Node</c> or
/// <c>Sharpy.Compiler.Lowering.IrNode</c>. Switches with unresolvable scrutinee types
/// are reported separately — <c>unresolved &gt; 0</c> is a harness failure because an
/// unresolved scrutinee is exactly where a dispatch can hide (#1715).
///
/// References are gathered from the trusted-platform-assembly list (hermetic, never bin/
/// probing). Aliases (SharpyRT, SharpyStdlib) are read from the project's csproj and
/// applied to the matching metadata references. Global usings are synthesized from the
/// SDK implicit set plus any <c>&lt;Using Include="..."&gt;</c> items in the csproj.
/// </summary>
public static class DispatchSiteScan
{
    public record DispatchSite(
        string Key,
        string Root,
        string Form,
        string ScrutineeText,
        string ScrutineeTypeName,
        bool HasDefaultArm,
        int Line);

    public record UnresolvedSite(
        string File,
        string ScrutineeText,
        int Line,
        string EnclosingContext);

    public record ScanResult(
        IReadOnlyList<DispatchSite> Sites,
        IReadOnlyList<UnresolvedSite> Unresolved,
        int TotalSwitchCount,
        Dictionary<string, int> SiteCountByKey);

    private static readonly string[] SdkImplicitUsings =
    {
        "System",
        "System.Collections.Generic",
        "System.IO",
        "System.Linq",
        "System.Net.Http",
        "System.Threading",
        "System.Threading.Tasks"
    };

    /// <summary>
    /// Scans a project for AST/IrNode dispatch switches by building a Roslyn compilation
    /// from source. The <paramref name="keyPrefix"/> is prepended to every site key
    /// (e.g. "Sharpy.Lsp" → keys become "Sharpy.Lsp/path::Type.Method").
    /// </summary>
    public static ScanResult Scan(
        string projectDir,
        string projectCsproj,
        string? keyPrefix = null,
        Dictionary<string, string>? aliasOverrides = null,
        bool suppressGlobalUsings = false)
    {
        var repoRoot = FindRepoRoot();
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
            var globalUsingsSource = BuildGlobalUsingsSource(fullCsprojPath);
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(
                globalUsingsSource,
                CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                path: "GlobalUsings.synthesized.cs"));
        }

        var aliasMap = aliasOverrides ?? ReadAliasMap(fullCsprojPath);
        var projectAssemblyName = GetAssemblyName(fullCsprojPath);
        var references = BuildReferences(aliasMap, projectAssemblyName);

        var compilation = CSharpCompilation.Create(
            $"{projectAssemblyName}.Scan",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        var nodeType = FindType(compilation, "Sharpy.Compiler.Parser.Ast.Node");
        var irNodeType = FindType(compilation, "Sharpy.Compiler.Lowering.IrNode");

        var sites = new List<DispatchSite>();
        var unresolved = new List<UnresolvedSite>();
        int totalSwitches = 0;
        var siteCountByKey = new Dictionary<string, int>();

        foreach (var tree in syntaxTrees)
        {
            if (tree.FilePath is "GlobalUsings.synthesized.cs")
                continue;

            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetCompilationUnitRoot();
            var relativePath = Path.GetRelativePath(fullProjectDir, tree.FilePath)
                .Replace('\\', '/');

            var keyPath = keyPrefix != null ? $"{keyPrefix}/{relativePath}" : relativePath;

            foreach (var switchStmt in root.DescendantNodes().OfType<SwitchStatementSyntax>())
            {
                totalSwitches++;
                ClassifySwitch(
                    switchStmt.Expression, switchStmt, "SwitchStatement",
                    model, keyPath, nodeType, irNodeType,
                    HasDefaultLabel(switchStmt),
                    relativePath, sites, unresolved, siteCountByKey);
            }

            foreach (var switchExpr in root.DescendantNodes().OfType<SwitchExpressionSyntax>())
            {
                totalSwitches++;
                ClassifySwitch(
                    switchExpr.GoverningExpression, switchExpr, "SwitchExpression",
                    model, keyPath, nodeType, irNodeType,
                    HasDiscardArm(switchExpr),
                    relativePath, sites, unresolved, siteCountByKey);
            }
        }

        return new ScanResult(sites, unresolved, totalSwitches, siteCountByKey);
    }

    private static void ClassifySwitch(
        ExpressionSyntax scrutinee,
        SyntaxNode switchNode,
        string form,
        SemanticModel model,
        string keyPath,
        INamedTypeSymbol? nodeType,
        INamedTypeSymbol? irNodeType,
        bool hasDefaultArm,
        string relativePath,
        List<DispatchSite> sites,
        List<UnresolvedSite> unresolved,
        Dictionary<string, int> siteCountByKey)
    {
        var typeInfo = model.GetTypeInfo(scrutinee);
        var type = typeInfo.Type ?? typeInfo.ConvertedType;
        var line = scrutinee.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        var scrutineeText = scrutinee.ToFullString().Trim();
        var enclosing = EnclosingContext(switchNode);

        if (type == null || type.TypeKind == TypeKind.Error)
        {
            unresolved.Add(new UnresolvedSite(relativePath, scrutineeText, line, enclosing));
            return;
        }

        var typeName = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        string? root = null;

        if (nodeType != null && DerivesFrom(type, nodeType))
            root = "Node";
        else if (irNodeType != null && DerivesFrom(type, irNodeType))
            root = "IrNode";

        if (root == null)
            return;

        var key = $"{keyPath}::{enclosing}";

        sites.Add(new DispatchSite(key, root, form, scrutineeText, typeName, hasDefaultArm, line));
        siteCountByKey[key] = siteCountByKey.GetValueOrDefault(key) + 1;
    }

    private static bool DerivesFrom(ITypeSymbol type, INamedTypeSymbol baseType)
    {
        var current = type;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
            current = current.BaseType;
        }
        return false;
    }

    private static bool HasDefaultLabel(SwitchStatementSyntax switchStmt)
    {
        return switchStmt.Sections
            .SelectMany(s => s.Labels)
            .Any(l => l is DefaultSwitchLabelSyntax);
    }

    private static bool HasDiscardArm(SwitchExpressionSyntax switchExpr)
    {
        return switchExpr.Arms.Any(a => a.Pattern is DiscardPatternSyntax);
    }

    /// <summary>
    /// Enclosing context: "Type.Method" using the first <see cref="MethodDeclarationSyntax"/>
    /// ancestor (skips local functions, so a switch inside a local function keeps its
    /// enclosing method's key — preserving the 75 existing keys) and the first
    /// <see cref="TypeDeclarationSyntax"/> ancestor, with arity-suffixed type names.
    /// </summary>
    internal static string EnclosingContext(SyntaxNode node)
    {
        var method = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        var type = node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();

        var typeName = type == null
            ? "<top-level>"
            : type.TypeParameterList is { Parameters.Count: > 0 }
                ? $"{type.Identifier.Text}`{type.TypeParameterList.Parameters.Count}"
                : type.Identifier.Text;

        return $"{typeName}.{method?.Identifier.Text ?? "<no-method>"}";
    }

    private static string BuildGlobalUsingsSource(string csprojPath)
    {
        var lines = SdkImplicitUsings
            .Select(u => $"global using global::{u};")
            .ToList();

        var csprojUsings = ReadCsprojUsings(csprojPath);
        foreach (var u in csprojUsings)
            lines.Add($"global using global::{u};");

        return string.Join("\n", lines);
    }

    private static List<string> ReadCsprojUsings(string csprojPath)
    {
        var usings = new List<string>();
        var doc = XDocument.Load(csprojPath);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

        foreach (var usingElement in doc.Descendants(ns + "Using"))
        {
            var include = usingElement.Attribute("Include")?.Value;
            if (!string.IsNullOrEmpty(include))
                usings.Add(include);
        }

        return usings;
    }

    /// <summary>
    /// Reads <c>&lt;ProjectReference&gt;&lt;Aliases&gt;</c> from the csproj and maps
    /// assembly name → alias. Assembly names are derived from the referenced project's
    /// filename (e.g. <c>../Sharpy.Core/Sharpy.Core.csproj</c> → <c>Sharpy.Core</c>).
    /// </summary>
    internal static Dictionary<string, string> ReadAliasMap(string csprojPath)
    {
        var map = new Dictionary<string, string>();
        var doc = XDocument.Load(csprojPath);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

        foreach (var projRef in doc.Descendants(ns + "ProjectReference"))
        {
            var aliasesElement = projRef.Element(ns + "Aliases");
            if (aliasesElement == null)
                continue;

            var alias = aliasesElement.Value.Trim();
            if (string.IsNullOrEmpty(alias))
                continue;

            var include = projRef.Attribute("Include")?.Value;
            if (string.IsNullOrEmpty(include))
                continue;

            var refProjectName = Path.GetFileNameWithoutExtension(include);
            map[refProjectName] = alias;
        }

        return map;
    }

    private static string GetAssemblyName(string csprojPath)
    {
        var doc = XDocument.Load(csprojPath);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

        var assemblyNameElement = doc.Descendants(ns + "AssemblyName").FirstOrDefault();
        if (assemblyNameElement != null && !string.IsNullOrWhiteSpace(assemblyNameElement.Value))
            return assemblyNameElement.Value.Trim();

        return Path.GetFileNameWithoutExtension(csprojPath);
    }

    /// <summary>
    /// Builds metadata references from the trusted-platform-assembly list, applying
    /// aliases from the project's csproj. Excludes the project's own assembly (we are
    /// compiling it from source). The TPA list is the definitive set of assemblies
    /// the runtime resolved from the deps.json — hermetic, no bin/ probing.
    /// </summary>
    internal static IReadOnlyList<MetadataReference> BuildReferences(
        Dictionary<string, string> aliasMap,
        string projectAssemblyName)
    {
        var tpaString = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrEmpty(tpaString))
            throw new InvalidOperationException("TRUSTED_PLATFORM_ASSEMBLIES not available.");

        var references = new List<MetadataReference>();
        var separator = Path.PathSeparator;

        foreach (var path in tpaString.Split(separator))
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                continue;

            var assemblyFileName = Path.GetFileNameWithoutExtension(path);

            if (string.Equals(assemblyFileName, projectAssemblyName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (aliasMap.TryGetValue(assemblyFileName, out var alias))
            {
                references.Add(MetadataReference.CreateFromFile(path,
                    new MetadataReferenceProperties(aliases: ImmutableArray.Create(alias, "global"))));
            }
            else
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        return references;
    }

    private static INamedTypeSymbol? FindType(CSharpCompilation compilation, string fullyQualifiedName)
    {
        return compilation.GetTypeByMetadataName(fullyQualifiedName);
    }

    internal static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current, ".git"))
                || File.Exists(Path.Combine(current, ".git")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new InvalidOperationException("Repository root not found.");
    }
}
