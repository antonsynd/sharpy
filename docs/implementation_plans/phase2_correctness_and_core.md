# Phase 2: Correctness & Core Features (Medium Effort)

This plan covers five compiler tasks that address codegen gaps, a parser precedence bug, a semantic restriction to relax, and two missing builtin functions. Each task is self-contained and can be committed independently.

---

## General Guidance

### Compiler Pipeline Order

Every feature in Sharpy flows through this pipeline:

```
Source (.spy) --> Lexer --> Parser (AST) --> Semantic --> ValidationPipeline --> RoslynEmitter --> C# --> .NET IL
```

When adding codegen support for an existing AST node, always verify that semantic analysis already handles that node. If the TypeChecker switch in `CheckExpression()` does not have a case for the node, you must add one there first. Codegen must never encounter an AST node type that semantic analysis has not already validated and typed.

### RoslynEmitter Rules

- All C# output is built with `SyntaxFactory` calls. Never use string interpolation to produce C# source.
- The emitter uses `static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;` so factory methods like `IdentifierName(...)`, `AssignmentExpression(...)`, etc. are available without qualification.
- Temporary variables use the `_tempVarCounter` field: `$"__walrus_{_tempVarCounter++}"`.
- Local variable tracking uses `_declaredVariables` (HashSet of mangled names) and `_variableVersions` (Dictionary mapping base names to version numbers).

### Sharpy.Core Constraints

- Targets `netstandard2.1;netstandard2.0` with `LangVersion 9.0`.
- No file-scoped namespaces, no global usings, no record structs.
- All builtins are `public static partial class Builtins` in the `Sharpy` namespace with `[SharpyModule("builtins")]` on the primary declaration.

### Testing

```bash
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"   # File-based tests
dotnet test                                                           # All tests
dotnet format whitespace                                              # Required before commit
```

File-based tests live in `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`. Each test is a `.spy` file with a matching `.expected` file (exact stdout match) or `.error` file (substring match in compiler error output).

---

## Task 1: Walrus Operator `:=` Codegen

### Context

The walrus operator `:=` is fully implemented in the lexer (`ColonAssign` token), parser (`WalrusExpression` AST node in `Parser.Expressions.cs` lines 17-43), and type checker (`CheckWalrusExpression` in `TypeChecker.Expressions.cs` line 2167). However, `RoslynEmitter.Expressions.cs` has no case for `WalrusExpression` in the `GenerateExpression` switch (line 20-85). Using `:=` in a Sharpy program produces the fallback error: `"Unsupported expression type in code generation: 'WalrusExpression'"` (SPY0518).

### Files to Modify

