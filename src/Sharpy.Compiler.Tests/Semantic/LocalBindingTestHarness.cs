using System.Collections.Generic;
using System.Linq;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Runs the semantic pipeline over a source string and exposes the #1560 facts: the per-owner
/// <see cref="LocalBindingLedger"/>s, the recorded <see cref="TargetBinding"/>s, and (with
/// <c>computeCodeGenInfo</c>) the <see cref="CodeGenInfo"/> spellings the allocator assigned.
/// </summary>
internal static class LocalBindingTestHarness
{
    internal sealed record Analysis(Module Module, SymbolTable SymbolTable, SemanticInfo Info, SemanticBinding Binding);

    public static Analysis Analyze(string source, bool computeCodeGenInfo = false)
    {
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var parser = new global::Sharpy.Compiler.Parser.Parser(lexer.TokenizeAll(), NullLogger.Instance);
        var module = parser.ParseModule();

        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var info = new SemanticInfo();
        var binding = new SemanticBinding();

        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance, binding);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();
        binding.MaterializeInheritance();

        var typeResolver = new TypeResolver(symbolTable, info, NullLogger.Instance);
        var checker = new TypeChecker(symbolTable, info, typeResolver, NullLogger.Instance)
        {
            SemanticBinding = binding
        };
        checker.CheckModule(module, computeCodeGenInfo: computeCodeGenInfo, isEntryPoint: false);

        return new Analysis(module, symbolTable, info, binding);
    }

    /// <summary>The single ledger whose owner scope is named <paramref name="ownerScopeName"/>.</summary>
    public static LocalBindingLedger Ledger(this Analysis a, string ownerScopeName)
        => a.SymbolTable.AllLedgers.Values.Single(l => l.OwnerScopeName == ownerScopeName);

    /// <summary>Every ledger whose owner scope is named <paramref name="ownerScopeName"/>, in creation order.</summary>
    public static IReadOnlyList<LocalBindingLedger> Ledgers(this Analysis a, string ownerScopeName)
        => a.SymbolTable.AllLedgers.Values.Where(l => l.OwnerScopeName == ownerScopeName).OrderBy(l => l.OwnerScopeId).ToList();

    /// <summary>
    /// The ledger's variable rows as <c>name@path#ordinal</c>: <c>path</c> is the scope-name chain
    /// from the owner (exclusive) down to the scope the symbol was bound in, <c>/</c>-joined, empty
    /// when bound directly in the owner; <c>ordinal</c> is the row's position in the ledger.
    /// </summary>
    public static IReadOnlyList<string> Rows(this Analysis a, LocalBindingLedger ledger)
    {
        var rows = new List<string>();
        foreach (var entry in ledger.Entries)
        {
            if (entry.Symbol is not VariableSymbol variable)
                continue;
            rows.Add($"{variable.Name}@{ScopePath(a.SymbolTable, ledger, entry.ScopeId)}#{entry.Ordinal}");
        }

        return rows;
    }

    public static string ScopePath(SymbolTable table, LocalBindingLedger ledger, int scopeId)
    {
        var names = new List<string>();
        var scope = table.GetScope(scopeId);
        while (scope != null && scope.Id != ledger.OwnerScopeId)
        {
            names.Add(scope.Name);
            scope = scope.Parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }

    /// <summary>
    /// The C# spelling of every binding of <paramref name="name"/> in the method owned by
    /// <paramref name="ownerScopeName"/>, nested ledgers included, in source order.
    /// </summary>
    public static IReadOnlyList<string> Spellings(this Analysis a, string ownerScopeName, string name)
    {
        var owner = a.Ledger(ownerScopeName);
        return a.SymbolTable.AllLedgers.Values
            .Where(l => ReferenceEquals(l, owner) || IsInside(a.SymbolTable, l, owner))
            .SelectMany(l => l.Entries)
            .OrderBy(e => e.Sequence)
            .Where(e => e.Symbol is VariableSymbol v && v.Name == name)
            .Select(e => a.Binding.GetCodeGenInfo(e.Symbol)?.GetVersionedCSharpName() ?? "<none>")
            .ToList();
    }

    private static bool IsInside(SymbolTable table, LocalBindingLedger ledger, LocalBindingLedger owner)
    {
        while (ledger.IsNested)
        {
            if (ledger.ParentOwnerScopeId == owner.OwnerScopeId)
                return true;
            ledger = table.GetLedger(ledger.ParentOwnerScopeId)!;
        }

        return false;
    }

    public static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildNodes())
        {
            yield return child;
            foreach (var d in Descendants(child))
                yield return d;
        }
    }

    /// <summary>The recorded binding kind of a node, or null when none was recorded.</summary>
    public static TargetBindingKind? BindingOf(this Analysis a, Node node)
        => a.Info.GetTargetBinding(node)?.Kind;

    /// <summary>The <paramref name="occurrence"/>-th (0-based) assignment whose target is the identifier <paramref name="name"/>.</summary>
    public static Identifier AssignmentTarget(this Analysis a, string name, int occurrence)
        => Descendants(a.Module).OfType<Assignment>()
            .Select(x => x.Target as Identifier)
            .Where(id => id != null && id.Name == name)
            .ElementAt(occurrence)!;
}
