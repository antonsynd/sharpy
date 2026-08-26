using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Assigns the C# spelling of every local — <see cref="CodeGenInfo"/>
/// <c>{ CSharpName, Version, IsConstant, IsModuleLevel = false }</c> — from the
/// <see cref="LocalBindingLedger"/>s the <see cref="SymbolTable"/> recorded (#1560 D1 §3, #1647).
/// </summary>
/// <remarks>
/// <para>
/// <b>One walk per top-level C# method.</b> A method's own ledger and every ledger nested in it
/// (a lambda's, a nested def's — <see cref="LocalBindingLedger.IsNested"/>) are merged by
/// <see cref="LocalBinding.Sequence"/> and walked in source order against one claim table. A
/// chain head claims its base spelling (<see cref="NameCasing.ResolveVariable"/>,
/// <see cref="NameCasing.ResolveConstant"/> for a local const, verbatim for a backtick escape)
/// unless an earlier claim of that spelling <i>conflicts</i>; otherwise it takes the smallest free
/// <c>base_N</c>, skipping spellings that occur as source names anywhere in the method. Every
/// member of a rebinding chain inherits its head's spelling.
/// </para>
/// <para>
/// <b>When two claims conflict.</b> Within ONE ledger the table is monotonic: sibling blocks,
/// closed child blocks and match sections all count, so a closed <c>if</c>-block's <c>x</c> keeps
/// the outer <c>x = 5</c> after it from being spelled the same (CS0136) without modelling the
/// emitted C# block structure. Across ledgers the relation is C#'s own: a lambda's or nested def's
/// binding conflicts with a binding whose scope ENCLOSES it or which it encloses, never with a
/// sibling's. That versions a lambda parameter spelled like an enclosing local (the #1647 class)
/// and an outer local bound after a lambda that used the name, while two sibling lambdas both
/// spelled <c>x =&gt; …</c> keep their names — the shape the <c>.expected.cs</c> corpus pins.
/// </para>
/// <para>
/// <b>Reserved spellings.</b> An accessor whose C# body has the implicit <c>value</c>
/// (<see cref="LocalBindingLedger.ReservesImplicitValue"/>) pre-claims it at the method root, so
/// a local spelled <c>value</c> in a setter or observer is versioned instead of colliding.
/// </para>
/// <para>
/// Parameters are bindings like any other: a top-level function's parameters are the first rows
/// of its ledger and always keep their base spelling; a nested one's are versioned only when they
/// conflict. A binding that already has <see cref="CodeGenInfo"/> (a module-level variable a
/// function writes through to, a field) is left alone, and a chain member whose head has one
/// inherits it.
/// </para>
/// </remarks>
internal sealed class LocalNameAllocator
{
    private readonly SemanticBinding _binding;
    private readonly SemanticInfo _semanticInfo;

    /// <summary>One binding's row together with the ledger it was recorded in.</summary>
    private readonly record struct Row(LocalBinding Binding, LocalBindingLedger Ledger);

    /// <summary>An earlier claim of a spelling: where it was made.</summary>
    private readonly record struct Claim(int ScopeId, LocalBindingLedger? Ledger);

    public LocalNameAllocator(SemanticBinding binding, SemanticInfo semanticInfo)
    {
        _binding = binding;
        _semanticInfo = semanticInfo;
    }

    /// <summary>
    /// Names every local of every ledger in <paramref name="symbolTable"/>: one walk per top-level
    /// method, nested ledgers folded in.
    /// </summary>
    public void AllocateAll(SymbolTable symbolTable)
    {
        var ledgers = symbolTable.AllLedgers.Values;
        var children = new Dictionary<int, List<LocalBindingLedger>>();
        foreach (var ledger in ledgers)
        {
            if (!ledger.IsNested)
                continue;
            if (!children.TryGetValue(ledger.ParentOwnerScopeId, out var list))
                children[ledger.ParentOwnerScopeId] = list = new List<LocalBindingLedger>();
            list.Add(ledger);
        }

        foreach (var ledger in ledgers.Where(l => !l.IsNested).OrderBy(l => l.OwnerScopeId))
            AllocateMethod(symbolTable, ledger, children);
    }