| File | Purpose |
|------|---------|
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs` | Add `WalrusExpression` case to `GenerateExpression` switch + new `GenerateWalrusExpression` method |

### Decision: Lowering Strategy

There are two ways to emit the walrus operator in C#:

**Option A: C# inline assignment expression.** C# supports `(x = value)` as an expression that both assigns and returns the value. This maps directly to `:=` semantics.

**Option B: Hoist a `var` declaration before the containing statement.** Declare the variable before the statement, then use `(x = value)` inline. This is what the spec describes.

**Recommendation: Option A (inline assignment expression).**

Rationale: C# natively supports assignment-as-expression, so `(target = value)` is the most direct lowering. However, the variable must be declared beforehand if it does not yet exist. Since the walrus operator introduces a new variable, we need to ensure the variable is declared before the expression is evaluated.

The practical approach: When the emitter encounters a `WalrusExpression`, it should:
1. Register the variable in `_declaredVariables` and `_variableVersions` (so later references resolve correctly).
2. Emit a C# simple assignment expression `(varName = value)`. The variable must already be declared by the time this executes at runtime.

The challenge is that `GenerateExpression` returns an `ExpressionSyntax`, not a `StatementSyntax` -- it cannot emit a preceding declaration statement. For the initial implementation, we leverage the fact that C# supports declaring a variable inline using a declaration expression pattern, or we can use the simpler approach: since the TypeChecker has already validated the walrus expression, we know the variable's type. We emit the walrus as an inline assignment `(varName = expr)` and rely on the containing statement generation to have pre-declared the variable.

**Simplest correct approach:** Emit the walrus expression as a C# `SimpleAssignmentExpression` wrapped in parentheses. The variable declaration must be hoisted. We add a pre-scan pass to detect walrus expressions in a statement and emit their variable declarations before that statement.

However, for the **initial implementation**, we can take a simpler path that handles the most common cases (walrus in `if` conditions, while conditions, and list literals):

- Emit the walrus as `(varName = value)` (assignment expression).
- Before emitting the assignment, if the variable is not already declared, declare it with `var` type and `default` value by inserting a local declaration statement. We do this by tracking walrus variables that need hoisting.

Since this requires statement-level awareness from an expression-level method, the cleanest approach is:

1. In `GenerateWalrusExpression`, register the variable name in a new `_pendingWalrusDeclarations` set.
2. In `GenerateBodyStatement` (the statement-level driver), after generating a statement, check if any walrus declarations were registered during expression generation, and prepend `var` declarations.

Actually, the cleanest approach that avoids cross-cutting complexity: **Use the existing `_declaredVariables` tracking.** If the walrus target is not yet declared, declare it inline using `GetMangledVariableName` with `isNewDeclaration: true`, which registers it. Then emit the assignment expression. The variable will be implicitly typed by C# through the assignment.

Wait -- C# does not allow undeclared variables in assignment expressions. We need a declaration somewhere.

**Final recommended approach:** Use a two-phase approach within expression generation:

1. The `GenerateWalrusExpression` method adds the variable to a `_walrusDeclarations` list (a `List<(string name, TypeSyntax type)>` field on the emitter).
2. The `GenerateBodyStatement` method wraps its output: after generating a statement, it prepends any accumulated walrus declarations and clears the list.

This is architecturally sound because it keeps expression generation pure (returns `ExpressionSyntax`) while hoisting declarations at the statement boundary.

### Step-by-Step Implementation

**Step 1: Add the walrus declaration tracking field to `RoslynEmitter.cs`.**

File: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

Add near the other local scope tracking fields (after `_tempVarCounter` around line 85):

```csharp
/// <summary>
/// Tracks walrus operator (:=) variable declarations that need to be hoisted
/// before the containing statement. Populated during expression generation,
/// consumed and cleared during statement generation.
/// </summary>
private readonly List<LocalDeclarationStatementSyntax> _walrusDeclarations = new();
```

**Step 2: Add `WalrusExpression` case to the `GenerateExpression` switch.**

File: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`

In the `GenerateExpression` switch (around line 78, before the `TryExpression` case), add:

```csharp
// Walrus operator
WalrusExpression walrus => GenerateWalrusExpression(walrus),
```

**Step 3: Implement `GenerateWalrusExpression`.**

File: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`

Add this method (e.g., after `GenerateMaybeExpression` or at the end of the file, before the closing brace):

```csharp
private ExpressionSyntax GenerateWalrusExpression(WalrusExpression walrus)
{
    // Generate the value expression first (before registering the new variable)
    var value = GenerateExpression(walrus.Value);

    // Get the mangled variable name, registering it as a new declaration
    var varName = GetMangledVariableName(walrus.Target, isNewDeclaration: true);

    // Determine the type from semantic info
    var semanticType = GetExpressionSemanticType(walrus);
    var typeSyntax = semanticType != null
        ? _typeMapper.MapSemanticType(semanticType)
        : IdentifierName("var");

    // Hoist a variable declaration: <type> varName = default;
    // This will be prepended before the containing statement.
    var declaration = LocalDeclarationStatement(
        VariableDeclaration(typeSyntax)
            .WithVariables(SingletonSeparatedList(
                VariableDeclarator(Identifier(varName))
                    .WithInitializer(EqualsValueClause(
                        DefaultExpression(typeSyntax))))));

    _walrusDeclarations.Add(declaration);
    _declaredVariables.Add(varName);

    // Emit the walrus as: (varName = value)
    return ParenthesizedExpression(
        AssignmentExpression(
            SyntaxKind.SimpleAssignmentExpression,
            IdentifierName(varName),
            value));
}
```

**Step 4: Consume walrus declarations in `GenerateBodyStatement`.**

File: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs`

Modify the `GenerateBodyStatement` method. Change the return logic to check for pending walrus declarations. Replace the final return around line 50:

