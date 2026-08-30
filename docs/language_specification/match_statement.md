# Pattern Matching

## Match Statement

```python
match value:
    case 0:
        print("zero")
    case 1:
        print("one")
    case (x, y):
        print(f"tuple: ({x}, {y})")
    case Color.RED:
        print("red")
    case "a" | "b":
        print("a or b")
    case > 100:
        print("large")
    case int() as n if n > 0:
        print(f"positive: {n}")
    case str() as s:
        print(f"string: {s}")
    case _:
        print("other")
```

*Implementation*
- *✅ Match statement maps to C# `switch` statement. Supports literal, wildcard, binding, tuple, member access, or-pattern, relational, type, property, and positional patterns + guard clauses (see [Supported Patterns](#supported-patterns) below).*

## Match Statement vs Match Expression

> **Implementation status:** Both match **statement** and match **expression** forms are implemented.

Sharpy supports both statement and expression forms of `match`, corresponding to C#'s switch statement and switch expression:

**Statement Form:**

Used when you need to execute statements for each case:

```python
match value:
    case 1:
        do_something()
        log("did something")
    case 2:
        do_other()
    case _:
        handle_default()
```

**Expression Form:**

Used when you want to produce a value:

```python
result = match value:
    case 1: "one"
    case 2: "two"
    case _: "other"

# Can be used anywhere an expression is expected
print(match x:
    case True: "yes"
    case False: "no"
)

# In a return statement
def categorize(n: int) -> str:
    return match n:
        case 0: "zero"
        case _ if n > 0: "positive"
        case _: "negative"
```

**Expression Form Rules:**
- Each case must be a single expression (not statements)
- All cases must produce values of compatible types
- Must be exhaustive (all possible values handled)
- Cases use `:` followed by an expression, not a block

## Disambiguation: Expression vs Statement Context

The parser determines whether `match` is an expression or statement based on syntactic context:

**Expression contexts** (match produces a value):
```python
# Assignment RHS
x = match value:
    case 1: "one"
    case _: "other"

# Return statement
return match value:
    case True: "yes"
    case False: "no"

# Function argument
f(match value:
    case 1: "a"
    case _: "b"
)

# Inside larger expression
result = prefix + match value:
    case 1: "one"
    case _: "other"

# List/dict literal element
items = [match x:
    case 1: "one"
    case _: "other"
]

# Conditional expression
y = (match x: case 1: "a" case _: "b") if flag else default
```

**Statement contexts** (match is standalone):
```python
# At statement level (not part of larger expression)
match value:
    case 1:
        do_something()
        log_result()
    case _:
        handle_default()

# After if/elif/else at statement level
if condition:
    match value:
        case 1:
            action1()
        case _:
            action2()
```

**Syntactic distinction:**

| Feature | Expression Form | Statement Form |
|---------|-----------------|----------------|
| Case body | Single expression after `:` | Indented block |
| Used in | Assignment, return, arguments | Standalone statement |
| Newline after `case X:` | Expression on same line | Block on next line |
| Produces value | Yes | No |

**Parser hint:** If `case pattern:` is followed by `NEWLINE INDENT`, it's statement form. If followed by an expression on the same line, it's expression form.

*Implementation*
- *Statement form: ✅ Implemented — C# `switch` statement*
- *Expression form: ✅ Implemented — C# `switch` expression*

## Supported Patterns