    /// <summary>
    /// Names the locals of one top-level method: <paramref name="root"/>'s rows plus those of every
    /// ledger nested in it (transitively), in <see cref="LocalBinding.Sequence"/> order.
    /// </summary>
    private void AllocateMethod(
        SymbolTable symbolTable,
        LocalBindingLedger root,
        IReadOnlyDictionary<int, List<LocalBindingLedger>> children)
    {
        var rows = new List<Row>();
        var reserved = new HashSet<string>(StringComparer.Ordinal);
        Collect(root, children, rows, reserved);
        rows.Sort((a, b) => a.Binding.Sequence.CompareTo(b.Binding.Sequence));

        var sourceNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (row.Binding.Symbol is VariableSymbol vs)
                sourceNames.Add(vs.Name);
        }

        // Spelling → every place it has been claimed so far. A reserved spelling is claimed at the
        // method root with no ledger, which conflicts with everything in the method.
        var claims = new Dictionary<string, List<Claim>>(StringComparer.Ordinal);
        foreach (var name in reserved)
            claims[name] = new List<Claim> { new(root.OwnerScopeId, null) };

        foreach (var row in rows)
        {
            if (row.Binding.Symbol is not VariableSymbol varSym)
                continue;

            if (_binding.HasCodeGenInfo(varSym))
                continue;

            var chainRoot = _semanticInfo.GetBindingChain(varSym)[0];
            if (!ReferenceEquals(chainRoot, varSym))
            {
                var rootInfo = _binding.GetCodeGenInfo(chainRoot);
                if (rootInfo != null)
                {
                    _binding.SetCodeGenInfo(varSym, rootInfo);
                    continue;
                }
            }

            string baseSpelling = varSym.IsConstant
                ? NameCasing.ResolveConstant(varSym.Name, varSym.IsNameBacktickEscaped)
                : NameCasing.ResolveVariable(varSym.Name, varSym.IsNameBacktickEscaped);

            int version = 0;
            string spelling = baseSpelling;
            if (Conflicts(symbolTable, claims, spelling, row)
                || (spelling != varSym.Name && sourceNames.Contains(spelling)))
            {
                version = 1;
                spelling = $"{baseSpelling}_{version}";
                while (Conflicts(symbolTable, claims, spelling, row) || sourceNames.Contains(spelling))
                {
                    version++;
                    spelling = $"{baseSpelling}_{version}";
                }
            }

            if (!claims.TryGetValue(spelling, out var holders))
                claims[spelling] = holders = new List<Claim>();
            holders.Add(new Claim(row.Binding.ScopeId, row.Ledger));

            _binding.SetCodeGenInfo(varSym, new CodeGenInfo
            {
                CSharpName = baseSpelling,
                Version = version,
                OriginalName = varSym.Name,
                IsConstant = varSym.IsConstant,
                IsModuleLevel = false
            });
        }
    }

    private static void Collect(
        LocalBindingLedger ledger,
        IReadOnlyDictionary<int, List<LocalBindingLedger>> children,
        List<Row> rows,
        HashSet<string> reserved)
    {
        foreach (var entry in ledger.Entries)
            rows.Add(new Row(entry, ledger));
        if (ledger.ReservesImplicitValue)
            reserved.Add("value");

        if (children.TryGetValue(ledger.OwnerScopeId, out var nested))
        {
            foreach (var child in nested)
                Collect(child, children, rows, reserved);
        }
    }

    /// <summary>
    /// True when an earlier claim of <paramref name="spelling"/> would make a binding in
    /// <paramref name="row"/>'s scope illegal C#: same ledger (monotonic), or — across ledgers —
    /// one scope encloses the other.
    /// </summary>
    private static bool Conflicts(
        SymbolTable symbolTable,
        Dictionary<string, List<Claim>> claims,
        string spelling,
        Row row)
    {
        if (!claims.TryGetValue(spelling, out var holders))
            return false;

        foreach (var claim in holders)
        {
            if (claim.Ledger == null || ReferenceEquals(claim.Ledger, row.Ledger))
                return true;

            if (IsAncestorOrSelf(symbolTable, claim.ScopeId, row.Binding.ScopeId)
                || IsAncestorOrSelf(symbolTable, row.Binding.ScopeId, claim.ScopeId))
                return true;
        }

        return false;
    }

    private static bool IsAncestorOrSelf(SymbolTable symbolTable, int ancestorId, int scopeId)
    {
        var scope = symbolTable.GetScope(scopeId);
        while (scope != null)
        {
            if (scope.Id == ancestorId)
                return true;
            scope = scope.Parent;
        }

        return false;
    }
}
