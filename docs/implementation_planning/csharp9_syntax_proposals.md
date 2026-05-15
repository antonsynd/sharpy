# C# 9.0 Gap Closure: Syntax & Semantic Proposals

> **Author:** Staff compiler engineering analysis
> **Date:** 2026-03-29
> **Inputs:** `csharp9_gap_analysis.md`, GitHub issues #417 and #419,
> `docs/language_specification/*.md`, existing compiler implementation
>
> **Guiding principles:**
> - **Axiom precedence:** .NET (1) > Type Safety (3) > Python Syntax (2)
> - Reject C# syntax when an equally viable Pythonic equivalent exists, unless it hinders .NET interop
> - Every proposal must state its C# emission target (C# 9.0 ceiling)
> - `dynamic` is out of scope (Axiom 3)

---

## Table of Contents

- [Tier 1: High Priority](#tier-1-high-priority)
  - [1. ref/out/in Parameter Modifiers](#1-refoutin-parameter-modifiers)
  - [2. Implicit/Explicit Conversion Operators](#2-implicitexplicit-conversion-operators)
  - [3. Nested Types](#3-nested-types)
  - [4. Exception Filters (when clause)](#4-exception-filters-when-clause)
  - [5. lock Statement](#5-lock-statement)
  - [6. do...while Loops](#6-dowhile-loops)
  - [7. Static Constructors](#7-static-constructors)
  - [8. checked/unchecked Arithmetic](#8-checkedunchecked-arithmetic)
  - [9. Partial Classes](#9-partial-classes)
  - [10. Records and with Expressions](#10-records-and-with-expressions)
  - [11. private protected Access Modifier](#11-private-protected-access-modifier)
  - [12. nameof Equivalent](#12-nameof-equivalent)
  - [13. Object Initializers](#13-object-initializers)
  - [14. default Literal and default(T)](#14-default-literal-and-defaultt)
- [Tier 2: Medium Priority](#tier-2-medium-priority)
  - [15. Raise/Throw Expressions](#15-raisethrow-expressions)
  - [16. Multi-Catch Exception Types](#16-multi-catch-exception-types)
  - [17. is Type Pattern Outside match](#17-is-type-pattern-outside-match)
  - [18. notnull Generic Constraint](#18-notnull-generic-constraint)
  - [19. Static Classes](#19-static-classes)
  - [20. Multi-Dimensional Arrays](#20-multi-dimensional-arrays)
  - [21. LINQ Query Syntax](#21-linq-query-syntax)
  - [22. Anonymous Types](#22-anonymous-types)
  - [23. Static Lambdas](#23-static-lambdas)
  - [24. readonly struct](#24-readonly-struct)
  - [25. ref struct](#25-ref-struct)
  - [26. readonly Fields](#26-readonly-fields)
  - [27. Range/Index Operators](#27-rangeindex-operators)
  - [28. Block-less Disposable Scope](#28-block-less-disposable-scope)
  - [29. volatile Fields](#29-volatile-fields)
  - [30. Lambda Type Annotations](#30-lambda-type-annotations)
  - [31. General Delegate Combination](#31-general-delegate-combination)
  - [32. Expression Trees](#32-expression-trees)
  - [33. Caller Info Attributes](#33-caller-info-attributes)
  - [34. Await in Catch/Finally](#34-await-in-catchfinally)
  - [35. Ref Locals and Ref Returns](#35-ref-locals-and-ref-returns)
  - [36. Tuple Equality](#36-tuple-equality)
  - [37. Static Local Functions](#37-static-local-functions)
- [Appendix: Rejected Alternatives](#appendix-rejected-alternatives)

---

## Tier 1: High Priority

### 1. ref/out/in Parameter Modifiers

**Status:** Spec exists (`parameter_modifiers.md`, `structs.md`), issue #419. Not implemented.

**Recommendation: Adopt the existing spec as-is.** The spec already provides a Pythonic
design with `ref[T]`, `out[T]`, `in[T]` type-wrapper syntax. The struct spec additionally
documents `mut[T]` as a Sharpy-specific alias for `ref[T]` on structs.

**Syntax (already specified):**

```python
def swap(a: ref[int], b: ref[int]):
    temp = a
    a = b
    b = temp

def try_parse(s: str, result: out[int]) -> bool:
    result = int(s) if s.is_digit() else 0
    return s.is_digit()

def analyze(data: in[LargeStruct]) -> float:
    return data.value
```

**Call-site syntax (already specified):**

```python
swap(ref x, ref y)
try_parse("42", out value: int)    # inline out declaration
analyze(large)                      # in is optional at call site
```

**Reconciliation of `mut[T]` vs `ref[T]`:**

The struct spec introduces `mut[T]` as a Pythonic alias for `ref[T]` specifically for
struct mutation semantics. The parameter_modifiers spec uses `ref[T]` directly. These
should be unified:

| Sharpy | C# Emission | Semantics |
|--------|-------------|-----------|
| `ref[T]` | `ref T` | Read/write reference (any type) |
| `mut[T]` | `ref T` | Alias for `ref[T]` (Pythonic intent signaling for structs) |
| `out[T]` | `out T` | Output parameter |
| `in[T]` | `in T` | Readonly reference |

**Design decision needed:** Should `mut[T]` be kept as a synonym for `ref[T]`, or should
it be dropped? **Recommendation: Drop `mut[T]`.** Having two spellings for the same
C# concept (`ref`) violates "one way to do things." The `ref` name is already well-known
to .NET developers and the semantic difference ("I intend to mutate" vs "I need a reference")
is not enforced by the compiler — both emit `ref`. Keep `ref[T]` only. Update the struct
spec accordingly.

**C# emission:**

```csharp
public static void Swap(ref int a, ref int b) { ... }
public static bool TryParse(string s, out int result) { ... }
public static float Analyze(in LargeStruct data) { ... }

// Call site
Swap(ref x, ref y);
TryParse("42", out int value);
Analyze(in largeData);   // or just Analyze(largeData)
```

**Scope:** Lexer (contextual keywords `ref`, `out`, `in` at parameter + call site),
Parser (parameter modifier + call-site expression), Semantic (definite assignment for `out`,
immutability for `in`, no-default/no-varargs validation), CodeGen (emit modifiers).

---

### 2. Implicit/Explicit Conversion Operators

**Status:** Spec stub exists (`conversion_operators.md`) — placeholder only. Not implemented.

**Recommendation: Use dunder methods `__implicit__` and `__explicit__`.**

The gap analysis mentions `@implicit`/`@explicit` decorators but the actual spec is blank.
Dunders are the established Sharpy pattern for operator-like behavior (see `__add__`, `__eq__`,
`__bool__`, etc.). Decorators in Sharpy are either access/modifier keywords or .NET attributes —
using them for conversion semantics breaks that clean split.

**Proposed syntax:**

```python
class Celsius:
    value: float

    def __init__(self, value: float):
        self.value = value

    # Implicit conversion FROM float TO Celsius
    @static
    def __implicit__(value: float) -> Celsius:
        return Celsius(value)

    # Explicit conversion FROM Celsius TO int
    @static
    def __explicit__(self_val: Celsius) -> int:
        return int(self_val.value)
```

**Usage:**

```python
temp: Celsius = 98.6              # implicit conversion (float → Celsius)
degrees: int = temp to int        # explicit conversion via `to` operator
```

**Why dunders over decorators:**

1. **Consistency**: All other operator-like behaviors use dunders (`__add__`, `__str__`, `__bool__`).
2. **Discoverability**: `__implicit__` and `__explicit__` are self-documenting — "this type
   has an implicit conversion."
3. **Clean decorator model**: Decorators remain either modifier keywords or .NET attributes.
4. **Static by nature**: Conversion operators in C# are always `static`. The `@static` decorator
   (or absence of `self`) makes this explicit.

**Rules:**

- `__implicit__` / `__explicit__` must be `@static` (no `self` first param).
- Exactly one parameter (the source type). Return type is the target type.
- At least one of {parameter type, return type} must be the enclosing type.
- Cannot define both implicit and explicit for the same source→target pair.
- Implicit conversions must be lossless (compiler cannot enforce this, but documentation
  should emphasize it per C# conventions).

**C# emission:**

```csharp
public static implicit operator Celsius(double value) => new Celsius(value);
public static explicit operator int(Celsius selfVal) => (int)selfVal.Value;
```

**Alternative rejected:** `@implicit def __convert__` — mixes decorator and dunder
unnecessarily. See [Appendix](#appendix-rejected-alternatives).

---

### 3. Nested Types

**Status:** No spec, no implementation.

**Recommendation: Allow indented type declarations inside class/struct/interface bodies.**

Python supports nested classes naturally via indentation. Sharpy should follow suit — the
indentation-based scoping already provides the visual containment that C# achieves with braces.

**Proposed syntax:**

```python
class LinkedList[T]:
    _head: Node[T]?

    class Node[T]:
        """Private helper type nested inside LinkedList."""
        value: T
        next: Node[T]?

        def __init__(self, value: T, next: Node[T]? = None):
            self.value = value
            self.next = next

    def __init__(self):
        self._head = None

    def prepend(self, value: T):
        self._head = LinkedList.Node(value, self._head)
```

**Access semantics:**

- Nested types can access `@private` members of the enclosing type (matches C#).
- The default access modifier for nested types is `@private` (matches C# `private`),
  not `@public` (Sharpy's usual default for top-level types). This is an Axiom 1 win:
  C# nested types default to `private`, and this is actually the more sensible default
  for helper types.
- Outer code refers to nested type as `Outer.Inner` (dot notation).
- Nested types can be classes, structs, interfaces, or enums. Not delegates (delegates
  in Sharpy are already standalone).

**Nesting depth:** Arbitrary (C# allows arbitrary nesting). Pragmatically, more than 2
levels deep is a code smell, but the compiler should not restrict it.

**Restrictions:**
- Nested types cannot directly reference `self` of the enclosing type (unlike Java inner
  classes). They are statically nested, matching C#'s semantics. If the nested type needs
  a reference to the outer instance, it must be passed explicitly.
- No `outer` keyword — this would introduce complexity for minimal gain.

**C# emission:**

```csharp
public class LinkedList<T>
{
    private Node<T>? _head;

    private class Node<T>    // nested, private by default
    {
        public T Value { get; set; }
        public Node<T>? Next { get; set; }
        public Node(T value, Node<T>? next = null) { ... }
    }
    ...
}
```

---

### 4. Exception Filters (`when` clause)

**Status:** No spec, no implementation.

**Recommendation: `except Type as e when condition:`**

This extends the existing `except` clause with `when`, which is both Pythonic in feel
(reads as English: "except ValueError as e when the code is 42") and maps directly to
C# `catch ... when`. Python does not have exception filters, so there is no conflicting
Python idiom — `when` is a clean extension.

**Proposed syntax:**

```python
try:
    result = risky_operation()
except ValueError as e when e.message.contains("timeout"):
    handle_timeout(e)
except ValueError as e when e.message.contains("auth"):
    handle_auth_error(e)
except ValueError as e:
    handle_other_value_error(e)
except Exception as e:
    handle_generic(e)
```

**`when` without binding:**

```python
except IOError when retries < max_retries:
    retries += 1
    retry()
```

**Grammar extension:**

```
except_clause ::= 'except' [type ['as' name] ['when' expression]] ':'
```

**Key properties:**

- `when` is a **soft keyword** — only meaningful after `except ... as name` or `except Type`.
  The identifier `when` remains usable as a variable name elsewhere.
- The `when` expression can reference the bound exception variable (`e`).
- The `when` expression must be a `bool` expression.
- Exception filters preserve the stack trace (they don't catch-and-rethrow). This is a
  key advantage over `if` inside a catch body — matching C# semantics exactly.

**C# emission:**

```csharp
catch (ValueError e) when (e.Message.Contains("timeout"))
{
    HandleTimeout(e);
}
```

---

### 5. `lock` Statement

**Status:** No spec, no implementation.

**Recommendation: `with lock(obj):`**

Rather than introducing a new keyword or statement form, reuse the existing `with` statement
paired with a `lock()` builtin. This is Pythonic (Python's `threading.Lock` uses `with`),
and maps cleanly to C#'s `lock`.

**Proposed syntax:**

```python
_sync = object()

def thread_safe_increment(self):
    with lock(self._sync):
        self._count += 1
```

**Why `with lock(obj):` over bare `lock obj:`:**

1. **Pythonic**: Python programmers already use `with threading.Lock():` for synchronization.
2. **No new keyword**: `lock` becomes a builtin function, not a keyword. This avoids
   breaking existing code that might use `lock` as an identifier.
3. **Consistent with `with` semantics**: The `lock()` call returns an `IDisposable`-like
   scope guard, fitting the `with` pattern.
4. **Familiar to C# developers**: The emitted C# is a direct `lock` statement.

**`lock()` is a compiler intrinsic**, not a runtime function. It signals the compiler to
emit a `lock` statement rather than a `using` statement. This is necessary because C#'s
`lock` has specific semantics (Monitor.Enter/Monitor.Exit with exception safety) that
differ from `using`.

**C# emission:**

```csharp
lock (_sync)
{
    this._count += 1;
}
```

**Alternative considered:** `lock obj:` as a standalone statement. Rejected because it
introduces a new statement form and keyword for a concept that `with` already covers. See
[Appendix](#appendix-rejected-alternatives).

---

### 6. `do...while` Loops

**Status:** `do` is a reserved keyword in the lexer (as a future keyword for `do:` expression
blocks). Not parsed.

**Recommendation: `while True: ... if not condition: break` remains the idiom.
Alternatively, introduce `loop: ... while condition` syntax.**

This is the most contentious gap because `do` is already reserved for expression blocks
(`do:` blocks, spec'd in `expression_blocks.md`). Using `do` for do-while would create
ambiguity.

**Option A — No new syntax (status quo):**

```python
while True:
    body()
    if not condition:
        break
```

This is 3 lines instead of C#'s 2 (`do { body } while (cond);`), but it is idiomatic
in Python. Python itself has never added do-while.

**Option B — `loop: ... while condition` (recommended):**

```python
loop:
    body()
while condition
```

This reads naturally: "loop the body while the condition holds." The `while condition`
without a colon (no block follows) acts as a terminator, not a new loop head.

**Grammar:**

```
loop_statement ::= 'loop' ':' NEWLINE INDENT statement+ DEDENT 'while' expression NEWLINE
```

**Why `loop` instead of `do`:**

- `do` is reserved for expression blocks (`do:` blocks). Reusing `do` for do-while
  creates parsing ambiguity: `do:` followed by statements could be either construct.
- `loop` is a clean, unambiguous keyword. It doesn't conflict with any existing Sharpy
  or Python keyword.
- `loop` is a **new hard keyword**. Code using `loop` as an identifier would need to change.
  Given that Sharpy has no prior use of `loop`, this is low-risk.

**C# emission:**

```csharp
do
{
    Body();
}
while (condition);
```

**Recommendation: Implement Option B.** The `while True: ... break` pattern is verbose for
a common loop shape, and C# interop sometimes requires 1:1 structural mapping for readability.

---

### 7. Static Constructors

**Status:** No spec, no implementation.

**Recommendation: `@static def __init__():` (no `self` parameter).**

This follows Sharpy's existing pattern where `@static` (or absence of `self`) marks a
method as static, and `__init__` is the constructor dunder.

**Proposed syntax:**

```python
class Registry:
    _instance: Registry?

    @static
    def __init__():
        """Runs once before any member is accessed."""
        Registry._instance = Registry._create()

    @static
    def _create() -> Registry:
        return Registry()
```

**Alternatively, without the `@static` decorator (implicit static due to no `self`):**

```python
class Registry:
    _cache: dict[str, object]

    def __init__():
        """Static constructor — no self parameter means static."""
        Registry._cache = {}
```

**Rules:**

- A static constructor has no parameters (no `self`, no other params).
- A class may have at most one static constructor.
- The static constructor cannot have access modifiers (always `private` in C#).
- Cannot be called directly by user code — the runtime calls it.
- Cannot have a return type annotation (implicitly `-> None`).

**Disambiguation from instance constructor:**

The parser already distinguishes static vs instance methods by the presence of `self`.
`def __init__(self, ...)` is an instance constructor; `def __init__()` (no params at all)
is a static constructor. This is unambiguous.

**C# emission:**

```csharp
static Registry()
{
    Registry._cache = new Dictionary<string, object>();
}
```

---

### 8. `checked`/`unchecked` Arithmetic

**Status:** No spec, no implementation.

**Recommendation: `checked:` / `unchecked:` block statements.**

These are inherently block-scoped concepts in C#. Sharpy should use indentation blocks,
consistent with all other block constructs (`if:`, `while:`, `with:`, etc.).

**Proposed syntax:**

```python
# Block form
checked:
    result = a + b          # OverflowException on overflow
    product = x * y

unchecked:
    hash_val = a * 2654435761    # Wraps silently on overflow
```

**Expression form (inline):**

For single expressions, allow `checked(expr)` and `unchecked(expr)` as builtin-like
call syntax:

```python
result = checked(a + b)
hash_val = unchecked(a * 2654435761)
```

**Keywords:** `checked` and `unchecked` become **soft keywords** — only recognized at
statement position (before `:`) or as call-like expressions. They remain usable as
identifiers in other positions (though this would be poor style).

**C# emission:**

```csharp
// Block form
checked
{
    var result = a + b;
    var product = x * y;
}

// Expression form
var result = checked(a + b);
var hashVal = unchecked(a * 2654435761);
```

---

### 9. Partial Classes

**Status:** No spec, no implementation.

**Recommendation: `@partial class Foo:` decorator.**

The `@partial` decorator aligns with Sharpy's decorator model for class modifiers
(`@abstract`, `@final`, `@dataclass`). This is purely a .NET interop feature — Python
has no equivalent concept.

**Proposed syntax:**

```python
# file: player_core.spy
@partial
class Player:
    name: str
    health: int

    def __init__(self, name: str):
        self.name = name
        self.health = 100

# file: player_rendering.spy  (same project)
@partial
class Player:
    def render(self, screen: Screen):
        screen.draw_sprite(self.name, self.x, self.y)
```

**Rules:**

- All parts of a partial class must be in the same project (assembly).
- All parts must use `@partial` (if any part omits it, error).
- Type parameters, base class, and interfaces must be consistent across all parts
  (or specified in only one part, with the others omitting the base list).
- Partial methods (C# 9.0 extended partial methods):

```python
@partial
class Player:
    # Declaring declaration (no body)
    @partial
    def on_damage(self, amount: int): ...

# In another part:
@partial
class Player:
    # Implementing declaration
    @partial
    def on_damage(self, amount: int):
        self.health -= amount
        self.play_hurt_animation()
```

**Semantic merging:** The semantic phase merges all `@partial` declarations of the same
class into a single `TypeSymbol`. Fields, methods, properties, and events from all parts
are combined. Conflicts (duplicate member names with incompatible signatures) are errors.

**C# emission:**

```csharp
// player_core.spy →
public partial class Player
{
    public string Name { get; set; }
    public int Health { get; set; }
    public Player(string name) { ... }
}

// player_rendering.spy →
public partial class Player
{
    public void Render(Screen screen) { ... }
}
```

---

### 10. Records and `with` Expressions

**Status:** No spec. `@dataclass` provides partial coverage.

**Recommendation: Extend `@dataclass` rather than introducing a `record` keyword.**

Sharpy already has `@dataclass` which generates `__eq__`, `__hash__`, `__repr__`, and
`__init__`. C# records are essentially dataclasses with `with`-expression support and
inheritance. Rather than introducing a new keyword that duplicates `@dataclass` semantics,
extend `@dataclass` to cover the remaining gaps.

**10a. `copy()` method for non-destructive mutation (replaces C#'s `with` expression):**

C#'s `p2 = p1 with { X = 5 }` cannot use `with` in Sharpy because `with` is already
the context manager keyword. Instead, provide a synthesized `copy()` method:

```python
@dataclass(frozen=True)
class Point:
    x: int
    y: int

p1 = Point(1, 2)
p2 = p1.copy(x=5)       # Point(x=5, y=2)
p3 = p1.copy(x=10, y=20)  # Point(x=10, y=20)
```

**Why `copy()` over a new keyword:**

1. **`with` is taken** — it's the context manager keyword. Reusing it for mutation syntax
   would require disambiguation that makes parsing harder and code less readable.
2. **Pythonic**: Python's `dataclasses.replace(obj, **kwargs)` serves the same purpose.
   A method syntax `obj.copy(field=value)` is more idiomatic than a keyword.
3. **Discoverable**: Method on the object, visible in autocomplete.
4. **No new syntax**: Works within existing method call + named argument syntax.

**`copy()` is auto-synthesized** by the `@dataclass` decorator when `frozen=True`. For
mutable dataclasses, `copy()` is also synthesized but returns a shallow clone with the
specified fields overridden.

**C# emission:**

```csharp
// p1.copy(x=5) emits as:
p1 with { X = 5 }
```

The `copy()` method does not exist in the generated C#. The compiler recognizes `copy()`
calls on `@dataclass` types and directly emits C# `with` expressions. This requires
the generated C# type to be a `record` (for classes) or `record struct` (post C# 10).

**Important C# emission detail:** To support `with` expressions in C# 9.0, `@dataclass`
classes must emit as C# `record` types (not regular classes). This is a codegen change
with semantic implications:

- C# `record` types have value equality by default → matches `@dataclass` semantics.
- C# `record` types support `with` expressions → enables `copy()`.
- C# `record` types support inheritance → matches `@dataclass` inheritance.
- **Caveat:** C# 9.0 only has `record` (class-based). `record struct` requires C# 10.
  Therefore, `@dataclass` on `struct` cannot use `with` expressions in C# 9.0 target.
  For structs, `copy()` must be lowered to manual field-by-field copy.

**10b. Dataclass inheritance (already partially supported):**

The existing spec already supports `@dataclass` inheritance. Verify that the implementation
handles the `record` inheritance chain in C# emission correctly.

**10c. Positional deconstruction:**

Dataclass fields should be deconstructible in `match` patterns via positional patterns
(already supported by the pattern matching system). Verify integration.

---

### 11. `private protected` Access Modifier

**Status:** No spec, no implementation.

**Recommendation: `@private @protected` decorator combination.**

Sharpy already supports `@private`, `@protected`, `@public`, and `@internal` as
individual decorators. The combined `private protected` access level (accessible within
same assembly AND derived types only) can be expressed as a decorator combination.

**Proposed syntax:**

```python
class BaseLibrary:
    @private
    @protected
    def internal_hook(self):
        """Accessible only by derived types within this assembly."""
        pass
```

**Order does not matter** (`@protected @private` is equivalent) — the compiler recognizes
the combination.

**C# emission:**

```csharp
private protected void InternalHook() { ... }
```

**Also needed: `@protected @internal`** (C#'s `protected internal` — accessible by derived
types OR within same assembly). This is distinct from `@private @protected`:

| Sharpy | C# | Meaning |
|--------|----|----|
| `@private @protected` | `private protected` | Derived types AND same assembly |
| `@protected @internal` | `protected internal` | Derived types OR same assembly |

**Scope:** Semantic (validate combined modifier), CodeGen (emit compound access modifier).
Small change.

---

### 12. `nameof` Equivalent

**Status:** No spec, no implementation.

**Recommendation: `nameof(x)` as a compile-time builtin.**

There is no Pythonic alternative — Python has no `nameof`. The C# syntax is already clean
and readable. Adopting it verbatim costs nothing in Pythonic-ness and provides exact .NET
interop.

**Proposed syntax:**

```python
def set_name(self, name: str):
    if name is None:
        raise ValueError(f"{nameof(name)} cannot be None")
    self._name = name

class Observable:
    _value: int

    property value: int

    property set value(self, val: int):
        self._value = val
        self.notify(nameof(self.value))
```

**Compile-time semantics:**

- `nameof(x)` evaluates to the **Sharpy-side name** as a string literal at compile time.
- `nameof(self.value)` → `"value"` (snake_case, the Sharpy identifier).
- The argument must be a valid symbol reference (variable, parameter, member, type).
- The symbol must be in scope but need not be initialized.
- Cannot be used on expressions or literals: `nameof(1 + 2)` is an error.

**C# emission:**

```csharp
// nameof(name) → nameof(name)  (C# nameof uses C# identifier)
// BUT: the Sharpy identifier is snake_case while C# is PascalCase
// So nameof(self.value) emits as nameof(Value) — the mangled name
throw new ValueError($"{nameof(name)} cannot be None");
```

**Name mangling consideration:** `nameof()` should emit the **C# (PascalCase) name** in
the generated code (since `nameof` is a C# construct). But the **Sharpy-side value** should
also be documented — users might expect `nameof(my_field)` to produce `"my_field"`, but it
will actually produce `"MyField"` at runtime (because C# `nameof(MyField)` returns
`"MyField"`). This is an Axiom 1 win: the runtime name is the .NET name.

**Scope:** Lexer (soft keyword or builtin), Parser (special expression),
Semantic (resolve symbol, validate), CodeGen (emit `nameof()` with mangled name).

---

### 13. Object Initializers

**Status:** No spec, no implementation.

**Recommendation: Extend constructor call syntax with trailing named arguments that
bind to settable properties.**

Sharpy already supports named arguments in constructor calls. The natural extension is to
allow named arguments that don't match constructor parameters to bind to settable properties
instead, forming an object initializer.

**Proposed syntax:**

```python
class Config:
    property name: str = ""
    property debug: bool = False
    property retries: int = 3

    def __init__(self):
        pass

# Object initializer — named args that aren't constructor params set properties
config = Config(name="prod", debug=True, retries=5)
```

**Disambiguation rules:**

1. Named arguments that match constructor parameters bind to the constructor (existing behavior).
2. Named arguments that do NOT match any constructor parameter attempt to bind to a
   settable property (or `init` property) on the type.
3. If a named argument matches neither a constructor parameter nor a settable property,
   it's a compile error.
4. Constructor parameter bindings must come before property initializer bindings in the
   argument list (matching C#'s requirement that initializers come after construction).

**Example with mixed constructor params and property init:**

```python
class User:
    property email: str = ""

    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age

# Constructor params first, then property initializers
user = User("Alice", 30, email="alice@example.com")
```

**C# emission:**

```csharp
var config = new Config { Name = "prod", Debug = true, Retries = 5 };
var user = new User("Alice", 30) { Email = "alice@example.com" };
```

**Why this over `Foo() with { ... }` syntax:**

1. **No new syntax needed**: Existing named-argument calling convention naturally extends.
2. **Pythonic**: Python's own `dataclass` constructors accept keyword arguments for fields,
   blurring the line between "constructor parameter" and "field initialization."
3. **Readable**: `Config(name="prod", debug=True)` reads like Python object creation.
4. **C#'s `with` keyword conflicts** with Sharpy's context manager `with`.

**Collection initializers:** Out of scope for this proposal. Sharpy's list/dict literals
(`[1, 2, 3]`, `{"key": "value"}`) already cover the most common initialization patterns.

---

### 14. `default` Literal and `default(T)`

**Status:** No spec, no implementation.

**Recommendation: `default` as a keyword and `default[T]` for explicit type.**

**Proposed syntax:**

```python
# Bare default (type inferred from context)
x: int = default          # 0
name: str? = default      # None
flag: bool = default      # False

# Explicit type (for generic contexts or disambiguation)
def create[T]() -> T:
    return default[T]

# In function calls
process(default[int])
```

**Why `default[T]` instead of `default(T)`:**

Sharpy uses `[]` for type parameterization everywhere (`list[int]`, `dict[str, int]`,
`Box[T]`). Using `default[T]` is consistent with this convention. `default(T)` would
look like a function call, which it is not — `default` is a compile-time construct.

**Bare `default` inference:**

- In typed declarations: `x: int = default` → type from annotation.
- In return statements: `return default` → type from function return type.
- In assignments to typed variables: existing variable type provides context.
- Without context: compile error ("cannot infer type for `default`").

**C# emission:**

```csharp
int x = default;
string? name = default;
bool flag = default;
return default(T);    // or just default in C# 7.1+
```

**`default` becomes a hard keyword.** It is currently not reserved. Impact: code using
`default` as an identifier will break. Given that `default` is a keyword in both C# and
Python (in `match`/`case`), this is acceptable.

---

## Tier 2: Medium Priority

### 15. Raise/Throw Expressions

**Recommendation: Allow `raise` in expression position.**

```python
name = input_name ?? raise ValueError("name required")
value = x if x > 0 else raise ValueError("must be positive")
```

**Grammar change:** `raise` becomes an expression when used in `??` RHS, conditional else
branch, or other expression contexts. The type of a `raise` expression is `Never` (bottom
type) — compatible with any expected type.

**C# emission:** `throw new ValueError(...)` (C# throw expression, available since C# 7.0).

---

### 16. Multi-Catch Exception Types

**Recommendation: `except (Type1 | Type2) as e:` — Python 3 syntax.**

```python
try:
    data = fetch()
except (IOError | TimeoutError) as e:
    retry(e)
except (ValueError | TypeError) as e:
    log_bad_input(e)
```

**Why parenthesized union:** Python 3 uses `except (T1, T2)` with a tuple. Sharpy should
use `|` (pipe) inside parentheses because: (a) Sharpy doesn't have union types as tuples,
(b) `|` visually reads as "or" which matches the intent, (c) avoids confusion with tuple
syntax.

**Type of `e`:** The common base type of all listed exception types. If `IOError` and
`TimeoutError` both inherit from `Exception`, then `e: Exception`.

**C# emission:** Multiple `catch` clauses with `when` filters, or (if all types share a
common base) a single `catch` with `is` checks. The most direct C# 9.0 mapping:

```csharp
catch (Exception e) when (e is IOException or e is TimeoutException)
{
    Retry(e);
}
```

---

### 17. `is` Type Pattern Outside `match`

**Recommendation: Extend `isinstance()` with binding via walrus operator.**

Sharpy already has `isinstance(x, T)` with type narrowing in `if` blocks. For binding
(C#'s `if (x is Type t)`), combine with the existing walrus operator:

```python
# Type test with narrowing (already works)
if isinstance(obj, Dog):
    obj.bark()          # obj is narrowed to Dog

# Type test with binding to new name (proposed)
if isinstance(obj, Dog) and (dog := obj) is not None:
    dog.bark()

# BETTER: extend isinstance to support binding directly
if (dog := obj to Dog?) is not None:
    dog.bark()          # dog is Dog
```

**The safe cast + walrus pattern already works** with existing syntax:
`(name := expr to Type?) is not None`. No new syntax needed for the binding case.

**For relational patterns outside `match`:** Not proposed. `if x > 5:` already works.
C#'s `if (x is > 5)` adds nothing over `if x > 5` in Sharpy.

---

### 18. `notnull` Generic Constraint

**Recommendation: `T: notnull` constraint syntax.**

```python
def ensure[T: notnull](value: T) -> T:
    return value

class Registry[K: notnull, V](dict[K, V]):
    pass
```

**`notnull`** becomes a **soft keyword** in constraint position. It prevents `T?`
(optional/nullable) from being used as a type argument.

**C# emission:**

```csharp
public static T Ensure<T>(T value) where T : notnull => value;
```

**Also add `unmanaged` constraint** for completeness with low-level interop:

```python
def pin[T: unmanaged](value: T) -> T:
    return value
```

---

### 19. Static Classes

**Recommendation: No new syntax. Document that modules serve this purpose.**

Sharpy modules already compile to `static class ModuleName`. A user-defined "static class"
would be a module. If a user needs a static class within a namespace, they write a module
file. This is the Pythonic way — modules are the organization unit.

If strong demand arises, `@static class Foo:` could be supported where the `@static`
decorator is applied to a class, constraining all members to be static. But this is
low value.

---

### 20. Multi-Dimensional Arrays

**Recommendation: `array[T, rank]` syntax for multi-dimensional arrays.**

```python
# 2D array
matrix: array[int, 2] = array[int, 2](3, 4)     # 3x4 matrix
matrix[0, 1] = 42

# 3D array
cube: array[float, 3] = array[float, 3](2, 3, 4)
cube[0, 1, 2] = 3.14

# Jagged arrays (already possible)
jagged: list[list[int]] = [[1, 2], [3, 4, 5]]
```

**C# emission:**

```csharp
int[,] matrix = new int[3, 4];
matrix[0, 1] = 42;

float[,,] cube = new float[2, 3, 4];
cube[0, 1, 2] = 3.14;
```

**Deferred**: This is low priority. `list[list[T]]` covers most cases.

---

### 21. LINQ Query Syntax

**Recommendation: Do not implement.** LINQ method syntax works via .NET interop, and
Sharpy's comprehensions (`[x for x in items if x > 0]`) cover the functional equivalent.
LINQ query syntax (`from x in y where z select x`) is C#-specific sugar that has no
Pythonic equivalent and would be jarring in Sharpy code.

---

### 22. Anonymous Types

**Recommendation: Do not implement.** Named tuples (`(name: "x", value: 1)`) cover the
primary use case. Anonymous types are a C#-specific compiler trick primarily useful for
LINQ projections, which Sharpy handles differently.

---

### 23. Static Lambdas

**Recommendation: No special syntax. Use `@static` on nested `def` instead (#37).**

C#'s `static x => x + 1` prevents accidental captures. In Sharpy, if capture prevention
is needed, users should use `@static def` (see #37). Lambdas are intentionally lightweight
and should not carry modifier burden.

---

### 24. `readonly struct`

**Recommendation: `@dataclass(frozen=True)` on structs.**

`@dataclass(frozen=True)` on a struct already signals immutability. The codegen should
emit `readonly struct` when this combination is used.

```python
@dataclass(frozen=True)
struct Vec2:
    x: float
    y: float
```

**C# emission:**

```csharp
public readonly struct Vec2
{
    public readonly double X;
    public readonly double Y;
    ...
}
```

**Note:** The `@dataclass` spec currently says it can only be applied to classes (SPY0380).
This restriction should be lifted for `frozen=True` on structs specifically to enable
`readonly struct` emission.

---

### 25. `ref struct`

**Recommendation: `@ref struct` decorator.**

```python
@ref
struct SpanWrapper[T]:
    _span: Span[T]

    def __init__(self, span: Span[T]):
        self._span = span
```

**Rules (matching C#):**
- Cannot be boxed (no interface implementation).
- Cannot be a field of a class or non-ref struct.
- Cannot be used in async methods.
- Cannot be captured in lambdas.

**C# emission:** `public ref struct SpanWrapper<T> { ... }`

---

### 26. `readonly` Fields

**Recommendation: `final` keyword for runtime-immutable fields.**

Sharpy has `const` for compile-time constants. For runtime-computed values that are
assigned once (in the constructor) and then immutable, use `final`:

```python
class Connection:
    final connection_id: str

    def __init__(self, id: str):
        self.connection_id = id    # OK: assignment in constructor
        # self.connection_id = "x" # ERROR after first assignment
```

**Why `final` over `readonly`:**

- `final` is already a Sharpy keyword concept (`@final` for sealed classes/methods).
  Extending it to fields is natural.
- `readonly` is C#-specific terminology. Python has no direct equivalent, but Java/Kotlin
  use `final` and `val` respectively — `final` is more widely understood.
- `const` = compile-time constant. `final` = runtime-assigned-once immutability. Clean split.

**C# emission:**

```csharp
public readonly string ConnectionId;
```

**Alternative:** `const` could be overloaded, but this conflates compile-time and runtime
immutability, which are fundamentally different in .NET.

---

### 27. Range/Index Operators

**Recommendation: No new syntax. Sharpy's Python-style slicing covers this.**

Sharpy's `arr[-1]` (negative indexing) and `arr[1:3]` (slicing) are more Pythonic than
C#'s `arr[^1]` and `arr[1..3]`. The compiler already handles these patterns.

If `System.Range`/`System.Index` types are needed for .NET interop, they can be constructed
explicitly:

```python
from system import Index, Range
idx = Index(1, from_end=True)    # equivalent to ^1
rng = Range(Index(1), Index(3))  # equivalent to 1..3
```

---

### 28. Block-less Disposable Scope

**Recommendation: `use x = Resource():` statement.**

```python
def process():
    use conn = open_connection()
    use stream = conn.open_stream()
    # Both disposed at end of enclosing block (function scope)
    stream.write(data)
    return stream.read_all()
```

**Why `use` instead of extending `with`:**

- `with` always introduces a block in Sharpy (and Python). Changing this would break the
  mental model.
- `use` is a new keyword that clearly signals "dispose at end of enclosing scope."
- Reads naturally: "use this resource for the rest of the scope."

**C# emission:**

```csharp
using var conn = OpenConnection();
using var stream = conn.OpenStream();
stream.Write(data);
return stream.ReadAll();
```

**`use` becomes a hard keyword.** Low impact — `use` is not commonly used as an identifier.

---

### 29. `volatile` Fields

**Recommendation: `@volatile` modifier decorator on fields.**

```python
class SharedState:
    @volatile
    _flag: bool = False
```

**C# emission:** `private volatile bool _flag = false;`

This is a .NET interop concern — `@volatile` would be a modifier keyword decorator
(like `@static`), added to `DecoratorNames.KnownModifierDecorators`, not a bracket attribute.

---

### 30. Lambda Type Annotations

**Status:** RFC open (#417).

**Recommendation: Parenthesized parameters for typed lambdas.**

```python
f = lambda (x: int): x + 1
g = lambda (x: int, y: str): f"{y}: {x}"
h = lambda (items: list[int]): sum(items)
```

**Why parenthesized:**

The double-colon problem (`lambda x: int: x + 1`) is genuinely ambiguous. Parenthesized
parameters resolve this cleanly:

- `lambda x: x + 1` — untyped (existing syntax, unchanged)
- `lambda (x: int): x + 1` — typed (new syntax)

This mirrors the `def` parameter list syntax: `def f(x: int):` uses parentheses around
typed parameters. Extending the same pattern to lambdas is consistent.

**Grammar:**

```
lambda_expr ::= 'lambda' param_list ':' expression
param_list  ::= identifier (',' identifier)*                     # untyped
              | '(' typed_param (',' typed_param)* ')'           # typed
typed_param ::= identifier ':' type
```

**Other alternatives from #417 and why they are rejected:**

| Alternative | Problem |
|-------------|---------|
| `lambda x as int: x + 1` | `as` is reserved for exception binding, imports, match patterns |
| `lambda x: int: x + 1` | Ambiguous: is `int` the type or the start of the body? |
| Require `def` for typed lambdas | Too restrictive — lambdas with types are common in LINQ-heavy code |

**C# emission:**

```csharp
(int x) => x + 1
(int x, string y) => $"{y}: {x}"
```

---

### 31. General Delegate Combination

**Recommendation: Do not implement.** Events with `+=`/`-=` cover the primary multicast
use case. General `delegate + delegate` arithmetic is rarely used outside events and adds
complexity for minimal value.

---

### 32. Expression Trees

**Recommendation: Defer to Phase 5.** This is a massive feature (every expression node
type needs an `Expression<>` builder). The scope is too large for this proposal. When
implemented, the approach should be:

- Semantic phase detects when a lambda is assigned to `Expression[Func[T]]` target type.
- CodeGen emits `System.Linq.Expressions` API calls instead of a delegate.
- No new syntax needed — target typing handles it automatically.

---

### 33. Caller Info Attributes

**Recommendation: Parameter-level decorators.**

```python
def log(
    message: str,
    @caller_member_name member: str = "",
    @caller_file_path file: str = "",
    @caller_line_number line: int = 0,
):
    print(f"[{file}:{line} {member}] {message}")
```

**Grammar extension:** Allow `@decorator` before a parameter name in function definitions.

**Parameter-level decorators are restricted** to known caller-info attributes and
other .NET parameter attributes. They are always emitted as C# parameter attributes.

**C# emission:**

```csharp
public static void Log(
    string message,
    [CallerMemberName] string member = "",
    [CallerFilePath] string file = "",
    [CallerLineNumber] int line = 0)
{
    Console.WriteLine($"[{file}:{line} {member}] {message}");
}
```

---

### 34. Await in Catch/Finally

**Recommendation: Lift the restriction. No new syntax needed.**

```python
async def fetch_with_retry():
    try:
        return await fetch_data()
    except IOError as e:
        await log_error_async(e)     # Currently not allowed; should be
        raise
    finally:
        await cleanup_async()        # Currently not allowed; should be
```

This is purely a semantic/codegen change. C# 6.0+ supports `await` in catch/finally,
and the async state machine generator must handle these cases.

---

### 35. Ref Locals and Ref Returns

**Recommendation: Extend `ref[T]` syntax to locals and return types.**

```python
def find[T](items: list[T], index: int) -> ref[T]:
    return ref items[index]

x: ref[int] = ref array[0]
x = 42    # modifies array[0]
```

**Depends on:** #1 (ref/out/in parameters) being implemented first.

**C# emission:**

```csharp
public static ref T Find<T>(List<T> items, int index) => ref items[index];
ref int x = ref array[0];
x = 42;
```

---

### 36. Tuple Equality

**Recommendation: Enable `==`/`!=` on tuples. No new syntax needed.**

```python
point1 = (1, 2)
point2 = (1, 2)
print(point1 == point2)    # True (element-wise comparison)
print(point1 != point2)    # False
```

This is a semantic + codegen change. The type checker should recognize `==`/`!=` on
`TupleType` and emit element-wise comparison.

**C# emission:**

```csharp
(1, 2) == (1, 2)    // C# 7.3 tuple equality
```

---

### 37. Static Local Functions

**Recommendation: `@static` decorator on nested `def`.**

```python
def outer():
    x = 10

    @static
    def helper(a: int) -> int:
        # Cannot capture x — compile error if it tries
        return a * 2

    result = helper(x)
```

**Semantic validation:** The compiler rejects any reference to enclosing-scope variables
within a `@static` nested function. This provides compile-time enforcement of
no-capture, avoiding accidental closure allocations.

**C# emission:**

```csharp
void Outer()
{
    var x = 10;
    static int Helper(int a) => a * 2;
    var result = Helper(x);
}
```

---

## Appendix: Rejected Alternatives

### Conversion operators via decorators (`@implicit`/`@explicit`)

**Proposal:** `@implicit def convert(value: float) -> Celsius:`

**Rejection reason:** Decorators in Sharpy are either modifier keywords (`@virtual`,
`@static`, `@override`) or .NET attributes (`@[obsolete("msg")]`). Using decorators for
operator-like behavior breaks this clean split. Dunders are the established pattern for
operator customization. Additionally, `@implicit def convert(...)` requires an arbitrary
method name that carries no semantic weight — the dunder `__implicit__` is self-documenting.

### `lock obj:` as a standalone statement

**Proposal:** New keyword `lock`, new statement form `lock expression:`.

**Rejection reason:** Introduces a new statement form for a concept that `with` already
handles idiomatically. Python programmers use `with lock:` for synchronization. Adding
`lock obj:` means two ways to express the same thing, violating "one way to do it."

### `do:` for do-while loops

**Proposal:** `do: body while condition`

**Rejection reason:** `do:` is already reserved for expression blocks
(`expression_blocks.md`). The `do:` block evaluates to a value and uses the last expression
as the result. Overloading `do:` for loops would require look-ahead disambiguation and
confuse readers.

### C# `with` expression syntax for records

**Proposal:** `p2 = p1 with { x: 5 }`

**Rejection reason:** `with` is the context manager keyword in Sharpy (and Python). Reusing
it for a completely different purpose (non-destructive mutation) would be confusing and
require parsing disambiguation. The `copy()` method approach uses existing syntax
(method calls + named arguments).

### `readonly` keyword for immutable fields

**Proposal:** `readonly connection_id: str`

**Rejection reason:** `readonly` is C#-specific. `final` is more widely understood across
languages (Java, Kotlin) and consistent with Sharpy's existing `@final` decorator for
sealed classes/methods.

### `mut[T]` as ref alias

**Proposal:** Keep `mut[T]` as a semantic alias for `ref[T]` on structs.

**Rejection reason:** Two spellings for the same C# construct (`ref`) violates "one way
to do things." The distinction between "I intend to mutate" and "I need a reference" is
intent-signaling that the compiler cannot enforce. Keep `ref[T]` only; document that
`ref[T]` is the way to pass structs by mutable reference.

---

## Summary of New Keywords and Soft Keywords

| Addition | Type | Used for |
|----------|------|----------|
| `loop` | Hard keyword | do-while loops (#6) |
| `default` | Hard keyword | default values (#14) |
| `use` | Hard keyword | block-less disposable scope (#28) |
| `final` (field) | Existing keyword, new context | readonly fields (#26) |
| `when` | Soft keyword (in `except`) | exception filters (#4) |
| `checked` | Soft keyword | checked arithmetic (#8) |
| `unchecked` | Soft keyword | unchecked arithmetic (#8) |
| `notnull` | Soft keyword (in constraints) | generic constraint (#18) |
| `unmanaged` | Soft keyword (in constraints) | generic constraint (#18) |
| `lock` | Builtin function | thread synchronization (#5) |
| `nameof` | Builtin function | compile-time name (#12) |
| `@partial` | Decorator (modifier keyword) | partial classes (#9) |
| `@ref` | Decorator (modifier keyword) | ref structs (#25) |
| `@volatile` | Decorator (modifier keyword) | volatile fields (#29) |

## Summary of New Dunders

| Dunder | Purpose | C# Emission |
|--------|---------|-------------|
| `__implicit__` | Implicit conversion operator | `implicit operator T(...)` |
| `__explicit__` | Explicit conversion operator | `explicit operator T(...)` |

## Summary: Items NOT Proposed (Deliberately Excluded)

| Gap | Reason |
|-----|--------|
| LINQ query syntax (#21) | Comprehensions + method syntax cover it; C#-specific sugar |
| Anonymous types (#22) | Named tuples cover the use case |
| Static lambdas (#23) | Use `@static def` on nested functions instead |
| General delegate combination (#31) | Events cover multicast; edge case |
| Expression trees (#32) | Too large for this proposal; deferred |
| Range/Index operators (#27) | Python slicing already covers this |
| Static classes (#19) | Modules serve this purpose |
