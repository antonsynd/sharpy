# Phase 1: Bug Fixes (Immediate, Small Effort)

This document provides step-by-step guidance for fixing four bugs in the Sharpy compiler. Each bug fix is designed to be an independent commit. Work through them in order.

---

## General Guidance

### Running the compiler and tests

```bash
# Build everything
dotnet build sharpy.sln

# Run all tests
dotnet test

# Run specific test suites
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"   # File-based integration tests
dotnet test --filter "FullyQualifiedName~Lexer"                      # Lexer tests only

# Compile and run a .spy file
dotnet run --project src/Sharpy.Cli -- run file.spy

# Inspect generated C# (useful for debugging codegen)
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy

# Format before committing (required by CI)
dotnet format whitespace
```

### Principles

1. **Make the smallest correct change.** Do not refactor unrelated code. Do not add features beyond the bug fix.
2. **Never modify `.expected` files to make tests pass.** Fix the implementation instead.
3. **Verify Python behavior first.** Before implementing Python semantics, run `python3 -c "..."` to confirm the expected behavior. For example: `python3 -c "print(3 * 'ab')"`.
4. **Use `dotnet run --project src/Sharpy.Cli -- emit csharp file.spy`** to inspect what C# the compiler generates. This is the fastest way to see if your codegen change is correct.
5. **Run the full test suite before committing.** A single `dotnet test` catches regressions.

### When to ask for help vs. push forward

- If you are unsure which of two approaches is architecturally better, read this document's "Decision guidance" section for that bug. If still unsure, ask.
- If all existing tests pass and your new test passes, you are almost certainly done. Push forward.
- If an existing test breaks and you do not understand why, stop and ask.

---

## Bug 1: `@final` decorator silently ignored

### Context

The Sharpy language specification says `@final` makes a class sealed (cannot be inherited) or a method sealed (cannot be overridden). The codegen currently checks for the string `"sealed"` but users write `@final` in Sharpy source. Since `"final"` is not recognized, the decorator is silently dropped and has no effect.

### Files to modify

| File | Purpose |
|------|---------|
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.TypeDeclarations.cs` | Type-level modifiers (classes, structs) |
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.cs` | Method-level modifiers |

### Step-by-step implementation

**Step 1: Add `"final"` to `GenerateTypeModifiersFromDecorators` (type-level)**

Open `src/Sharpy.Compiler/CodeGen/RoslynEmitter.TypeDeclarations.cs`. Find the method `GenerateTypeModifiersFromDecorators` (line 825). Inside the second `foreach` loop (line 861-875), there is a `switch` statement that handles `"abstract"`, `"sealed"`, and `"static"`. Add `"final"` as a case that emits the `SealedKeyword`, right next to the existing `"sealed"` case:

```csharp
// Before (lines 863-874):
switch (decorator.Name)
{
    case "abstract":
        tokens.Add(Token(SyntaxKind.AbstractKeyword));
        break;
    case "sealed":
        tokens.Add(Token(SyntaxKind.SealedKeyword));
        break;
    case "static":
        tokens.Add(Token(SyntaxKind.StaticKeyword));
        break;
}

// After:
switch (decorator.Name)
{
    case "abstract":
        tokens.Add(Token(SyntaxKind.AbstractKeyword));
        break;
    case "sealed":
    case "final":
        tokens.Add(Token(SyntaxKind.SealedKeyword));
        break;
    case "static":
        tokens.Add(Token(SyntaxKind.StaticKeyword));
        break;
}
```

**Step 2: Add `"final"` to `GenerateMethodModifiersFromDecorators` (method-level)**

Open `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.cs`. Find the method `GenerateMethodModifiersFromDecorators` (line 815). Inside the second `foreach` loop (lines 853-868), there is a `switch` statement for `"static"`, `"abstract"`, `"virtual"`, and `"override"`. Add a `"final"` case that emits `SealedKeyword`:

