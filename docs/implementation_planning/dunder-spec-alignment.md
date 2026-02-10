# Dunder Spec Alignment — Implementation Plan

**Date:** 2026-02-09
**Status:** Draft
**Spec docs:** `docs/language_specification/dunder_methods.md`, `dunder_invocation_rules.md`, `dunder_methods_recommendations.md`

This plan brings the compiler implementation into alignment with the dunder method specification. It is organized into three phases that can be landed as incremental commits. Each phase is self-contained: tests should pass at every commit boundary.

---

## How to Use This Plan

Each task is a checkbox. Work top-to-bottom within each phase. Each major task maps to one commit. Sub-bullets are steps within that commit. **Run `dotnet test` after every commit.**

When a task says "update tests," check for existing tests first — fix them to match the new behavior rather than deleting them. When adding new error/warning diagnostics, always add both the diagnostic code constant and at least one test fixture.

---

## Phase A — Registry Cleanup (Spec Alignment)

**Goal:** Remove dead code and rename identifiers so the compiler's internal registries match the spec exactly. No behavioral changes visible to users except `__truediv__` → `__div__` rename.

**Context:** The spec (`dunder_methods.md`) is authoritative. It says:
- `__truediv__` is renamed to `__div__` (line 109-110)
- `__floordiv__`, `__pow__` are "Not supported" (lines 108, 227)
- Reverse operators "do not exist in Sharpy" (line 100)
- In-place operators "do not exist in Sharpy yet" (lines 102-104)
- `__repr__` is "Not supported" (line 228)
- `__delitem__` is "Not supported yet" (line 214)

### A1. Rename `__truediv__` → `__div__` everywhere

**Rationale:** The spec dropped `__truediv__` because Sharpy has no `__floordiv__` to contrast with. Python has both; Sharpy only has `/`, so the dunder is simply `__div__`.

- [x] In `DunderNames.cs`: rename `TrueDiv = "__truediv__"` → `Div = "__div__"`. Update the field name and string value.
- [x] Find-and-replace all references to `DunderNames.TrueDiv` → `DunderNames.Div` across the codebase. Key files:
  - `OperatorRegistry.cs` (BinaryArithmeticOps set)
  - `TypeInferenceService.cs` (BinaryOperator.Divide mapping)
  - `SignatureValidator.cs` (if referenced)
  - `OperatorValidator.cs` (if referenced)
  - `RoslynEmitter.Operators.cs` (operator token mapping)
- [x] In `DunderNames.cs`: also rename `ITrueDiv = "__itruediv__"` → `IDiv = "__idiv__"` and `RTrueDiv = "__rtruediv__"` → `RDiv = "__rdiv__"` (these will be removed in A3/A4, but rename first for consistency within this commit).
- [x] Update any test files that reference `__truediv__` (search test projects).
- [x] If any `.spy` test fixtures use `__truediv__`, rename to `__div__`.
- [x] Run `dotnet test` — all tests must pass.

**What NOT to do:** Don't add a "helpful error if user writes `__truediv__`" in this commit. That's a nice-to-have for later.

### A2. Remove `__floordiv__` and `__pow__` from registries

**Rationale:** C# has no `**` or `//` operators to overload. The spec explicitly lists these as unsupported. Keeping them in the registry is misleading — they suggest the compiler supports features it doesn't.

- [x] In `DunderNames.cs`: remove `FloorDiv`, `Pow`, `IFloorDiv`, `IPow`, `RFloorDiv`, `RPow` constants.
- [x] In `OperatorRegistry.cs`: remove `DunderNames.FloorDiv` and `DunderNames.Pow` from `BinaryArithmeticOps`.
- [x] In `TypeInferenceService.cs`: remove `BinaryOperator.FloorDivide → __floordiv__` and `BinaryOperator.Power → __pow__` mappings from user-defined operator inference. **Keep** the built-in numeric floor divide and power operations (`TryInferBuiltinBinaryOp`) since `//` and `**` still work on primitives — they just aren't overloadable via dunders.
- [x] In `TypeInferenceService.cs`: remove `AssignmentOperator → __ifloordiv__` and `AssignmentOperator → __ipow__` mappings.
- [x] Fix any compilation errors from removed constants.
- [x] Update/remove any test assertions that expect `__floordiv__` or `__pow__` to be recognized as operator dunders.
- [x] Run `dotnet test`.

