namespace Sharpy.Compiler.Semantic;

/// <summary>
/// A single expression-level narrowing: the narrowed type plus the optional accessor codegen must
/// apply when reading the narrowed value. The lowering is <c>null</c> for narrowing forms that need
/// no accessor (e.g. match-arm scope guards), and non-null for <c>and</c>-RHS / ternary-arm entries
/// where the read site must emit <c>.Unwrap()</c>/<c>.Value</c>/<c>!</c>/a cast (#1081).
/// </summary>
public readonly struct NarrowingEntry
{
    public NarrowingEntry(SemanticType type, NarrowedReadLowering? lowering = null)
    {
        Type = type;
        Lowering = lowering;
    }

    public SemanticType Type { get; }

    public NarrowedReadLowering? Lowering { get; }
}

/// <summary>
/// Manages type narrowing within conditional contexts using a stack-based scope model.
/// Type narrowing occurs when control flow analysis determines a more specific type
/// for a variable (e.g., after <c>if x is not None:</c>, the type of <c>x</c> narrows
/// from <c>T?</c> to <c>T</c>).
///
/// <para>
/// This class provides scope isolation ensuring that:
/// <list type="bullet">
///   <item>Narrowings in inner scopes don't leak to outer scopes</item>
///   <item>Inner scopes can shadow outer scope narrowings</item>
///   <item>Scope cleanup is automatic via IDisposable</item>
/// </list>
/// </para>
/// </summary>
/// <remarks>
/// Thread safety: This class is not thread-safe. Each TypeChecker instance should
/// have its own TypeNarrowingContext.
/// </remarks>
public sealed class TypeNarrowingContext
{
    private readonly Stack<Dictionary<string, NarrowingEntry>> _scopeStack = new();

    /// <summary>
    /// Creates a new TypeNarrowingContext with an initial root scope.
    /// </summary>
    public TypeNarrowingContext()
    {
        _scopeStack.Push(new Dictionary<string, NarrowingEntry>());
    }

    /// <summary>
    /// Gets the current scope depth. Primarily for testing and debugging.
    /// </summary>
    public int ScopeDepth => _scopeStack.Count;

    /// <summary>
    /// Enters a new narrowing scope. The returned IDisposable must be disposed
    /// to exit the scope (typically via a using statement).
    /// </summary>
    /// <returns>An IDisposable that pops the scope when disposed.</returns>
    /// <example>
    /// <code>
    /// using (narrowingContext.EnterScope())
    /// {
    ///     narrowingContext.Narrow("x", SemanticType.Str);
    ///     // narrowing is valid here
    /// }
    /// // narrowing is no longer valid
    /// </code>
    /// </example>
    public IDisposable EnterScope()
    {
        _scopeStack.Push(new Dictionary<string, NarrowingEntry>());
        return new ScopeDisposer(this);
    }

    /// <summary>
    /// Enters a completely isolated narrowing scope that does NOT inherit narrowings
    /// from parent scopes. Use this when entering a function definition, as control-flow
    /// narrowings should not cross function boundaries (nested functions can be called
    /// later when the narrowing condition no longer holds).
    /// </summary>
    /// <returns>An IDisposable that restores the previous scope stack when disposed.</returns>
    /// <example>
    /// <code>
    /// // In outer function with narrowing active
    /// narrowingContext.Narrow("x", SemanticType.Str);
    ///
    /// using (narrowingContext.EnterIsolatedScope())
    /// {
    ///     // x is NOT narrowed here - isolated scope
    ///     narrowingContext.GetNarrowedType("x"); // returns null
    /// }
    ///
    /// // x is narrowed again after isolated scope exits
    /// narrowingContext.GetNarrowedType("x"); // returns Str
    /// </code>
    /// </example>
    public IDisposable EnterIsolatedScope()
    {
        // Save the current stack and create a fresh isolated context
        var savedStack = _scopeStack.ToArray();
        _scopeStack.Clear();
        _scopeStack.Push(new Dictionary<string, NarrowingEntry>());
        return new IsolatedScopeDisposer(this, savedStack);
    }

    /// <summary>
    /// Records a type narrowing for the given variable name in the current scope.
    /// If a narrowing for this name already exists in the current scope, it is overwritten.
    /// </summary>
    /// <param name="name">The variable name (or narrowing key for subscript expressions).</param>
    /// <param name="type">The narrowed type.</param>
    /// <param name="lowering">
    /// The accessor codegen must apply when reading this narrowed value, or <c>null</c> when no
    /// accessor is needed (#1081).
    /// </param>
    public void Narrow(string name, SemanticType type, NarrowedReadLowering? lowering = null)
    {
        if (_scopeStack.Count == 0)
            throw new InvalidOperationException("Cannot narrow without an active scope.");

        _scopeStack.Peek()[name] = new NarrowingEntry(type, lowering);
    }

