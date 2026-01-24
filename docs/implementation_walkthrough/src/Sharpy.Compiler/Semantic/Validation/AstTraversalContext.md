# Walkthrough: AstTraversalContext.cs

**Source File**: `src/Sharpy.Compiler/Semantic/Validation/AstTraversalContext.cs`

---

## Overview

`AstTraversalContext` is a lightweight state-tracking utility that manages the current traversal position within the AST during semantic validation. It provides **stack-based scope tracking** for three critical pieces of context that validators frequently need:

1. **Current Class** - Which class (if any) is being processed
2. **Current Function** - Which function (if any) is being processed  
3. **Loop Depth** - Whether we're inside a loop and how deep

This class is designed to replace the older pattern of manually tracking these states using properties on `SemanticContext`. It uses the **RAII pattern** (Resource Acquisition Is Initialization) via C#'s `IDisposable` interface to ensure automatic cleanup when exiting scopes.

### Role in Compiler Pipeline

```
Parser (AST) → Semantic Analysis → ValidationPipeline → CodeGen
                       ↓
              [TypeChecker completes]
                       ↓
         ValidationPipeline.Validate(module, context)
                       ↓
         ┌─────────────┴─────────────┐
         ↓                           ↓
   Validator1.Validate()      Validator2.Validate()
         ↓                           ↓
   Uses context.Traversal      Uses context.Traversal
   to track scope               to track scope
```

**Upstream**: Receives AST from Parser, used during semantic validation phase  
**Downstream**: Provides context information to validators but doesn't modify AST or generate code

---

## Class Structure

### Main Class: `AstTraversalContext`

A non-static class with three internal stacks for tracking traversal state:

```csharp
public class AstTraversalContext
{
    private readonly Stack<TypeSymbol?> _classStack = new();
    private readonly Stack<FunctionSymbol?> _functionStack = new();
    private readonly Stack<bool> _loopStack = new();
    
    // Public readonly properties and methods...
}
```

### Helper Class: `StackPopper<T>`

A private nested class that implements the cleanup mechanism:

```csharp
private class StackPopper<T> : IDisposable
{
    private readonly Stack<T> _stack;
    public StackPopper(Stack<T> stack) => _stack = stack;
    public void Dispose() => _stack.Pop();
}
```

---

## Key Public Properties

### `CurrentClass`

```csharp
public TypeSymbol? CurrentClass => _classStack.Count > 0 ? _classStack.Peek() : null;
```

**Purpose**: Returns the currently active class being validated, or `null` if at module scope.

**When to use**:
- Validating whether instance members are accessed correctly
- Checking if `self`/`this` is valid in the current context
- Determining if a method override is valid

**Example scenario**: An `AccessValidator` needs to know if we're inside a class to validate private member access.

---

### `CurrentFunction`

```csharp
public FunctionSymbol? CurrentFunction => _functionStack.Count > 0 ? _functionStack.Peek() : null;
```

**Purpose**: Returns the currently active function being validated, or `null` if at module/class scope.

**When to use**:
- Validating return statements match the function's return type
- Checking if `return` is used at the appropriate scope
- Determining closure capture for nested functions

**Example scenario**: A `ControlFlowValidator` needs to know which function a `return` statement belongs to.

---

### `InLoop`

```csharp
public bool InLoop => _loopStack.Count > 0 && _loopStack.Peek();
```

**Purpose**: Returns `true` if currently inside any loop construct (`for`, `while`).

**When to use**:
- Validating that `break` and `continue` statements only appear inside loops
- Warning about unreachable code after loop control flow statements

**Example scenario**: A validator encounters `break` and needs to verify it's inside a loop.

---

### `LoopDepth`

```csharp
public int LoopDepth => _loopStack.Count(l => l);
```

**Purpose**: Returns the number of nested loops the current position is inside.

**When to use**:
- Advanced control flow analysis
- Detecting deeply nested loops (performance warnings)
- Understanding break/continue scope in nested loops

**Note**: Uses LINQ's `Count()` with a predicate, counting only `true` values in the stack.

---

## Key Methods

### `EnterClass(TypeSymbol? symbol)`

```csharp
public IDisposable EnterClass(TypeSymbol? symbol)
{
    _classStack.Push(symbol);
    return new StackPopper<TypeSymbol?>(_classStack);
}
```

**Purpose**: Pushes a class onto the traversal stack and returns a disposable that will automatically pop it when disposed.

