# Plan: Dogfood C3 Compiler Bug Fixes + Prompt Improvements

**Date**: 2026-02-24
**Context**: Dogfood run 2026-02-24 surfaced 5 compiler bugs (C3), 1 borderline C3/C4 (`Some()` in arguments), 14 prompting issues (C1), and 6 retry flow issues (C5). This plan addresses all of them in 9 incremental commits.

**Commit strategy**: Each commit is independently buildable and testable. Compiler fixes come first (Commits 1–6) because they unblock dogfood scenarios. Prompt improvements follow (Commits 7–9) because they depend on the compiler being correct.

---

## Commit 1: Fix protocol property virtual dispatch (`__bool__`, `__len__`)

### Problem

`GenerateBoolProperty()` and `GenerateLenProperty()` in `RoslynEmitter.ClassMembers.cs` hardcode `public` as the only modifier. When a class defines `@abstract __bool__()` or `@virtual __bool__()`, the synthesized `IsTrue` property is emitted as non-virtual. The `operator true`/`operator false` in the base class then statically dispatches to the base `IsTrue`, ignoring the subclass override.

**Dogfood**: issue 0003 — `Resource.get_IsTrue()` throws `NotImplementedException` at runtime.

### Root Cause

```csharp
// RoslynEmitter.ClassMembers.cs:653-676 (GenerateBoolProperty)
var property = PropertyDeclaration(returnType, "IsTrue")
    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)));  // BUG: hardcoded
```

The same bug exists in `GenerateLenProperty()` (lines 682-708).

Both methods ignore the decorators on the source `FunctionDef` (`@abstract`, `@virtual`, `@override`), unlike `GenerateClassMethod()` (lines 448-620) and `GenerateFunctionStyleProperty()` (lines 1546-1665) which correctly call `GenerateMethodModifiersFromDecorators()` and handle abstract/virtual cases.

### Fix

Apply the same modifier-extraction pattern used by `GenerateClassMethod()`:

**File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.cs`

In **both** `GenerateBoolProperty()` and `GenerateLenProperty()`:

1. Replace hardcoded `TokenList(Token(SyntaxKind.PublicKeyword))` with computed modifiers from `GenerateMethodModifiersFromDecorators(func.Decorators)`.
2. Check for abstract (`@abstract` decorator OR ellipsis body in `@abstract` class) using the same pattern as `GenerateClassMethod()` lines 570-583.
3. If abstract: add `SyntaxKind.AbstractKeyword`, emit getter with semicolon (no body).
4. If `@virtual` (not abstract, not override): ensure `SyntaxKind.VirtualKeyword` is present.
5. If `@override`: ensure `SyntaxKind.OverrideKeyword` is present.

**Why general, not a spot fix**: This treats protocol properties identically to regular methods and user-defined properties. Any new protocol interface property (future `ISized`-like protocols) will automatically inherit the same modifier logic.

**Additional consideration**: When `__bool__` has `@override`, the emitted `IsTrue` property must also be `override`. Currently subclasses that override `__bool__` would emit `public bool IsTrue { get { ... } }` which HIDES the base property instead of overriding it. The fix must handle `@override` too.

### Tests

Add the following test fixtures:

1. **`protocol_bool_virtual.spy`** + `.expected` — Base class with `@virtual __bool__()`, subclass with `@override __bool__()`. Call `bool()` on subclass via base-typed variable. Verifies polymorphic dispatch through `IBoolConvertible.IsTrue`.

2. **`protocol_bool_abstract.spy`** + `.expected` — `@abstract` class with `@abstract __bool__()`, concrete subclass with `@override __bool__()`. Verifies abstract property + override dispatch.

3. **`protocol_len_virtual.spy`** + `.expected` — Same pattern for `__len__` / `ISized.Count`.

### Verification

```bash
dotnet build sharpy.sln
dotnet test --filter "DisplayName~protocol_bool_virtual"
dotnet test --filter "DisplayName~protocol_bool_abstract"
dotnet test --filter "DisplayName~protocol_len_virtual"
dotnet test  # Full suite — no regressions
```

---

## Commit 2: Fix enum equality across modules

### Problem

`circle.category == ShapeCategory.BASIC` fails with SPY0222 in cross-module scenarios, despite the enum comparison code existing at `TypeInferenceService.cs:85-93`.

**Dogfood**: skip 0004 — `ShapeCategory` enum defined in `geometry_types.spy`, used in `main.spy` via cross-module field access.

### Root Cause

The enum comparison check at `TypeInferenceService.cs:85-93` requires both operands to be `UserDefinedType { Symbol.TypeKind: TypeKind.Enum }`:

```csharp
if (left is UserDefinedType { Symbol.TypeKind: TypeKind.Enum } &&
    right is UserDefinedType { Symbol.TypeKind: TypeKind.Enum })