    /// <summary>
    /// Gets the narrowed type for the given name, searching from the innermost
    /// scope outward. Returns null if no narrowing exists.
    /// </summary>
    /// <param name="name">The variable name to look up.</param>
    /// <returns>The narrowed type, or null if not narrowed.</returns>
    public SemanticType? GetNarrowedType(string name)
    {
        // Search from innermost to outermost scope
        foreach (var scope in _scopeStack)
        {
            if (scope.TryGetValue(name, out var entry))
                return entry.Type;
        }
        return null;
    }

    /// <summary>
    /// Tries to get the narrowed type for the given name.
    /// </summary>
    /// <param name="name">The variable name to look up.</param>
    /// <param name="type">The narrowed type, if found.</param>
    /// <returns>True if a narrowing was found, false otherwise.</returns>
    public bool TryGetNarrowedType(string name, out SemanticType? type)
    {
        type = GetNarrowedType(name);
        return type != null;
    }

    /// <summary>
    /// Tries to get both the narrowed type and its optional read lowering for the given name,
    /// searching from the innermost scope outward.
    /// </summary>
    /// <param name="name">The variable name (or narrowing key) to look up.</param>
    /// <param name="type">The narrowed type, if found.</param>
    /// <param name="lowering">The accessor lowering recorded on the entry, or <c>null</c> if none.</param>
    /// <returns>True if a narrowing was found, false otherwise.</returns>
    public bool TryGetNarrowing(string name, out SemanticType? type, out NarrowedReadLowering? lowering)
    {
        foreach (var scope in _scopeStack)
        {
            if (scope.TryGetValue(name, out var entry))
            {
                type = entry.Type;
                lowering = entry.Lowering;
                return true;
            }
        }
        type = null;
        lowering = null;
        return false;
    }

    /// <summary>
    /// Clears all narrowings in the current (innermost) scope only.
    /// Outer scope narrowings are preserved.
    /// </summary>
    public void ClearNarrowings()
    {
        if (_scopeStack.Count == 0)
            return;

        _scopeStack.Peek().Clear();
    }

    /// <summary>
    /// Clears all narrowings in all scopes, effectively resetting to a clean state
    /// with only the root scope remaining.
    /// </summary>
    public void ClearAllNarrowings()
    {
        _scopeStack.Clear();
        _scopeStack.Push(new Dictionary<string, NarrowingEntry>());
    }

    /// <summary>
    /// Applies multiple narrowings (types only, no read lowering) to the current scope at once.
    /// </summary>
    /// <param name="narrowings">The narrowings to apply.</param>
    public void ApplyNarrowings(IEnumerable<KeyValuePair<string, SemanticType>> narrowings)
    {
        var currentScope = _scopeStack.Peek();
        foreach (var kvp in narrowings)
        {
            currentScope[kvp.Key] = new NarrowingEntry(kvp.Value);
        }
    }

    /// <summary>
    /// Applies multiple narrowings (each carrying an optional read lowering) to the current scope at
    /// once. Used by expression-level narrowing (<c>and</c>-RHS, ternary arms) so the read sites in the
    /// scope can copy the recorded accessor for codegen (#1081).
    /// </summary>
    /// <param name="narrowings">The narrowing entries to apply.</param>
    public void ApplyNarrowings(IEnumerable<KeyValuePair<string, NarrowingEntry>> narrowings)
    {
        var currentScope = _scopeStack.Peek();
        foreach (var kvp in narrowings)
        {
            currentScope[kvp.Key] = kvp.Value;
        }
    }

    private void ExitScope()
    {
        if (_scopeStack.Count > 1) // Preserve root scope
        {
            _scopeStack.Pop();
        }
    }

    /// <summary>
    /// IDisposable implementation that exits a scope when disposed.
    /// </summary>
    private sealed class ScopeDisposer : IDisposable
    {
        private readonly TypeNarrowingContext _context;
        private bool _disposed;

        public ScopeDisposer(TypeNarrowingContext context)
        {
            _context = context;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _context.ExitScope();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// IDisposable implementation that restores a saved scope stack when disposed.
    /// Used by EnterIsolatedScope for function boundaries.
    /// </summary>
    private sealed class IsolatedScopeDisposer : IDisposable
    {
        private readonly TypeNarrowingContext _context;
        private readonly Dictionary<string, NarrowingEntry>[]? _savedStack;
        private bool _disposed;

        public IsolatedScopeDisposer(TypeNarrowingContext context, Dictionary<string, NarrowingEntry>[] savedStack)
        {
            _context = context;
            _savedStack = savedStack;
        }

        public void Dispose()
        {
            if (!_disposed && _savedStack != null)
            {
                // Restore the saved stack
                _context._scopeStack.Clear();
                // Push in reverse order since ToArray() returns top-first
                for (int i = _savedStack.Length - 1; i >= 0; i--)
                {
                    _context._scopeStack.Push(_savedStack[i]);
                }
                _disposed = true;
            }
        }
    }
}
