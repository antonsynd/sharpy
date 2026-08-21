using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
using Xunit;
// The scan uses Roslyn's syntax API; the behavioral checks use Sharpy's own symbol model, whose
// SymbolKind/TypeKind names collide with Microsoft.CodeAnalysis'.
using SymbolKind = Sharpy.Compiler.Semantic.SymbolKind;
using TypeKind = Sharpy.Compiler.Semantic.TypeKind;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Conformance test for the module-exports seam: the value-position export lookup and the
/// types-only lookup it must agree with are owned by <see cref="ModuleExports"/> and by nothing
/// else (the parallel-site completeness contract of #1145; Class G of the defect taxonomy —
/// member history #1105/#1106/#1135 plus a fourth checker site found while collapsing the seam).
///
/// <para>
/// The two lookups exist because a value-position export can share a name with a type
/// (<c>sqlite3.Row</c> is both the <c>Row</c> type and the <c>row_factory</c> value), and
/// <c>ResolveQualifiedType</c> consults the types-only lookup FIRST (#1092). While they were two
/// independent dictionaries mirrored by convention, every new construction/merge/serialization
/// site was a chance to populate one and forget the other — and four separate audits found four
/// such sites. This test used to check that each site remembered the mirror. It now checks the
/// stronger property that made those sites unforgettable: <b>only <see cref="ModuleExports"/> can
/// write an export lookup at all</b>, so the mirror is a derivation rather than a discipline.
/// </para>
///
/// <para>
/// Two source scans over <c>Sharpy.Compiler</c> and <c>Sharpy.Lsp</c> (Roslyn syntax only, no
/// semantic model), plus behavioral checks that the invariant actually holds through the real
/// symbol types:
/// </para>
///
/// <list type="number">
///   <item><description>
///     <b>Ownership</b> — outside <c>ModuleExports.cs</c>, no member named <c>Exports</c>/
///     <c>ExportedSymbols</c>/<c>ExportedTypes</c> is a writable dictionary, and no single type
///     declares a <c>Dictionary&lt;string, Symbol&gt;</c> together with a
///     <c>Dictionary&lt;string, TypeSymbol&gt;</c> — the mirror pair, re-created.
///   </description></item>
///   <item><description>
///     <b>No out-of-seam mutation</b> — outside <c>ModuleExports.cs</c>, no element-write to an
///     export lookup, no mutating call on the read-only types view, and no cast that would reach
///     the underlying dictionary through it.
///   </description></item>
/// </list>
///
/// <para>
/// Both scans carry justification-only allowlists. Mirrors the source-scan style of
/// <c>SemanticInfoMergeConformanceTests</c>, <c>WrapperNodeUnwrapConformanceTests</c> and
/// <c>EmitterBannedTokenScanTests</c>.
/// </para>
/// </summary>
public class ModuleExportsMirrorConformanceTests
{
    private const string SeamFile = "ModuleExports.cs";

    /// <summary>Member names that hold a module's exports at any layer.</summary>
    private static readonly HashSet<string> ExportMembers = new(StringComparer.Ordinal)
    {
        "Exports", "ExportedSymbols", "ExportedTypes",
    };

    /// <summary>The read-only types view; mutating it means reaching around the seam.</summary>
    private static readonly HashSet<string> TypesViewMembers = new(StringComparer.Ordinal)
    {
        "ExportedTypes", "Types",
    };

    private static readonly HashSet<string> MutatingOps = new(StringComparer.Ordinal)
    {
        "Add", "TryAdd", "Remove", "Clear",
    };

    /// <summary>
    /// Types allowed to declare an export lookup as a raw dictionary, or to declare a
    /// value-map/type-map pair of their own. Key = "<c>{fileName} :: {memberName}</c>".
    /// </summary>
    private static readonly Dictionary<string, string> OwnershipAllowlist = new(StringComparer.Ordinal)
    {
        // (empty — ModuleExports owns both lookups)
    };

