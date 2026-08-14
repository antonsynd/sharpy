using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Resolves a type DECLARATION in the AST to its <see cref="TypeSymbol"/>, through nesting (#1461).
///
/// <para><b>The defect this exists to kill.</b> <c>NameResolver</c> defines a nested type's symbol
/// inside the ENCLOSING class's scope, so <c>SymbolTable.LookupType(classDef.Name)</c> — a global
/// lookup by bare name — returns null for every nested type. Validators that resolve a declaration
/// that way and early-return on null are therefore DEAD for nested declarations: the check they
/// exist to perform silently does not run, and a program the compiler already knows how to refuse
/// one nesting level up reaches Roslyn instead. <c>AbstractMemberValidator</c> is the measured case
/// — a nested <c>@abstract</c> member in a non-abstract class produced CS0513 behind SPY0908 where
/// the top-level twin drew a clean SPY0493 — but the defect belongs to the LOOKUP, not to that
/// validator, and #1461 explicitly forbids patching one caller.</para>
///
/// <para><b>Why an index and not a stack.</b> #1371 fixed the same gap for code generation by
/// consulting the enclosing symbol's <see cref="TypeSymbol.NestedTypes"/>, which works because the
/// emitter threads a <c>_currentTypeSymbol</c> through its own recursion. Validators have no such
/// thread: some are <see cref="ValidatingAstWalker"/>s whose subclasses may or may not call
/// <c>base.VisitClassDef</c>, and some walk the AST themselves. A resolution that depended on
/// visitor discipline would be correct only for the validators that remembered it — which is the
/// same shape of bug, one level up. This index is a pure function of the module and the symbol
/// table, computed once, so a validator gets the right answer for a declaration node no matter how
/// it arrived there.</para>
///
/// <para><b>Reference identity, not names.</b> Keyed on the AST node, so two nested classes with the
/// same bare name under different parents resolve to their own symbols, and a nested name that
/// shadows a top-level one resolves to the nested symbol.</para>
///
/// <para>Nodes whose symbol cannot be found are absent from the index and resolve to null, exactly
/// as the bare lookup did — the index changes which declarations RESOLVE, never what a validator
/// does with a null.</para>
/// </summary>
internal sealed class NestedTypeIndex
{
    private readonly Dictionary<Statement, TypeSymbol> _byDeclaration =
        new(ReferenceEqualityComparer.Instance);

    private NestedTypeIndex() { }

    /// <summary>An index over every type declaration reachable from <paramref name="module"/>.</summary>
    public static NestedTypeIndex Build(Module module, IGlobalSymbolTable symbolTable)
    {
        var index = new NestedTypeIndex();
        index.Add(module.Body, parent: null, symbolTable);
        return index;
    }

    /// <summary>
    /// The symbol for a type declaration, or null when the declaration has none. Falls back to the
    /// global lookup for a node this index never saw — a declaration synthesized after the index was
    /// built, or one reached from outside the module walk — so a caller can always use this in place
    /// of <c>SymbolTable.LookupType(node.Name)</c> and never resolve LESS than before.
    /// </summary>
    public TypeSymbol? SymbolFor(Statement declaration, string name, IGlobalSymbolTable symbolTable)
        => _byDeclaration.TryGetValue(declaration, out var symbol)
            ? symbol
            : symbolTable.LookupType(name);

    private void Add(
        IEnumerable<Statement> body, TypeSymbol? parent, IGlobalSymbolTable symbolTable)
    {
        foreach (var statement in body)
        {
            var declaration = statement.UnwrapDecorated();
            var (name, nestedBody) = TypeDeclarationOf(declaration);
            if (name == null)
            {
                // A type can be declared inside a function body, so the walk does not stop at the
                // first non-type statement. Nesting for such a declaration is LEXICAL only — the
                // symbol still belongs to the enclosing type's scope, which is what `parent` holds.
                if (declaration is FunctionDef function)
                    Add(function.Body, parent, symbolTable);
                continue;
            }

            // Nested is consulted first: an inner name shadows an outer one, so when both exist the
            // inner symbol is the right answer. At module level `parent` is null and this is exactly
            // the global lookup it replaces.
            var symbol = parent?.NestedTypes.FirstOrDefault(n => n.Name == name)
                ?? symbolTable.LookupType(name);

            if (symbol != null)
                _byDeclaration[declaration] = symbol;

            // Recurse even when the symbol is null, so a deeper declaration whose own parent DID
            // resolve is still indexed rather than lost behind one unresolved ancestor.
            Add(nestedBody, symbol ?? parent, symbolTable);
        }
    }

    /// <summary>The name and body of a type-declaring statement, or <c>(null, empty)</c>.</summary>
    private static (string? Name, ImmutableArray<Statement> Body) TypeDeclarationOf(Statement statement)
        => statement switch
        {
            ClassDef classDef => (classDef.Name, classDef.Body),
            StructDef structDef => (structDef.Name, structDef.Body),
            InterfaceDef interfaceDef => (interfaceDef.Name, interfaceDef.Body),
            // An enum declares a type but holds `Members`, not statements — it can contain no
            // nested declaration, so it is indexed as a leaf.
            EnumDef enumDef => (enumDef.Name, ImmutableArray<Statement>.Empty),
            _ => (null, ImmutableArray<Statement>.Empty)
        };
}