```

In cross-module scenarios, the field access `circle.category` may not return a `UserDefinedType` with a fully-resolved `TypeSymbol` that has `TypeKind.Enum`. This can happen when:

1. The field's type is resolved from the imported module's symbol table but the `TypeSymbol` reference is different from the one used for `ShapeCategory.BASIC`.
2. The member access for `circle.category` returns the field type, but the imported `TypeSymbol`'s `TypeKind` hasn't been materialized yet.

### Investigation Steps (must be done before coding)

Before implementing the fix, the implementer must:

1. Create a minimal multi-file test that reproduces SPY0222 for enum comparison:
   - File `types.spy`: `enum Color: RED = 1, GREEN = 2`
   - File `main.spy`: `from types import Color` + `c: Color = Color.RED` + `print(str(c == Color.RED))`
2. Add a temporary debug diagnostic in `TypeInferenceService.InferBinaryOpTypeUncached()` at line 85 to log the actual types of both operands (`left.GetType()`, `right.GetType()`, and if `UserDefinedType`, log `Symbol.TypeKind`).
3. Identify the exact failure: is one operand NOT a `UserDefinedType`? Or is `TypeKind` not `Enum`?

### Fix (based on likely root cause)

**File**: `src/Sharpy.Compiler/Semantic/TypeInferenceService.cs`

The most robust fix is to make the enum check resilient to symbol identity issues. Instead of relying solely on the `UserDefinedType` pattern match, also check by name when the pattern match fails:

```csharp
// Check if both types represent enum types
bool leftIsEnum = IsEnumType(left);
bool rightIsEnum = IsEnumType(right);

