using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// What a bare callee, or a bare reference to a function, denotes: the symbol the scope walk bound,
/// and the declarations a call of it chooses among (plan-777361 D5).
/// </summary>
/// <param name="Bound">The symbol the bare read's walk bound, or null when nothing in scope binds the name.</param>
/// <param name="Overloads">
/// The declarations a call chooses among: the builtin registry's set when the name denotes the
/// builtin, the overload list the bound user function belongs to, or null (a single user function,
/// a variable, a type the user declared, a module).
/// </param>
/// <param name="DenotesBuiltin">
/// Whether the name denotes the builtin: nothing bound it, or what bound it is the registry's own
/// symbol (a builtin function, or a builtin type whose conversions <see cref="Overloads"/> holds).
/// </param>
internal readonly record struct BareCallee(Symbol? Bound, List<FunctionSymbol>? Overloads, bool DenotesBuiltin)
{
    /// <summary>The bound symbol as a function, or null.</summary>
    public FunctionSymbol? Function => Bound as FunctionSymbol;

    /// <summary>The builtin registry's set when the name denotes the builtin, else null.</summary>
    public List<FunctionSymbol>? BuiltinOverloads => DenotesBuiltin ? Overloads : null;

    /// <summary>The overload list of the bound user function, else null.</summary>
    public List<FunctionSymbol>? UserOverloads => DenotesBuiltin ? null : Overloads;
}

/// <summary>
/// TypeChecker partial class: the ONE callee resolver (plan-777361 D5; N1, the innermost binding wins).
/// </summary>
/// <remarks>
/// <para>A bare callee resolves by the scope walk a bare read uses and dispatches on the symbol it
/// bound, whatever an outer binding of the name is. Before D5 each call route asked its own question
/// by NAME: <c>SymbolTable.LookupFunctionOverloads</c> walks the chain's overload lists alone, past
/// every inner binding, and <c>BuiltinRegistry.GetFunctionOverloads</c> answers the builtin whatever
/// shadows it. So a local, a parameter or a nested <c>def</c> spelled like an overloaded module
/// function, or a variable spelled like a builtin, was dispatched to the outer declaration.</para>
/// <para>Every TypeChecker route that resolves a bare callee or a bare function reference asks this
/// file. <c>DispatchSiteInventoryTests.BareCalleeLookups_AreTheResolversOwnOrARosteredRemainder</c>
/// pins the direct overload lookups left in the TypeChecker at this file's and a rostered remainder,
/// each with its reason (a declaration's own symbol, a qualified <c>builtins.X</c>, a resolved
/// type's conversions).</para>
/// </remarks>
internal partial class TypeChecker
{
    /// <summary>
    /// The bare callee <paramref name="callee"/> as the bare read bound it: a class-body sibling
    /// METHOD the read admitted (<c>TryResolveClassBodySiblingRead</c>, P21h D8 and F2: the member
    /// outranks an outer binding of its name), recorded on the identifier with its sibling-read fact,
    /// else the scope walk. Read after <c>CheckExpression</c> has checked the callee, which records
    /// the sibling read.
    /// </summary>
    /// <remarks>
    /// A sibling with no symbol of its own (a property) stays on the walk until D3/D4 (T4, T5): the
    /// read binds it inside a comprehension element of an initializer too, where R-Y skips the class
    /// scope, so following it there would refuse a correct program (lead ruling on T3).
    /// </remarks>
    private BareCallee ResolveBareCallee(Identifier callee)
    {
        if (_semanticInfo.GetClassBodySiblingRead(callee) != null
            && _semanticInfo.GetIdentifierSymbol(callee) is FunctionSymbol siblingMethod)
        {
            return new BareCallee(siblingMethod, null, DenotesBuiltin: false);
        }

        return BareCalleeOf(callee.Name, _symbolTable.Lookup(callee.Name));
    }

    /// <summary>
    /// The bare name <paramref name="name"/> resolved from the current scope by the bare read's walk,
    /// for a site that holds only the spelling (a generic reference's callee name, <c>range</c>).
    /// </summary>
    private BareCallee ResolveBareCallee(string name)
        => BareCalleeOf(name, _symbolTable.Lookup(name));

    /// <summary>
    /// What <paramref name="name"/> denotes given <paramref name="bound"/>, the symbol the walk bound
    /// for it. The one place in the TypeChecker that reads a bare name's overload list or the builtin
    /// registry's function set for it.
    /// </summary>
    private BareCallee BareCalleeOf(string name, Symbol? bound)
    {
        var registry = _symbolTable.BuiltinRegistry;
        if (bound == null || registry.IsBuiltinSymbol(bound))
            return new BareCallee(bound, registry.GetFunctionOverloads(name), DenotesBuiltin: true);

        return bound is FunctionSymbol function
            ? new BareCallee(bound, OverloadListOf(name, function), DenotesBuiltin: false)
            : new BareCallee(bound, null, DenotesBuiltin: false);
    }

    /// <summary>
    /// The overload list <paramref name="function"/> belongs to, read up the current scope chain, or
    /// null for a single declaration. Membership is identity through the import clone chain
    /// (<see cref="Symbol.DeclarationRoot"/>), so an outer scope's list of the same spelling, the one
    /// an inner <c>def</c> or binding shadows, is never the answer.
    /// </summary>
    private List<FunctionSymbol>? OverloadListOf(string name, FunctionSymbol function)
    {
        var root = function.DeclarationRoot();
        for (var scope = _symbolTable.CurrentScope; scope != null; scope = scope.Parent)
        {
            if (scope.LookupFunctionOverloads(name, searchParent: false) is { } overloads
                && overloads.Any(o => ReferenceEquals(o, function) || ReferenceEquals(o.DeclarationRoot(), root)))
            {
                return overloads;
            }
        }

        return null;
    }
}