```csharp
// Before: return result != null ? AttachLineDirective(result, stmt) : null;

// After:
if (result == null)
    return null;

result = AttachLineDirective(result, stmt);

// If any walrus declarations were accumulated during expression generation,
// wrap them together with the statement in a block.
if (_walrusDeclarations.Count > 0)
{
    var allStatements = new List<StatementSyntax>(_walrusDeclarations);
    allStatements.Add(result);
    _walrusDeclarations.Clear();
    return Block(allStatements);
}

return result;
```

**Step 5: Verify `MapSemanticType` exists on TypeMapper.**

Check whether `_typeMapper` has a `MapSemanticType(SemanticType)` method. If not, use the `_typeMapper.MapType(TypeAnnotation?)` overload by constructing a `TypeAnnotation` from the semantic type, or use `IdentifierName("var")` as a fallback. Search for existing usage patterns:

```bash
grep -n "MapSemanticType" src/Sharpy.Compiler/CodeGen/TypeMapper.cs
```

If `MapSemanticType` does not exist, use `IdentifierName("var")` instead for the hoisted declaration type:

```csharp
var typeSyntax = IdentifierName("var");
```

But the declaration then needs a real initializer rather than `default`. Change to:

```csharp
// Use var with the actual value as initializer (will be reassigned in the walrus expression)
var declaration = LocalDeclarationStatement(
    VariableDeclaration(IdentifierName("var"))
        .WithVariables(SingletonSeparatedList(
            VariableDeclarator(Identifier(varName))
                .WithInitializer(EqualsValueClause(value)))));
```

And emit just the variable reference (not an assignment) from the walrus expression:

```csharp
return IdentifierName(varName);
```

This alternative approach is simpler and avoids the type mapping issue entirely. The hoisted declaration `var x = <value>;` both declares and initializes, and the walrus expression evaluates to just `x`. However, this changes the evaluation order: the value is computed at the declaration point, not inline. For `if` conditions like `if (n := len(items)) > 0:`, this still works correctly because the declaration is hoisted to just before the `if` statement.

**Recommended simplification:** Use the `var x = value; ... x ...` pattern for the initial implementation. It handles all common walrus cases correctly and avoids complexity.

Revised `GenerateWalrusExpression`:

```csharp
private ExpressionSyntax GenerateWalrusExpression(WalrusExpression walrus)
{
    var value = GenerateExpression(walrus.Value);
    var varName = GetMangledVariableName(walrus.Target, isNewDeclaration: true);

    // Hoist: var varName = value;
    _walrusDeclarations.Add(
        LocalDeclarationStatement(
            VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(Identifier(varName))
                        .WithInitializer(EqualsValueClause(value))))));
    _declaredVariables.Add(varName);

    // The walrus expression evaluates to the variable itself
    return IdentifierName(varName);
}
```

### Testing

Create two test fixtures:

**File: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/basics/walrus_operator_0001.spy`**

```python
def main():
    items: list[int] = [1, 2, 3, 4, 5]
    if (n := len(items)) > 0:
        print(n)
    else:
        print(0)
```

**File: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/basics/walrus_operator_0001.expected`**

```
5
```

**File: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/basics/walrus_operator_0002.spy`**

```python
def main():
    x: list[int] = [y := 10, y + 1, y + 2]
    print(x[0])
    print(x[1])
    print(x[2])
```

**File: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/basics/walrus_operator_0002.expected`**

```
10
11
12
```

Note: The list literal test (walrus_operator_0002) is tricky because the walrus assignment happens inside a list literal expression, and subsequent elements reference `y`. The hoisted declaration approach handles this because `y` is declared before the list is constructed. However, the hoisted `var y = 10` will be placed before the `x` declaration statement, and `y + 1` and `y + 2` will reference `y` correctly.

### Commit Message

```
feat: Add walrus operator (:=) codegen support

Emit WalrusExpression as a hoisted var declaration before the
containing statement. The variable is declared with its value,
and the expression evaluates to the variable reference.
```

### Verification Command

```bash
dotnet test --filter "DisplayName~walrus_operator"
```

---

## Task 2: `to` Operator Precedence Fix

### Context

The language specification (`docs/language_specification/operator_precedence.md`) defines `to` at precedence level 11, between pipe (`|>`, level 10) and comparisons (level 12). This means `x + 1 to float` should parse as `(x + 1) to float`.

Currently, `to` is handled inside `ParsePostfix()` (`src/Sharpy.Compiler/Parser/Parser.Expressions.cs`, line 774), which is at the highest precedence level (level 1 -- member access, indexing, function calls). This means `x + 1 to float` is parsed as `x + (1 to float)`, which is incorrect.