**Decision point for implementer:** If `RoslynEmitter.Operators.cs` has a mapping for `__pow__` → some method, remove it. `**` on primitives is handled as a built-in operation (likely `Math.Pow`), not via dunder dispatch.

### A3. Remove in-place operator dunders from registries

**Rationale:** The spec says in-place operators don't exist in Sharpy (C# 9 has no way to define them). Augmented assignment `x += y` desugars to `x = x + y` using the regular binary operator. The current code already falls back to regular operators when in-place aren't found, so removing the in-place constants just removes dead paths.

- [x] In `DunderNames.cs`: remove all `I*` constants (`IAdd`, `ISub`, `IMul`, `IDiv` (renamed from ITrueDiv in A1), `IMod`, `IAnd`, `IOr`, `IXor`, `ILShift`, `IRShift`). Also remove `IFloorDiv` and `IPow` if not already removed in A2.
- [x] In `OperatorRegistry.cs`: remove the entire `InPlaceOps` FrozenSet and its registration in the constructor. Remove `OperatorKind.InPlace` from the enum.
- [x] In `OperatorRegistry.cs` `GetExpectedParamCount`: remove the `OperatorKind.InPlace` case.
- [x] In `TypeInferenceService.cs`: in `InferAugmentedAssignmentType`, remove the "try in-place operator first" path (`AssignmentOperatorToInPlaceDunder`). The method should now only try the regular binary operator. Remove `AssignmentOperatorToInPlaceDunder` entirely.
- [x] Fix compilation errors. Update tests.
- [x] Run `dotnet test`.

**Important:** Verify that augmented assignment (`+=`, `-=`, etc.) still works after this change. It should, because the fallback to regular binary operators was already the primary path for most types.

### A4. Remove reflected operator dunders from registries

**Rationale:** The spec says "Reverse operators (e.g. `__radd__`) do not exist in Sharpy." The current code has 12 reflected operator constants in `DunderNames.cs` and a `GetReflectedDunder()` method in `OperatorValidator.cs`. These are only used for enhanced error messages ("operation may succeed via reflected call"), but since reflected operators don't exist, this messaging is incorrect.

- [x] In `DunderNames.cs`: remove all `R*` constants (`RAdd`, `RSub`, `RMul`, `RDiv`, `RMod`, `RAnd`, `ROr`, `RXor`, `RLShift`, `RRShift`). Also remove `RFloorDiv` and `RPow` if not already removed.
- [x] In `OperatorValidator.cs`: remove the `GetReflectedDunder()` method and the call site that checks for reflected operators on the right-hand side.
- [x] Fix compilation errors. Update tests that assert reflected operator behavior.
- [x] Run `dotnet test`.

### A5. Remove `__repr__` and `__delitem__` from registries

**Rationale:** `__repr__` is listed as "Not supported" in the spec — there's no direct C# equivalent and `__str__` serves both purposes. `__delitem__` is "Not supported yet." Keeping unsupported dunders in the registry means the compiler accepts them silently and generates bad C# code (e.g., `__Repr__()` method nobody calls, `__DelItem__()` with no .NET equivalent).