```csharp
// Before (lines 853-868):
switch (decorator.Name)
{
    case "static":
        tokens.Add(Token(SyntaxKind.StaticKeyword));
        break;
    case "abstract":
        tokens.Add(Token(SyntaxKind.AbstractKeyword));
        break;
    case "virtual":
        tokens.Add(Token(SyntaxKind.VirtualKeyword));
        break;
    case "override":
        tokens.Add(Token(SyntaxKind.OverrideKeyword));
        break;
}

// After:
switch (decorator.Name)
{
    case "static":
        tokens.Add(Token(SyntaxKind.StaticKeyword));
        break;
    case "abstract":
        tokens.Add(Token(SyntaxKind.AbstractKeyword));
        break;
    case "virtual":
        tokens.Add(Token(SyntaxKind.VirtualKeyword));
        break;
    case "override":
        tokens.Add(Token(SyntaxKind.OverrideKeyword));
        break;
    case "final":
        tokens.Add(Token(SyntaxKind.SealedKeyword));
        break;
}
```

**Step 3: Also add `"final"` to `GenerateModifiersFromDecorators` (module-level functions)**

Open `src/Sharpy.Compiler/CodeGen/RoslynEmitter.TypeDeclarations.cs`. Find the method `GenerateModifiersFromDecorators` (line 138). Inside the second `foreach` loop (lines 174-191), there is a `switch` for `"static"`, `"abstract"`, `"virtual"`, `"override"`. Add `"final"` here too for completeness, even though `@final` on a module-level function is unusual:

```csharp
// After the "override" case, add:
case "final":
    tokens.Add(Token(SyntaxKind.SealedKeyword));
    break;
```

### Decision guidance

**Q: Should `@final` on a method also require `@override`?**
A: In C#, `sealed` on a method only makes sense with `override` (you cannot seal a non-virtual method). However, adding that validation is beyond the scope of this bug fix. The user may already have `@override` alongside `@final`. If they do not, the C# compiler will report the error, which is acceptable for now. File a follow-up issue for a Sharpy-level diagnostic if desired.

**Q: Should we also handle `@sealed`?**
A: `@sealed` already works (see the existing `case "sealed"` in `GenerateTypeModifiersFromDecorators`). This fix adds the Pythonic `@final` as a synonym.

### Testing

Create two test fixture files:

**File: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/classes/final_class_0001.spy`**
```python
@final
class Immutable:
    x: int

    def __init__(self, x: int):
        self.x = x

    def get_x(self) -> int:
        return self.x

obj = Immutable(42)
print(obj.get_x())
```

**File: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/classes/final_class_0001.expected`**
```
42
```

**File: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/classes/final_method_0001.spy`**
```python
class Base:
    @virtual
    def greet(self) -> str:
        return "Hello from Base"

class Child(Base):
    @final
    @override
    def greet(self) -> str:
        return "Hello from Child"

c = Child()
print(c.greet())
```

**File: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/classes/final_method_0001.expected`**
```
Hello from Child
```

### Verification

```bash
# Run the file-based integration tests (includes new fixtures)
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"

# Inspect the generated C# to verify sealed is present
dotnet run --project src/Sharpy.Cli -- emit csharp \
  src/Sharpy.Compiler.Tests/Integration/TestFixtures/classes/final_class_0001.spy
# You should see: public sealed class Immutable

dotnet run --project src/Sharpy.Cli -- emit csharp \
  src/Sharpy.Compiler.Tests/Integration/TestFixtures/classes/final_method_0001.spy
# You should see: public sealed override string Greet()
```

### Commit message

```
fix: Handle @final decorator for classes and methods

The @final decorator was silently ignored because codegen only checked
for "sealed" string. Add "final" as a recognized decorator that emits
the C# sealed keyword, matching the language specification.
```

---

## Bug 2: `raise X from Y` silently discards exception cause

### Context

Python's `raise X from Y` sets the `__cause__` attribute on the exception, which becomes `InnerException` in .NET. The parser already populates `RaiseStatement.Cause` (see `src/Sharpy.Compiler/Parser/Ast/Statement.cs` lines 169-182). However, both the type checker and codegen ignore it entirely. The `from Y` clause is silently dropped.

### Files to modify