if (leftIsEnum && rightIsEnum)
{
    if (op is BinaryOperator.Equal or BinaryOperator.NotEqual
        or BinaryOperator.LessThan or BinaryOperator.LessThanOrEqual
        or BinaryOperator.GreaterThan or BinaryOperator.GreaterThanOrEqual)
        return SemanticType.Bool;
}
```

Where `IsEnumType()` is a new private method that checks:
1. `type is UserDefinedType { Symbol.TypeKind: TypeKind.Enum }` (existing fast path)
2. If that fails, check if the type's symbol has `TypeKind.Enum` through alternative means (e.g., looking up the type name in the symbol table)

**Why general**: This doesn't hardcode any particular enum name. Any enum type — builtin or user-defined, same-module or cross-module — benefits from the same resilient check.

**Important**: Also verify that same-type checking is still correct. Two different enum types (e.g., `Color == ShapeCategory`) should still compile (C# enums support `==` only between same type, but the compiler doesn't enforce this — C# will catch it). OR we may want to add a check that both enums are the same type by comparing `Symbol.Name` + `Symbol.DeclaringFilePath`.

### Tests

1. **Multi-file test**: `cross_module_enum_equality/` directory:
   - `types.spy`: define enum
   - `main.spy`: import enum, compare with `==` and `!=`
   - `main.expected`: expected output

2. **Single-file test**: `enum_cross_compare.spy` — Verify that enum comparison works in the simple case (regression guard for existing functionality).

### Verification

```bash
dotnet build sharpy.sln
dotnet test --filter "DisplayName~cross_module_enum_equality"
dotnet test --filter "DisplayName~enum_cross_compare"
dotnet test  # Full suite
```

---

## Commit 3: Fix `Some(value)` contextual typing in constructor arguments

### Problem

`Some(value)` is rejected with SPY0230 when passed as a function argument to a constructor whose parameter type is `T?` (OptionalType).

**Dogfood**: skip 0000 — `Person("Alice", Some(addr1))` where `addr: Address? = None()`.

### Root Cause

In `TypeChecker.Expressions.Access.cs`, the early symbol resolution for function arguments (lines 475-493) only handles `FunctionSymbol`:

```csharp
if (call.Function is Identifier earlyId)
{
    var earlySymbol = _symbolTable.Lookup(earlyId.Name);
    if (earlySymbol is FunctionSymbol fs && !fs.IsGeneric)  // <-- TypeSymbol not handled
    {
        earlyFuncSymbol = fs;
    }
}
```

When calling a constructor like `Person(...)`, the symbol is a `TypeSymbol`, not a `FunctionSymbol`. Since `earlyFuncSymbol` remains `null`, the argument-checking loop (lines 527-536) never sets `_expectedType` for each argument. Without `_expectedType`, `TryCheckTaggedUnionConstructor()` (line 425-431) can't identify `Some(value)` as an optional constructor — it checks `_expectedType is OptionalType`, which is `null`.

**Why this works in assignments**: `x: int? = Some(42)` sets `_expectedType` from the variable's type annotation in `CheckStatement()` (line 142).

**Why this works in default parameters**: `def foo(x: int? = None())` sets `_expectedType` from the parameter type in `CheckFunctionDefinition()`.

### Fix

**File**: `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.Access.cs`

Extend the early symbol resolution block (lines 479-493) to handle `TypeSymbol` constructors by extracting parameter types from the `__init__` method:

```csharp
if (call.Function is Identifier earlyId)
{
    var earlySymbol = _symbolTable.Lookup(earlyId.Name);
    if (earlySymbol is FunctionSymbol fs && !fs.IsGeneric)
    {
        // ... existing code ...
        earlyFuncSymbol = fs;
    }
    else if (earlySymbol is TypeSymbol ts && !ts.IsGeneric)
    {
        // Constructor call: extract parameter types from __init__
        var initMethod = ts.Methods.FirstOrDefault(m => m.Name == DunderNames.Init);
        if (initMethod != null)
        {
            // Create a synthetic FunctionSymbol with __init__ params (minus 'self')
            // for the purpose of setting _expectedType on arguments.
            earlyFuncSymbol = initMethod;
            // Note: initMethod.Parameters[0] is 'self', which maps to no argument.
            // The argument-to-parameter mapping at lines 527-530 uses argIdx,
            // but __init__ parameters are offset by 1 (self).
            // We need to account for this offset.
        }
    }
}
```

**Critical detail**: The `__init__` method's parameters include `self` at index 0, but the call arguments don't include `self`. The existing argument mapping at line 527 uses `argIdx < earlyFuncSymbol.Parameters.Count`, which would be off-by-one. The fix must adjust for this offset.

**Two implementation options**:

**Option A (Preferred)**: Instead of using `earlyFuncSymbol` directly, create a new variable `earlyParamSource` that holds a list of parameter types (already offset to exclude `self`). Refactor lines 527-536 to use this list instead of `earlyFuncSymbol.Parameters`. This is cleaner and avoids the off-by-one pitfall.

**Option B**: Use a flag `isConstructorCall` to offset the index when accessing `earlyFuncSymbol.Parameters`. Less clean but more minimal.

**Why general**: This fix makes `Some()`, `None()`, `Ok()`, `Err()` all work in constructor arguments — any tagged union shorthand that depends on `_expectedType` being set. It also enables future contextual type inference patterns in constructor calls.

**Edge cases to handle**:
- Generic constructors (`Box[int](Some(5))`) — already excluded by `!ts.IsGeneric` guard
- Constructors with `*args` or `**kwargs` — the existing spread handling continues to work
- Constructors without `__init__` — `initMethod` is null, no early resolution attempted
- Keyword arguments — the kwarg handling at lines 542-554 already checks `earlyFuncSymbol` and should work once it's set

### Tests

1. **`optional_some_constructor_arg.spy`** + `.expected` — Pass `Some(value)` and `None()` as arguments to a constructor with `T?` parameters.

2. **`optional_some_function_arg.spy`** + `.expected` — Pass `Some(value)` and `None()` as arguments to a regular function with `T?` parameters (should already work via `earlyFuncSymbol`, but verify).

3. **`optional_some_nested_constructor.spy`** + `.expected` — Nested constructors: `Outer(Inner(Some(42)))` where `Inner.__init__` takes `int?`. Verifies that the fix works through multiple levels.

### Verification

```bash
dotnet build sharpy.sln
dotnet test --filter "DisplayName~optional_some_constructor_arg"
dotnet test --filter "DisplayName~optional_some_function_arg"
dotnet test --filter "DisplayName~optional_some_nested_constructor"
dotnet test  # Full suite
```

---

## Commit 4: Fix dict/set/list literal contextual type inference

### Problem

Dict literals ignore `_expectedType` for non-empty dicts, causing `FindLeastCommonAncestor()` to infer `dict[str, object]` when the contextual type is `dict[str, float]`.

**Dogfood**: issue 0001 — `return {"total_area": total_area, ...}` in a function with return type `-> dict[str, float]` produces `dict[str, object]`.

### Root Cause

`CheckDictLiteral()` in `TypeChecker.Expressions.Literals.cs` (lines 62-112) only checks `_expectedType` for empty dicts (lines 64-73). For non-empty dicts, it infers types purely from element LCA. When `FindLeastCommonAncestor()` returns `SemanticType.Object` (because values have no common type chain — e.g., variables whose types are resolved but have no common ancestor in the type hierarchy), the dict is typed as `dict[str, object]`.

The same issue exists for `CheckListLiteral()` and `CheckSetLiteral()` — they also ignore `_expectedType` for non-empty collections, though it's less commonly triggered because list elements tend to have obvious common types.

### Fix

**File**: `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.Literals.cs`

**Strategy**: After computing the LCA-based element types, check if `_expectedType` provides a compatible contextual type. If the LCA result is `object` (the fallback) but all elements are assignable to the expected element type, use the expected type instead.

**For `CheckDictLiteral()`** (modify after line 105):

```csharp
var commonKeyType = FindLeastCommonAncestor(keyTypes);
var commonValueType = FindLeastCommonAncestor(valueTypes);

