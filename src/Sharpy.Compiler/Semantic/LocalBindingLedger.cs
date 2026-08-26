namespace Sharpy.Compiler.Semantic;

internal record LocalBinding(Symbol Symbol, int ScopeId, int Ordinal);

internal class LocalBindingLedger
{
    private readonly List<LocalBinding> _entries = new();
    private int _nextOrdinal;

    public void Append(Symbol symbol, int scopeId)
    {
        _entries.Add(new LocalBinding(symbol, scopeId, _nextOrdinal++));
    }

    public IReadOnlyList<LocalBinding> Entries => _entries;

    public void MergeFrom(LocalBindingLedger other)
    {
        foreach (var entry in other._entries)
        {
            _entries.Add(entry with { Ordinal = _nextOrdinal++ });
        }
    }
}
