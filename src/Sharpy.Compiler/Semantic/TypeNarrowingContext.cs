namespace Sharpy.Compiler.Semantic;

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
    private readonly Stack<Dictionary<string, SemanticType>> _scopeStack = new();

    /// <summary>
    /// Creates a new TypeNarrowingContext with an initial root scope.
    /// </summary>
    public TypeNarrowingContext()
    {
        _scopeStack.Push(new Dictionary<string, SemanticType>());
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
        _scopeStack.Push(new Dictionary<string, SemanticType>());
        return new ScopeDisposer(this);
    }

    /// <summary>
    /// Records a type narrowing for the given variable name in the current scope.
    /// If a narrowing for this name already exists in the current scope, it is overwritten.
    /// </summary>
    /// <param name="name">The variable name (or narrowing key for subscript expressions).</param>
    /// <param name="type">The narrowed type.</param>
    public void Narrow(string name, SemanticType type)
    {
        if (_scopeStack.Count == 0)
            throw new InvalidOperationException("Cannot narrow without an active scope.");

        _scopeStack.Peek()[name] = type;
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
            if (scope.TryGetValue(name, out var type))
                return type;
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
        _scopeStack.Push(new Dictionary<string, SemanticType>());
    }

    /// <summary>
    /// Applies multiple narrowings to the current scope at once.
    /// </summary>
    /// <param name="narrowings">The narrowings to apply.</param>
    public void ApplyNarrowings(IEnumerable<KeyValuePair<string, SemanticType>> narrowings)
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
}