The `as` operator (type cast) has the same problem -- it is also inside `ParsePostfix()` (line 756). However, `as` is not listed in the precedence table and is described separately. For this task, we focus only on `to`.

### Files to Modify

| File | Purpose |
|------|---------|
| `src/Sharpy.Compiler/Parser/Parser.Expressions.cs` | Move `to` handling from `ParsePostfix()` to a new `ParseCast()` function at the correct precedence level |

### Step-by-Step Implementation

**Step 1: Understand the current call chain.**

The expression parsing call chain (highest to lowest precedence) is:

```
ParseExpression
  -> ParseWalrusExpression
    -> ParseTryMaybeExpression
      -> ParseConditionalExpression
        -> ParseNullCoalesce
          -> ParseLogicalOr
            -> ParseLogicalAnd
              -> ParseLogicalNot
                -> ParseComparison
                  -> ParsePipe
                    -> ParseBitwiseOr
                      -> ParseBitwiseXor
                        -> ParseBitwiseAnd
                          -> ParseShift
                            -> ParseAdditive
                              -> ParseMultiplicative
                                -> ParseUnary
                                  -> ParsePower
                                    -> ParsePostfix  <-- `to` is currently here (WRONG)
                                      -> ParsePrimary
```

According to the spec, `to` should be between `ParsePipe` and `ParseComparison`:

```
ParseComparison -> ParseCast -> ParsePipe
```

**Step 2: Create `ParseCast()` method.**

Add a new method between `ParsePipe()` and `ParseComparison()`. This method parses the `to` operator at the correct precedence level.

In `src/Sharpy.Compiler/Parser/Parser.Expressions.cs`, add after `ParsePipe()` (after line 400):

```csharp
private Expression ParseCast()
{
    var expr = ParsePipe();

    // Handle `to` (type coercion) at this precedence level
    // Left-to-right associativity: `a to int to float` = `(a to int) to float`
    while (Current.Type == TokenType.To)
    {
        Advance();
        var targetType = ParseTypeAnnotation();

        expr = new TypeCoercion
        {
            Value = expr,
            TargetType = targetType,
            LineStart = expr.LineStart,
            ColumnStart = expr.ColumnStart,
            LineEnd = Previous.Line,
            ColumnEnd = Previous.Column + Previous.Value.Length,
            Span = expr.Span
        };
    }

    return expr;
}
```

**Step 3: Update `ParseComparison()` to call `ParseCast()` instead of `ParsePipe()`.**

In `ParseComparison()` (line 249), change:

```csharp
// Before:
var left = ParsePipe();
```

to:

```csharp
// After:
var left = ParseCast();
```

Also update the comparison chain loop body (line 305) from:

```csharp
operands.Add(ParsePipe());
```

to:

```csharp
operands.Add(ParseCast());
```

**Step 4: Remove `to` handling from `ParsePostfix()`.**

In `ParsePostfix()`, remove the `else if` block for `TokenType.To` (lines 774-792):

```csharp
// REMOVE this entire block:
else if (Current.Type == TokenType.To)
{
    // Type coercion (value to T or value to T?)
    // Throws InvalidCastException on failure for T, returns None for T?
    Advance();
    var targetType = ParseTypeAnnotation();

    expr = new TypeCoercion
    {
        Value = expr,
        TargetType = targetType,
        LineStart = expr.LineStart,
        ColumnStart = expr.ColumnStart,
        LineEnd = Previous.Line,
        ColumnEnd = Previous.Column + Previous.Value.Length,
        // TypeAnnotation doesn't have Span yet (A.12), use expr's span for now
        Span = expr.Span
    };
}
```

**Step 5: Audit existing test fixtures.**

Search for all existing uses of `to` in test fixtures:

```bash
grep -rn " to " src/Sharpy.Compiler.Tests/Integration/TestFixtures/ --include="*.spy" | grep -v "#"
```

Known uses:
- `enums/enum_to_int_coercion.spy`: `c to int` -- simple identifier, no precedence issue.
- `module_imports/complex_type_relationships/calculator.spy`: `value to int` -- same.
- `module_imports/complex_type_relationships/main.spy`: `dims.length to int` -- member access, which is higher precedence than `to`, so `(dims.length) to int` is correct both before and after the fix.
- `type_system/optional_type_coercion.spy`: `obj to int?` -- simple identifier, no issue.