| File | Purpose |
|------|---------|
| `src/Sharpy.Compiler/Semantic/TypeChecker.Statements.cs` | Type-check the cause expression |
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs` | Emit C# that preserves the inner exception |

### Step-by-step implementation

**Step 1: Type-check the cause expression in `CheckRaise`**

Open `src/Sharpy.Compiler/Semantic/TypeChecker.Statements.cs`. Find the method `CheckRaise` (line 608). It currently only checks `raiseStmt.Exception` but ignores `raiseStmt.Cause`. Add a check for the cause:

```csharp
// Before (lines 608-622):
private void CheckRaise(RaiseStatement raiseStmt)
{
    // Bare raise (no exception) is only valid inside an except block
    if (raiseStmt.Exception == null && !_inExceptBlock)
    {
        AddError("Bare 'raise' statement can only be used inside an exception handler",
            raiseStmt.LineStart, raiseStmt.ColumnStart, code: DiagnosticCodes.Semantic.InvalidRaise,
            span: raiseStmt.Span);
    }

    if (raiseStmt.Exception != null)
    {
        CheckExpression(raiseStmt.Exception);
    }
}

// After:
private void CheckRaise(RaiseStatement raiseStmt)
{
    // Bare raise (no exception) is only valid inside an except block
    if (raiseStmt.Exception == null && !_inExceptBlock)
    {
        AddError("Bare 'raise' statement can only be used inside an exception handler",
            raiseStmt.LineStart, raiseStmt.ColumnStart, code: DiagnosticCodes.Semantic.InvalidRaise,
            span: raiseStmt.Span);
    }

    if (raiseStmt.Exception != null)
    {
        CheckExpression(raiseStmt.Exception);
    }

    if (raiseStmt.Cause != null)
    {
        CheckExpression(raiseStmt.Cause);
    }
}
```

**Step 2: Emit C# that wraps the cause as an inner exception in `GenerateRaise`**

Open `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs`. Find the method `GenerateRaise` (line 714). Currently it is:

```csharp
private StatementSyntax GenerateRaise(RaiseStatement raise)
{
    if (raise.Exception != null)
    {
        var exception = GenerateExpression(raise.Exception);
        return ThrowStatement(exception);
    }

    // Re-throw the current exception
    return ThrowStatement();
}
```

Replace it with code that handles the `Cause` property. When `raise X from Y` is used, we need to generate C# that passes Y as the inner exception. The most robust pattern is:

```csharp
// Sharpy: raise ValueError("msg") from original_error
// C#:
//   var __cause = original_error;
//   throw new ValueError("msg") { InnerException = __cause };
//
// But this doesn't work because InnerException's setter is not public.
// Instead, we need to use a helper approach. The simplest correct approach:
//   throw new ValueError("msg", original_error);  // if constructor accepts inner exception
//
// However, we cannot assume all exception types have a 2-arg constructor.
// The most general approach that works in .NET:
//   var __exc = new ValueError("msg");
//   // Use reflection or ExceptionDispatchInfo? No, too heavy.
//
// Actually, the standard .NET pattern is to use the constructor overload:
//   throw new Exception(message, innerException)
// All standard .NET exceptions have this constructor.
//
// The cleanest codegen approach: when Cause is present and the exception is
// a constructor call (FunctionCall), inject the cause as an additional argument.
```

The implementation should handle two cases:
1. **Exception is a constructor call** (e.g., `raise ValueError("msg") from cause`): Inject the cause as a second argument to the constructor.
2. **Exception is a variable** (e.g., `raise ex from cause`): This is rare but valid in Python. For this case, we cannot easily set `InnerException` in C# without reflection. For v1, emit a diagnostic or use a helper. However, Python's `raise ex from cause` creates a *new* exception chain, not modifying `ex`. The simplest correct approach for all cases is to use a block with a temp variable.

Here is the recommended implementation:

```csharp
private StatementSyntax GenerateRaise(RaiseStatement raise)
{
    if (raise.Exception != null)
    {
        var exception = GenerateExpression(raise.Exception);

        // raise X from Y: inject cause as inner exception
        if (raise.Cause != null)
        {
            var cause = GenerateExpression(raise.Cause);

            // If the exception is a constructor call (ObjectCreationExpression),
            // append the cause as an additional constructor argument.
            // All standard .NET exception types accept (string, Exception) or
            // (string message, Exception innerException).
            if (exception is ObjectCreationExpressionSyntax creation)
            {
                var existingArgs = creation.ArgumentList?.Arguments ?? SeparatedList<ArgumentSyntax>();
                var newArgs = existingArgs.Add(Argument(cause));
                var newCreation = creation.WithArgumentList(ArgumentList(newArgs));
                return ThrowStatement(newCreation);
            }

            // For non-constructor expressions (e.g., raise some_var from cause),
            // we cannot inject inner exception without reflection.
            // Fall through to throw without cause (best-effort).
            // TODO: Consider emitting a diagnostic here.
        }

        return ThrowStatement(exception);
    }

    // Re-throw the current exception
    return ThrowStatement();
}
```

### Decision guidance

**Q: Should we handle the case where the exception is a variable (not a constructor call)?**
A: For this bug fix, focus on the common case: `raise ExType(...) from cause`. The case `raise some_variable from cause` is rare in practice and would require runtime reflection or a helper method to set `InnerException`. Leave that as a follow-up with a TODO comment. The critical improvement is that the common case now works instead of silently dropping the cause.

**Q: What if the exception type does not have a constructor that accepts an inner exception?**
A: All standard .NET exception types do. User-defined exception types that inherit from `Exception` inherit its `(string, Exception)` constructor by default. If a user defines a custom exception without that constructor, the C# compiler will report the error, which is acceptable.

**Q: The exception expression could be an `InvocationExpression` (Sharpy constructors are function calls). How do I detect it?**
A: In the generated Roslyn AST, constructor calls produce `ObjectCreationExpressionSyntax` nodes (via `ObjectCreationExpression(...)`). The `GenerateExpression` call for a class instantiation returns this type. You can check with `is ObjectCreationExpressionSyntax creation`.

### Testing

**File: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/exception_handling/raise_from_0001.spy`**
```python
def main():
    try:
        try:
            x: int = int("not_a_number")
        except Exception as original:
            raise ValueError("conversion failed") from original
    except ValueError as ve:
        print(ve.Message)
        if ve.InnerException is not None:
            print("Has inner exception")
        else:
            print("No inner exception")
```

