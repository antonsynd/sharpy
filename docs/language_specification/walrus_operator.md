# Walrus Operator

The walrus operator `:=` allows assignment within expressions:

```python
# Capture value in conditional
if (match := pattern.search(text)) is not None:
    print(f"Found match at {match.start()}")

# Reuse computed value
results = [y for x in data if (y := transform(x)) is not None]

# Avoid repeated calls
while (line := file.read_line()) is not None:
    process(line)
```

**Re-evaluation in `while` tests.** A walrus anywhere inside a `while` test is re-evaluated on every iteration, whatever expression hosts it — a member call on the bound value, an index, a keyword argument, a container element, a conditional branch, or a nested call all behave the same way:

```python
def main():
    xs: list[str] = [" a ", " b ", "   "]
    while (s := xs.pop(0)).strip():
        print(s.strip())
    print("done", len(xs))
```

Output (identical to Python):

```
a
b
done 0
```

Before this was fixed, the walrus was hoisted once ahead of the loop for every host other than a direct comparison, so the example above looped forever (#1723).

**Value-Typed (R-V):**

A walrus is a store followed by a read of its target; its type is what the target reads as after
the store. When the target is a wrapper type (`T?` or `T | None`), the walrus's type reflects the
stored value, not the wrapper:

```python
def main() -> None:
    m: int | None = None
    n: int = (m := 5)          # m stores 5, reads as int; n = 5
    print(n)
```

```
5
```

`Some(…)` and `None()` take the target's declared slot, so a walrus that stores an Optional reads
as the declared type:

```python
def main() -> None:
    x: int? = None()
    y: int? = (x := Some(2))   # stores Some(2); reads as int?
    print(y)
```

```
2
```

**Fresh Walrus Under a Slot (R-AE):**

A fresh walrus (one that creates a new binding) directly under a store slot takes the slot's type. Without a slot, a bare `None` walrus is refused — `None` has no type on its own:

```python
def g(s: str | None):
    print(s)

g((q := None))  # q takes str | None from the parameter slot
print(q)        # None
```

```
None
None
```

A fresh walrus whose value is a void call (a function that returns nothing) is always refused:

```python
x = (q := print("hi"))  # error: produces no value
```

**Type Inference Only:**

The walrus operator always infers the type from the right-hand side expression. Type annotations are not supported with `:=` (matching Python 3.8+ behavior):

```python
# ✅ Valid - type inferred from get_value()
if (x := get_value()) > 0:
    pass

# ❌ Invalid - cannot annotate with walrus
if (x: int := get_value()) > 0:  # ERROR: type annotation not supported with :=
    pass
```

Since Sharpy has full static type information, the type of `get_value()` is known at compile time, making explicit annotation unnecessary.

**Walrus Operator in Comprehensions:**

Variables assigned with `:=` inside a comprehension are **local to the comprehension** and do not leak to the outer scope:

```python
# Walrus is useful within a comprehension to avoid recomputation
results = [y * 2 for x in data if (y := transform(x)) > 0]
# y is used within the comprehension - valid!

# But y does NOT leak to outer scope
print(y)  # ERROR: 'y' does not exist in this scope

# Same for iteration variables
print(x)  # ERROR: 'x' does not exist in this scope
```

**Departure from Python:** In Python 3.8+, walrus assignments inside comprehensions leak to the containing scope. Sharpy deliberately differs here for cleaner semantics: the syntactic boundary (`[...]`, `{...}`) equals the semantic boundary. Everything inside the comprehension delimiters stays inside.

**If you need a value after the comprehension:**

```python
# Assign before the comprehension
items = get_items()  # Not: [(x := get_items()) ...]
[x for x in items]

# Or use an explicit loop
last_valid: int | None = None
results: list[int] = []
for x in data:
    y = transform(x)
    if y > 0:
        last_valid = y
        results.append(y * 2)
```

## Evaluation Placement

A walrus expression evaluates **exactly where it is written** — in a short-circuit branch, a
ternary arm, an `elif` test, a `while` test, a comparison-chain operand, a `??` right operand, a
lambda body, or a match guard. The compiler manufactures a sink at each of those constructs so a
hoisted lowering runs where the construct evaluates it.

The name a walrus binds has the lifetime of the enclosing **scope**, not of the enclosing
evaluation, so its declaration is placed at the enclosing scope even when its assignment is placed
inside a manufactured conditional or loop body. That is why a walrus declared in a `while` test is
readable in the loop's `else` clause and after the loop:

```python
def probe(xs: list[int]) -> int:
    print("probe", len(xs))
    return len(xs)

def main() -> None:
    xs: list[int] = [1, 2, 3]
    while (n := probe(xs)) > 1:
        xs.pop(0)
    else:
        print("else", n)
    print("after", n)
```

```
probe 3
probe 2
probe 1
else 1
after 1
```


```python
def probe() -> int:
    print("evaluated")
    return 3

def main() -> None:
    if False and (w := probe()):
        pass
    print("done")
```

```
done
```

The short-circuited `and` RHS is never evaluated.

In an `or` expression, the RHS evaluates only when the LHS is falsy:

```python
def probe() -> int:
    print("evaluated")
    return 3

def main() -> None:
    w: int = 0
    if 0 or (w := probe()):
        pass
    print(f"w {w}")
```

```
evaluated
w 3
```

In a `while` test, the walrus re-evaluates on every iteration:

```python
def main() -> None:
    xs: list[int] = [1, 2, 0, 3]
    while (v := xs.pop(0)):
        print(f"iter {v}")
    print(f"done {len(xs)}")
```

```
iter 1
iter 2
done 1
```

## Definite Assignment for Walrus Names

A walrus in a short-circuit branch may or may not execute — the name it binds is not
necessarily assigned on all paths to a subsequent read. Sharpy applies the C# §9.4.4
when-true/when-false definite assignment rule:

- After `a and b`: definitely assigned when **true** = T(a) ∪ T(b); when **false** = F(a) ∩ (T(a) ∪ F(b)).
- After `a or b`: definitely assigned when **true** = T(a) ∩ (F(a) ∪ T(b)); when **false** = F(a) ∪ F(b).
- After `not e`: true/false swap.
- After `x if c else y`: when **true** = (T(c) ∪ T(x)) ∩ (F(c) ∪ T(y)); when **false** =
  (T(c) ∪ F(x)) ∩ (F(c) ∪ F(y)). Only the condition runs unconditionally.
- After a comparison chain `a < b < c`: operands 0 and 1 always run, so their walruses are assigned
  on both outcomes; an operand past the first link runs only when every earlier link held, so it is
  assigned when **true** only.
- After `a ?? b`: only `a` runs unconditionally.
- In a comprehension or generator expression: only the first `for` clause's iterator is evaluated
  exactly once. A walrus in the element, the value, or an `if` clause runs once per item — zero times
  over an empty iterable — so it is not definitely assigned. python3 agrees: `[(w := v) for v in []]`
  followed by a read of `w` raises `UnboundLocalError`.

A walrus in a **match guard** is assigned on entry to the arm it guards, so the arm's body reads it:

```python
def f() -> int:
    print("f")
    return 3

def main() -> None:
    m: int = 1
    match m:
        case 1 if (w2 := f()) > 0:
            print("one", w2)
        case _:
            print("other")
```

```
f
one 3
```

A read on a path where the walrus may not have executed is SPY0600, mirroring Python's
`NameError`:

```python
def probe() -> int:
    return 3

def main() -> None:
    if True or (w := probe()):
        print(w)   # error SPY0600: variable 'w' is used before being assigned
```

A ternary arm and a comparison-chain operand past the first link refuse the same way:

```python
def f() -> int:
    return 3

def main() -> None:
    c: bool = False
    w: int
    v: int = (w := f()) if c else 0
    print(v, w)   # error SPY0600: Variable 'w' is used before being assigned
```

```python
def f() -> int:
    return 3

def main() -> None:
    w: int
    if 5 < 1 < (w := f()):
        print("in")
    print("after", w)   # error SPY0600: Variable 'w' is used before being assigned
```

python3 raises `UnboundLocalError` on both programs.

A walrus that is unconditional (e.g., the LHS of `and`) is available on both branches:

```python
def probe() -> int:
    return 1

def main() -> None:
    if (w := probe()) > 0 and False:
        pass
    else:
        print(f"else {w}")
```

```
else 1
```

`w` is definitely assigned regardless of the `and` result.

## PEP 572 Prohibited Positions (SPY0704)

Following PEP 572, a walrus inside a comprehension's **iterable** expression is refused:

```python
xs: list[int] = [1, 2, 3]
r = [y for y in (t := xs)]   # error SPY0704: walrus operator not allowed in iterable
```

A walrus in the **element** or **condition** position of a comprehension is accepted:

```python
def main() -> None:
    xs: list[int] = [1, 2, 3]
    r = [v for x in xs if (v := x * 2) > 2]
    print(r)
```

```
[4, 6]
```

Rebinding the iteration variable with a walrus in the element or condition is also refused:

```python
xs: list[int] = [1, 2, 3]
r = [i := 0 for i in xs]   # error SPY0704: walrus rebinds iteration variable 'i'
```

## Walrus in F-String Holes

A walrus inside a parenthesized f-string hole is accepted — the parentheses distinguish it from
a format specifier:

```python
def f() -> str:
    return "hello"

def main() -> None:
    print(f"{(s := f())} {s}")
```

```
hello hello
```

Without parentheses, `{s := f()}` would parse `:` as the format spec start. Expressions that
need parentheses in holes: walrus, lambda, and slices.

*Implementation*
- *🔄 Lowered - Hoisted variable declaration with manufactured sinks:*

```python
# Sharpy
if (match := pattern.search(text)) is not None:
    print(match.group())
```
```csharp
// C# 9.0
var match = pattern.Match(text);
if (match.Success) {
    Console.WriteLine(match.Value);
}
```