None of the existing fixtures exercise `expr + value to Type` patterns, so this change should not break existing tests.

### Risk Assessment

This change alters parsing behavior. Any Sharpy code that currently relies on `to` binding tighter than arithmetic will change behavior. For example:

```python
# Before (broken): x + (1 to float)  -- 1 is coerced, then added to x
# After  (correct): (x + 1) to float  -- sum is coerced
x + 1 to float
```

This is the correct fix per the spec. Run the full test suite to verify no regressions.

### Testing

**File: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/basics/to_operator_precedence_0001.spy`**

```python
def main():
    x: int = 5
    # Should parse as (x + 1) to float, not x + (1 to float)
    # Both produce 6.0 for this case, but test with a float result
    result: float = x + 1 to float
    print(result to int)

    # Member access should still bind tighter than to
    # dims.length to int => (dims.length) to int
    y: float = 3.14
    z: int = y to int
    print(z)
```

**File: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/basics/to_operator_precedence_0001.expected`**

```
6
3
```

### Commit Message

```
fix: Move `to` operator from postfix to correct precedence level

The spec puts `to` at level 11 (between pipe and comparisons),
but it was parsed at postfix level (level 1). This caused
`x + 1 to float` to parse as `x + (1 to float)` instead of
`(x + 1) to float`.
```

### Verification Command

```bash
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"
```

Run the full file-based test suite since this changes parsing behavior globally.

---

## Task 3: Interface Default Methods

### Context

The language specification (`docs/language_specification/interface_default_methods.md`) describes interface default method support, mapping directly to C# 8.0+ default interface methods. Currently, the compiler actively rejects interface methods with bodies. The error is produced in `NameResolver.cs` (line 718): `"Interface method 'X' in interface 'Y' cannot have an implementation. Use '...' or 'pass' instead"` (SPY0251).

The existing test fixture `errors/interface_method_with_body.spy` specifically tests that this error is produced.

### Files to Modify

| File | Purpose |
|------|---------|
| `src/Sharpy.Compiler/Semantic/NameResolver.cs` | Relax `ValidateInterfaceMethod` to allow non-ellipsis/pass bodies |
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.cs` | Update `GenerateInterfaceMethod` to emit body for default methods |
| `src/Sharpy.Compiler.Tests/Integration/TestFixtures/errors/interface_method_with_body.spy` | Update or replace test to reflect new behavior |
| `src/Sharpy.Compiler.Tests/Integration/TestFixtures/errors/interface_method_with_body.error` | Update or replace |

### Step-by-Step Implementation

**Step 1: Relax the semantic validation in `NameResolver.cs`.**

File: `src/Sharpy.Compiler/Semantic/NameResolver.cs`, method `ValidateInterfaceMethod` (line 692).

Currently the method rejects any body that is not `...` or `pass`. Change it to accept any body:

Replace the entire `ValidateInterfaceMethod` method:

```csharp
private void ValidateInterfaceMethod(FunctionDef method, string interfaceName)
{
    // Interface methods can have:
    // 1. ... (ellipsis) or pass -> abstract (no C# body)
    // 2. A real body -> default implementation (C# 8.0+ default interface method)

    if (method.Body.Length == 0)
    {
        AddError($"Interface method '{method.Name}' in interface '{interfaceName}' must have a body with '...' or 'pass'",
            method.LineStart, method.ColumnStart, code: DiagnosticCodes.Semantic.InterfaceMethodBody, span: method.Span);
    }

    // Any non-empty body is now valid -- either abstract (ellipsis/pass) or default implementation
}
```

**Step 2: Update `GenerateInterfaceMethod` in `RoslynEmitter.ClassMembers.cs`.**

File: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.cs`, method `GenerateInterfaceMethod` (line 968).

Currently, all interface methods are emitted with a semicolon (no body). Change this to distinguish between abstract methods (ellipsis/pass body) and default methods (real body):

Replace the `GenerateInterfaceMethod` method:

