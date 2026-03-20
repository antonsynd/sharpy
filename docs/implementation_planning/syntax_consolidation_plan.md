# Syntax Consolidation Plan

> Staff engineer assessment — aligned changes from the dataclass-property-syntax-analysis review.
> Covers syntax consolidation, new features, and spec/documentation work.

## Context

A thorough review of 1,430 test fixtures, the language specification, programmatic tests, and the
spec-vs-test alignment audit identified opportunities for syntax consolidation, new features, and
targeted fixes. After iterative design discussion, the following items were aligned on.

### Decisions Made

| Proposal | Decision | Rationale |
|----------|----------|-----------|
| Unified property accessors (Kotlin-style) | **Rejected** | Double-colon problem (`property name: str:`), loses field-vs-function visual signal |
| Unified event accessors | **Rejected** | Same reasoning; parallel structure with properties |
| `@dataclass` decorator | **Accepted** | Strongest proposal; orthogonal to property syntax; pure boilerplate reduction |
| Deprecate `_`/`__` access modifiers | **Rejected** | Underscore convention is the right default (terse, Pythonic, C#-idiomatic for private fields) |
| Fix `_`/`__` + decorator composability | **Accepted** | Decorators override naming-convention access level; fills implementation gap |
| Standardize on `...` for abstract bodies | **Accepted** | Independent of property/event design; resolves ambiguity |
| Document delegate vs type-alias guidance | **Accepted** | Zero cost; spec clarification only |
| Allow mixed auto+custom properties | **Accepted** | Addresses composability pain point without new syntax |
| Property observers (willset/didset) | **Deferred** | Tracked in #416; needs RFC-level design after property foundation is solid |

---

## Phase 1: Quick Wins

Low-cost, high-clarity changes. No new syntax, no breaking changes.

### 1.1 — Document Delegate vs Type-Alias Guidance

**Scope: Small | Priority: Medium | Breaking: No**

Add a section to the spec clarifying when to use `delegate` vs `type` alias with function types.

**Guidance to document:**
- Use `delegate` when: event handler types (required by .NET), variance annotations needed (`in`/`out`), distinct named C# type needed for interop
- Use `type` alias with function types for everything else (internal callbacks, local function signatures)
- Cross-reference from `delegates.md`, `function_types.md`, and `type_aliases.md`

**Files to change:**
- `docs/language_specification/delegates.md` — add "When to use delegates" section
- `docs/language_specification/function_types.md` — add "Delegates vs function types" note
- `docs/language_specification/type_aliases.md` — add cross-reference

### 1.2 — Standardize Abstract Bodies on Ellipsis

**Scope: Small | Priority: Medium | Breaking: Minor (deprecation warnings)**

Currently three forms are accepted for "no implementation":
1. `def method(self) -> T: ...` (inline ellipsis)
2. `def method(self) -> T:` + newline + `    ...` (block ellipsis)
3. `def method(self) -> T` (body-less, no colon)

Standardize: forms 1 and 2 are canonical. Form 3 (body-less) should emit a deprecation warning
directing users to add `: ...`.

Separate from abstract bodies, `pass` means "intentionally empty concrete body (do nothing)" — this
distinction should be documented in the spec.

**Semantic rule:**
- `...` = abstract, no implementation provided (requires `@abstract` or interface context)
- `pass` = concrete empty body (intentionally does nothing)
- Body-less = deprecated, emit SPY0451 warning

**Files to change:**
- `src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs` — add SPY0451
- `src/Sharpy.Compiler/Semantic/Validation/` — add or extend validator for body-less detection
- `docs/language_specification/abstract_classes.md` — document `...` vs `pass` distinction
- `docs/language_specification/interfaces.md` — same
- Test fixtures using body-less form — update to use `...`

---

## Phase 2: Access Modifier Composability

### 2.1 — Decorator Overrides Naming Convention Access Level

**Scope: Medium | Priority: High | Breaking: No (additive)**

Current behavior: `AccessValidator.DetermineAccessLevel()` determines access purely from naming
convention (`__` → private, `_` → protected). Explicit `@private`/`@protected`/`@public` decorators
are parsed and stored but do NOT override the naming-convention inference.

Target behavior:
```
__name              → private (from naming, default)
_name               → protected (from naming, default)
name                → public (default)

@protected __name   → protected (decorator overrides naming)
@private name       → private (decorator overrides naming)
@public _name       → public (decorator overrides naming)
```

**Implementation:**

1. **Store explicit access level on symbols.** During semantic analysis (TypeChecker or NameResolver),
   when an `@private`/`@protected`/`@public`/`@internal` decorator is present, record the explicit
   access level on the symbol (new `ExplicitAccessLevel` property, or reuse existing decorator
   metadata).

2. **Update `AccessValidator.DetermineAccessLevel()`** to accept the symbol (not just the name
   string) and check for explicit decorator-based access level first, falling back to
   naming-convention inference.

3. **Update `DetermineAccessLevel` signature:**
   ```csharp
   // Before
   private AccessLevel DetermineAccessLevel(string name)

   // After
   private AccessLevel DetermineAccessLevel(string name, Symbol? symbol = null)
   ```
   If `symbol` has an explicit access modifier decorator, return that. Otherwise, fall back to
   the existing name-based logic.

4. **Propagate symbol to call sites.** `ValidateMemberAccess` already looks up the owning type — it
   needs to resolve the specific member symbol to pass to `DetermineAccessLevel`.

**Files to change:**
- `src/Sharpy.Compiler/Semantic/Validation/AccessValidator.cs` — core logic change
- `src/Sharpy.Compiler/Semantic/TypeChecker.Definitions.cs` — record explicit access from decorators
- Possibly `src/Sharpy.Compiler/Semantic/Symbol.cs` — add `ExplicitAccessLevel` if not already present

**Test cases to add:**
- `@protected __name` → accessible from subclass (override private → protected)
- `@private name` → inaccessible from outside (override public → private)
- `@public _name` → accessible from anywhere (override protected → public)
- `@internal _name` → accessible within assembly
- Conflicting decorators → error (e.g., `@private @protected`)
- Decorator on dunder → error or warning (dunders are protocol methods, access modifiers don't apply)

---

## Phase 3: Mixed Auto+Custom Properties

### 3.1 — Allow Auto-Property with Custom Setter (or Getter)

**Scope: Medium | Priority: Medium | Breaking: No (lifts existing restriction)**

Currently, `PropertyValidator` rejects mixing auto-property declarations with function-style
property accessors for the same name. This forces a full rewrite when you need custom logic on
one accessor but auto behavior on the other.

Target: Allow declaring an auto-property alongside a custom setter or getter.

```python
# Auto-getter generated, custom setter validates
property name: str
property set name(self, value: str):
    if len(value) > 0:
        self._name = value
```

**Semantics:**
- `property name: str` defines the backing field and type
- `property set name(self, value: str):` replaces the auto-setter
- `property get name(self) -> str:` replaces the auto-getter
- If both custom get and set are provided alongside an auto-declaration, the auto-declaration
  defines the backing field only (no auto-accessors generated)

**Implementation:**

1. **Update `PropertyValidator`** to allow the mixed pattern. Currently it rejects when it sees
   both auto and function-style declarations for the same property name. Change to: allow if they're
   complementary (auto + custom set, or auto + custom get).

2. **Update codegen** (`RoslynEmitter.ClassMembers.Properties.cs`) to handle the mixed case:
   - When auto-property has a companion custom setter: emit property with auto-get + custom set body
   - When auto-property has a companion custom getter: emit property with custom get body + auto-set
   - Generate the backing field as `_name` (matching existing private field convention)

3. **Update semantic analysis** to recognize the mixed pattern and set appropriate `CodeGenInfo`.

**Files to change:**
- `src/Sharpy.Compiler/Semantic/Validation/PropertyValidator.cs` — lift restriction
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.Properties.cs` — mixed codegen
- `src/Sharpy.Compiler/Semantic/TypeChecker.Definitions.cs` — mixed property recognition

**Test cases to add:**
- Auto-property + custom setter (validation in setter)
- Auto-property + custom getter (computed transform on get)
- Auto-property + custom getter + custom setter (auto only defines backing field)
- Custom setter type mismatch with auto-property type → error
- Custom getter return type mismatch with auto-property type → error

---

## Phase 4: `@dataclass` Decorator

### 4.1 — Core `@dataclass` Support

**Scope: Large | Priority: High | Breaking: No (additive)**

Add a built-in `@dataclass` decorator recognized by the compiler that auto-generates `__init__`,
`__eq__`, and `__repr__` from class field declarations.

```python
@dataclass
class Point:
    x: float
    y: float
    # Auto-generates:
    #   __init__(self, x: float, y: float)
    #   __eq__(self, other: object) -> bool
    #   __repr__(self) -> str
```

**Design rules:**
- Fields with defaults must come after fields without (same as Python)
- Explicit `__init__` overrides the generated one (opt-out per method)
- `__post_init__(self)` hook called at end of generated `__init__`
- Inherited fields from `@dataclass` parent are included in parameter list (parent fields first)
- `@dataclass` can be applied to classes only (not structs, enums, interfaces, unions)

**Parameters (phase 1 — keep simple):**
- `@dataclass` — default: generate `__init__`, `__eq__`, `__repr__`
- `@dataclass(frozen=True)` — all fields become init-only, also generates `__hash__`
- `@dataclass(eq=False)` — skip `__eq__` generation
- `@dataclass(repr=False)` — skip `__repr__` generation

**Implementation steps:**

1. **Lexer/Parser:** `@dataclass` is already parseable as a decorator with optional arguments.
   No lexer/parser changes needed. The decorator arguments (`frozen=True`, etc.) parse as keyword
   arguments.

2. **Semantic — decorator recognition** (`TypeChecker.Definitions.cs` or `DecoratorValidator.cs`):
   - Recognize `@dataclass` as a built-in decorator on class definitions
   - Extract parameters (`frozen`, `eq`, `repr`)
   - Validate: applied to class only, not combined with conflicting decorators
   - Record on `TypeSymbol` (new `IsDataclass` flag + `DataclassOptions` record)

3. **Semantic — field analysis** (new `DataclassFieldCollector` or in TypeChecker):
   - Collect all typed field declarations (not properties, not methods)
   - Enforce ordering: non-default fields before default fields
   - Handle inheritance: collect parent `@dataclass` fields recursively
   - Store field list on `TypeSymbol.DataclassFields`

4. **Semantic — synthetic method generation:**
   - If no explicit `__init__`: synthesize `FunctionSymbol` for `__init__` with parameters matching fields
   - If no explicit `__eq__`: synthesize `FunctionSymbol` for `__eq__`
   - If no explicit `__repr__`: synthesize `FunctionSymbol` for `__repr__`
   - If `frozen=True` and no explicit `__hash__`: synthesize `__hash__`
   - Register synthetic methods in symbol table

5. **CodeGen** (`RoslynEmitter.ClassMembers.Constructors.cs` and new `.Dataclass.cs` partial):
   - Emit constructor from field list (parameter per field, `this.field = param` assignments)
   - If `__post_init__` exists, emit call at end of constructor
   - Emit `Equals`/`GetHashCode` override for `__eq__`
   - Emit `ToString` override for `__repr__`
   - If `frozen=True`: emit fields as `{ get; init; }` properties instead of mutable fields

6. **Validation:**
   - `@dataclass` on non-class → error
   - Field without type annotation in `@dataclass` class → error
   - Non-default field after default field → error
   - `frozen=True` with mutable field assignment in method → error

**C# output example:**

```python
@dataclass
class Point:
    x: float
    y: float
    label: str = "origin"
```

Generates:
```csharp
public class Point
{
    public double X { get; set; }
    public double Y { get; set; }
    public string Label { get; set; } = "origin";

    public Point(double x, double y, string label = "origin")
    {
        X = x;
        Y = y;
        Label = label;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Point other) return false;
        return Equals(X, other.X) && Equals(Y, other.Y) && Equals(Label, other.Label);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y, Label);
    }

    public override string ToString()
    {
        return $"Point(x={X}, y={Y}, label={Label})";
    }
}
```

**Files to change:**
- `src/Sharpy.Compiler/Semantic/TypeChecker.Definitions.cs` — dataclass recognition
- `src/Sharpy.Compiler/Semantic/Validation/DecoratorValidator.cs` — validate @dataclass usage
- `src/Sharpy.Compiler/Semantic/Symbol.cs` — `IsDataclass`, `DataclassFields`, `DataclassOptions`
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.cs` — dispatch to dataclass codegen
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.Constructors.cs` — synthetic constructor
- New: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.Dataclass.cs` — `__eq__`, `__repr__`, `__hash__`
- `docs/language_specification/` — new `dataclass.md` spec page

