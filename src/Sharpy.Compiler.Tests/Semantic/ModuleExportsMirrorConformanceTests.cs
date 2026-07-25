using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Conformance test enforcing that <c>ModuleSymbol.ExportedTypes</c> is mirrored everywhere
/// <c>ModuleSymbol.Exports</c> is constructed, populated, merged, or serialized (the parallel-site
/// completeness contract of #1145; Class G of the defect taxonomy — member history #1105/#1106/#1135).
///
/// <para>
/// <c>ExportedTypes</c> is a second dictionary added for #1092 so a value-position export cannot
/// shadow a same-named type in annotation position (e.g. <c>sqlite3.Row</c> — both a type and the
/// <c>row_factory</c> value). <c>ResolveQualifiedType</c> consults <c>ExportedTypes</c> BEFORE
/// <c>Exports</c>, so any site that builds/merges/serializes <c>Exports</c> but forgets the mirror
/// silently drops the type lookup. That gap was re-filed three times — the serializer round-trip
/// (#1105), three lazy <c>new ModuleSymbol</c> sites (#1106), and <c>MergeModuleExports</c> (#1135) —
/// each found by a different reviewer noticing a different parallel site, because nothing enforced
/// the mirror. This test makes the omission mechanically impossible to reintroduce.
/// </para>
///
/// <para>
/// The scan parses each <c>Sharpy.Compiler</c> source file with Roslyn (syntax only — no semantic
/// model) and applies two checks:
/// </para>
///
/// <list type="number">
///   <item><description>
///     <b>Construction mirror</b> — every <c>new ModuleSymbol { ... }</c> object initializer that
///     assigns <c>Exports</c> must also assign <c>ExportedTypes</c> in the same initializer.
///   </description></item>
///   <item><description>
///     <b>Mutation / serialization mirror</b> — every method that mutates or serializes
///     <c>.Exports</c> (indexer-set, <c>.Add</c>/<c>.TryAdd</c>, <c>.ToDictionary</c>, or a
///     <c>.Exports = ...</c> member assignment) must reference <c>ExportedTypes</c> somewhere in the
///     same method body.
///   </description></item>
/// </list>
///
/// <para>
/// Genuinely-asymmetric sites (structural parent modules that only hold nested child modules in
/// <c>Exports</c>; error-recovery placeholder population on a module that already failed to load)
/// are listed in the two allowlists with a written justification. A new site that writes
/// <c>Exports</c> without the mirror fails here and must either add <c>ExportedTypes</c> or
/// self-justify. Mirrors the source-scan style of <c>SemanticInfoMergeConformanceTests</c> and
/// <c>EmitterPurityConformanceTests</c>.
/// </para>
/// </summary>
public class ModuleExportsMirrorConformanceTests
{
    private const string ExportsMember = "Exports";
    private const string MirrorMember = "ExportedTypes";
    private const string ModuleSymbolType = "ModuleSymbol";

    /// <summary>
    /// Member-access operations on <c>.Exports</c> that constitute a write or a serialization of the
    /// value-export map (as opposed to a read like <c>TryGetValue</c>/<c>ContainsKey</c>/<c>Keys</c>).
    /// A method performing any of these must also touch <c>ExportedTypes</c>.
    /// </summary>
    private static readonly HashSet<string> MutatingOrSerializingOps = new(StringComparer.Ordinal)
    {
        "Add", "TryAdd", "Remove", "Clear", "ToDictionary",
    };

    /// <summary>
    /// Construction sites (<c>new ModuleSymbol</c> initializers) that assign <c>Exports</c> but
    /// intentionally not <c>ExportedTypes</c>. Key = "<c>{fileName} :: new ModuleSymbol({Name = ...})</c>".
    /// </summary>
    private static readonly Dictionary<string, string> ConstructionAllowlist = new(StringComparer.Ordinal)
    {
        // Structural parent modules built for a dotted plain import ("import a.b.c"): the parent's
        // Exports holds ONLY the single nested child ModuleSymbol; it exports no types of its own, so
        // there is no type to mirror. The leaf module (built in the same loop, and NOT allowlisted)
        // carries the real Exports+ExportedTypes pair.
        ["ProjectCompiler.Phases.cs :: new ModuleSymbol(Name = parts[j])"] =
            "Structural parent module for a dotted import — Exports holds only the nested child module; no own types.",
        ["ImportResolver.ModuleLoading.cs :: new ModuleSymbol(Name = parts[j])"] =
            "Structural parent module for a dotted import — Exports holds only the nested child module; no own types.",

        // Error-recovery placeholder module for a module that failed to resolve. Its Exports holds
        // synthesized placeholder symbols only (to suppress cascading errors); a diagnostic has
        // already been emitted and ResolveQualifiedType falls back to Exports, so there is no real
        // type to place in the mirror.
        ["ImportResolver.Helpers.cs :: new ModuleSymbol(Name = moduleName)"] =
            "Error-recovery placeholder module (empty/placeholder Exports); the module already failed to load.",
    };