```csharp
private MethodDeclarationSyntax GenerateInterfaceMethod(FunctionDef func)
{
    var mangledName = DunderMapping.ResolveCSharpName(func.Name)
        ?? NameMangler.Transform(func.Name, NameContext.Method);

    // Determine return type from annotation or infer void
    TypeSyntax returnType = func.ReturnType != null
        ? _typeMapper.MapType(func.ReturnType)
        : PredefinedType(Token(SyntaxKind.VoidKeyword));

    // Interface methods skip 'self' parameter
    var parameters = func.Parameters
        .Where(p => p.Name != "self")
        .Select(GenerateParameter)
        .ToArray();

    var method = MethodDeclaration(returnType, mangledName)
        .WithParameterList(ParameterList(SeparatedList(parameters)));

    // Check if this is an abstract method (body is single ellipsis or pass)
    bool isAbstract = func.Body.Length == 1 &&
        (func.Body[0] is PassStatement ||
         (func.Body[0] is ExpressionStatement es && es.Expression is EllipsisLiteral));

    if (isAbstract)
    {
        // Abstract interface method: no body, just semicolon
        method = method.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
    }
    else
    {
        // Default interface method: emit the full body
        // Clear scope tracking for the method body
        _declaredVariables.Clear();
        _variableVersions.Clear();
        _constVariables.Clear();
        _sourceVariableNames.Clear();
        _narrowedOptionals.Clear();
        _isNullableNarrowing.Clear();
        CollectSourceVariableNames(func.Body);

        // Track parameters as declared variables (skip self)
        foreach (var param in func.Parameters)
        {
            if (string.Equals(param.Name, "self", StringComparison.OrdinalIgnoreCase))
                continue;
            var paramName = NameMangler.Transform(param.Name, NameContext.Parameter);
            _declaredVariables.Add(paramName);
            var baseName = NameMangler.ToCamelCase(param.Name);
            _variableVersions[baseName] = 0;
        }

        var bodyStatements = func.Body
            .Select(GenerateBodyStatement)
            .OfType<StatementSyntax>();
        method = method.WithBody(Block(bodyStatements));
    }

    // Add XML documentation from docstring if present
    if (!string.IsNullOrEmpty(func.DocString))
    {
        method = method.WithLeadingTrivia(GenerateXmlDocComment(func.DocString));
    }

    return method;
}
```

**Step 3: Update the existing error test fixture.**

The current error test `errors/interface_method_with_body.spy` expects an error for an interface method with a body. Since we are now allowing bodies, this test should be converted to a success test and moved.

**Option A:** Delete the error test and create a new success test.
**Option B:** Modify the error test to test a different invalid case (e.g., empty body).

**Recommendation:** Delete the old error test files and create a new success test.

Delete:
- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/errors/interface_method_with_body.spy`
- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/errors/interface_method_with_body.error`

**Step 4: Check if TypeChecker needs updates.**

The TypeChecker processes interface methods during type checking. Since we are now allowing bodies in interface methods, verify that the TypeChecker already handles method bodies in interface context. Search:

```bash
grep -n "interface" src/Sharpy.Compiler/Semantic/TypeChecker.Definitions.cs
```

The TypeChecker should already type-check method bodies regardless of whether they are in a class or interface context. If it skips interface method bodies, you will need to add body checking. However, since the TypeChecker dispatches to `CheckFunctionDef` for all function definitions, it likely already handles this.

### Testing

**File: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/interfaces/interface_default_method_0001.spy`**

```python
interface IGreeter:
    def greet(self) -> str: ...

    def greet_loudly(self) -> str:
        return self.greet().upper()

class SimpleGreeter(IGreeter):
    def greet(self) -> str:
        return "hello"

class CustomGreeter(IGreeter):
    def greet(self) -> str:
        return "howdy"

    @override
    def greet_loudly(self) -> str:
        return "HOWDY PARTNER"

def main():
    s = SimpleGreeter()
    print(s.greet())
    # Note: Default interface methods in C# require calling through the interface
    g: IGreeter = s
    print(g.greet_loudly())

    c = CustomGreeter()
    print(c.greet())
    print(c.greet_loudly())
```

**File: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/interfaces/interface_default_method_0001.expected`**

```
hello
HELLO
howdy
HOWDY PARTNER
```

**Important caveat about C# default interface methods:** In C#, default interface methods are only callable through an interface reference, not through the concrete class reference. This means `s.greet_loudly()` will fail if `SimpleGreeter` does not override it -- you must cast to `IGreeter` first: `((IGreeter)s).greet_loudly()`. This is a C# runtime limitation.

