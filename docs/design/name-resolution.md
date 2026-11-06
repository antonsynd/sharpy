# Name resolution, shadowing and initialization order

> **Status:** Principle — ruled by the owner 2026-11-01 (model **B**; sub-rulings **S1 a**, **S2 a**,
> **S3**). **Not yet implemented**: each rule below names the issue that implements it and today's
> behavior until then. Tracker: #2427.
> **Supersedes as the governing rule:** R-JJ ("resolve what C# resolves in that position"), F1,
> F1-const / L16, F2, L20, R-JT's forward refusal, R-JV(b), #1974 ruling D and its B9 write-through
> amendment, and the CLAUDE.md axiom row "`global`/`nonlocal`: Axiom 1 wins". See
> [What this retires](#what-this-retires).
> **Companions:** `docs/language_specification/variable_scoping.md` (rewritten around this file as each
> rule lands, #2426), [python-fidelity-scope.md](python-fidelity-scope.md),
> [verification-contract.md](verification-contract.md).

## Why this file exists

By 2026-11-01 the question "what does this name mean here, and is its value ready?" had been ruled
separately for each place it came up: class-body siblings, constants, `@static` initializers, module
variables read from functions, lambdas in initializers, generator expressions, qualified spellings.
Each ruling was sound given its premise, but the premises disagreed. A measured matrix of 159 cells
(Sharpy @ 3ef53c6ec against python3 3.12) found 15 inconsistencies with four root causes:

| Root cause | Example (Sharpy / python3) |
|---|---|
| **A name's meaning depended on the kind of thing it named.** | Under a module binding of the same name, a later `@static` sibling yielded to the module binding, but a later literal `const` outranked it (`3` / `10`), and a later computed `const` was refused. |
| **Declaration order was applied differently in each place.** | A function reading a module variable declared below it was refused (SPY0200), while the same read of a later `const`, `def`, class or property ran. |
| **The spelling changed the verdict.** | A forward `@static` read was SPY0200 bare and SPY0618 qualified. In an instance initializer the bare form was refused and the qualified form ran. |
| **C#'s runtime leaked through as default values.** | A module variable initialized from a later computed `const` read `0`. A cross-type initialization chain printed `0 0 5`. Module initializers ran *after* `main` started. |

Nobody — human or agent — could predict a cell without the lookup table. This file replaces the
table with one principle and seven rules.

## The principle

> **A name means the same thing everywhere in its scope. Declaration order never changes *what* a
> name means; it only affects *when* the value is computed, and Sharpy guarantees the value is
> computed before it is read, or refuses the program. Sharpy never shows you a default value, and
> never gives a Python-valid program a different meaning; where it cannot match Python, it refuses.**

**The test for any program.** Take a program python3 runs. Sharpy prints the same thing or refuses
to compile it, except for the closed list of designed deviations under [N5](#n5-locals-are-ordered).
A Python `NameError` that exists *only* because of declaration order becomes a working program, as
it would in Rust or Swift. This is mechanical, so a generative differential harness checks it
(#2421) rather than a hand matrix.

## The rules

### N1. Scope decides meaning, not order

- Every module-level and class-level declaration — `def`, `class`, `const`, `let`, `var`, module
  variable, property, `@static` member — is visible throughout its scope, above and below its
  declaration.
- The innermost scope wins: a class member beats a module name of the same spelling.
- Python's one scope rule is kept: **class-body names are not visible by bare name inside methods,
  nested functions, lambdas or comprehension elements.** Write `H.x` or `self.x`. (This is the
  existing methods rule, R-Y / P21g–h, unchanged.)

```python
def show() -> None:
    print(limit)        # fine: module names are visible throughout the module

limit: int = 3

def main() -> None:
    show()              # 3
```

*Today:* a function reading a module variable declared below it is SPY0200. Implemented by #2423.

### N2. Ambiguity is an error

If reading top to bottom (Python) and reading by scope (N1) would give a name **different
meanings**, Sharpy refuses. There is exactly one such place: **a class-body initializer that reads a
member at or before that member's declaration, while an outer binding of the same name exists.**

```python
size: int = 8

class Box:
    @static
    area: int = size * size   # error: 'size' is ambiguous here — Python reads the module
                              # 'size' (8), scope reads the later member 'size' (2).
                              # Rename one, or write 'Box.size' or a module-qualified name.
    @static
    size: int = 2
```

The steer is "rename, or qualify". A Python-valid program therefore never silently changes meaning.
Without the outer binding there is no ambiguity, and the forward read simply works (N3).

*Today:* a later `@static`, property or method yields to the outer binding (prints `64`, Python's
value), and a later literal `const` outranks it (prints Sharpy's value, not Python's). Census @
d2d5cbcab: 2 sites in the corpus, both in the fixture written for the old rule. Implemented by #2423.

### N3. Values are ready before use

- Every module-level and class-level value is initialized before its first read, **whatever path
  reaches it**: bare, qualified, aliased, through a call, inside a lambda or a generator, or from
  another type or module.
- At load, each module runs its initializers **top to bottom**, after the modules it imports, and
  before `main`. Side effects therefore happen in Python's order for a Python-valid program.
- A forward read initializes its target first, as in Swift.
- An initialization **cycle** is a compile error where the compiler can see it (as Rust E0391 and C#
  CS0110; Sharpy's SPY0278 today), and otherwise a run-time circular-initialization error (as
  Swift's trap). It is **never** a default value.
- An initializer that throws leaves its value uninitialized; the next read runs it again.

```python
total: int = base * 2     # runs base's initializer first
base: int = compute()

def compute() -> int:
    print("compute")
    return 7

def main() -> None:
    print(total)          # compute / 14   (python3: NameError — an order-only error)
```

*Today:* the example above is refused (SPY0200: a module variable's initializer may not read a later
module variable), but the same forward read of a later computed `const` or module property silently
reads `0`; a forward `@static` read is refused (SPY0618, P75); and module initializers run after
`main` starts. Implemented by #2422 (see
[How N3 is emitted](#how-n3-is-emitted)).

### N4. Constants are not special

A `const` is an immutable value the compiler may also fold. It follows N1–N3 like any other
binding. There is no separate ordering rule for literal and computed constants, and a constant never
outranks an outer binding by being a constant.

*Today:* a literal `const` is unordered and outranks outer bindings; a computed `const` is ordered.
Implemented by #2423.

### N5. Locals are ordered

- A local is visible from its `let` / `var` (or first assignment) to the end of its block. This is
  Rust's and Swift's rule, and Python's `UnboundLocalError` points the same way. C#'s whole-block
  rule (CS0844) is not adopted.
- Shadowing follows #1974 unchanged: `let` / `var` may shadow, in nested and in the same scope.
- A local `def` is a local binding, so it is ordered too: calling it above its definition is refused
  (S3; Python raises at run time).
- `for`, `except`, `with` and comprehension targets are **fresh bindings for their block**, and a
  loop variable is **fresh per iteration**. Rust, Swift and C# `foreach` agree.

**Designed deviations from Python (the closed list):**

1. A loop variable is fresh per iteration, so a closure created in the loop captures that iteration's
   value (Python's closures see the last value). Ledger row in `docs/deviations.yaml` (#2426).
2. A `for` / `except` / `with` target does not overwrite an outer binding of the same name. Where
   that would change the output — **the outer name is read after the construct** — the program is
   refused instead (S2):

```python
def main() -> None:
    x: int = 100
    for x in range(3):   # error: target 'x' reuses a binding that is read after the loop;
        print(x)         # Python would print 2 below, Sharpy's fresh binding would print 100.
    print(x)             # Rename the loop variable.
```

   The refusal applies to any visible binding the target reuses — local, enclosing or module. Without
   a later read the program is legal and prints what Python prints.
3. Forward reads that Python rejects with `NameError` run (N1 + N3). This is an addition, not a
   divergence of output.

*Today:* the target is a fresh binding and the later read silently sees the outer value. Census @
d2d5cbcab: 4 refused sites (2 `for`, 2 `with`). Implemented by #2425.

### N6. Writing across a function boundary needs a declaration

`=` (and every augmented, tuple, star and walrus store) assigns a binding of the **same function**.
To write a module binding from a function, declare `global x`; to write an enclosing function's
local, declare `nonlocal x`. A bare store that would cross a function boundary is refused with the
steer "`let x = …` for a new local, or `global x` / `nonlocal x` to write the outer binding".

```python
count: int = 0

def bump() -> None:
    global count
    count += 1

def make_counter() -> () -> int:
    n: int = 0
    def step() -> int:
        nonlocal n
        n += 1
        return n
    return step
```

- This is Python's rule. On .NET it costs nothing: a static field write and a by-reference capture.
  It was never a .NET constraint, so the CLAUDE.md axiom row that said so is removed when N6 lands.
- **Module properties are module bindings.** Assigning a module-level property from a function
  calls its setter only under `global`; spelling never changes the verdict (N7).
- The declared name must resolve to a writable binding in the named scope; `global` or `nonlocal`
  on a `let` or `const` is refused as any write to them is. Whether `global x` may *create* a module
  binding (Python allows it) is decided in #2424's plan; the default is no — declarations are
  explicit.
- Python's own errors are kept: `nonlocal` with no enclosing binding, and a name used before its
  `global` / `nonlocal` declaration in the same function.

*Today:* `global` / `nonlocal` are refused (SPY0134), and a bare store writes through to the module
variable or enclosing local — where Python makes a new local (`5` / `1`). Census @ d2d5cbcab: 49
module stores in 25 files (at most 29 `global` lines; 2 in the `.spy` stdlib), 2 enclosing stores.
Implemented by #2424.

### N7. Spelling never changes the verdict

Bare, qualified (`H.x`, `module.x`), aliased, inside a lambda or generator, or through a call: the
same read gets the same answer, and a refusal gets the same diagnostic code. N3 makes this hold by
construction — readiness is guaranteed at the value, not checked per spelling — so no
per-spelling rule is ever needed.

## How N3 is emitted

C# gives Sharpy name lookup it can emit around (qualified and renamed names), but its static
initialization runtime is per type, lazy, and silent on order and cycles. N3 therefore does not use
C# type initializers to order Sharpy values. Each non-constant module or `@static` value is emitted
with its own initialization state, and readiness is checked through a flag the JIT folds away once
the sweep has run:

```csharp
static class Swept { public static readonly bool Done = RunSweep(); }   // one per module host

static int s_x; static byte s_x_state;          // 0 Uninitialized, 1 Initializing, 2 Ready
public static int x
{
    get
    {
        if (!Swept.Done && Volatile.Read(ref s_x_state) != 2) Init_x();
        return s_x;
    }
}
// Init_x: Initializing on entry (re-entry => circular-initialization error), Ready on success,
//         back to Uninitialized on throw (the next read retries); a lock in this slow path only.
```

- **Literal constants** stay C# `const` with no check.
- **Load order:** the entry point runs each imported module's sweep in import order, then its own,
  before `main`; a class's statics are swept at the class's position in its module. A library
  consumed from C# without a Sharpy entry point initializes lazily on first read.
- **Measured cost** (M4 Max, .NET 10.0.14, BenchmarkDotNet 0.15.8; full numbers on #2422): the
  folded flag runs at **0.99–1.01×** an unchecked static read under the default JIT, without PGO, and
  without tiered compilation. A plain per-read state check costs up to 1.77× (default JIT) and 2.5×
  (no PGO), because the JIT never hoists it out of a loop. `static readonly` + cctor and holder-class
  shapes were rejected: they cannot detect a cycle or retry after a throw. `Lazy<T>` costs up to
  3.5×.
- **Residue:**
  - Code compiled before the sweep and never recompiled keeps the check (ReadyToRun before tier-up;
    probably NativeAOT, not measured).
  - A failed sweep leaves the slow path on for the rest of the process.
  - Generic-class statics initialize per closed type on first use; the sweep cannot enumerate
    instantiations.

## The C# constraint map

What .NET actually forces, split by whether Sharpy can emit around it.

| | Constraint | Under this principle |
|---|---|---|
| **HARD** (runtime) | Type initializers are per type, lazy, and implementation-timed | Not used for ordering; per-value state + sweep (N3) |
| | Cross-type initialization cycles observe default values | Detected: circular-initialization error (N3) |
| | A throwing type initializer poisons the type for the process | Per-value state: the next read retries (N3) |
| | Every field starts as 0 / null | Never observed: reads wait for readiness (N3) |
| | Instance construction runs derived field initializers, then the base constructor, then the derived body | Out of scope; unchanged |
| | A C# optional-parameter default must be a constant | Out of scope; parameter defaults stay a separate class (SPY0401) |
| | Generic statics are per closed type | Residue: per-instantiation lazy init |
| **SOFT** (lookup and scoping) | Which declaration a simple name binds; member over outer; block scoping; CS0128, CS0136, CS0841, CS0844, CS0236; same-name collisions | Sharpy emits qualified or renamed names, so **none of these constrain Sharpy's surface** |

**Future C#:** nothing to wait for. These rules have been stable since C# 8; top-level members
(csharplang #9803) have no agreement and would be sugar over a static class; readonly locals (#188)
are "Likely Never"; there is no proposal to relax CS0236 or to warn on static-initialization order.

## What the reference languages do

Measured with python3 3.12, C# 14 (Roslyn 5.9, .NET 10), Rust 1.99.1 and Swift 6.4.2.

| Question | Python | C# | Rust | Swift | **Sharpy** |
|---|---|---|---|---|---|
| Can a module- or type-level name be used above its declaration? | Only from function bodies | Yes | Yes | Yes (library code) | **Yes** (N1) |
| Is the value ready when read early? | `NameError` | **No: silently 0 / null** | Always: statics must be const-evaluable | Yes: lazy per variable | **Yes** (N3) |
| Initialization cycle | `NameError` | Silent defaults (const: CS0110) | Compile error | Run-time trap | **Compile or run-time error** (N3) |
| Constants vs other statics | Same | Two different rules | Same | Same | **Same** (N4) |
| Class member by bare name inside a method | No | Yes | Never (`Self::K`) | Yes | **No** (N1, Python) |
| Local visible from… | Function-wide (`UnboundLocalError`) | Whole block | Declaration on | Declaration on | **Declaration on** (N5) |
| Write an outer binding from a function | `global` / `nonlocal` | Static fields writable; closures by reference | `static mut` needs `unsafe`; nested `fn` cannot capture | Bare | **`global` / `nonlocal`** (N6) |

C#'s rules are themselves inconsistent — names are order-free while values are textually ordered and
silently default; constants and static readonly fields follow two rules; shadowing rules differ by
construct; initialization timing depends on whether an empty static constructor exists. Swift is sound
for library globals but not for `main.swift` top-level code (`let a = b + 1; let b = 5` prints `1`).
Rust is sound by restriction. Sharpy takes Rust's and Swift's "names are order-free", Swift's "values
are lazily ready", and Python's run order and write declarations.

## What this retires

| Ruling or behavior | Was | Now |
|---|---|---|
| R-JT forward refusal (P75, SPY0618) | Forward static read refused | Works (N3). P75's source-order emission stays as the sweep order. |
| F1: initializers read only earlier siblings (SPY0200 "declared later") | Refused | Works (N1 + N3) |
| F1-const / L16: a literal const is unordered and outranks the outer binding | Sharpy's value, not Python's | Refused as ambiguous (N2 + N4) |
| F2: a later member yields to the outer binding | Python's value by a special rule | Refused as ambiguous (N2) |
| L20: an earlier `@static` method sees later statics at default | `0` | The correct value (N3) |
| R-JJ: resolve what C# resolves in that position | Governing rule | Replaced by N1–N4 |
| R-JV(b): a lambda in an initializer reads what its initializer reads | Ruled, unimplemented (P21j, #2398) | A lambda is a body: the class-name rule applies (N1); qualified reads are safe (N3) |
| Per-spelling rulings (stored generator expressions, lambda / IIFE cells #2413, qualified spellings #2416) | Separate rules | Subsumed by N3 + N7 |
| #2415 (a later const under an outer binding) | Open | Resolved by N2: refuse |
| Module variable visible only below (#2207's premise) | SPY0200 | Visible (N1) |
| Module initializers run after `main` starts | Yes | Before `main`, in source order (N3) |
| #1974 ruling D and the B9 write-through | A bare store writes the outer binding | `global` / `nonlocal` required (N6) |
| CLAUDE.md axiom row "`global`/`nonlocal`: Axiom 1 wins" | Refused (SPY0134) | Row removed when N6 lands |

**Unchanged:** the methods rule (class names are not bare in methods); block scoping; #1974's
`let` / `var` / `const` and shadowing; SPY0278 static cycle detection (it becomes N3's compile-time
half); `match` arms as sibling blocks; builtin-shadowing behavior.

**Out of scope, flagged:** parameter and dataclass / struct defaults (SPY0401); an instance-field
initializer reading an earlier instance field (refused today; CS0236 is SOFT, so N3 could admit it
later); `name_shadowing.md`'s governing sentence "where Python and C# disagree, C# decides" — its
behavior already fits "same or refuse", and the sentence is replaced by this principle (#2426).

## Implementation

| Issue | Scope | Order |
|---|---|---|
| #2421 | Generative differential harness: Sharpy vs python3, "same output or compile-time refusal", over binding kind × read position × order × spelling × outer binding | **First** |
| #2422 | N3: per-value state, folded readiness flag, load-order sweep, cycle and retry semantics | After #2421 |
| #2423 | N1, N2, N4, N7 | After #2422 (forward reads need N3 to be safe) |
| #2424 | N6: `global` / `nonlocal` | Independent |
| #2425 | S2: refuse a construct target whose outer name is read after | Independent |
| #2426 | This file, the `variable_scoping.md` rewrite, stale spec and deviation rows | Rolling, with each rule |

Placement into plans happens at `/batch-issues`; the tracker is #2427.

**Exposure census** (@ d2d5cbcab, measured over 3677 fixtures, 251 stdlib-test fixtures, the `.spy`
stdlib and spy-test projects, and 1256 spec blocks; details on #2427):

| Rule | Sites the rule changes |
|---|---|
| N2 | 2, in 1 fixture |
| N4 (later const outranks) | 0 |
| N6 module stores | 49 in 25 files; at most 29 `global` lines; 2 in the `.spy` stdlib (`random_module.spy`, `socket_module.spy`) |
| N6 enclosing stores | 2 |
| S2 refusals | 4 |
| N3 initializer timing | 163 initializers contain a call; none was found to print or do I/O at initialization |