// Contextual type inference: if we have an expected dict type and the LCA
// produced Object (the fallback), check if all elements are assignable to
// the expected element types. This handles cases like:
//   def f() -> dict[str, float]:
//       return {"a": x, "b": y}  # x, y are float but LCA yields object
if (_expectedType is GenericType expectedDict
    && expectedDict.Name == BuiltinNames.Dict
    && expectedDict.TypeArguments.Count == 2)
{
    if (commonKeyType == SemanticType.Object
        && AllAssignableTo(keyTypes, expectedDict.TypeArguments[0]))
    {
        commonKeyType = expectedDict.TypeArguments[0];
    }
    if (commonValueType == SemanticType.Object
        && AllAssignableTo(valueTypes, expectedDict.TypeArguments[1]))
    {
        commonValueType = expectedDict.TypeArguments[1];
    }
}
```

**`AllAssignableTo` helper** (add to `TypeChecker.Utilities.cs`):

```csharp
private bool AllAssignableTo(List<SemanticType> types, SemanticType target)
{
    return types.All(t => IsAssignableTo(t, target));
}
```

This uses the existing `IsAssignableTo()` method which already handles subtype relationships, nullability, etc.

**Apply the same pattern to `CheckListLiteral()` and `CheckSetLiteral()`** for consistency. For lists:

```csharp
if (_expectedType is GenericType expectedList
    && expectedList.Name == BuiltinNames.List
    && expectedList.TypeArguments.Count == 1)
{
    if (commonType == SemanticType.Object
        && AllAssignableTo(elementTypes, expectedList.TypeArguments[0]))
    {
        commonType = expectedList.TypeArguments[0];
    }
}
```

**Why general**: This pattern works for any collection literal in any contextual position (return statements, assignments, function arguments). It doesn't special-case any particular type — it uses the existing assignability check. Future collection types would automatically benefit.

**Why not just always use `_expectedType`**: We only fall back to `_expectedType` when the LCA fails (returns `object`). When the LCA succeeds with a specific type, we trust it — this preserves type safety. For example, `list[int]` assigned to `list[object]` should still be typed as `list[int]`, not silently widened to `list[object]`.

### Tests

1. **`dict_literal_return_type.spy`** + `.expected` — Function returning `dict[str, float]` with a dict literal. Verifies contextual type inference in return position.

2. **`dict_literal_assignment.spy`** + `.expected` — Annotated assignment `d: dict[str, float] = {"a": x, "b": y}`. Verifies assignment context.

3. **`list_literal_return_type.spy`** + `.expected` — Same pattern for lists (regression guard).

4. **`collection_literal_mixed_types.spy`** + `.expected` — Dict with genuinely mixed types that SHOULD remain `object` (e.g., `{"a": 1, "b": "hello"}`). Verifies we don't over-constrain.

### Verification

```bash
dotnet build sharpy.sln
dotnet test --filter "DisplayName~dict_literal_return_type"
dotnet test --filter "DisplayName~collection_literal_mixed_types"
dotnet test  # Full suite
```

---

## Commit 5: Fix `list()`/`set()` constructor type inference from iterables

### Problem

`list(dict.values())` produces `list[Unknown]`, causing subsequent `items[0]` to fail with SPY0907 (internal compiler error: UnknownType without error diagnostic).

**Dogfood**: issue 0005 — `list(inventory.values())[0]` produces UnknownType.

### Root Cause

When a generic type constructor like `list()` is called with a non-empty argument but no explicit type annotation, the code at `TypeChecker.Expressions.Access.cs:659-663` defaults to `Unknown` type arguments:

```csharp
else  // non-empty arguments, no type annotation
{
    typeArgs = Enumerable.Range(0, typeSymbol.TypeParameters.Count)
        .Select(_ => (SemanticType)SemanticType.Unknown)
        .ToList();
}
```

This is inconsistent with how `sorted()` (lines 596-602) correctly infers element type from iterable arguments using `_typeInference.InferIterableElementType()`.

**Note**: The argument to `list()` might itself be `Unknown` (e.g., `dict.values()` returns `Unknown` because generic type member access is delegated to CLR reflection). In that case, inference will still fail. This commit addresses the case where the argument type IS known (e.g., `list([1, 2, 3])`, `list(some_set)`, `set(some_list)`).

### Fix

**File**: `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.Access.cs`

Add iterable element type inference for `list()`, `set()`, and `dict()` constructors, analogous to how `sorted()` works. Insert this logic before the Unknown fallback at line 659:

```csharp
else if (call.Arguments.Length == 1 && call.KeywordArguments.Length == 0)
{
    // Single-argument constructor: try to infer type args from iterable argument type.
    // Examples: list(some_set) -> list[T], set(some_list) -> set[T]
    var argType = argTypes.Count > 0 ? argTypes[0] : null;
    if (argType != null && argType != SemanticType.Unknown)
    {
        var elementType = _typeInference.InferIterableElementType(argType);
        if (elementType != null && elementType != SemanticType.Unknown)
        {
            if (typeSymbol.Name is BuiltinNames.List or BuiltinNames.Set
                && typeSymbol.TypeParameters.Count == 1)
            {
                typeArgs = new List<SemanticType> { elementType };
            }
            // dict() from iterable of tuples: dict([(k, v), ...]) -> dict[K, V]
            else if (typeSymbol.Name == BuiltinNames.Dict
                     && typeSymbol.TypeParameters.Count == 2
                     && elementType is TupleType tt && tt.ElementTypes.Count == 2)
            {
                typeArgs = new List<SemanticType> { tt.ElementTypes[0], tt.ElementTypes[1] };
            }
        }
    }
    // Fall through to Unknown if inference failed
    typeArgs ??= Enumerable.Range(0, typeSymbol.TypeParameters.Count)
        .Select(_ => (SemanticType)SemanticType.Unknown)
        .ToList();
}
```

**Also**: Enhance `InferIterableElementType()` in `TypeInferenceService.cs` to handle `UserDefinedType` instances with `__iter__()` methods:

```csharp
// User-defined iterable types: check for __iter__ return type
if (iterableType is UserDefinedType udt && udt.Symbol != null)
{
    // Check for __iter__ which returns Iterator<T>
    var iterMethod = udt.Symbol.Methods.FirstOrDefault(m => m.Name == DunderNames.Iter);
    if (iterMethod?.ReturnType is GenericType iterReturn
        && iterReturn.Name == BuiltinNames.Iterator
        && iterReturn.TypeArguments.Count > 0)
    {
        return iterReturn.TypeArguments[0];
    }

    // Check for __getitem__ (sequence protocol)
    var getitemMethod = udt.Symbol.Methods.FirstOrDefault(m => m.Name == DunderNames.GetItem);
    if (getitemMethod?.ReturnType is { } itemType && itemType != SemanticType.Unknown)
    {
        return itemType;
    }
}
```

**Why general**: This inference works for any iterable-to-collection conversion, not just `dict.values()`. It handles `list(some_set)`, `set(some_list)`, `list(range(10))`, etc. The `InferIterableElementType` enhancement also benefits `sorted()`, `reversed()`, `enumerate()`, and any future builtin that consumes iterables.

### Tests

1. **`list_from_set.spy`** + `.expected` — `list(my_set)` preserves element type.

2. **`set_from_list.spy`** + `.expected` — `set(my_list)` preserves element type.

3. **`list_from_iterable_class.spy`** + `.expected` — `list(my_obj)` where `my_obj.__iter__() -> Iterator[int]`.

4. **`collection_constructor_unknown.spy`** + `.expected` or `.error` — Verify graceful fallback when argument type is genuinely unknown.

### Verification

```bash
dotnet build sharpy.sln
dotnet test --filter "DisplayName~list_from_set"
dotnet test --filter "DisplayName~set_from_list"
dotnet test --filter "DisplayName~list_from_iterable_class"
dotnet test  # Full suite
```

---

## Commit 6: Fix `reversed()` on user classes with `__reversed__()`

### Problem

`reversed(tracker)` fails with SPY0907 when `ScoreTracker` implements `__reversed__()`.

**Dogfood**: issue 0008 — `reversed[int](tracker)` produces UnknownType.

### Root Cause

The `reversed()` builtin handling at `TypeChecker.Expressions.Access.cs:588-594` calls `InferIterableElementType()`, which doesn't handle `UserDefinedType` instances with `__reversed__()`. The element type inference returns `null`, causing the function call to fall through to standard overload resolution, which also fails.

### Fix

**File**: `src/Sharpy.Compiler/Semantic/TypeInferenceService.cs`

Add a new method `InferReversedElementType()` that checks for `__reversed__()` on user-defined types:

```csharp
/// <summary>
/// Infers the element type when calling reversed() on a type.
/// Checks for __reversed__ protocol method on user-defined types.
/// Returns null if the type doesn't support reversed().
/// </summary>
public SemanticType? InferReversedElementType(SemanticType type)
{
    // First try standard iterable inference (works for list, etc.)
    var iterableElement = InferIterableElementType(type);
    if (iterableElement != null)
        return iterableElement;

    // Check for __reversed__ protocol method on user-defined types
    if (type is UserDefinedType udt && udt.Symbol != null)
    {
        var reversedMethod = udt.Symbol.Methods.FirstOrDefault(
            m => m.Name == DunderNames.Reversed);
        if (reversedMethod?.ReturnType is { } returnType
            && returnType != SemanticType.Unknown
            && returnType != SemanticType.Void)
        {
            // __reversed__ returns the element type (it's a generator yielding T)
            return returnType;
        }
    }

    return null;
}
```

**File**: `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.Access.cs`

Update the `reversed()` handling at line 588-594 to use the new method:

```csharp
if (id.Name == BuiltinNames.Reversed && argTypes.Count == 1)
{
    var elementType = _typeInference.InferReversedElementType(argTypes[0]);
    if (elementType != null)
        return new GenericType { Name = BuiltinNames.Iterator,
            TypeArguments = new List<SemanticType> { elementType } };
}
```

**Why a separate method instead of adding to `InferIterableElementType`**: `reversed()` semantics differ from general iteration. A type might be iterable (forward) but not reversible. Having a separate method keeps the semantics clear and avoids `sorted(obj_with_reversed)` incorrectly using the reversed element type.

**Note on `reversed[int](tracker)` syntax**: The dogfood code used explicit generic syntax `reversed[int](tracker)`. This may or may not be supported. The fix should make `reversed(tracker)` (without explicit type args) work, since the type can be inferred from `__reversed__()`. If explicit generic args on builtins aren't supported, that's a separate issue.

### Tests

1. **`reversed_user_class.spy`** + `.expected` — Class with `__reversed__()` generator, call `reversed(instance)` in a for loop.

2. **`reversed_user_class_typed.spy`** + `.expected` — Same but assign result to `Iterator[int]` variable to verify inferred type.

3. **`reversed_list_regression.spy`** + `.expected` — Existing `reversed([1,2,3])` continues to work (regression guard).

### Verification

```bash
dotnet build sharpy.sln
dotnet test --filter "DisplayName~reversed_user_class"
dotnet test --filter "DisplayName~reversed_list_regression"
dotnet test  # Full suite
```

---

## Commit 7: Add dogfood prompt rules for `@abstract`, `@virtual/@override`, and exception hierarchy

### Problem

14 C1 issues trace to the AI generating structurally invalid code. Three patterns recur:

1. **Missing `@abstract` on class** (3 failures): Classes with `@abstract` methods but the class itself lacks `@abstract`.
2. **Missing `@virtual`/`@override`** (2 failures): AI expects polymorphic dispatch but forgets the decorators.
3. **Exception hierarchy** (2 failures): Custom exception classes not extending `Exception`.

### Fix

**File**: `build_tools/sharpy_dogfood/prompts.py`

**A. Add to `BEHAVIORAL_RULES_SECTION`** (after existing rules around line 87):

```python
"- **Virtual/override required for polymorphism**: Polymorphic dispatch requires `@virtual` on the base class method AND `@override` on each subclass method. Without these decorators, the base class method is called even when the object is a subclass instance."
"- **Custom exception hierarchy**: Classes used with `raise` or `except` MUST extend `Exception` (or a builtin exception type like `ValueError`, `RuntimeError`). A plain class cannot be raised or caught."
```

The rule for `@abstract` class decorator already exists at line 87 (`"- **Abstract class decorator**: ..."`). No change needed.

**B. Add to `RETRY_REMEDIATION` list**:

```python
(
    r"CS0513.*abstract.*non-abstract",
    "The class containing @abstract methods must itself be decorated with @abstract. "
    "Add @abstract before the class definition.",
),
(
    r"CS0506.*not.*virtual",
    "Cannot @override a method that is not @virtual or @abstract in the base class. "
    "Add @virtual to the base class method, or remove @override from the subclass method.",
),
(
    r"SPY0222",
    "Operator not supported on this type. For enum comparisons, ensure both sides "
    "are the same enum type. For class comparisons, implement __eq__.",
),
(
    r"CS0029.*Exception|CS0155",
    "Custom exception classes must extend Exception or a builtin exception type. "
    "Change 'class MyError:' to 'class MyError(Exception):'.",
),
(
    r"SPY0220.*list\[.*\].*list\[",
    "Generic collections are INVARIANT in Sharpy. list[Child] cannot be assigned to "
    "list[Parent]. Declare the collection with the base/interface type instead.",
),
```

### Tests

No compiler tests needed. Run `pytest build_tools/tests/ -v` to verify no Python test regressions.

### Verification

```bash
cd /Users/anton/Documents/github/sharpy
python3 -m pytest build_tools/tests/ -v
```

---

## Commit 8: Add expected output verification instruction to dogfood prompts

### Problem

8 of 14 C1 issues are the AI miscalculating expected output. The AI generates code correctly but then writes wrong expected values (e.g., counting unique words as 3 instead of 4, miscalculating hash values).

### Fix

**File**: `build_tools/sharpy_dogfood/prompts.py`

Add an explicit verification instruction to the code generation prompts. This should be added to **both** `get_code_generation_prompt()` and `get_multifile_generation_prompt()`, in the instructions section where expected output is discussed.

Find the section in each prompt that asks for expected output comments and add:

```python
"""
### Expected Output Verification (CRITICAL)

After writing the code and expected output:
1. **Mentally trace `main()` line by line** — compute each print() argument by hand.
2. **Verify arithmetic** — especially modular arithmetic (%), floating-point (*), and hash computations.
3. **Count carefully** — for collections, count unique elements by listing them explicitly.
4. **Check method dispatch** — if a method is NOT @virtual, the BASE class version runs regardless of the actual type.
5. **Verify every line of expected output matches your trace.** If it doesn't, fix the expected output.
"""
```

**Why a dedicated section**: Burying this in the behavioral rules makes it easy to miss. Expected output errors are the #1 failure mode (8/14 C1s), so it deserves prominence.

### Tests

No compiler tests needed. Run `pytest build_tools/tests/ -v`.

### Verification

```bash
cd /Users/anton/Documents/github/sharpy
python3 -m pytest build_tools/tests/ -v
```

---

## Commit 9: Improve retry feedback for list invariance and symbol resolution

### Problem

4 of 6 C5 (retry flow) issues involve `list[SubType]` assigned to `list[BaseType]`, which Sharpy correctly rejects (generic invariance). The retry feedback doesn't explain WHY this fails or HOW to fix it. Other C5 issues involve the AI referencing symbols that don't exist in modules.

### Fix

**File**: `build_tools/sharpy_dogfood/prompts.py`

**A. Add to `RETRY_REMEDIATION` list** — a more specific pattern for list invariance (broader than the existing `SPY0220.*list\[.*\?\]` pattern):

```python
(
    r"SPY0220.*list\[.*\].*list\[",
    "Generic collections are INVARIANT in Sharpy — list[Child] cannot be assigned to "
    "list[Parent], even if Child extends Parent. Fix: declare the variable/parameter as "
    "list[Parent] from the start, and add items individually. "
    "Example: shapes: list[Shape] = [] then shapes.append(Circle(5.0)).",
),
(
    r"SPY0301.*no exported symbol",
    "Check that the imported symbol name matches exactly "
    "(case-sensitive) and that the symbol is defined at the module's top level. "
    "If the symbol is an @abstract class, try simplifying: remove the @abstract decorator "
    "or move the class to the importing file. Abstract class cross-module imports can be unreliable. "
    "IMPORTANT: Do NOT reference symbols that you did not define in the source file.",
),
```

Note: The `SPY0301` entry already exists but should be updated with the stronger wording about not referencing undefined symbols.

**B. Improve the retry prompt template** in `get_regeneration_prompt()` and `get_multifile_regeneration_prompt()` to include the actual symbols available in each module when SPY0301 is present.

**File**: `build_tools/sharpy_dogfood/orchestrator.py`

In the retry loop where `get_regeneration_prompt()` / `get_multifile_regeneration_prompt()` is called, if the error contains `SPY0301`, extract the module name and list available top-level symbols from the source file. Pass this as an additional context parameter to the prompt.

### Tests

No compiler tests needed. Run `pytest build_tools/tests/ -v`.

### Verification

```bash
cd /Users/anton/Documents/github/sharpy
python3 -m pytest build_tools/tests/ -v
```

---

## Implementation Order & Dependencies

```
Commit 1: Protocol property virtual dispatch     [no dependencies]
Commit 2: Enum equality across modules           [no dependencies]
Commit 3: Some(value) in constructor arguments    [no dependencies]
Commit 4: Dict/set/list literal contextual types  [no dependencies]
Commit 5: list()/set() constructor inference      [no dependencies]
Commit 6: reversed() on user classes              [depends on Commit 5 for InferIterableElementType enhancement]
Commit 7: Prompt rules (@abstract, @virtual, etc) [no dependencies, can parallel with 1-6]
Commit 8: Expected output verification prompt     [no dependencies, can parallel with 1-6]
Commit 9: Retry feedback improvements             [no dependencies, can parallel with 1-6]
```

Commits 1-5 are fully independent and can be implemented in any order. Commit 6 should follow Commit 5 because the `InferIterableElementType` enhancement in Commit 5 provides the user-defined type iteration handling that Commit 6 builds on. Commits 7-9 are independent of each other and of commits 1-6.

## Risk Assessment

| Commit | Risk | Mitigation |
|--------|------|------------|
| 1 | Modifier changes could affect existing protocol property emission | Run full test suite; check existing `__bool__`/`__len__` test fixtures |
| 2 | Enum comparison fix might allow invalid cross-type comparisons | Test `Color == ShapeCategory` separately; C# will still catch type mismatches |
| 3 | Off-by-one in `__init__` parameter offset could break all constructor calls | Use parameter list extraction (Option A) to avoid index math |
| 4 | Over-constraining to `_expectedType` could mask real type mismatches | Only apply when LCA returns `Object` AND all elements assignable |
| 5 | Generic constructor inference could interfere with existing wildcard matching | Guard with `argType != Unknown` check; existing Unknown fallback preserved |
| 6 | Separate `InferReversedElementType` could diverge from `InferIterableElementType` | Keep both methods simple; document the semantic difference |
| 7-9 | Prompt changes could affect passing dogfood scenarios | Python tests + manual review of prompt text |

## Files Modified (Summary)

| File | Commits |
|------|---------|
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.cs` | 1 |
| `src/Sharpy.Compiler/Semantic/TypeInferenceService.cs` | 2, 5, 6 |
| `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.Access.cs` | 3, 5, 6 |
| `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.Literals.cs` | 4 |
| `src/Sharpy.Compiler/Semantic/TypeChecker.Utilities.cs` | 4 |
| `build_tools/sharpy_dogfood/prompts.py` | 7, 8, 9 |
| `build_tools/sharpy_dogfood/orchestrator.py` | 9 |
| `src/Sharpy.Compiler.Tests/Integration/TestFixtures/` | 1, 2, 3, 4, 5, 6 (new test fixtures) |