If this caveat makes the test more complex than desired, use a simpler test where all default methods are called through interface-typed variables. Adjust the `.spy` and `.expected` files accordingly.

Alternatively, if this C# limitation turns out to be problematic, consider emitting default interface methods as regular methods on the class in addition to the interface. This is a larger scope change and should be deferred.

For the initial implementation, test with interface-typed variables only, as shown above.

### Commit Message

```
feat: Support interface default methods

Relax NameResolver validation to allow interface method bodies.
Emit default interface methods with full bodies in
GenerateInterfaceMethod when the body is not ellipsis/pass.
```

### Verification Command

```bash
dotnet test --filter "DisplayName~interface_default_method"
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"
```

---

## Task 4: `str()` Builtin Function

### Context

The `str()` builtin is documented in the spec (`docs/language_specification/builtin_functions.md`) as converting any value to its string representation by calling `.ToString()`. It is used in common patterns like `str(42)` to produce `"42"`.

Currently, there is no `Str()` method in `Sharpy.Builtins`. The BuiltinRegistry auto-discovers functions from the `Sharpy.Core` assembly via reflection (`CachedModuleDiscovery`), so adding `Str(...)` overloads to the `Builtins` partial class is sufficient -- the compiler will automatically discover and register them.

### Files to Modify

| File | Purpose |
|------|---------|
| `src/Sharpy.Core/Builtins/Str.cs` | **New file** -- `Str()` overloads following the `Int.cs` pattern |

Note: This is one of the rare cases where a new file is required. The Builtins directory uses one file per builtin function (e.g., `Int.cs`, `Bool.cs`, `Len.cs`).

### Step-by-Step Implementation

**Step 1: Create `Str.cs` in the Builtins directory.**

File: `src/Sharpy.Core/Builtins/Str.cs`

Follow the exact pattern from `Int.cs` (namespace, partial class, overloads):

```csharp
using System;
namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Convert an object to its string representation.
        /// Calls .ToString() on the value, matching Python's str() behavior.
        /// </summary>
        public static string Str(object? obj)
        {
            if (obj is null)
            {
                return "None";
            }

            return obj.ToString() ?? "";
        }

        /// <summary>
        /// Convert a string to string (identity).
        /// </summary>
        public static string Str(string s)
        {
            return s;
        }

        /// <summary>
        /// Convert an int to string.
        /// </summary>
        public static string Str(int i)
        {
            return i.ToString();
        }

        /// <summary>
        /// Convert a long to string.
        /// </summary>
        public static string Str(long l)
        {
            return l.ToString();
        }

        /// <summary>
        /// Convert a double to string.
        /// </summary>
        public static string Str(double d)
        {
            return d.ToString();
        }

        /// <summary>
        /// Convert a float to string.
        /// </summary>
        public static string Str(float f)
        {
            return f.ToString();
        }

        /// <summary>
        /// Convert a bool to string.
        /// Returns "True" or "False" matching Python's str(True)/str(False).
        /// </summary>
        public static string Str(bool b)
        {
            return b ? "True" : "False";
        }
    }
}
```

**Step 2: Verify auto-discovery works.**

The `BuiltinRegistry` uses `CachedModuleDiscovery` to find all `public static` methods in types marked with `[SharpyModule("builtins")]`. Since `Builtins` has this attribute and `Str` will be a new `public static` method on it, discovery should work automatically.

However, the NameMangler transforms `str(...)` to `Str(...)` via PascalCase. The builtin discovery maps Python names to C# names. Verify that `str` is mapped:

```bash
grep -n "\"str\"" src/Sharpy.Compiler/Semantic/BuiltinRegistry.cs
grep -rn "\"str\"" src/Sharpy.Compiler/Discovery/
```

The discovery system should handle this automatically because:
1. The Sharpy source code calls `str(x)`.
2. The compiler looks up `str` in the BuiltinRegistry.
3. The registry auto-discovers `Str(...)` from the Sharpy.Core assembly.
4. The name mangler converts `str` to `Str` for the C# call.

If the discovery does not find it (because it is looking for an exact Python-name match), check how `int` maps to `Int` in the discovery pipeline and ensure `str` follows the same path.

### Testing

**File: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/builtins/str_builtin_0001.spy`**

First, create the `builtins` test directory if it does not exist:

```bash
mkdir -p src/Sharpy.Compiler.Tests/Integration/TestFixtures/builtins
```

```python
def main():
    print(str(42))
    print(str(3.14))
    print(str(True))
    print(str(False))
    print(str("hello"))