- [x] In `DunderNames.cs`: remove `Repr` and `DelItem` constants.
- [x] In `ProtocolRegistry.cs`: remove the `__repr__` and `__delitem__` registrations. Remove `ProtocolKind.Representation` if `__str__` is the only remaining member (move `__str__` to a different kind, or keep Representation with just `__str__`). Actually — keep `ProtocolKind.Representation` with just `__str__`, that's fine.
- [x] In `DunderMapping.cs`: remove `{ DunderNames.Repr, "ToString" }` entry.
- [x] In `ProtocolRegistry.cs`: also clean up `__delitem__` from the `IMutableSequence` interface references.
- [x] Fix compilation errors. Update tests.
- [x] Run `dotnet test`.

**Decision point:** When a user writes `def __repr__(self) -> str:`, should it be:
  - (a) A generic "unknown dunder" that gets mangled to `__Repr__()` — no error, just a regular method.
  - (b) A specific error: "Sharpy does not support `__repr__`. Use `__str__` for string representation."

Option (b) is friendlier. If implementing (b), add a new diagnostic check in `SignatureValidator` or a dedicated `UnsupportedDunderValidator`. If this feels like scope creep, start with (a) and file a GitHub issue for (b).

### A6. Fix `__len__` DunderMapping: `Length` → `Count`

**Rationale:** The spec says `__len__` maps to `int Count` property (line 193). The `ProtocolRegistry` correctly says `ClrMethodName: "get_Count"`, but `DunderMapping` says `"Length"`. The codegen uses DunderMapping to determine the C# method name, so it's currently generating a `Length()` method when it should generate a `Count` property.

- [x] In `DunderMapping.cs`: change `{ DunderNames.Len, "Length" }` → `{ DunderNames.Len, "Count" }`.
- [x] Verify that `RoslynEmitter.ClassMembers.cs` handles the `Count` name correctly. If it generates a method, it needs to generate a *property* instead. Check how `__len__` codegen works — it may need additional changes in the emitter to produce a property getter rather than a method.
- [x] Update any tests that assert `Length` in the generated C# output.
- [x] Run `dotnet test`.

**Note:** The full property generation (with `ISized` interface) is Phase C work. This commit just fixes the naming inconsistency.

---

## Phase B — Spec Rule Enforcement

**Goal:** Enforce the spec's semantic rules that aren't currently checked. These are correctness improvements — invalid programs that previously compiled will now correctly produce errors.

### B1. Enforce `__eq__` ↔ `__hash__` mutual requirement as error

**Rationale (from spec, lines 168-174):**
> Defining an override of `__eq__(self, other: object)` without an override `__hash__(self)` is a compile-time error. Similarly, the opposite case of overriding `__hash__(self)` without an override of `__eq__(self, other: object)` is also a compile-time error.

The current `EqualityContractValidator` only warns (SPY0454) when `__eq__` exists without an `object` overload. It does NOT check the mutual `__eq__(object)` ↔ `__hash__` requirement at all.