    /// <summary>
    /// Methods that mutate/serialize <c>.Exports</c> but intentionally do not touch
    /// <c>ExportedTypes</c>. Key = "<c>{fileName} :: {methodName}</c>".
    /// </summary>
    private static readonly Dictionary<string, string> MutationAllowlist = new(StringComparer.Ordinal)
    {
        // Error-recovery branches populate errorRecoveryModule.Exports[name] with synthesized
        // placeholder symbols so downstream type checking does not cascade after a failed import
        // (namedtuple rejection, __future__ handling, missing symbols). The module already carries a
        // diagnostic; the placeholders are values, not real types, and ResolveQualifiedType falls
        // back to Exports — there is nothing to mirror into ExportedTypes.
        ["ImportResolver.ModuleLoading.cs :: ResolveFromImport"] =
            "Error-recovery placeholder population on a failed import; placeholders are values, not real types.",
    };

    [Fact]
    public void ModuleSymbolExports_ConstructionSites_MirrorExportedTypes()
    {
        var violations = new List<string>();

        foreach (var (fileName, root) in EnumerateCompilerSyntaxTrees())
        {
            foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                if (!IsModuleSymbolCreation(creation) || creation.Initializer is null)
                    continue;

                var assigned = AssignedMemberNames(creation.Initializer);
                if (!assigned.Contains(ExportsMember))
                    continue; // construction that does not set Exports here (populated later / not at all)
                if (assigned.Contains(MirrorMember))
                    continue; // mirrored — good

                var key = $"{fileName} :: new ModuleSymbol({NameAssignmentText(creation.Initializer)})";
                if (ConstructionAllowlist.ContainsKey(key))
                    continue;

                var line = creation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                violations.Add(
                    $"{key}  [{fileName}:{line}] — assigns Exports but not ExportedTypes in the initializer.");
            }
        }