**Parameters**:
- `symbol`: The `TypeSymbol` representing the class being entered (or `null` for exiting class scope)

**Returns**: An `IDisposable` that pops the stack on `Dispose()`

**Usage pattern**:
```csharp
using (context.Traversal.EnterClass(classSymbol))
{
    // Inside class scope
    // context.Traversal.CurrentClass == classSymbol
    ValidateClassMembers();
}
// Automatically restored to previous class (or null)
```

**Why this design?**  
The `using` statement guarantees cleanup even if an exception is thrown during validation, preventing state corruption.

---

### `EnterFunction(FunctionSymbol? symbol)`

```csharp
public IDisposable EnterFunction(FunctionSymbol? symbol)
{
    _functionStack.Push(symbol);
    return new StackPopper<FunctionSymbol?>(_functionStack);
}
```

**Purpose**: Pushes a function onto the traversal stack with automatic cleanup.

**Parameters**:
- `symbol`: The `FunctionSymbol` representing the function being entered

**Returns**: An `IDisposable` for automatic stack cleanup

**Usage pattern**:
```csharp
using (context.Traversal.EnterFunction(functionSymbol))
{
    // Inside function scope
    // Can validate return statements, parameter usage, etc.
    ValidateFunctionBody(function);
}
// Function context automatically restored
```

**Nested functions**: This naturally handles nested function definitions since each `using` block maintains its own stack frame.

---

### `EnterLoop()`

```csharp
public IDisposable EnterLoop()
{
    _loopStack.Push(true);
    return new StackPopper<bool>(_loopStack);
}
```

**Purpose**: Marks entry into a loop construct with automatic exit tracking.

**Parameters**: None

**Returns**: An `IDisposable` for automatic stack cleanup

**Usage pattern**:
```csharp
// When visiting a ForStatement or WhileStatement
using (context.Traversal.EnterLoop())
{
    // context.Traversal.InLoop == true
    // context.Traversal.LoopDepth incremented
    ValidateLoopBody(loopNode);
}
// Loop context automatically cleaned up
```

**Why push `true`?**  
The boolean value allows the stack to track multiple nested loops. Each `true` represents one loop level.

---

## Dependencies

### Direct Dependencies

| Type | From | Purpose |
|------|------|---------|
| `TypeSymbol` | `Sharpy.Compiler.Semantic.Symbol.cs` | Represents class/struct types |
| `FunctionSymbol` | `Sharpy.Compiler.Semantic.Symbol.cs` | Represents function/method definitions |
| `Stack<T>` | `System.Collections.Generic` | Stack-based scope tracking |
| `IDisposable` | `System` | RAII pattern implementation |

### Usage Context

This class is **instantiated and owned by `SemanticContext`**:

```csharp
// In SemanticContext.cs
public AstTraversalContext Traversal { get; } = new();
```

Validators access it via:

```csharp
context.Traversal.CurrentClass
context.Traversal.EnterFunction(symbol)
```

### Related Files

- **`SemanticContext.cs`** - Contains the `Traversal` property, the primary access point
- **`ISemanticValidator.cs`** - Interface that validators implement; they receive `SemanticContext`
- **`Symbol.cs`** - Defines `TypeSymbol` and `FunctionSymbol`
- **Legacy properties on `SemanticContext`** (deprecated):
  - `CurrentClass`, `CurrentFunction`, `InLoop`, `LoopDepth` (marked `[Obsolete]`)

---

## Patterns and Design Decisions

### 1. **RAII Pattern (Resource Acquisition Is Initialization)**

The core design uses C#'s `using` statement to guarantee scope cleanup:

```csharp
using (context.Traversal.EnterClass(symbol))
{
    // Scope automatically managed
}
```

**Benefits**:
- **Exception safety**: Stack is popped even if validation throws
- **Readability**: Scope is visually clear with indentation
- **No manual cleanup**: No need to remember to call "ExitClass"

**Alternative (rejected)**: Manual push/pop pairs would be error-prone:
```csharp
// ❌ Error-prone approach
context.Traversal.PushClass(symbol);
try {
    ValidateClass();
} finally {
    context.Traversal.PopClass(); // Easy to forget!
}
```

---

### 2. **Stack-Based Tracking**

Using `Stack<T>` allows natural handling of nested scopes:

```csharp
using (context.Traversal.EnterClass(OuterClass))
{
    // CurrentClass == OuterClass
    using (context.Traversal.EnterClass(InnerClass))
    {
        // CurrentClass == InnerClass (nested class)
    }
    // CurrentClass == OuterClass (restored)
}
```