**What needs to change:**
1. Keep SPY0454 warning as-is (it's about missing `object` overload, not about `__hash__`).
2. Add new error: if `__eq__(self, other: object)` exists but `__hash__` does not → error.
3. Add new error: if `__hash__` exists but `__eq__(self, other: object)` does not → error.

- [x] Add two new diagnostic codes in `DiagnosticCodes.cs`:
  - `SPY0455` (or next available): "Class '{0}' defines `__eq__(self, other: object)` but not `__hash__`. The .NET equality contract requires both."
  - `SPY0456` (or next available): "Class '{0}' defines `__hash__` but not `__eq__(self, other: object)`. The .NET equality contract requires both."
- [x] In `EqualityContractValidator.cs`, in `CheckEqOverloads`, after the existing SPY0454 check:
  - Check if any `__eq__` overload has parameter type `object`. If yes, check if `__hash__` is also defined in the class body. If not → emit SPY0455 as error.
  - Also scan for `__hash__` methods. If `__hash__` exists but no `__eq__(self, other: object)` → emit SPY0456 as error.
- [x] Add test fixture: `errors/dunder_eq_object_without_hash.spy` + `.error` file.
- [x] Add test fixture: `errors/dunder_hash_without_eq_object.spy` + `.error` file.
- [x] Add test fixture: `classes/dunder_eq_hash_correct.spy` + `.expected` — a class with both, should compile and run cleanly.
- [x] Add unit tests in the EqualityContractValidator test file.
- [x] Run `dotnet test`.

**Edge case to consider:** A class that inherits `__hash__` from its base class but defines `__eq__(self, other: object)` locally. Per .NET semantics, the inherited `GetHashCode()` still satisfies the contract. The validator should only error if the *class itself* defines one without the other. Inherited dunders are fine. Document this in the test fixture with a comment.

### B2. Fix `__bool__` codegen to emit `operator true`/`operator false`

**Rationale (from spec, lines 182):**
> `__bool__(self) -> bool` → `public static bool operator true(T self)`, and `public static bool operator false(T self)`. The latter invokes the former and returns the negated value.

Currently `__bool__` generates a `ToBoolean()` method via DunderMapping. This is wrong — it should generate two static operators.

**What needs to change:**

- [x] In `DunderMapping.cs`: remove `{ DunderNames.Bool, "ToBoolean" }` entry. `__bool__` is no longer a simple name-mapping case — it needs special codegen handling (like `__eq__` does).
- [x] In `RoslynEmitter.ClassMembers.cs` (or `.Operators.cs`): add special handling for `__bool__` similar to how `__eq__` gets special treatment. When a method named `__bool__` is encountered:
  - Generate `public static bool operator true(T self)` that contains the user's method body.
  - Generate `public static bool operator false(T self)` that calls `operator true` and negates it: `return !(self ? true : false)` or more simply invokes the body and negates.
- [x] Update `ProtocolRegistry.cs`: change `ClrMethodName: "op_Explicit"` → `ClrMethodName: null` for `__bool__` (two operators, not one method).
- [x] Add test fixture: `classes/dunder_bool.spy` + `.expected` — a class with `__bool__`, used in an `if` statement.
- [x] Add test fixture: `classes/dunder_bool.expected.cs` — C# snapshot showing `operator true` and `operator false`.
- [x] Add unit test in `RoslynEmitterOperatorTests.cs`.
- [x] Run `dotnet test`.

**Generated C# example:**
```csharp
// For: def __bool__(self) -> bool: return self.value != 0
public static bool operator true(MyClass self)
{
    return self.Value != 0;
}

public static bool operator false(MyClass self)
{
    return !(self.Value != 0);  // or: return !((bool)self)
}
```

### B3. Enforce dunder invocation rules

**Rationale (from `dunder_invocation_rules.md`):** This is the most impactful un-enforced spec rule. Currently nothing prevents user code from writing `x.__eq__(y)` or `self.__str__()` outside a dunder method. The spec has precise rules:

| Call site | `self.__dunder__()` | `super().__dunder__()` | `other.__dunder__()` |
|-----------|---|---|---|
| Inside dunder method | Allowed (immediate call only) | Allowed (immediate call only) | Error |
| Outside dunder method | Error | Error | Error |

**Implementation approach:** Add a new validator `DunderInvocationValidator` (or add checks to an existing validator like `AccessValidator`). This runs after type checking so all member accesses are resolved.

- [x] Create `DunderInvocationValidator.cs` in `Semantic/Validation/`. Give it an order around 460 (after AccessValidator at 450, before ProtocolValidator at 500).
- [x] Add a diagnostic code: `SPY0460` (or next available): "Cannot invoke dunder method '{0}' directly. Use the corresponding operator or built-in function."
- [x] Add a diagnostic code: `SPY0461`: "Dunder method '{0}' can only be called on 'self' or 'super()' within another dunder method."
- [x] Add a diagnostic code: `SPY0462`: "Cannot capture dunder method reference. Dunder methods must be called immediately."
- [x] The validator should walk the AST and find `FunctionCall` nodes where the callee is a `MemberAccess` with a dunder name (`DunderMapping.IsDunderMethod(name)`). For each such call:
  1. **Check: is the call site inside a dunder method body?** Walk up the AST (or track context) to find the enclosing `FunctionDef`. If the enclosing function is not a dunder → emit SPY0460.
  2. **Check: is the receiver `self` or `super()`?** If the `MemberAccess.Object` is not a `SelfExpression` or `SuperExpression` → emit SPY0461.
  3. **Check: is it an immediate call?** The `MemberAccess` must be the direct callee of a `FunctionCall`. If the dunder member access appears in any other context (assigned to variable, passed as argument) → emit SPY0462.
- [x] **Exception:** `__init__` has special rules — `self.__init__(...)` is allowed for constructor dispatch, and `super().__init__(...)` is allowed for base constructor call. These are already handled by other code paths (constructor chaining). The validator should skip `__init__` or handle it specially.
- [x] Add test fixtures:
  - `errors/dunder_direct_invocation.spy` + `.error`: `x.__eq__(y)` outside dunder.
  - `errors/dunder_invocation_wrong_receiver.spy` + `.error`: `other.__lt__(self)` inside a dunder.
  - `errors/dunder_capture.spy` + `.error`: `f = self.__eq__` inside a dunder.
  - `classes/dunder_cross_call.spy` + `.expected`: valid cross-dunder call (`self.__lt__(other)` inside `__le__`).
  - `classes/dunder_super_call.spy` + `.expected`: valid `super().__str__()` inside `__str__`.
- [x] Run `dotnet test`.

**Complexity note:** The hardest part is tracking "are we inside a dunder method body?" during AST traversal. The cleanest approach is a stack-based context in the validator: push when entering a `FunctionDef`, pop when leaving. Check the top of the stack to see if the current function is a dunder.

---

## Phase C — Protocol & Interface Work

**Goal:** Wire up the runtime protocols that make user-defined types truly Pythonic: iteration, truthiness dispatch, and implicit interface synthesis.

### C1. Create `ISized` interface in Sharpy.Core

**Rationale:** The spec maps `__len__` to `int Count` property. For `bool()` and `len()` built-in dispatch to work on arbitrary user types, we need a discoverable interface. The user has chosen `ISized` (following Python's `collections.abc.Sized` ABC).

- [x] Create `src/Sharpy.Core/ISized.cs`:
  ```csharp
  namespace Sharpy.Core
  {
      /// <summary>
      /// Implemented by types that define __len__() in Sharpy.
      /// Provides a Count property for len() dispatch.
      /// Follows Python's collections.abc.Sized protocol.
      /// </summary>
      public interface ISized
      {
          int Count { get; }
      }
  }
  ```
- [x] Verify it compiles with both `netstandard2.0` and `netstandard2.1` targets (C# 9.0 constraint).
- [x] Verify that existing Sharpy.Core types that have `Count` properties (e.g., `List`, `Set`, `Str`) already satisfy this interface or can be updated to implement it.
  - **Don't** add `ISized` to existing types in this commit — that's a separate task. Just create the interface.
- [x] Add a comment in `ProtocolRegistry.cs` noting that `ISized` now exists in Sharpy.Core (it was already referenced as `SharpyCoreInterface: "ISized"` — this commit makes it real).
- [x] Run `dotnet test`.

### C2. Add `ISized` to Sharpy.Core collection types

**Rationale:** Sharpy's `List`, `Set`, `Str`, `Dict` types already have `Count` properties. Making them implement `ISized` enables generic `len()` dispatch.

- [x] Add `: ISized` to the partial class declarations for types that have `Count`:
  - `Partial.List/List.cs` (or whichever partial defines the class declaration)
  - `Partial.Set/Set.cs`
  - ~~`Partial.Str/Str.cs`~~ (skipped: Str's `Count(sub, ...)` is a method, not a property; len works via implicit string conversion)
  - `Dict.cs` (if it exists)
- [x] Verify each type's `Count` property satisfies the interface (returns `int`, has getter).
- [x] Run `dotnet test`.

### C3. Emit `Count` property + `ISized` for `__len__` codegen

**Rationale:** Currently `__len__` generates a `Length()` method (or `Count()` method after A6). It should generate a read-only `Count` property and implement `ISized`.

- [x] In `RoslynEmitter.ClassMembers.cs`: add special handling for `__len__`. Instead of generating a method, generate:
  ```csharp
  public int Count
  {
      get
      {
          // user's __len__ body
      }
  }
  ```
- [x] Additionally, add `ISized` to the class's base type list in the emitter. This is the "implicit interface synthesis" — when the compiler sees `__len__`, it automatically adds `: ISized` to the generated C# class.
- [x] Emit a compiler info/note diagnostic (SPY1001 or similar, Info severity): "Type '{0}' implicitly implements 'ISized' via '__len__'." This is not a warning, just informational.
- [x] Add test fixture: `classes/dunder_len.spy` + `.expected` — class with `__len__`, used with `len()`.
- [x] Add test fixture: `classes/dunder_len.expected.cs` — C# snapshot showing `Count` property and `ISized` interface.
- [x] Run `dotnet test`.

**Code generation example:**
```python
# Sharpy
class MyList:
    items: list[int]

    def __len__(self) -> int:
        return len(self.items)
```
```csharp
// Generated C#
public class MyList : ISized
{
    public List<int> Items;

    public int Count
    {
        get
        {
            return Items.Count;
        }
    }
}
```

### C4. Implement `bool()` dispatch in Sharpy.Core

**Rationale (from recommendations doc section 5):** Python's `bool(x)` has a fallback chain: `__bool__` → `__len__ != 0` → `True`. In Sharpy, the compiler handles `__bool__` → `operator true/false` (Phase B2). The `bool()` built-in in Sharpy.Core needs to handle the remaining dispatch.

**Dispatch order for `Builtins.@bool(x)`:**
1. If `x` is already `bool` → return it
2. If `x` has `operator true` (type with `__bool__`) → use it
3. If `x` implements `ISized` → return `x.Count != 0`
4. Default → return `true` (objects are truthy)

- [x] In `src/Sharpy.Core/Bool.cs`: updated `Builtins.Bool(object)` with the spec fallback chain:
  1. `IBoolConvertible` (`__bool__`) → direct call
  2. `ISized` (`__len__ != 0`) → count-based truthiness
  3. `ICollection` → Count > 0
  4. Default → true (non-null objects are truthy)
  - **Decision:** Used option (a) with `IBoolConvertible` interface (matching ProtocolRegistry's existing `SharpyCoreInterface` name). The interface has `bool __Bool__()` which the existing `__bool__` codegen already generates.
- [x] Created `src/Sharpy.Core/IBoolConvertible.cs`:
  ```csharp
  namespace Sharpy
  {
      public interface IBoolConvertible
      {
          bool __Bool__();
      }
  }
  ```
- [x] Updated `__bool__` codegen to implicitly implement `IBoolConvertible` via `CollectSynthesizedInterfaces`.
- [x] Add test fixture: `classes/dunder_bool_truthiness.spy` + `.expected` — class with `__bool__`, used in `bool()` call and `if` statement.
- [x] Add test fixture: `classes/dunder_len_truthiness.spy` + `.expected` — class with `__len__` but no `__bool__`, used in `bool()` to show fallback to count-based truthiness.
- [x] Run `dotnet test`.

### C5. Iterator protocol: `StopIteration` → `MoveNext()` bridging

**Rationale (from recommendations doc section 7):** Python's iterator protocol uses `__next__()` which returns the next value or raises `StopIteration`. C#'s `IEnumerator<T>` uses `MoveNext()` → `bool` + `Current` property. The compiler must bridge these.

This is the most complex task in the plan. It involves:
1. Codegen changes for classes defining `__next__`
2. A `StopIteration` exception type in Sharpy.Core
3. Wrapper codegen that catches `StopIteration` and converts to `MoveNext() → false`

**Prerequisite:** Decide on the interface synthesis approach first (C6).

- [x] Create `src/Sharpy.Core/StopIteration.cs`:
  ```csharp
  namespace Sharpy.Core
  {
      public class StopIteration : System.Exception
      {
          public StopIteration() : base("StopIteration") { }
          public StopIteration(string message) : base(message) { }
      }
  }
  ```
  - **Note:** StopIteration already existed with parameterless constructor. Added message constructor.
- [x] In `RoslynEmitter.ClassMembers.cs`: when a class defines `__next__`, generate the `IEnumerator<T>` implementation:
  ```csharp
  // Generated from __next__(self) -> T
  private T _current;

  public bool MoveNext()
  {
      try
      {
          _current = __NextImpl();  // user's __next__ body
          return true;
      }
      catch (StopIteration)
      {
          return false;
      }
  }

  public T Current => _current;

  // Also need: Reset(), Dispose(), non-generic Current
  public void Reset() => throw new System.NotSupportedException();
  public void Dispose() { }
  object System.Collections.IEnumerator.Current => Current;
  ```
- [x] The user's `__next__` body goes into a private `__NextImpl__()` method.
- [x] When a class defines `__iter__` returning `self` AND defines `__next__`, implement both `IEnumerable<T>` and `IEnumerator<T>`:
  ```csharp
  public class Counter : IEnumerable<int>, IEnumerator<int>
  {
      public IEnumerator<int> GetEnumerator() => this;
      IEnumerator IEnumerable.GetEnumerator() => this;
      // ... MoveNext, Current, etc. from above
  }
  ```
- [x] When a class defines only `__iter__` (not `__next__`), generate `GetEnumerator()` via DunderMapping. C# foreach uses duck-typing (GetEnumerator pattern), so no `IEnumerable<T>` synthesis needed for this case.
- [x] Add test fixtures:
  - `classes/dunder_iter_next.spy` + `.expected`: self-iterating class (defines both `__iter__` returning self and `__next__`), used in `for` loop.
  - `classes/dunder_iter_separate.spy` + `.expected`: separate iterable + iterator classes.
- [x] Run `dotnet test`.

**Complexity warning:** This task involves generating multiple members from a single dunder definition. The emitter needs to know the element type `T` from the `__next__` return type annotation to parameterize `IEnumerator<T>`. If the return type is missing, it should be a type error.

### C6. Implicit interface synthesis framework

**Rationale:** When a user defines certain dunders, the generated C# class should automatically implement the corresponding .NET interface. The user decided: implicit synthesis with an info diagnostic.

This task creates the *framework* for implicit synthesis. C1/C3 already handle `ISized`. This task generalizes it and handles:

| Dunder | Synthesized Interface |
|--------|-----------------------|
| `__len__` | `ISized` (already done in C3) |
| `__iter__` | `IEnumerable<T>` |
| `__next__` | `IEnumerator<T>` (usually combined with `__iter__`) |
| `__contains__` | nothing (just a method, no standard interface) |
| `__eq__(self, other: T)` | `IEquatable<T>` (for each overload type `T`) |
| `__bool__` | `IBoolConvertible` (if using option (a) from C4) |

- [x] In `RoslynEmitter.TypeDeclarations.cs` (or wherever class declarations are built): before emitting the class, scan its methods for dunders that trigger interface synthesis. Build a list of additional base types to add.
- [x] Use `ProtocolRegistry.GetProtocol(name)?.SharpyCoreInterface` as the source of truth for which dunders trigger which interfaces.
- [x] For `IEnumerable<T>` and `IEnumerator<T>`, the type parameter `T` comes from the return type of `__iter__` / `__next__`.
- [ ] For `IEquatable<T>`, the type parameter `T` comes from the `other` parameter type of `__eq__`. *(Deferred: noted as Future Phase 3 in code)*
- [x] Emit info diagnostic for each synthesized interface: "Type '{0}' implicitly implements '{1}' via '{2}'."
- [x] Handle conflict: if the user explicitly lists an interface in their inheritance AND the compiler would synthesize it, don't duplicate. Check the existing base list before adding.
- [ ] Handle conflict: if a base class already implements `IEnumerable<string>` and the derived class defines `__iter__` returning `Iterator[int]`, this is an error (conflicting interface implementations). *(Deferred: requires cross-class analysis)*
- [x] Add test fixture: `classes/dunder_implicit_interface.spy` + `.expected`: class with `__len__` and `__iter__`, verify it works with `for` loop and `len()`.
- [x] Add test fixture: `warnings/dunder_implicit_interface_info.spy` + `.warning`: verify the info diagnostic is emitted.
- [x] Run `dotnet test`.

### C7. Wire `len()` built-in to use `ISized`

**Rationale:** The `len()` built-in currently dispatches to `.Count` on known collection types. With `ISized`, it can dispatch generically.

- [x] In `src/Sharpy.Core/Builtins/Len.cs` (or wherever `len()` is implemented): add an overload or generic path that accepts `ISized`:
  ```csharp
  public static int Len(ISized sized)
  {
      return sized.Count;
  }
  ```
  *(Already existed in `src/Sharpy.Core/Len.cs` — verified correct)*
- [x] Verify that `len(my_custom_type)` works when `my_custom_type` defines `__len__`.
- [x] Add test fixture: `classes/dunder_len_builtin.spy` + `.expected`: custom class with `__len__`, called with `len()`.
- [x] Run `dotnet test`.

---

## Summary of New Files

| File | Phase | Purpose |
|------|-------|---------|
| `src/Sharpy.Core/ISized.cs` | C1 | Interface for types with `__len__` |
| `src/Sharpy.Core/IBoolConvertible.cs` | C4 | Interface for types with `__bool__` (option a) |
| `src/Sharpy.Core/StopIteration.cs` | C5 | Exception for iterator protocol bridging |
| `Semantic/Validation/DunderInvocationValidator.cs` | B3 | Enforces dunder call rules |
| Multiple `.spy`/`.expected`/`.error` test fixtures | All | Test coverage |

## Summary of Diagnostic Codes

| Code | Severity | Phase | Message |
|------|----------|-------|---------|
| SPY0454 | Warning | (exists) | `__eq__` without `object` overload |
| SPY0455 | Error | B1 | `__eq__(object)` without `__hash__` |
| SPY0456 | Error | B1 | `__hash__` without `__eq__(object)` |
| SPY0460 | Error | B3 | Direct dunder invocation |
| SPY0461 | Error | B3 | Dunder call on wrong receiver |
| SPY0462 | Error | B3 | Captured dunder reference |
| SPY1001 | Info | C3/C6 | Implicit interface synthesis note |

## Commit Order

```
Phase A (6 commits):
  A1: Rename __truediv__ → __div__
  A2: Remove __floordiv__ and __pow__ from operator registries
  A3: Remove in-place operator dunders
  A4: Remove reflected operator dunders
  A5: Remove __repr__ and __delitem__ from registries
  A6: Fix __len__ DunderMapping to Count

Phase B (3 commits):
  B1: Enforce __eq__/__hash__ mutual requirement
  B2: Fix __bool__ to emit operator true/false
  B3: Enforce dunder invocation rules

Phase C (7 commits):
  C1: Create ISized interface
  C2: Add ISized to collection types
  C3: Emit Count property + ISized for __len__ codegen
  C4: Implement bool() dispatch with ITruthy
  C5: Iterator protocol bridging
  C6: Implicit interface synthesis framework
  C7: Wire len() to ISized
```

Total: ~16 commits across 3 phases.