```

**File: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/builtins/str_builtin_0001.expected`**

```
42
3.14
True
False
hello
```

Note: The exact output of `str(3.14)` depends on `double.ToString()` in C#, which produces `"3.14"` by default. Verify with:

```bash
python3 -c "print(str(3.14))"
dotnet run --project src/Sharpy.Cli -- run snippets/test.spy
```

If C# produces a different format (e.g., `3.14` vs `3.1400000000000001`), adjust the `.expected` file accordingly. C# `double.ToString()` for `3.14` produces `"3.14"`, so this should match.

### Commit Message

```
feat: Add str() builtin function

Add Str() overloads to Sharpy.Core Builtins for int, long, double,
float, bool, string, and object types. Follows the same pattern
as Int.cs and Bool.cs.
```

### Verification Command

```bash
dotnet test --filter "DisplayName~str_builtin"
```

---

## Task 5: `id()` Builtin Function

### Context

The `id()` builtin is documented in the spec (`docs/language_specification/builtin_functions.md`) as returning the object identity, mapping to `RuntimeHelpers.GetHashCode()`. This function returns a unique integer for each object instance (based on its identity, not its value).

### Files to Modify

| File | Purpose |
|------|---------|
| `src/Sharpy.Core/Builtins/Id.cs` | **New file** -- `Id()` method |

### Step-by-Step Implementation

**Step 1: Verify `RuntimeHelpers.GetHashCode` availability in netstandard2.0/2.1.**

`System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(object)` is available in both `netstandard2.0` and `netstandard2.1`. It returns the default hash code for the object (identity-based, like Python's `id()`).

**Step 2: Create `Id.cs` in the Builtins directory.**

File: `src/Sharpy.Core/Builtins/Id.cs`

```csharp
using System.Runtime.CompilerServices;
namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Return the identity of an object.
        /// This is an integer which is guaranteed to be unique and constant
        /// for this object during its lifetime.
        /// Maps to RuntimeHelpers.GetHashCode() which returns the sync block index.
        /// </summary>
        public static int Id(object obj)
        {
            if (obj is null)
            {
                throw new TypeError("id() argument must not be None");
            }

            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
```

**Step 3: Verify auto-discovery.**

Same as Task 4. The function name `id` will be mangled to `Id` by PascalCase conversion, matching the C# method name. The discovery pipeline should find it automatically.

### Testing

**File: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/builtins/id_builtin_0001.spy`**

```python
def main():
    a: list[int] = [1, 2, 3]
    b: list[int] = [1, 2, 3]
    c = a

    # a and c should have the same id (same object)
    print(id(a) == id(c))

    # a and b should have different ids (different objects)
    print(id(a) == id(b))
```

**File: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/builtins/id_builtin_0001.expected`**

```
True
False
```

Note: `id(a) == id(b)` being `False` is not guaranteed by the runtime (hash codes could theoretically collide), but in practice `RuntimeHelpers.GetHashCode` returns distinct values for distinct objects. If this test is flaky, change the test to only verify that `id(a) == id(c)` is `True`:

```python
def main():
    a: list[int] = [1, 2, 3]
    c = a
    print(id(a) == id(c))
```

```
True
```

### Commit Message

```
feat: Add id() builtin function

Add Id() to Sharpy.Core Builtins using
RuntimeHelpers.GetHashCode() for object identity,
matching the spec definition.
```

### Verification Command

```bash
dotnet test --filter "DisplayName~id_builtin"
```

---

## Implementation Order

The tasks have no dependencies on each other. However, the recommended order is:

1. **Task 4: `str()` builtin** -- Simplest, self-contained in Sharpy.Core, no compiler changes.
2. **Task 5: `id()` builtin** -- Same pattern as Task 4, equally simple.
3. **Task 1: Walrus operator codegen** -- Moderate complexity, touches only CodeGen.
4. **Task 2: `to` operator precedence** -- Moderate complexity, touches only Parser. Run full test suite after.
5. **Task 3: Interface default methods** -- Highest complexity, touches Semantic + CodeGen + existing test fixtures.

Each task should be committed separately after verifying all tests pass:

```bash
dotnet test && dotnet format whitespace
```
