# Type Checker Architecture: Direct Inference vs. Constraint Solving

**Status:** Exploration
**Author:** antonsynd
**Date:** 2026-06-03

## 1. Motivation

Swift 6.4 shipped significant type checker performance improvements (June 2026)
to address long-standing exponential blowup in constraint solving. This prompted
an exploration of Sharpy's type checker architecture: what tradeoffs does the
current design make, where does it excel, and where might it limit the language
as it evolves?

This document captures the analysis for future reference. It is not a proposal
to change anything — the current architecture is well-suited to Sharpy's design
goals.

## 2. Sharpy's Type Checker Architecture

Sharpy uses **single-pass direct inference** with a post-validation pipeline.

### 2.1 How It Works

The TypeChecker walks the AST top-to-bottom in a single pass, eagerly inferring
and recording types as it encounters each expression. There is no separate
constraint collection phase — types are resolved immediately at each node.

```
CheckModule()
  ├── Pre-pass: resolve return/parameter types for forward references
  ├── Main pass: traverse AST statements sequentially
  │     └── CheckExpression() → infer type, record in SemanticInfo, return
  └── Post-pass: run ValidationPipeline (pluggable validators)
```

### 2.2 Generic Type Inference

Generic types are inferred via `GenericTypeInferenceService.InferTypeArguments()`,
which uses **left-to-right unification**:

1. Process function parameters sequentially (left to right)
2. Unify each formal parameter type with the actual argument type
3. Build a substitution map (type parameter name → inferred type)
4. Check all type constraints after inference completes

The unification algorithm handles 8 structural cases (GenericType, FunctionType,
NullableType, TupleType, etc.) and is recursive but not backtracking — each
parameter binding is final.

### 2.3 Overload Resolution

Overload resolution is a **three-pass sequential filter**:

1. **Arity filter** — eliminate candidates with wrong parameter count (respecting
   defaults and variadic parameters)
2. **Keyword argument filter** — eliminate candidates whose parameter names don't
   match keyword arguments
3. **Type compatibility filter** — check argument type assignability to parameter
   types; disambiguate by preferring exact arity match, then fewest type
   parameters

This is a one-shot process with no backtracking. If multiple candidates survive
all three passes, it reports ambiguity.

### 2.4 Caching

Several caching layers avoid redundant work:

| Cache | Scope | Key |
|-------|-------|-----|
| Expression types | Per-compilation (SemanticInfo) | AST node identity (ReferenceEqualityComparer) |
| Binary/unary operator results | Per-TypeInferenceService | (left type, op, right type) |
| .NET assembly overloads | Persistent (JSON.gz on disk) | Assembly identity |
| Module functions | Per-ModuleRegistry | Module name |

### 2.5 Complexity

| Operation | Complexity | Notes |
|-----------|-----------|-------|
| Overload resolution | O(n × m) | n=candidates, m=parameters |
| Generic inference | O(p × d) | p=parameters, d=type nesting depth |
| Method resolution | O(h × m) | h=hierarchy depth, m=methods per class |
| Type unification | O(d) | d=type structure depth; no backtracking |

All operations are polynomial. There is no combinatorial explosion path.

## 3. Swift's Constraint Solver (for Comparison)

Swift uses a **constraint-based type checker**: it collects type constraints from
the entire expression tree, then solves them simultaneously using a search-based
solver with backtracking.

### 3.1 How It Works

```
Collect constraints from expression tree
  → Build constraint graph (type variables, disjunctions for overloads)
  → Solve via search with backtracking
  → Apply solution (bind all type variables)
```

Each overload choice creates a **disjunction** node in the constraint graph. The
solver must explore combinations of these disjunctions to find a consistent
assignment. This is inherently exponential in the number of disjunction nodes.

### 3.2 Swift 6.4 Improvements

Swift 6.4 addressed the exponential blowup with three optimizations:

1. **Disjunction pruning** — categorize overload choices as "favored," "not
   favored," or "impossible," eliminating dead branches before exploration
2. **Domain-based binding inference** — reason about the complete domain of
   possible types for each variable using algebraic operations on the subtype
   relation, rather than collecting individual candidates
3. **Incremental binding set generation** — avoid redundant computation when
   binding sets change

Results: expressions that previously timed out now type-check in <10ms.

## 4. Tradeoff Analysis

### 4.1 Where Sharpy's Approach Wins

**Predictable performance.** No expression, no matter how complex, can cause
exponential type checking time. The worst case is always polynomial in obvious
dimensions (number of overloads × number of parameters × hierarchy depth).