**Test cases to add (file-based fixtures):**
- `dataclass_basic.spy` — simple class with 2-3 fields
- `dataclass_defaults.spy` — fields with default values
- `dataclass_frozen.spy` — frozen=True (immutable, hashable)
- `dataclass_inheritance.spy` — parent + child both @dataclass
- `dataclass_post_init.spy` — `__post_init__` validation hook
- `dataclass_explicit_init.spy` — explicit `__init__` overrides generated
- `dataclass_eq_false.spy` — `@dataclass(eq=False)` skips `__eq__`
- `dataclass_on_struct.error` — error: @dataclass on struct
- `dataclass_ordering.error` — error: non-default field after default
- `dataclass_no_type.error` — error: field without type annotation

---

## Phase 5: Spec Alignment (from Audit)

These items from the spec-vs-test alignment audit should be addressed alongside or after the above
phases. They don't require new syntax but affect spec accuracy.

### 5.1 — Resolve Divergences (from audit DIV-1 through DIV-8)

| Item | Action |
|------|--------|
| DIV-1: Decimal `m`/`M` suffix | Move to `deferred_features.md` |
| DIV-2: Variadic args position | Fix `function_variadic_arguments.md` (spec bug) |
| DIV-3: Float leading decimal `.5` | Update spec to require leading digit `0.5` |
| DIV-4: `divmod()` → `div_mod()` | Document in `builtin_functions.md` + `name_mangling.md` |
| DIV-5: Lambda default params | Reword `function_types.md` to clarify restriction is on type annotations |
| DIV-6: Integer literal overflow | Document promotion chain in `integer_literals.md` |
| DIV-7: Scientific notation `E` | Add note to `extended_numeric_literals.md` |
| DIV-8: Exception hierarchy | Update `exception_handling.md` with full alias table |

### 5.2 — Remove Stale Banners

| Item | Action |
|------|--------|
| OUT-1: Optional/Result "planned" | Remove "planned for later" language |
| OUT-2: `__getitem__`/`__setitem__` gap | Check #276/#277 status, narrow or remove banner |

---

## Dependencies

```
Phase 1 (docs) ─────────────────────────────> can start immediately
Phase 2 (access composability) ─────────────> can start immediately
Phase 3 (mixed properties) ─────────────────> can start immediately
Phase 4 (@dataclass) ──────────────────────── depends on nothing, but largest scope
Phase 5 (spec alignment) ──────────────────── can start immediately

Phase 3 and Phase 4 are independent.
Phase 4 should land before #416 (willset/didset) is designed, since observers
interact with dataclass-generated properties.
```

## Related Issues

- #416 — Property observers (willset/didset) — deferred, needs RFC after this plan lands