**File: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/exception_handling/raise_from_0001.expected`**
```
conversion failed
Has inner exception
```

**Important note:** The exact error message for `ValueError("conversion failed")` should be `"conversion failed"`. The `InnerException` check verifies the cause was preserved.

If the `ValueError` constructor does not exist as a Sharpy builtin, use `Exception` instead:

**Alternative (if ValueError is not available):**
```python
def main():
    try:
        try:
            x: int = int("not_a_number")
        except Exception as original:
            raise Exception("conversion failed") from original
    except Exception as e:
        print(e.Message)
        if e.InnerException is not None:
            print("Has inner exception")
        else:
            print("No inner exception")
```

Check which exception types are available by looking at `src/Sharpy.Core/Builtins/Exceptions.cs`.

### Verification

```bash
# Run exception handling tests
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"

# Inspect generated C# to verify inner exception is passed
dotnet run --project src/Sharpy.Cli -- emit csharp \
  src/Sharpy.Compiler.Tests/Integration/TestFixtures/exception_handling/raise_from_0001.spy
# You should see something like: throw new Exception("conversion failed", original)
```

### Commit message

```
fix: Preserve exception cause in raise...from statements

The parser populates RaiseStatement.Cause for `raise X from Y`, but
both the type checker and codegen ignored it. Now the cause expression
is type-checked and injected as an inner exception argument in the
generated constructor call.
```

---

## Bug 3: `int * str` reversed string repetition not supported

### Context

Python allows both `"ab" * 3` and `3 * "ab"`, both producing `"ababab"`. In Sharpy, `str * int` works (the type inference service recognizes it at line 194-198 of `TypeInferenceService.cs`), but `int * str` fails because there is no symmetric case.

Additionally, the codegen for `str * int` currently falls through to the standard `BinaryExpression(SyntaxKind.MultiplyExpression, left, right)`, which generates `left * right` in C#. Since C# `string` does not have an `operator *`, this generates invalid C#. Both the semantic analysis and codegen need fixes.

### Files to modify

| File | Purpose |
|------|---------|
| `src/Sharpy.Compiler/Semantic/TypeInferenceService.cs` | Add `int * str` type inference |
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs` | Emit valid C# for `str * int` and `int * str` |

### Step-by-step implementation

**Step 1: Verify Python behavior**

```bash
python3 -c "print(3 * 'ab')"
# Expected: ababab

python3 -c "print('ab' * 3)"
# Expected: ababab

python3 -c "print('ab' * 0)"
# Expected: (empty string)

python3 -c "print('ab' * -1)"
# Expected: (empty string)
```

**Step 2: Add `int * str` to type inference**