    /// <summary>
    /// Sites allowed to write an export lookup outside the seam.
    /// Key = "<c>{fileName} :: {memberName}</c>".
    /// </summary>
    private static readonly Dictionary<string, string> MutationAllowlist = new(StringComparer.Ordinal)
    {
        ["OverloadIndexBuilder.cs :: DiscoverPublicTypes"] =
            "Writes to OverloadIndex.ModuleOverloads.Types (List<DiscoveredTypeInfo>), not ModuleExports.Types",
        ["OverloadIndexBuilder.cs :: DiscoverNestedModuleTypes"] =
            "Writes to OverloadIndex.ModuleOverloads.Types (List<DiscoveredTypeInfo>), not ModuleExports.Types",
    };

    [Fact]
    public void ExportLookups_AreDeclaredOnlyByModuleExports()
    {
        var violations = new List<string>();

        foreach (var (fileName, root) in EnumerateSyntaxTrees())
        {
            if (fileName == SeamFile)
                continue;

            foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var members = DeclaredMembers(type).ToList();

                // (a) An export lookup declared as a writable dictionary.
                foreach (var (member, name, declaredType) in members)
                {
                    if (!ExportMembers.Contains(name) || !IsWritableDictionary(declaredType))
                        continue;

                    var key = $"{fileName} :: {name}";
                    if (OwnershipAllowlist.ContainsKey(key))
                        continue;

                    var line = member.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    violations.Add(
                        $"{key}  [{fileName}:{line}] — declares an export lookup as `{declaredType}`.");
                }

                // (b) A value-map/type-map pair of its own: the mirror shape, re-created — including
                // a types dictionary parked next to a ModuleExports, which is the same trap.
                var valueMap = members.FirstOrDefault(m =>
                    IsWritableDictionaryOf(m.Type, "Symbol") || m.Type == "ModuleExports");
                var typeMap = members.FirstOrDefault(m => IsWritableDictionaryOf(m.Type, "TypeSymbol"));
                if (valueMap.Member is null || typeMap.Member is null)
                    continue;

                var pairKey = $"{fileName} :: {type.Identifier.Text}";
                if (OwnershipAllowlist.ContainsKey(pairKey))
                    continue;

                var pairLine = typeMap.Member.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                violations.Add(
                    $"{pairKey}  [{fileName}:{pairLine}] — declares both `{valueMap.Name}` and "
                    + $"`{typeMap.Name}`, a value-map/type-map pair maintained by hand.");
            }
        }