**Simpler error messages.** When type checking fails, the error is local — "this
argument has type X but parameter expects type Y." Constraint solvers produce
errors like "no solution found for constraint system," which require heuristics
to make human-readable.

**Simpler implementation.** ~10,600 lines for the TypeChecker (11 partial files)
vs. Swift's solver which is substantially larger. Easier to debug, maintain, and
reason about correctness.

**No "expression too complex" errors.** Swift historically produced these when
the solver timed out. Sharpy cannot — every expression completes in bounded time.

**Fast for the common case.** Direct inference with caching means most
expressions resolve in a single dictionary lookup. No solver setup overhead.

### 4.2 Where Sharpy's Approach Is Weaker

**No bidirectional type inference.** Sharpy cannot propagate type information
backward from usage context. Example:

```python
# Swift can infer the closure parameter type from the array element type
# and the expected return type simultaneously
items.map { $0.name }

# Sharpy requires explicit annotation when inference can't flow left-to-right
items.map(lambda x: x.name)  # works if items is typed, may need annotation otherwise
```

**Argument order sensitivity in generic inference.** Left-to-right unification
means the order of arguments matters. If the binding for a type parameter comes
from a later argument, earlier arguments using that parameter may not benefit:

```python
# If callback's type depends on T, and items provides the binding for T,
# left-to-right processing may fail to infer T from items first
def process(callback: Callable[[T], None], items: list[T]) -> None: ...
```

A constraint solver handles this regardless of parameter order because it
considers all constraints simultaneously.

**Less precise overload resolution in complex cases.** Sequential filtering picks
the first match that passes all filters. A constraint solver considers all
overloads simultaneously with full type context, potentially finding a "better"
match that sequential filtering would miss due to early elimination.

**No cross-expression type reasoning.** Sharpy resolves each sub-expression
independently. It cannot use information from how a value is *consumed* to
influence how it is *produced*:

```python
# A constraint solver could infer the dict literal types from the annotation
x: dict[str, int] = {key: compute_value()}  # solver infers compute_value() -> int

# Sharpy infers the dict literal's type from its contents, then checks assignability
```

### 4.3 Assessment for Sharpy

The current architecture aligns well with Sharpy's design philosophy:

- **Axiom precedence (.NET > Types > Python)** means explicit types at module
  boundaries are expected, reducing the need for deep inference
- **No associated types, existentials, or variadic generics** means the type
  system complexity stays within what direct inference handles well
- **Python-flavored syntax** means users expect explicit annotation in ambiguous
  cases (Python itself has minimal inference)

The main risk area is if future language evolution introduces features that
create more overload combinations or require cross-expression inference (e.g.,
complex operator chains on generic numeric types, builder-pattern DSLs with
heavy generic inference). Worth watching but not currently a problem.

## 5. Performance Data

### 5.1 Current Benchmarks

End-to-end compilation benchmarks exist in `CompilerBenchmarks.cs`:

| Fixture | Lines | What it exercises |
|---------|-------|-------------------|
| Hello World | 4 | Baseline |
| Fibonacci | 22 | Recursion, arithmetic |
| Classes | 35 | OOP, methods |
| Comprehensions | 26 | Generic inference, iteration |
| Large Functions | 73 | Deeper AST |
| Combined Corpus | ~160 | All of the above |

Module loading performance thresholds (from `CachedDiscoveryPerformanceTests`):

- First load (cache build): <1800ms
- Cached load: <200ms (at least 3/5 runs)
- Module loading overhead: <200ms

### 5.2 Gap: No Type Checker Isolation Benchmarks

Current benchmarks are end-to-end (source → IL). There are no benchmarks that
isolate type checker performance specifically, which would be useful for
detecting regressions as the type system grows. The validator timing data
(TypeChecker.ValidatorTimes) exists at runtime but isn't captured in benchmarks.

### 5.3 Gap: No Pathological-Case Tests

There are no tests for expressions designed to stress type checker performance
(e.g., deeply nested generics, long overload chains, complex operator
expressions). Given the polynomial complexity guarantee, these shouldn't blow up,
but having them would provide confidence and catch accidental quadratic behavior.

## 6. Possible Future Work

These are ideas, not proposals. None are currently needed.

- **Type checker isolation benchmarks** — measure CheckModule time separately
  from parsing and code generation to detect regressions
- **Pathological-case stress tests** — expressions with deep nesting, many
  overloads, or long generic chains to verify polynomial behavior holds
- **Bidirectional inference for literals** — propagate expected types into
  dict/list/set literal inference without a full constraint solver (targeted
  enhancement, not architectural change)
- **Overload scoring** — instead of filtering sequentially, score all candidates
  and pick the best match (improves precision without adding backtracking)
