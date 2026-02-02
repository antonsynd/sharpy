namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Centralized AST traversal state for validators.
/// Uses stack-based tracking to manage current class, function, and loop context.
/// </summary>
/// <remarks>
/// This class provides automatic cleanup via IDisposable pattern, allowing
/// validators to use 'using' statements for scope management:
/// <code>
/// using (context.Traversal.EnterClass(classSymbol))
/// {
///     // CurrentClass is now set
/// }
/// // CurrentClass is automatically restored
/// </code>
/// </remarks>
internal class AstTraversalContext
{
    private readonly Stack<TypeSymbol?> _classStack = new();
    private readonly Stack<FunctionSymbol?> _functionStack = new();
    private readonly Stack<bool> _loopStack = new();

    /// <summary>
    /// Gets the current class being processed, if any.
    /// </summary>
    public TypeSymbol? CurrentClass => _classStack.Count > 0 ? _classStack.Peek() : null;

    /// <summary>
    /// Gets the current function being processed, if any.
    /// </summary>
    public FunctionSymbol? CurrentFunction => _functionStack.Count > 0 ? _functionStack.Peek() : null;

    /// <summary>
    /// Gets whether we're currently inside a loop.
    /// </summary>
    public bool InLoop => _loopStack.Count > 0 && _loopStack.Peek();

    /// <summary>
    /// Gets the current loop nesting depth.
    /// </summary>
    public int LoopDepth => _loopStack.Count(l => l);

    /// <summary>
    /// Enters a class scope. Returns an IDisposable that pops the class when disposed.
    /// </summary>
    public IDisposable EnterClass(TypeSymbol? symbol)
    {
        _classStack.Push(symbol);
        return new StackPopper<TypeSymbol?>(_classStack);
    }

    /// <summary>
    /// Enters a function scope. Returns an IDisposable that pops the function when disposed.
    /// </summary>
    public IDisposable EnterFunction(FunctionSymbol? symbol)
    {
        _functionStack.Push(symbol);
        return new StackPopper<FunctionSymbol?>(_functionStack);
    }

    /// <summary>
    /// Enters a loop scope. Returns an IDisposable that pops the loop when disposed.
    /// </summary>
    public IDisposable EnterLoop()
    {
        _loopStack.Push(true);
        return new StackPopper<bool>(_loopStack);
    }

    /// <summary>
    /// Helper class that pops from a stack when disposed.
    /// </summary>
    private class StackPopper<T> : IDisposable
    {
        private readonly Stack<T> _stack;
        public StackPopper(Stack<T> stack) => _stack = stack;
        public void Dispose() => _stack.Pop();
    }
}