| Pattern | Syntax | C# 9.0 Mapping | Status |
|---------|--------|----------------|--------|
| Literal | `case 0:` | `case 0:` | ✅ Implemented |
| Wildcard | `case _:` | `default:` or `_` | ✅ Implemented |
| Binding | `case x:` | `var x` | ✅ Implemented |
| Tuple | `case (0, 0):` | Direct support | ✅ Implemented |
| Member access | `case Color.RED:` | `case Color.RED:` | ✅ Implemented |
| Guard clause | `case x if x > 0:` | `when` clause | ✅ Implemented |
| Type with binding | `case int() as n:` | `case int n:` | ✅ Implemented |
| Self-matching builtin | `case int(n):` | `case int n:` | ✅ Implemented — PEP 634: a builtin name takes exactly one positional sub-pattern, matched against the whole subject (SPY0363 otherwise). Which names denote a testable type is a separate question — see [Positional Capture on Collections](#positional-capture-on-collections-listxs-dictd-sets) |
| `as` binding | `case <pattern> as n:` | `case <pattern> and var n` | ✅ Implemented — `as` is the outermost combinator: `case A() \| B() as n:` binds `n` to whichever alternative matched |
| Or | `case "a" \| "b":` | `case "a" or "b":` | ✅ Implemented |
| Property | `case Point(x=0):` | `case Point { X: 0 }:` | ✅ Implemented |
| Positional | `case Point(0, y):` | `case Point { X: 0 }:` (mapped via fields) | ✅ Implemented |
| Relational | `case > 0:` | Direct support (C# 9) | ✅ Implemented |

*Implementation*
- *All pattern types map to C# 9.0 pattern matching. Guard clauses (`if expr`) are supported on any pattern via C# `when` clauses.*
- *Or-patterns use C# `or` pattern (`BinaryPattern`). A name bound on only some alternatives is rejected (SPY0359): `case (int() as n) | str():` leaves `n` unbound when `str()` matches. Bind after the or-pattern (`case int() | str() as n:`) or bind the same name inside every parenthesized alternative (`case (int() as n) | (str() as n):`); an unparenthesized `case int() as n | str() as n:` is a syntax error, as in CPython, because `as` closes the pattern.*
- *Relational patterns use C# `RelationalPattern` and require numeric scrutinee types.*
- *Positional patterns are mapped to property patterns using field declaration order (no `Deconstruct` required).*

## Tuple Patterns

```python
match point:
    case (0, 0):
        print("Origin")
    case (0, y):
        print(f"On Y-axis at {y}")
    case (x, 0):
        print(f"On X-axis at {x}")
    case (x, y):
        print(f"Point at ({x}, {y})")
```

## Property Patterns

```python
match shape:
    case Point(x=0, y=0):
        print("Origin point")
    case Point(x=x, y=0):
        print(f"On X-axis at {x}")
```

*Implementation: ✅ Implemented — maps to C# recursive pattern with property clause (`case Point { X: 0, Y: 0 }`). Property names are mangled to PascalCase.*

## Positional Patterns

Positional patterns match fields by declaration order (no `Deconstruct` method required):

```python
match point:
    case Point(0, 0):              # Positional - matches x=0, y=0
        print("Origin")
    case Point(x, 0):              # Positional with binding
        print(f"On X-axis at {x}")
    case Point(0, y):              # Positional with binding
        print(f"On Y-axis at {y}")
    case Point(x, y):              # Positional with both bound
        print(f"Point at ({x}, {y})")
```

*Implementation: ✅ Implemented — positional patterns are mapped to C# property patterns using field declaration order. The element count must match the number of fields on the type (SPY0363).*

## Type Patterns with Binding

```python
match value:
    case int() as n:               # Type check and bind
        print(f"Integer: {n}")
    case str() as s if len(s) > 0: # Type, bind, and guard
        print(f"Non-empty string: {s}")
    case int():                    # Type check only (no binding)
        print("Some integer")
```

*Implementation: ✅ Implemented — maps to C# declaration pattern (`case int n:`) with binding, or type pattern with discard designation when no binding is needed.*

### Matching Optional (`T?`) and Result (`T !E`)

`Optional` and `Result` are tagged unions, and a tagged union is matched through its **constructor
cases** — the one spelling for both:

```python
def describe(x: str?) -> str:
    match x:
        case Some(v):
            return "got " + v
        case None():
            return "empty"
```

```python
def unwrap_or_zero(r: int !ValueError) -> int:
    match r:
        case Ok(v):
            return v
        case Err(e):
            return 0
```

A bare **payload type pattern** over a union scrutinee — `case str():` over a `str?`, or
`case int():` over an `int !E` — is **refused** with `SPY0498`, steering to the constructor cases
for that family (`case Some(v):` / `case None():`, or `case Ok(v):` / `case Err(e):`). It is a
second spelling of the same match, and the Optional form was previously unsound (it reached code
generation as a C# `CS8121`):

```python
x: str? = get()
match x:
    case str():        # error[SPY0498] — use case Some(v): / case None():
        print("str")
    case None:
        print("none")
```

If you have already **narrowed** the scrutinee to its payload with `if x is not None:`, then `x`'s
type is the payload (`str`), not the union, and an ordinary type pattern applies there as usual:

```python
def narrowed(x: str?) -> str:
    if x is not None:
        match x:
            case str() as s:
                return "narrowed " + s
            case _:
                return "other"
    return "empty"
```

*Implementation: ✅ Implemented — the refusal is `SPY0498`, emitted during semantic analysis. All
examples above were executed against HEAD before being documented.*

### Bare Collection Patterns (`dict()`, `list()`, `set()`)

When matching an `object` scrutinee against a bare collection type pattern (no type arguments), the compiler specializes the binding type to use `object` type arguments:

```python
def process(value: object) -> None:
    match value:
        case dict() as d:
            # d is typed as dict[object, object]
            for k, v in d.items():
                print(f"{k}: {v}")
            print(d["key"])       # indexing works
            print("x" in d)       # membership works
            print(len(d))         # len() works (ISized)
        case list() as items:
            # items is typed as list[object]
            pass
        case set() as s:
            # s is typed as set[object]
            pass
```

**Semantic behavior:** The binding variable receives the specialized generic type (`dict[object, object]`, `list[object]`, or `set[object]`), so all collection methods are available through normal type-checked member access.

**Runtime check (`dict`):** The `case dict()` pattern checks against `Sharpy.IDict` — the non-generic Pythonic protocol interface implemented by every `Dict<K, V>` instantiation. This means:
- Any `Dict[K, V]` matches, regardless of type arguments.
- The bound variable provides the full dict surface: `.items()`, `.keys()`, `.values()`, `[key]` indexing, `key in d` membership, `len(d)`, `.get()`, `.pop()`, etc.
- **Aliasing preserved:** The binding is the same object, not a copy. Mutation through `d` is visible via the original reference.

**Type-erased access semantics (Axiom 1 — .NET first):**
- Reads with a wrong-typed key behave as if the key is absent: indexer raises `KeyError`, `.get()` returns `None`, `in` returns `False`.
- Writes cast key/value to the underlying generic types; a type mismatch throws at runtime.

**Interop note:** Raw .NET `Dictionary<K, V>` boxed as `object` does **not** match bare `case dict()`, because the runtime check is against `Sharpy.IDict` (which only `Sharpy.Dict<K, V>` implements). Sharpy values flow through Sharpy collections; this is an intentional Axiom 1 resolution.

**Runtime check (`list`/`set`):** `case list()` checks against `Sharpy.IList` and `case set()` against `Sharpy.ISet` — the dedicated non-generic protocol interfaces (#876, #877), the same shape as `dict`. Every closed instantiation implements them, so the test is independent of the type arguments.

**Pattern Forms:**

| Pattern | Syntax | Use Case | Status |
|---------|--------|----------|--------|
| Property | `Point(x=0, y=y)` | Extract by property name | ✅ Implemented |
| Positional | `Point(0, y)` | Extract by position (field order) | ✅ Implemented |
| Type with binding | `int() as n` | Check type and bind entire value | ✅ Implemented |
| Positional capture | `list(xs)` | Check type and bind entire value | ✅ Implemented — see below |

### Positional Capture on Collections (`list(xs)`, `dict(d)`, `set(s)`)

A collection name may take a single positional sub-pattern. Per PEP 634 that sub-pattern matches the
**whole subject**, so `case list(xs):` is the same test as `case list() as xs:` and binds the same
value:

```python
def check(o: object) -> None:
    match o:
        case list(xs):
            xs.append(3)
            print(len(xs))
        case _:
            print("not a list")
```

The type the pattern **tests** and the type the capture **gets** are both decided from the
scrutinee's static type. A class pattern is static, exactly like `isinstance`: no reflection happens
at run time, and the same three outcomes apply.

| Scrutinee | Test emitted | Capture type |
|-----------|--------------|--------------|
| `object` | `Sharpy.IList` — erased to the protocol interface | `list[object]` |
| `list[int]` | `Sharpy.List<int>` — filled from the subject | `list[int]` |
| `str`, `dict[str, int]`, any type no list can be | refused: **SPY0361** | — |

```python
def erased(o: object) -> None:
    match o:
        case list(xs):
            print(len(xs))      # xs: list[object]
        case _:
            print("miss")

def closed(xs: list[int]) -> None:
    match xs:
        case list(ys):
            n: int = ys[0]      # ys: list[int] — the element type survives the match
            print(n)
        case _:
            print("miss")

def impossible(s: str) -> None:
    match s:
        case list(xs):          # SPY0361: a str is never a list, so this arm is dead code
            print(len(xs))
        case _:
            print("miss")
```

The answer does not depend on where the pattern is written. It is the same at the top level, nested
in a sequence pattern, and nested in a class positional pattern:

```python
class Box:
    value: object

    def __init__(self, value: object):
        self.value = value

def seq(xs: list[object]) -> None:
    match xs:
        case [list(inner), int(n)]:
            print(f"seq {len(inner)} {n}")
        case _:
            print("miss")

def boxed(b: Box) -> None:
    match b:
        case Box(list(xs)):
            print(f"box {len(xs)}")
        case _:
            print("miss")
```

**Which names may head a class pattern.** A class pattern names a *registered type*, and only names
that denote one can be tested:

| Spelling | Result |
|----------|--------|
| `case list(xs):`, `case dict(d):`, `case set(s):` | tested per the table above |
| `case int(n):`, `case str(s):`, `case float(f):`, `case bool(b):`, `case bytes(b):` | closed test on the primitive |
| `case tuple(v):`, `case frozenset(v):` | **SPY0345** — generic types with no type-erased protocol interface, so nothing determines their type arguments and there is no single runtime type to test (spec-vs-implementation tracked in #1693) |
| `case range(x):`, `case bytearray(v):` | **SPY0202** — the name denotes no registered type |
| `case list[int](xs):` | **SPY0125** — a pattern cannot name type arguments; write `case list(xs):` and let the scrutinee supply the vector |

A refusal is the honest answer at these spellings, not a limitation to route around: `.NET` reifies
generics, so an open name denotes nothing to test against.

## Guard Patterns

Guard clauses add conditions to any pattern using `if`. The `GuardPattern` AST node wraps an inner pattern with a boolean guard expression.

### Basic Guard

```python
match value:
    case x if x > 0:
        print(f"positive: {x}")
    case x if x < 0:
        print(f"negative: {x}")
    case _:
        print("zero")
```

### Guards on Type Patterns

```python
match value:
    case int() as n if n > 0:
        print(f"positive int: {n}")
    case str() as s if len(s) > 0:
        print(f"non-empty string: {s}")
```

### Per-Alternative Guards

Guards can be applied to individual alternatives within an or-pattern:

```python
match value:
    case Foo(x) if x > 0 | Bar(y) if y < 0:
        print("matched")
```

Each alternative in the or-pattern can have its own guard condition. This maps to C# `when` clauses on individual arms of a disjunctive pattern.

### Guard vs Exhaustiveness

Guards make patterns conditional, so a guarded pattern is **not** considered exhaustive — even `case _ if condition:` does not cover all values. For exhaustive matching, include an unguarded wildcard or binding pattern:

```python
match value:
    case _ if value > 0:
        print("positive")
    case _:              # Unguarded wildcard ensures exhaustiveness
        print("non-positive")
```

*Implementation*
- *✅ Implemented — `GuardPattern` AST node with `Inner` (pattern) and `Guard` (expression) properties*
- *Maps to C# `when` clause on `case` arms*
- *Guards do not affect exhaustiveness analysis*

## Arm Ordering

An irrefutable pattern — a wildcard (`_`), an unguarded name capture, or an or-pattern
containing either — matches all values, making any following arms unreachable. The
fall-through arm **must be the last arm** (SPY0700). This matches CPython's rule:
`SyntaxError: name capture 'x' makes remaining patterns unreachable`.

A guarded irrefutable pattern (`case x if cond:`) is refutable and may appear anywhere.
Parentheses do not change a pattern's meaning: `case (x):` is a *group* pattern — the same
capture as `case x:` — and is ordered the same way; only a trailing comma (`case (x,):`) or two
or more elements make a tuple pattern. A capture nested inside a refutable pattern (`case [x]:`)
does not make the arm irrefutable; a class pattern such as `case int() as n:` is irrefutable only
through totality for the scrutinee's static type (next paragraph).

**Class-pattern totality.** A class pattern with no sub-patterns (`case int():`,
`case int() as n:`, `case int(n):`) is *total* for the scrutinee's static type when every value
of that type is an instance of the pattern's type — `case int():` over an `int` scrutinee
matches everything. Totality is a fact of the scrutinee's static type, recorded during semantic
analysis, not of the pattern's spelling. A total class pattern followed by a **refutable** arm
makes that arm unreachable and is refused (SPY0700), the same rule as a capture. Irrefutable
arms after it (`case _:`, `case x:`) stay legal: they lower to C#'s `default:`, which the C#
compiler never marks unreachable, so the program compiles and the total arm runs. (Before this
rule the total arm reached the C# compiler and failed with CS8120 behind SPY0908.)

```python
# OK — the trailing wildcard is irrefutable
def kind(x: int) -> str:
    match x:
        case int() as n:
            return f"int {n}"
        case _:
            return "other"

# SPY0700 — `case 99:` is refutable and can never run after a total `case int():`
def kind2(x: int) -> str:
    match x:
        case int():
            return "int"
        case 99:           # error: total class pattern 'int()' makes remaining patterns unreachable
            return "ninety-nine"
        case _:
            return "other"
```

**Subsumption.** Totality for the scrutinee is not the only way an arm shadows a later one. An
unguarded arm that matches **every value of its own type** — `case int():`, `case int(n):`,
`case int() as n:` — makes any later arm whose type is contained in it unreachable, even when that
arm is not total for the scrutinee. Over an `object` scrutinee `case int():` is not total, yet it
still matches every `int`, so a later `case 99:` can never run (SPY0700). The rule requires the
earlier arm to refute on its **type alone**: a literal refutes on a value as well, so `case 99:`
first leaves `case int():` behind it reachable.

```python
# SPY0700 — `case 99:` is unreachable behind an arm that matches every int
def kind(x: object) -> str:
    match x:
        case int():
            return "int"
        case 99:           # error: an earlier arm matches every 'int32'
            return "ninety-nine"
        case _:
            return "other"

# OK — the literal refutes on a value, so the type arm behind it is still reachable
def kind2(x: object) -> str:
    match x:
        case 99:
            return "ninety-nine"
        case int():
            return "int"
        case _:
            return "other"

# OK — a guarded arm decides nothing statically
def kind3(x: object, deep: bool) -> str:
    match x:
        case int() if deep:
            return "int"
        case 99:
            return "ninety-nine"
        case _:
            return "other"
```

A pattern is a **runtime** type test, so subsumption is exact for the builtin types: `case float():`
does not match a boxed `int` even though `int` is implicitly convertible to `float`, and this runs —
printing `one`, as CPython does:

```python
def kind4(x: object) -> str:
    match x:
        case float():
            return "float"
        case 1:
            return "one"
        case _:
            return "other"
```

```python
# OK — trailing capture
match status:
    case 200:
        print("ok")
    case code:
        print(f"error: {code}")

# SPY0700 — capture before literal
match status:
    case code:           # error: name capture 'code' makes remaining patterns unreachable
        print(code)
    case 200:
        print("ok")

# OK — guarded capture is refutable
match status:
    case code if code >= 400:
        print(f"error: {code}")
    case 200:
        print("ok")
    case _:
        print("other")
```

## Exhaustiveness Checking

The `ExhaustivenessValidator` checks that `match` statements and expressions cover all possible cases.

**Checked Types (Finite):**

| Type | Requirement | Diagnostic |
|------|-------------|------------|
| `bool` | Must cover `True` and `False` | SPY0463 (warning) |
| Tagged unions | All cases must be covered | SPY0463 (warning) |
| Enums | All enum values must be covered | SPY0463 (warning) |

**Non-Finite Types (int, str, etc.):**

| Form | Requirement | Diagnostic |
|------|-------------|------------|
| Match expression | Must have at least one unconditionally exhaustive arm (wildcard `_` or binding pattern without guard) | SPY0416 (error) |
| Match statement | Should have a wildcard or binding arm for safety | SPY0463 (warning) |

> **Note:** Match expressions produce SPY0416 errors (not warnings) because a missing arm results in a runtime `SwitchExpressionException`. Match statements produce SPY0463 warnings since the code simply falls through.

```python
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

# ERROR: Non-exhaustive match (missing BLUE)
match color:
    case Color.RED:
        print("Red")
    case Color.GREEN:
        print("Green")

# OK: Exhaustive with wildcard
match color:
    case Color.RED:
        print("Red")
    case _:
        print("Other color")

# OK: Fully exhaustive
match color:
    case Color.RED:
        print("Red")
    case Color.GREEN:
        print("Green")
    case Color.BLUE:
        print("Blue")

# Boolean exhaustiveness
match flag:
    case True:
        print("Yes")
    # ERROR: missing False case
```