        violations.Should().BeEmpty(
            "every `new ModuleSymbol` that assigns Exports must also assign the ExportedTypes mirror in "
            + "the same initializer, or the type-only lookup ResolveQualifiedType consults first is lost "
            + "for that module (Class G / #1092/#1106). Add `ExportedTypes = ...` to the initializer, or "
            + "add the printed key to ConstructionAllowlist with a written reason.\nViolations:\n"
            + string.Join("\n", violations));
    }

    [Fact]
    public void ModuleSymbolExports_MutationAndSerializationSites_MirrorExportedTypes()
    {
        var violations = new List<string>();

        foreach (var (fileName, root) in EnumerateCompilerSyntaxTrees())
        {
            foreach (var member in root.DescendantNodes().OfType<MemberDeclarationSyntax>())
            {
                var body = MethodLikeBody(member);
                if (body is null)
                    continue;

                if (!WritesOrSerializesExports(body, insideInitializer: false))
                    continue;
                if (ReferencesIdentifier(body, MirrorMember))
                    continue; // method touches ExportedTypes somewhere — good

                var key = $"{fileName} :: {MemberName(member)}";
                if (MutationAllowlist.ContainsKey(key))
                    continue;

                var line = member.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                violations.Add(
                    $"{key}  [{fileName}:{line}] — mutates/serializes .Exports but never references ExportedTypes.");
            }
        }

        violations.Should().BeEmpty(
            "every method that mutates or serializes ModuleSymbol.Exports (indexer-set, Add/TryAdd, "
            + "ToDictionary, or `.Exports = ...`) must also handle the ExportedTypes mirror, or a merge/"
            + "populate/serialize path silently drops the type-only lookup (Class G / #1105/#1135). Handle "
            + "ExportedTypes in the method, or add the printed key to MutationAllowlist with a written "
            + "reason.\nViolations:\n" + string.Join("\n", violations));
    }

    // --- Syntax helpers ---------------------------------------------------------------------

    private static bool IsModuleSymbolCreation(ObjectCreationExpressionSyntax creation)
        => creation.Type is IdentifierNameSyntax id && id.Identifier.Text == ModuleSymbolType;

    /// <summary>Names of the members assigned by an object initializer (<c>Name = ...</c> ⇒ "Name").</summary>
    private static HashSet<string> AssignedMemberNames(InitializerExpressionSyntax initializer)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var expr in initializer.Expressions)
        {
            if (expr is AssignmentExpressionSyntax { Left: IdentifierNameSyntax left })
                names.Add(left.Identifier.Text);
        }
        return names;
    }

    /// <summary>Text of the initializer's <c>Name = ...</c> assignment, used to key a construction site.</summary>
    private static string NameAssignmentText(InitializerExpressionSyntax initializer)
    {
        foreach (var expr in initializer.Expressions)
        {
            if (expr is AssignmentExpressionSyntax { Left: IdentifierNameSyntax { Identifier.Text: "Name" } } a)
                return NormalizeWhitespace(a.ToString());
        }
        // Fallback: the sorted set of assigned members (deterministic) when there is no Name assignment.
        return "members: " + string.Join(",", AssignedMemberNames(initializer).OrderBy(n => n, StringComparer.Ordinal));
    }

    /// <summary>
    /// True if the block/expression writes or serializes a <c>.Exports</c> member: an indexer-set, an
    /// <c>.Exports.Add/TryAdd/Remove/Clear/ToDictionary(...)</c> call, or a <c>.Exports = ...</c>
    /// assignment that is not itself part of a <c>new ModuleSymbol</c> initializer.
    /// </summary>
    private static bool WritesOrSerializesExports(SyntaxNode body, bool insideInitializer)
    {
        foreach (var node in body.DescendantNodes())
        {
            switch (node)
            {
                // ms.Exports[key] = value
                case AssignmentExpressionSyntax { Left: ElementAccessExpressionSyntax ea }
                    when ExpressionTargetsMember(ea.Expression, ExportsMember):
                    return true;

                // ms.Exports = ... (member assignment outside an object initializer)
                case AssignmentExpressionSyntax { Left: MemberAccessExpressionSyntax ma }
                    when ma.Name.Identifier.Text == ExportsMember
                         && ma.Parent is not InitializerExpressionSyntax:
                    return true;

                // ms.Exports.Add(...) / .ToDictionary(...) / etc.
                case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax call }
                    when MutatingOrSerializingOps.Contains(call.Name.Identifier.Text)
                         && ExpressionTargetsMember(call.Expression, ExportsMember):
                    return true;
            }
        }
        return false;
    }

    /// <summary>True if <paramref name="expression"/> is (or ends in) a member/identifier named <paramref name="member"/>.</summary>
    private static bool ExpressionTargetsMember(ExpressionSyntax expression, string member)
        => expression switch
        {
            MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text == member,
            IdentifierNameSyntax id => id.Identifier.Text == member,
            _ => false,
        };

    private static bool ReferencesIdentifier(SyntaxNode body, string identifier)
        => body.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Any(id => id.Identifier.Text == identifier);

    /// <summary>The body (block or expression) of a method/constructor/accessor/local function, else null.</summary>
    private static SyntaxNode? MethodLikeBody(MemberDeclarationSyntax member)
        => member switch
        {
            MethodDeclarationSyntax m => (SyntaxNode?)m.Body ?? m.ExpressionBody,
            ConstructorDeclarationSyntax c => (SyntaxNode?)c.Body ?? c.ExpressionBody,
            _ => null,
        };

    private static string MemberName(MemberDeclarationSyntax member)
        => member switch
        {
            MethodDeclarationSyntax m => m.Identifier.Text,
            ConstructorDeclarationSyntax c => c.Identifier.Text,
            _ => member.GetType().Name,
        };

    private static string NormalizeWhitespace(string s)
        => string.Join(" ", s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    // --- File discovery ---------------------------------------------------------------------

    private static IEnumerable<(string FileName, CompilationUnitSyntax Root)> EnumerateCompilerSyntaxTrees()
    {
        var dir = FindCompilerSourceDirectory();
        foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
        {
            // Skip generated/obj artifacts.
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                continue;

            var text = File.ReadAllText(file);
            // Cheap pre-filter: only files that mention the members can contain a relevant site.
            if (!text.Contains(ExportsMember, StringComparison.Ordinal))
                continue;

            var root = (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(text).GetRoot();
            yield return (Path.GetFileName(file), root);
        }
    }

    private static string FindCompilerSourceDirectory()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var path = Path.Combine(current, "src", "Sharpy.Compiler");
            if (Directory.Exists(path))
                return path;
            current = Directory.GetParent(current)?.FullName;
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Sharpy.Compiler"));
    }
}
