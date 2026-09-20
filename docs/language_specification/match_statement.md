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
| Member access | `case Color.RED:`, `case Outer.Holder.A:` | `case Color.RED:` | ✅ Implemented — the head resolves a nested-type chain at every depth (`Outer.Holder.A` binds the const on the deepest nested type), not only a single `Enum.Member` (#1799) |
| Sequence | `case [a, *rest]:` | list/positional deconstruction | ✅ Implemented — a sequence pattern decides its subject through the type-test classifier: a `list[T]` (or `list[T] \| None`) subject fills, an **open** subject (`object`, type parameter) is refused **SPY0345** with the `case list[int]([...])` steer (#1702) |
| Guard clause | `case x if x > 0:` | `when` clause | ✅ Implemented |
| Type with binding | `case int() as n:` | `case int n:` | ✅ Implemented |
| Self-matching builtin | `case int(n):` | `case int n:` | ✅ Implemented — PEP 634: a builtin name takes exactly one positional sub-pattern, matched against the whole subject (SPY0363 otherwise). Which names denote a testable type is a separate question — see [Class patterns on generic builtins](#class-patterns-on-generic-builtins-list-dict-set) |
| `as` binding | `case <pattern> as n:` | `case <pattern> and var n` | ✅ Implemented — `as` is the outermost combinator: `case A() \| B() as n:` binds `n` to whichever alternative matched |
| Or | `case "a" \| "b":` | `case "a" or "b":` | ✅ Implemented |
| Property | `case Point(x=0):` | `case Point { X: 0 }:` | ✅ Implemented |
| Positional | `case Point(0, y):` | `case Point { X: 0 }:` (mapped via fields) | ✅ Implemented |
| Relational | `case > 0:` | Direct support (C# 9) | ✅ Implemented |

### Guard Evaluation Order

Guards evaluate **per arm, in order, only for arms whose pattern matched**. A guard with a
side-effecting expression (a comprehension, a `?` operator, a walrus, or any call) runs only
when its arm's pattern matches the scrutinee. Unmatched arms' guards are never evaluated.

The example uses a **comprehension** in the guard, not a plain call: a plain call in a guard
evaluates per arm on either lowering, so it cannot tell the two apart. A comprehension is a
hoist-producing expression, and if its statements were placed above the match instead of inside the
arm, `take` would run and `xs` would lose an element:

```python
def take(xs: list[int]) -> list[int]:
    print("guard-eval")
    xs.pop(0)
    return xs

def main() -> None:
    xs: list[int] = [1, 2, 3]
    m: int = 1
    match m:
        case 0 if len([v for v in take(xs)]) > 0:
            print("zero")
        case 1:
            print("one")
        case _:
            print("other")
    print("left", len(xs))
```

```
one
left 3
```

Case 0's pattern does not match, so its guard never runs: `take` is not called and `xs` keeps all
three elements. python3 prints the same two lines for the same program.

When a guard contains a hoist-producing expression (a comprehension, spread, or `?`), the
compiler lowers the guarded match to an `is`-chain block so that each guard's side effects
execute exactly once, only when the pattern matches. The same lowering applies to the
match **expression** form, where an arm's **result** is likewise evaluated only
when that arm is selected.

A `break` or `continue` inside **any** arm targets the enclosing loop, exactly as in Python — not
the C# `switch` that a match would otherwise manufacture. A `match` hosting a `break` that targets an
outer loop therefore lowers to the `is`-chain (which manufactures no `switch`), so the `break`
reaches the loop and clears the loop's `else` flag (#1816). A `continue` needs no such lowering: a
C# `switch` already forwards `continue` to the enclosing loop.

*Implementation*
- *All pattern types map to C# 9.0 pattern matching. Guard clauses (`if expr`) are supported on any pattern via C# `when` clauses.*
- *Or-patterns use C# `or` pattern (`BinaryPattern`), which cannot declare a variable inside it (CS8780), so no alternative may bind a name — see [Captures in Or-Patterns](#captures-in-or-patterns) for the rule and its two cures (SPY0359). A name bound on only some alternatives is rejected the same way: `case (int() as n) | str():` leaves `n` unbound when `str()` matches. Bind after the or-pattern (`case int() | str() as n:`) or bind the same name inside every parenthesized alternative (`case (int() as n) | (str() as n):`); an unparenthesized `case int() as n | str() as n:` is a syntax error, as in CPython, because `as` closes the pattern. When one name is bound inside EVERY alternative, its type is the join of the alternatives' captures through best-common-type with the scrutinee type as the slot — `case (float() as v) | (list() as v):` over an `object` scrutinee types `v` as `object`. (The bind-after form, `case float() | list() as v:`, captures the whole or-pattern and takes the scrutinee's type directly; it never reaches that join.)*
- *Relational patterns use C# `RelationalPattern` and require numeric scrutinee types.*
- *Positional patterns are mapped to property patterns using field declaration order (no `Deconstruct` required).*

## Captures in Or-Patterns

An **alternative** of an or-pattern may not bind a name. The rule is the same for every kind of
alternative — a bare name, a class pattern, a reified head, a sequence element, a `*rest` capture,
a tuple element, a union-case payload, and an `as` nested inside an alternative:

<!-- spec-sweep: error SPY0359 -->
```python
class Point:
    x: int
    def __init__(self, x: int) -> None:
        self.x = x

class Circle:
    x: int
    def __init__(self, x: int) -> None:
        self.x = x

def main() -> None:
    o: object = Point(3)
    match o:
        case Point(v) | Circle(v):   # SPY0359: alternative binds 'v'
            print(v)
        case _:
            print("other")
```

CPython accepts that program, because a Python capture has no static type. Sharpy's does: `v` would
be a different type on each alternative, and the case body is checked once. The refusal names the
rule and gives the two cures.

**Split the alternatives into separate cases** — each capture is then local to its own arm, and the
arms may have different types:

```python
class Point:
    x: int
    def __init__(self, x: int) -> None:
        self.x = x

class Circle:
    x: int
    def __init__(self, x: int) -> None:
        self.x = x

def main() -> None:
    o: object = Point(3)
    match o:
        case Point(v):
            print(v)
        case Circle(v):
            print(v)
        case _:
            print("other")
```

```
3
```

**Or capture the whole subject** with `as` on every alternative under the same name. That one shape
binds once, outside the alternatives, so it lowers and runs — its type is the join of the
alternatives through best-common-type (see the or-pattern note under [Supported
Patterns](#supported-patterns)):

```python
def main() -> None:
    o: object = 42
    match o:
        case (int() as v) | (str() as v):
            print(v)
        case _:
            print("other")
```

```
42
```

The alternatives must bind the **same** name: `case (int() as a) | (str() as b):` is SPY0359 too,
for the same reason CPython refuses it (`alternative patterns bind different names`). A capture
**outside** the or-pattern is unaffected — `case 1 | 2 as n:` binds the whole or-pattern, and an
or-pattern nested inside a larger pattern leaves that pattern's own captures alone
(`case (1 | 2, v):`).

## Constant Patterns

A constant pattern (`case C:` where `C` is a `const` name) matches when the scrutinee equals the
constant's value. The referenced `const` must be a **compile-time constant** — that is, its declared
type must be C#-const-eligible (a numeric primitive, `char`, `str`, `bool`, or an enum) and its
initializer must fold to a constant expression without runtime calls. It is the same fact a parameter
default reads (see [Function Default Parameters](function_default_parameters.md)), so a `const`
declared in a function body reads here too. A `const` that is not compile-time (e.g., its initializer
is a function call, or its type is `T?`) cannot appear as a constant pattern; compare in a guard
instead:

```python
const THRESHOLD: int = 100

def classify(n: int) -> str:
    match n:
        case THRESHOLD:
            return "exact"
        case _ if n > THRESHOLD:
            return "above"
        case _:
            return "below"
```

A `const` whose initializer is a call (e.g., `const FM: float = max(4.0, 1.0)`) emits as
`static readonly` and cannot be used as a constant pattern. Use a guard:

```python
const FM: float = max(4.0, 1.0)   # not a compile-time constant

def check(v: float) -> str:
    match v:
        # case FM:                 # ERROR SPY0605: 'FM' is not a compile-time constant:
        #                          #                its initializer is a call
        case _ if v == FM:         # OK: compare in a guard
            return "hit"
        case _:
            return "miss"
```

An **enum** `const` is a compile-time constant, so it matches directly:

```python
enum Color:
    RED = 1
    GREEN = 2

const PRIMARY: Color = Color.RED

def name(c: Color) -> str:
    match c:
        case PRIMARY:
            return "primary"
        case _:
            return "other"
```

A **qualified** pattern head (`case Holder.A:`) is a different shape: it lowers to a guarded
comparison rather than a C# constant pattern, so it matches whatever the const's fact is.

Every constant pattern also emits `SPY0468`, a warning that the bare name matched a constant rather
than capturing the scrutinee. Rename the pattern variable if a capture was what you meant.

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

### Class patterns on generic builtins (`list`, `dict`, `set`)

A class pattern is **static**: it tests the scrutinee's declared type against a *closed* runtime
type, exactly as `isinstance` does, and performs no reflection. Sharpy's generics are **reified** —
`list[int]` and `list[object]` are distinct runtime types — so a class pattern that heads a generic
builtin must name a single closed type to test against. That type comes from one of two places, and
if it comes from neither the pattern is refused:

1. **Filled from a closed scrutinee.** When the scrutinee's static type already fixes the type
   arguments, a bare head reuses them:

   ```python
   def closed(xs: list[int]) -> None:
       match xs:
           case list(ys):
               n: int = ys[0]      # ys: list[int] — the element type survives the match
               print(n)            # prints 7 for closed([7, 8])
           case _:
               print("miss")
   ```

2. **Written explicitly in the head.** When the scrutinee is open (`object`, a base type, a type
   parameter), the head must carry the closed spelling:

   ```python
   def ok(o: object) -> None:
       match o:
           case list[object](xs):
               print("hit", len(xs))   # xs: list[object]; prints "hit 3" for a list[object]
           case _:
               print("miss")
   ```

3. **Refused when nothing determines the arguments.** A bare generic head on an open scrutinee
   names no single runtime type, so it is a compile error whose message steers to the closed
   spelling:

   <!-- spec-sweep: error SPY0345 -->
   ```python
   def erased(o: object) -> None:
       match o:
           case list(xs):          # SPY0345: a bare open head names no single reified type;
               print(len(xs))      #          write case list[object](xs)
           case _:
               print("miss")
   ```

Reification is visible at the match: a `list[int]` value is **not** a `list[object]`, so
`case list[object](xs)` does not match a `list[int]` subject (it falls through to `case _`), and a
captured `list[int]` keeps its element type — `ys[0] += 10` type-checks as `int` and prints
`[11, 2, 3]`. There is no type-erased protocol interface and no `object`-vector fallback; the
`Sharpy.IList`/`IDict`/`ISet` erasure of earlier versions is retired (#1708, #1619).

**Pattern Forms:**

| Pattern | Syntax | Use Case | Status |
|---------|--------|----------|--------|
| Property | `Point(x=0, y=y)` | Extract by property name | ✅ Implemented |
| Positional | `Point(0, y)` | Extract by position (field order) | ✅ Implemented |
| Type with binding | `int() as n` | Check type and bind entire value | ✅ Implemented |
| Positional capture | `list(xs)` | Check type and bind entire value | ✅ Implemented — see below |

### Positional capture and the verdict table

Per PEP 634 a collection head takes a single positional sub-pattern that matches the **whole
subject**, so `case list(xs):` is the same test as `case list() as xs:` and binds the same value.
The type a pattern tests and the type the capture receives are decided together — filled from a
closed scrutinee, taken from an explicit head, or refused — and the answer does not depend on where
the pattern is written. Top level, nested in a sequence pattern, and nested in a class positional
pattern all agree, and the `isinstance` form agrees with the pattern form for the same
(builtin, scrutinee, spelling):

| Scrutinee | Head | Verdict |
|-----------|------|---------|
| `list[int]` | `list(ys)` | fill from the subject → `list[int]`, runs |
| `object` | `list[object](xs)` | test `Sharpy.List<object>` from the head → `list[object]`, runs |
| `object`, `str`, a type parameter | `list(xs)` | **SPY0345** — nothing determines the arguments |
| `str` | `list[int](xs)` | **SPY0361** — a `str` is never a `list[int]` |
| `list[int]` | `list[str](ys)` | **SPY0361** — element type incompatible |
| any | `list[int, str](xs)` | **SPY0224** — `list` takes one type argument |

Filling works at any depth, so a closed nested scrutinee needs no explicit head:

```python
def seq(xs: list[list[int]]) -> None:
    match xs:
        case [list(inner)]:
            n: int = inner[0]   # inner: list[int]
            print(f"seq {n}")   # prints "seq 9" for seq([[9]])
        case _:
            print("miss")
```

and an open head is refused at any depth — nested in a sequence pattern or a class positional
pattern, the verdict is the same SPY0345 as at the top level:

<!-- spec-sweep: error SPY0345 -->
```python
def seq(xs: list[object]) -> None:
    match xs:
        case [list(inner)]:     # SPY0345 — the element type is object; write case [list[object](inner)]
            print(len(inner))
        case _:
            print("miss")
```

**Which names may head a class pattern.** A class pattern names a *registered type*:

| Spelling | Result |
|----------|--------|
| `case list[int](xs):`, `case dict[str, int](d):`, `case set[int](s):` | closed generic test — the head names the vector |
| `case list(ys):` on a closed scrutinee | filled from the subject |
| `case list(xs):` on an open scrutinee | **SPY0345** — write the closed spelling |
| `case int(n):`, `case str(s):`, `case float(f):`, `case bool(b):`, `case bytes(b):` | closed test on the primitive |
| `case tuple(v):`, `case frozenset(v):` on an open scrutinee | **SPY0345** — supply the element vector or match a closed instantiation |
| `case range(x):`, `case bytearray(v):` | **SPY0202** — the name denotes no registered type |

Writing the type arguments in the head is **required, not forbidden**: `case list[int](xs):` is the
canonical closed spelling and the earlier SPY0125 ("a pattern cannot name type arguments") is
retired (#1708). A refusal is the honest answer where nothing determines the vector: `.NET` reifies
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