        violations.Should().BeEmpty(
            "a module's exports live in ModuleExports, which keeps the value view and the types view "
            + "in step by construction. A second raw dictionary re-creates the mirror-by-convention that "
            + "#1105/#1106/#1135 kept breaking (Class G / #1145). Hold a ModuleExports instead, or add "
            + "the printed key to OwnershipAllowlist with a written reason.\nViolations:\n"
            + string.Join("\n", violations));
    }

    [Fact]
    public void ExportLookups_AreMutatedOnlyThroughModuleExports()
    {
        var violations = new List<string>();

        foreach (var (fileName, root) in EnumerateSyntaxTrees())
        {
            if (fileName == SeamFile)
                continue;

            foreach (var node in root.DescendantNodes())
            {
                var reason = OutOfSeamWrite(node);
                if (reason is null)
                    continue;

                var member = node.FirstAncestorOrSelf<MemberDeclarationSyntax>(m =>
                    m is MethodDeclarationSyntax or ConstructorDeclarationSyntax or PropertyDeclarationSyntax);
                var key = $"{fileName} :: {MemberName(member)}";
                if (MutationAllowlist.ContainsKey(key))
                    continue;

                var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                violations.Add($"{key}  [{fileName}:{line}] — {reason}");
            }
        }

        violations.Should().BeEmpty(
            "every write to a module's exports must go through ModuleExports (Add / MergeFrom, or "
            + "RestoreFrom for the symbol cache), which files exported types in the types-only view "
            + "automatically. Writing a lookup directly — or casting the read-only types view back to a "
            + "mutable dictionary — is how the two views used to drift apart (Class G / #1145). Use the "
            + "seam's API, or add the printed key to MutationAllowlist with a written reason.\nViolations:\n"
            + string.Join("\n", violations));
    }

    // --- Behavioral checks: the invariant the scans protect ----------------------------------

    [Fact]
    public void ExportingAType_MakesItResolvableInAnnotationPosition()
    {
        var module = new ModuleSymbol { Name = "sqlite3", Kind = SymbolKind.Module };
        var rowType = new TypeSymbol { Name = "Row", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };

        module.Exports.Add("Row", rowType);
        // A same-named value export must not take the type's place in annotation position (#1092).
        module.Exports.Add("Row", new VariableSymbol { Name = "Row", Kind = SymbolKind.Variable });

        module.Exports["Row"].Should().BeOfType<VariableSymbol>();
        module.ExportedTypes.Should().ContainKey("Row");
        module.TryGetExportedType("Row", out var resolved).Should().BeTrue();
        resolved.Should().BeSameAs(rowType);
    }

    [Fact]
    public void StagedModuleInfoExports_ReachTheModuleSymbolWithBothViews()
    {
        // The pre-symbol staging layer holds the same unit, so the hand-off is one copy of both
        // views — there is no second dictionary for a construction site to forget (#1106).
        var moduleInfo = new ModuleInfo { Path = "sqlite3.spy" };
        var rowType = new TypeSymbol { Name = "Row", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        moduleInfo.ExportedSymbols.Add("Row", rowType);
        moduleInfo.ExportedSymbols.Add("Row", new VariableSymbol { Name = "Row", Kind = SymbolKind.Variable });

        var module = new ModuleSymbol
        {
            Name = "sqlite3",
            Kind = SymbolKind.Module,
            Exports = new ModuleExports(moduleInfo.ExportedSymbols)
        };

        module.Exports["Row"].Should().BeOfType<VariableSymbol>();
        module.TryGetExportedType("Row", out var resolved).Should().BeTrue();
        resolved.Should().BeSameAs(rowType);
    }

    [Fact]
    public void MergingModules_CarriesBothViews()
    {
        var target = new ModuleSymbol { Name = "sqlite3", Kind = SymbolKind.Module };
        target.Exports.Add("Row", new VariableSymbol { Name = "Row", Kind = SymbolKind.Variable });

        var source = new ModuleSymbol { Name = "sqlite3", Kind = SymbolKind.Module };
        var rowType = new TypeSymbol { Name = "Row", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };
        source.Exports.Add("Row", rowType);

        target.Exports.MergeFrom(source.Exports, firstImportWins: true);

        target.Exports["Row"].Should().BeOfType<VariableSymbol>("first import wins in the value view");
        target.TryGetExportedType("Row", out var resolved).Should().BeTrue();
        resolved.Should().BeSameAs(rowType);
    }

    // --- Scan helpers ------------------------------------------------------------------------

    /// <summary>
    /// Property and field declarations of one type, as (declaration, member name, declared type
    /// text). Members of nested types are attributed to the nested type, not the outer one.
    /// </summary>
    private static IEnumerable<(MemberDeclarationSyntax Member, string Name, string Type)> DeclaredMembers(
        TypeDeclarationSyntax type)
    {
        foreach (var member in type.Members)
        {
            switch (member)
            {
                case PropertyDeclarationSyntax property:
                    yield return (property, property.Identifier.Text, Normalize(property.Type.ToString()));
                    break;

                case FieldDeclarationSyntax field:
                    var fieldType = Normalize(field.Declaration.Type.ToString());
                    foreach (var variable in field.Declaration.Variables)
                        yield return (field, variable.Identifier.Text, fieldType);
                    break;
            }
        }
    }

    private static bool IsWritableDictionary(string type)
        => type.StartsWith("Dictionary<", StringComparison.Ordinal)
           || type.StartsWith("IDictionary<", StringComparison.Ordinal)
           || type.StartsWith("ConcurrentDictionary<", StringComparison.Ordinal);

    /// <summary>True for a writable <c>…Dictionary&lt;string, {valueType}&gt;</c> (exactly that value type).</summary>
    private static bool IsWritableDictionaryOf(string type, string valueType)
        => IsWritableDictionary(type) && type.EndsWith($"<string,{valueType}>", StringComparison.Ordinal);

    /// <summary>
    /// Describes how <paramref name="node"/> writes an export lookup from outside the seam, or null
    /// if it does not.
    /// </summary>
    private static string? OutOfSeamWrite(SyntaxNode node)
    {
        switch (node)
        {
            // x.Exports[key] = value
            case AssignmentExpressionSyntax { Left: ElementAccessExpressionSyntax element }
                when TargetsMember(element.Expression, ExportMembers) is { } written:
                return $"writes `{written}` through its indexer; use ModuleExports.Add.";

            // x.ExportedTypes.Add(...) / ((IDictionary<…>)x.Exports.Types).Remove(...)
            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax call }
                when MutatingOps.Contains(call.Name.Identifier.Text)
                     && TargetsMember(StripCast(call.Expression), TypesViewMembers) is { } mutated:
                return $"calls {call.Name.Identifier.Text} on the read-only `{mutated}` view.";

            // (Dictionary<string, TypeSymbol>)x.ExportedTypes — reaching around the read-only view
            case CastExpressionSyntax cast
                when IsDictionaryType(cast.Type) && TargetsMember(cast.Expression, ExportMembers) is { } cast2:
                return $"casts `{cast2}` to a mutable dictionary.";

            default:
                return null;
        }
    }

    /// <summary>The member name if the expression is (or ends in) one of <paramref name="members"/>.</summary>
    private static string? TargetsMember(ExpressionSyntax expression, HashSet<string> members)
        => expression switch
        {
            MemberAccessExpressionSyntax ma when members.Contains(ma.Name.Identifier.Text)
                => ma.Name.Identifier.Text,
            IdentifierNameSyntax id when members.Contains(id.Identifier.Text)
                => id.Identifier.Text,
            _ => null,
        };

    /// <summary>Unwraps a parenthesized cast so `((IDictionary&lt;…&gt;)x.Types).Add(…)` is seen.</summary>
    private static ExpressionSyntax StripCast(ExpressionSyntax expression)
        => expression switch
        {
            ParenthesizedExpressionSyntax p => StripCast(p.Expression),
            CastExpressionSyntax c => StripCast(c.Expression),
            _ => expression,
        };

    private static bool IsDictionaryType(TypeSyntax type)
    {
        var text = Normalize(type.ToString());
        return text.StartsWith("Dictionary<", StringComparison.Ordinal)
               || text.StartsWith("IDictionary<", StringComparison.Ordinal);
    }

    private static string MemberName(MemberDeclarationSyntax? member)
        => member switch
        {
            MethodDeclarationSyntax m => m.Identifier.Text,
            ConstructorDeclarationSyntax c => c.Identifier.Text,
            PropertyDeclarationSyntax p => p.Identifier.Text,
            null => "<file scope>",
            _ => member.GetType().Name,
        };

    private static string Normalize(string s) => s.Replace(" ", "", StringComparison.Ordinal);

    // --- File discovery ---------------------------------------------------------------------

    private static IEnumerable<(string FileName, CompilationUnitSyntax Root)> EnumerateSyntaxTrees()
    {
        foreach (var dir in new[] { FindSourceDir("Sharpy.Compiler"), FindSourceDir("Sharpy.Lsp") })
        {
            foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                    continue;

                var text = File.ReadAllText(file);
                // Cheap pre-filter: only files naming an export lookup can contain a relevant site.
                if (!ExportMembers.Any(m => text.Contains(m, StringComparison.Ordinal))
                    && !text.Contains("TypeSymbol>", StringComparison.Ordinal))
                    continue;

                var root = (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(text).GetRoot();
                yield return (Path.GetFileName(file), root);
            }
        }
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