Open `src/Sharpy.Compiler/Semantic/TypeInferenceService.cs`. Find the string repetition block (lines 194-199):

```csharp
// String repetition
if (left == SemanticType.Str && right == SemanticType.Int)
{
    if (op == BinaryOperator.Multiply)
        return SemanticType.Str;
}
```

Add a symmetric case immediately after:

```csharp
// String repetition
if (left == SemanticType.Str && right == SemanticType.Int)
{
    if (op == BinaryOperator.Multiply)
        return SemanticType.Str;
}

// String repetition (reversed: int * str)
if (left == SemanticType.Int && right == SemanticType.Str)
{
    if (op == BinaryOperator.Multiply)
        return SemanticType.Str;
}
```

**Step 3: Emit valid C# for string repetition in codegen**

Open `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`. Find the `GenerateBinaryOp` method (line 393). We need to intercept `Multiply` when one operand is `str` and the other is `int`, and emit a helper call instead of `left * right`.

The C# equivalent for string repetition is:
```csharp
string.Concat(System.Linq.Enumerable.Repeat(s, n))
```

This handles `n <= 0` correctly (returns empty string).

Add a special case **before** the standard binary operators `switch` statement (before line 552 where `var kind = binOp.Operator switch`). Insert it right after the `case BinaryOperator.NotIn:` block ends (after line 466). A good location is just before the comment `// Standard binary operators` (line 552):

Actually, the best place is to add it as a new case in the special-cases `switch` block at the top of `GenerateBinaryOp`. However, since the current special-cases use a `switch` on `binOp.Operator`, and we need to check types as well, add it as an `if` block just before the `// Standard binary operators` comment at line 552:

```csharp
        // String repetition: str * int or int * str
        // C# string doesn't have operator *, so emit: string.Concat(Enumerable.Repeat(s, n))
        if (binOp.Operator == BinaryOperator.Multiply)
        {
            var leftType = GetExpressionSemanticType(binOp.Left);
            var rightType = GetExpressionSemanticType(binOp.Right);

            ExpressionSyntax? strExpr = null;
            ExpressionSyntax? countExpr = null;

            if (leftType == SemanticType.Str && IsIntegerSemanticType(rightType))
            {
                strExpr = left;
                countExpr = right;
            }
            else if (IsIntegerSemanticType(leftType) && rightType == SemanticType.Str)
            {
                strExpr = right;
                countExpr = left;
            }

            if (strExpr != null && countExpr != null)
            {
                // string.Concat(System.Linq.Enumerable.Repeat(strExpr, countExpr))
                var repeatCall = InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("System"),
                                IdentifierName("Linq")),
                            IdentifierName("Enumerable")),
                        IdentifierName("Repeat")))
                    .AddArgumentListArguments(
                        Argument(strExpr),
                        Argument(countExpr));

                return InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        PredefinedType(Token(SyntaxKind.StringKeyword)),
                        IdentifierName("Concat")))
                    .AddArgumentListArguments(Argument(repeatCall));
            }
        }

        // Standard binary operators
        var kind = binOp.Operator switch
        // ... existing code ...
```

You also need a helper method `IsIntegerSemanticType`. Check if one already exists in the emitter. If not, add a simple private method:

```csharp
private static bool IsIntegerSemanticType(SemanticType? type)
{
    return type == SemanticType.Int || type == SemanticType.Long;
}
```

If there's already a similar helper (like `IsFloatExpression`), use the same pattern. Look for existing `IsFloat*` or `IsInteger*` methods in the emitter to match the naming convention.

### Decision guidance

**Q: Should we use `string.Concat(Enumerable.Repeat(...))` or `new string(...)` or a Sharpy.Core helper?**
A: Use `string.Concat(System.Linq.Enumerable.Repeat(s, n))`. Rationale:
- It works with `string` (not just `Str`), which is what `str` maps to in the current type mapper.
- It handles edge cases: `n <= 0` returns empty string (Enumerable.Repeat with count 0 produces an empty sequence, and string.Concat of empty produces "").
- It uses only BCL types, no Sharpy.Core dependency needed.
- The fully qualified `System.Linq.Enumerable` avoids namespace conflicts.

**Note on `Enumerable.Repeat` with negative count:** `Enumerable.Repeat` throws `ArgumentOutOfRangeException` for negative counts. Python returns empty string for `"ab" * -1`. To match Python semantics, you could wrap countExpr in `System.Math.Max(0, countExpr)`:

```csharp
var safeCount = InvocationExpression(
    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
            IdentifierName("System"),
            IdentifierName("Math")),
        IdentifierName("Max")))
    .AddArgumentListArguments(
        Argument(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0))),
        Argument(countExpr));
```

Use `safeCount` in place of `countExpr` in the `Repeat` call. This is recommended for correctness.

**Q: What about `str * long`?**
A: `Enumerable.Repeat` takes an `int`, not `long`. Since string repetition with long counts is impractical (would exceed memory), casting to `int` is fine. You can add a cast if the count type is `Long`. However, for this bug fix, supporting `int` is sufficient.

### Testing

**File: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/strings/str_repeat_0001.spy`**
```python
print("ab" * 3)
print(3 * "ab")
print("x" * 0)
print("y" * 1)
```

**File: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/strings/str_repeat_0001.expected`**
```
ababab
ababab

y
```

(Note: `"x" * 0` produces an empty string, which means the third line of output is blank.)

### Verification

```bash
# Verify Python behavior first
python3 -c "print('ab' * 3); print(3 * 'ab'); print('x' * 0); print('y' * 1)"

# Run the integration tests
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"

# Inspect generated C# for the new test
dotnet run --project src/Sharpy.Cli -- emit csharp \
  src/Sharpy.Compiler.Tests/Integration/TestFixtures/strings/str_repeat_0001.spy
# Should see: string.Concat(System.Linq.Enumerable.Repeat("ab", 3))
# NOT: "ab" * 3
```

### Commit message

```
fix: Support int * str string repetition and emit valid C# for str * int

Python allows both str * int and int * str for string repetition. The
type inference only handled str * int. Additionally, the codegen emitted
raw C# multiply which is invalid for strings. Now emits
string.Concat(Enumerable.Repeat(s, Math.Max(0, n))) for both orderings.
```

---

## Bug 4: `abs()` in wrong module

### Context

Python's `abs()` is a builtin function available without any import. In Sharpy, `abs()` is currently only available in the `operator` module (`src/Sharpy.Core/Operator/Abs.cs`), requiring `from operator import abs`. The spec says it should be a builtin, like `print()`, `len()`, `round()`, and `pow()`.

### Files to modify

| File | Purpose |
|------|---------|
| `src/Sharpy.Core/Abs.cs` (new file) | Add `Abs` overloads to `Builtins` partial class |

### Step-by-step implementation

**Step 1: Understand the pattern**

Look at existing builtins that are top-level files in `src/Sharpy.Core/`:
- `Pow.cs` declares `public static partial class Builtins` with `Pow(...)` overloads
- `Round.cs` declares `public static partial class Builtins` with `Round(...)` overloads

These are partial class files that extend the `Builtins` class defined in `src/Sharpy.Core/Builtins/Builtins.cs`. The emitter resolves builtin function calls to `global::Sharpy.Builtins.FuncName(...)`.

**Step 2: Create `src/Sharpy.Core/Abs.cs`**

Create a new file `src/Sharpy.Core/Abs.cs` with `Abs` overloads on the `Builtins` partial class, following the same pattern as `Pow.cs` and `Round.cs`:

```csharp
namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Return the absolute value of a number.
        /// Python: <c>abs(x)</c>
        /// </summary>
        public static int Abs(int x)
        {
            return System.Math.Abs(x);
        }

        /// <summary>
        /// Return the absolute value of a number.
        /// Python: <c>abs(x)</c>
        /// </summary>
        public static long Abs(long x)
        {
            return System.Math.Abs(x);
        }

        /// <summary>
        /// Return the absolute value of a number.
        /// Python: <c>abs(x)</c>
        /// </summary>
        public static double Abs(double x)
        {
            return System.Math.Abs(x);
        }

        /// <summary>
        /// Return the absolute value of a number.
        /// Python: <c>abs(x)</c>
        /// </summary>
        public static float Abs(float x)
        {
            return System.Math.Abs(x);
        }

        /// <summary>
        /// Return the absolute value of a number.
        /// Python: <c>abs(x)</c>
        /// </summary>
        public static decimal Abs(decimal x)
        {
            return System.Math.Abs(x);
        }

        /// <summary>
        /// Return the absolute value of a number.
        /// Python: <c>abs(x)</c>
        /// </summary>
        public static short Abs(short x)
        {
            return System.Math.Abs(x);
        }

        /// <summary>
        /// Return the absolute value of a number.
        /// Python: <c>abs(x)</c>
        /// </summary>
        public static sbyte Abs(sbyte x)
        {
            return System.Math.Abs(x);
        }
    }
}
```