This is essential for:
- Nested classes
- Nested functions (closures)
- Nested loops

---

### 3. **Nullable Symbols**

Methods accept `TypeSymbol?` and `FunctionSymbol?` (nullable) to handle edge cases:

- Entering/exiting module scope (no class)
- Lambda/anonymous functions (no named symbol)
- Synthetic compiler-generated scopes

---

### 4. **Separation of Concerns**

This class **only tracks traversal state**—it doesn't:
- Perform validation logic
- Store semantic information (that's `SemanticInfo`)
- Resolve names or types (that's `NameResolver`, `TypeResolver`)

This makes it highly reusable across different validators with different needs.

---

### 5. **Migration Path from Legacy Code**

`SemanticContext` has deprecated properties to support gradual migration:

```csharp
[Obsolete("Use Traversal.CurrentClass instead. This property will be removed in v0.2.")]
public TypeSymbol? CurrentClass { get; set; }
```

Old validators can be updated incrementally:

```csharp
// Old style (deprecated)
context.CurrentClass = symbol;
try {
    // validate
} finally {
    context.CurrentClass = previousClass;
}

// New style (preferred)
using (context.Traversal.EnterClass(symbol))
{
    // validate
}
```

---

## Debugging Tips

### Problem: "CurrentClass is null when it shouldn't be"

**Check**:
1. Is the validator using `context.Traversal.EnterClass()`?
2. Is the class symbol being passed correctly?
3. Is validation happening inside the `using` block?

**Debug approach**:
```csharp
Console.WriteLine($"Current class: {context.Traversal.CurrentClass?.Name ?? "null"}");
Console.WriteLine($"Stack depth: {context.Traversal._classStack.Count}"); // Use debugger
```

---

### Problem: "Loop depth seems wrong"

**Check**:
- Is `EnterLoop()` being called for all loop constructs (`for`, `while`, `foreach`)?
- Are nested loops each getting their own `using` block?

**Debug approach**:
```csharp
Console.WriteLine($"In loop: {context.Traversal.InLoop}, Depth: {context.Traversal.LoopDepth}");
```

---

### Problem: "Stack overflow or 'Stack empty' exception"

**Possible causes**:
- Mismatched push/pop (shouldn't happen with `using`, but check for manual manipulation)
- Recursive validator calls without proper scope management
- Exception thrown before `using` block completes

**Debug approach**:
- Add logging at entry/exit of each `using` block
- Check exception stack trace for where scope was entered

---

### Debugging Tool: Traversal State Snapshot

Add this helper method to your validator during debugging:

```csharp
private void DumpTraversalState(SemanticContext context)
{
    var traversal = context.Traversal;
    Console.WriteLine("=== Traversal State ===");
    Console.WriteLine($"  CurrentClass: {traversal.CurrentClass?.Name ?? "null"}");
    Console.WriteLine($"  CurrentFunction: {traversal.CurrentFunction?.Name ?? "null"}");
    Console.WriteLine($"  InLoop: {traversal.InLoop}");
    Console.WriteLine($"  LoopDepth: {traversal.LoopDepth}");
    Console.WriteLine("=======================");
}
```

---

## Contribution Guidelines

### When to Modify This File

You should modify `AstTraversalContext.cs` when:

1. **Adding new traversal contexts**  
   Example: Adding support for `try`/`catch` blocks
   ```csharp
   private readonly Stack<ExceptionHandler?> _exceptionHandlerStack = new();
   public ExceptionHandler? CurrentExceptionHandler => ...;
   public IDisposable EnterExceptionHandler(ExceptionHandler handler) => ...;
   ```

2. **Adding computed properties**  
   Example: Checking if we're in a static context
   ```csharp
   public bool InStaticContext => CurrentFunction?.IsStatic ?? 
                                  CurrentClass?.IsStatic ?? 
                                  false;
   ```

3. **Performance optimization**  
   Example: If `LoopDepth` becomes a hot path, cache the count:
   ```csharp
   private int _loopDepthCache = 0;
   public int LoopDepth => _loopDepthCache;
   
   public IDisposable EnterLoop()
   {
       _loopStack.Push(true);
       _loopDepthCache++;
       return new StackPopper<bool>(_loopStack, () => _loopDepthCache--);
   }
   ```

---

### When NOT to Modify This File

**Don't add**:
- Validation logic (belongs in validators)
- Type resolution (belongs in `TypeResolver`)
- Symbol table manipulation (belongs in `NameResolver`, `SymbolTable`)
- Diagnostic reporting (use `context.Diagnostics`)

**Don't change**:
- The `IDisposable` pattern (it's critical for correctness)
- Stack semantics (LIFO must be preserved)
- Nullable handling (null symbols are valid for certain scopes)

---

### Testing Changes

When modifying this class:

1. **Unit test the basic mechanics**:
   ```csharp
   [Fact]
   public void EnterClass_SetsCurrentClass()
   {
       var context = new AstTraversalContext();
       var symbol = new TypeSymbol { Name = "TestClass" };
       
       using (context.EnterClass(symbol))
       {
           Assert.Equal(symbol, context.CurrentClass);
       }
       Assert.Null(context.CurrentClass); // Restored
   }
   ```

2. **Integration test with validators**:
   - Create a test validator that uses the new traversal feature
   - Run it through `ValidationPipeline`
   - Verify it tracks state correctly through nested scopes

3. **Test exception safety**:
   ```csharp
   [Fact]
   public void EnterClass_RestoresOnException()
   {
       var context = new AstTraversalContext();
       var symbol = new TypeSymbol { Name = "TestClass" };
       
       Assert.Throws<Exception>(() =>
       {
           using (context.EnterClass(symbol))
           {
               throw new Exception("Test");
           }
       });
       
       Assert.Null(context.CurrentClass); // Should be restored despite exception
   }
   ```

---

### Code Style Conventions

When adding new features, follow these patterns:

1. **Stack naming**: `_<context>Stack` (e.g., `_classStack`, `_functionStack`)
2. **Property naming**: `Current<Context>` for single item, `In<Context>` for boolean
3. **Method naming**: `Enter<Context>()` (not `Push`, `Set`, etc.)
4. **Always return IDisposable** from `Enter*` methods
5. **Use expression-bodied properties** for simple stack peeks
6. **Document with XML comments** including usage examples

---

## Cross-References

### Related Validation Documentation

- **[SemanticContext.md](../SemanticContext.md)** *(if exists)* - The container for this class
- **[ISemanticValidator.md](../ISemanticValidator.md)** *(if exists)* - Interface that uses this context
- **[ValidationPipeline.md](../ValidationPipeline.md)** *(if exists)* - How validators are orchestrated
- **[AccessValidatorV2.md](./AccessValidatorV2.md)** - Example validator using traversal context

### Related Semantic Analysis Documentation

- **[Symbol.md](../Symbol.md)** - Defines `TypeSymbol` and `FunctionSymbol`
- **[SymbolTable.md](../SymbolTable.md)** - Symbol storage and lookup
- **[TypeChecker.md](../TypeChecker.md)** - Main type checking pass (runs before validation)

### Source Code Files

- **Primary file**: [`src/Sharpy.Compiler/Semantic/Validation/AstTraversalContext.cs`](../../../../../../src/Sharpy.Compiler/Semantic/Validation/AstTraversalContext.cs)
- **Container**: [`src/Sharpy.Compiler/Semantic/Validation/SemanticContext.cs`](../../../../../../src/Sharpy.Compiler/Semantic/Validation/SemanticContext.cs)
- **Usage example**: [`src/Sharpy.Compiler/Semantic/Validation/AccessValidatorV2.cs`](../../../../../../src/Sharpy.Compiler/Semantic/Validation/AccessValidatorV2.cs)

---

## Summary

`AstTraversalContext` is a focused utility class that solves a common problem in compiler validation: **"Where am I in the AST?"**

**Key takeaways**:
- Uses stack-based RAII pattern for automatic scope management
- Tracks class, function, and loop context during validation
- Exception-safe by design (via `using` statements)
- Replaces error-prone manual state tracking
- Designed for incremental adoption (legacy properties deprecated)

**For newcomers**: Start by looking at how existing validators use `context.Traversal.EnterClass()` and `context.Traversal.CurrentClass`. The pattern is simple but powerful—once you see it in action, you'll understand why manual scope tracking was problematic.

**Next steps**:
1. Read `SemanticContext.cs` to see how this integrates with the broader validation system
2. Look at `AccessValidatorV2.cs` or another V2 validator to see real usage
3. Try writing a simple validator that checks if `return` statements are inside functions (using `CurrentFunction`)
