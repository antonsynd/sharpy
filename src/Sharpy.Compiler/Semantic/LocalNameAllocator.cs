using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Walks a function's LocalBindingLedger in ordinal order and assigns
/// CodeGenInfo { CSharpName, Version } to every local VariableSymbol.
/// Chain members (rebindings) inherit the chain head's spelling.
/// </summary>
internal class LocalNameAllocator
{
    private readonly SemanticBinding _binding;
    private readonly SemanticInfo _semanticInfo;

    public LocalNameAllocator(SemanticBinding binding, SemanticInfo semanticInfo)
    {
        _binding = binding;
        _semanticInfo = semanticInfo;
    }

    public void AllocateForFunction(LocalBindingLedger ledger)
    {
        var sourceNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in ledger.Entries)
        {
            if (entry.Symbol is VariableSymbol vs)
                sourceNames.Add(vs.Name);
        }

        var claimed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in ledger.Entries)
        {
            if (entry.Symbol is not VariableSymbol varSym)
                continue;

            if (_binding.HasCodeGenInfo(varSym))
                continue;

            if (varSym.IsParameter)
                continue;

            var chain = _semanticInfo.GetBindingChain(varSym);
            var root = chain[0];
            if (!ReferenceEquals(root, varSym))
            {
                var rootInfo = _binding.GetCodeGenInfo(root);
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
            if (claimed.Contains(spelling)
                || (spelling != varSym.Name && sourceNames.Contains(spelling)))
            {
                version = 1;
                spelling = $"{baseSpelling}_{version}";
                while (claimed.Contains(spelling) || sourceNames.Contains(spelling))
                {
                    version++;
                    spelling = $"{baseSpelling}_{version}";
                }
            }

            claimed.Add(spelling);

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
}