---

## Verification Summary

<\!-- Verified by /verify-plan on 2026-02-24 -->
<\!-- Verification result: PASS -->

**Result:** PASS
**Verified on:** 2026-02-24
**Plan file:** /Users/anton/Documents/github/sharpy/plans/dogfood-c3-fixes.md

### Verification Details

All structural references have been verified against the actual Sharpy codebase:

**Files verified (7/7 exist):**
- src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.cs
- src/Sharpy.Compiler/Semantic/TypeInferenceService.cs
- src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.Access.cs
- src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.Literals.cs
- src/Sharpy.Compiler/Semantic/TypeChecker.Utilities.cs
- build_tools/sharpy_dogfood/prompts.py
- build_tools/sharpy_dogfood/orchestrator.py

**Line numbers verified:** All references confirmed accurate
- GenerateBoolProperty (lines 653-676)
- GenerateLenProperty (lines 682-708)
- Enum comparison check (lines 85-93 in TypeInferenceService)
- Early symbol resolution (lines 478-493)
- Dict literal check (line 62+, with LCA at lines 104-105)
- Generic constructor Unknown fallback (lines 659-663)
- sorted() handling (lines 596-602)
- reversed() handling (lines 588-594)

**Diagnostic codes verified:**
- SPY0222 (InvalidBinaryOperation)
- SPY0230 (NotCallable)
- SPY0220 (TypeMismatch)
- SPY0301 (ImportError)
- SPY0907 (UnexpectedUnknownType)

**Key classes/constants verified:**
- DunderNames.cs: Init, Bool, Len, Iter, Reversed all present
- BuiltinNames.cs: List, Dict, Set, Iterator, Sorted, Reversed all present
- DecoratorNames.cs: Abstract, Virtual, Override all present

**Architectural soundness:** All proposed changes follow Sharpy conventions
- No violations of immutable AST rule
- No violations of SyntaxFactory-only rule
- Semantic pipeline phases referenced correctly
- No inappropriate materialization logic
- Test coverage specified for all commits

### Corrections Made
None required. All claims in the plan are accurate.

### Warnings
None identified.

### Missing Steps Added
None. All commits are complete with implementation details and test specifications.

### Unchecked Claims
None. All concrete, verifiable claims have been checked against the codebase.
