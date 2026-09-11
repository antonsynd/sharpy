# For Loop

```python
# Iterate over collection
for name in names:
    print(name)

# Iterate with range
for i in range(10):
    print(i)

# Enumerate for index and value
for index, name in enumerate(names):
    print(f"{index}: {name}")
```

A **tuple** is an iterable, never a list: `for x in (1, 2, 3)` iterates its members without building
a collection. The loop variable's type is the tuple's best common element type — the same rule a
collection literal uses (see
[collection_types.md](collection_types.md#best-common-type)) — so `for x in (1, 2.5)` binds `x` as
`float`, and a tuple with no best common type is refused by name (SPY0227) with the steer to
annotate the target:

```python
for x in (1, 2.5):
    print(x)              # 1.0 / 2.5  — python3 prints `1` / `2.5`; see the note below

# for x in (1, "a"):      # SPY0227 — no best common type ('int32', 'str')
```

**Departure from Python, and its scope.** Python leaves each member at its own type, so
`for x in (1, 2.5)` prints `1` then `2.5`. Sharpy binds ONE loop variable, so the element type is
the best common type and the first member prints as `1.0`. The widening is local to the ITERATION
route: unpacking does not widen (`a, b = (1, 2.5)` prints `1` and `2.5`, matching Python), because
each target takes its own member's type.

`else`-clauses are described in [loop_else.md](loop_else.md).

*Implementation:*
- *Collection: ✅ Native - `foreach (var item in collection)`*
- *`range()`: 🔄 Lowered - `for (int i = 0; i < n; i++)`*
- *`enumerate()`: 🔄 Lowered - `.Select((x, i) => (i, x))`*
- *Tuple: 🔄 Lowered - `foreach (var item in new T[] { t.Item1, ... })` from the recorded
  tuple-as-sequence fact*