**Step 3: Keep the `Operator.Abs` file for backward compatibility**

Do NOT delete `src/Sharpy.Core/Operator/Abs.cs`. Users who already write `from operator import abs` should continue to work. The new `Builtins.Abs` makes it available without import.

**Step 4: Verify the builtin resolution works**

The compiler resolves builtin function calls by checking if a function name is a known builtin. The emitter then generates `global::Sharpy.Builtins.Abs(...)`. The runtime discovers `Abs` on the `Builtins` class via CLR method discovery. Since `Builtins` is a partial class and `Abs.cs` adds methods to it, this should work automatically.

However, you need to verify that the compiler's builtin function detection recognizes `abs`. Check how builtins are discovered:

The compiler uses `CachedModuleDiscovery` to discover methods on the `Sharpy.Builtins` class at compile time. Since `Abs` will now be a method on `Builtins`, it should be automatically discovered. No compiler changes are needed -- just the runtime library addition.

If `abs()` still does not resolve after adding the file, check:
1. That the file is included in the `Sharpy.Core.csproj` build (it should be automatic for .cs files in the project directory).
2. That the namespace is `Sharpy` (not `Sharpy.Builtins`).
3. That the class is `public static partial class Builtins`.

### Decision guidance

**Q: Should I put the file in `src/Sharpy.Core/Builtins/` or in `src/Sharpy.Core/`?**
A: Put it at `src/Sharpy.Core/Abs.cs` (the root). This matches the pattern used by `Pow.cs`, `Round.cs`, `Min.cs`, `Max.cs`, and other builtin function files. The `src/Sharpy.Core/Builtins/` directory contains the main `Builtins.cs` class definition and `Exceptions.cs`, not the individual function files.

**Q: What about Sharpy.Core targeting netstandard2.0/2.1 with C# 9.0?**
A: The code above uses only basic C# features (no file-scoped namespaces, no records, no global usings). It is compatible with C# 9.0 and netstandard2.0/2.1.

**Q: Do I need to update the compiler to recognize `abs` as a builtin?**
A: No. The compiler discovers builtins by reflecting over the `Sharpy.Builtins` class at compile time. Adding a method to the partial class makes it automatically discoverable. The name mangler will convert `abs` to `Abs` (PascalCase), which matches the method name.

### Testing

**File: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/basics/abs_builtin_0001.spy`**
```python
print(abs(-5))
print(abs(3))
print(abs(-2.5))
print(abs(0))
```

**File: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/basics/abs_builtin_0001.expected`**
```
5
3
2.5
0
```

### Verification

```bash
# Verify Python behavior
python3 -c "print(abs(-5)); print(abs(3)); print(abs(-2.5)); print(abs(0))"

# Build (ensures Sharpy.Core compiles with the new file)
dotnet build sharpy.sln

# Run the integration tests
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"

# Quick smoke test
echo 'print(abs(-42))' > /tmp/test_abs.spy
dotnet run --project src/Sharpy.Cli -- run /tmp/test_abs.spy
# Should print: 42
```

### Commit message

```
fix: Make abs() available as a builtin without import

abs() was only available via `from operator import abs`. Add Abs
overloads to the Builtins partial class so it is available globally,
matching Python behavior. The operator module version is kept for
backward compatibility.
```

---

## Summary checklist

| Bug | Key change | Test fixture |
|-----|-----------|--------------|
| 1. `@final` ignored | Add `"final"` case in 3 modifier methods | `classes/final_class_0001`, `classes/final_method_0001` |
| 2. `raise from` dropped | Type-check cause + inject as inner exception arg | `exception_handling/raise_from_0001` |
| 3. `int * str` unsupported | Add symmetric type inference + emit `string.Concat(Repeat(...))` | `strings/str_repeat_0001` |
| 4. `abs()` in wrong module | Add `Abs.cs` to Builtins partial class | `basics/abs_builtin_0001` |

After all four fixes, run the full test suite one final time:

```bash
dotnet test
dotnet format whitespace
```
