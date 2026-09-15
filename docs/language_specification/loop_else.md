# Loop Else Clause

For both `for` and `while` loops, an `else` clause can be
added to execute if the loop completes without a break.

An example with a `for`-loop is shown below.

```python
for item in items:
    if item == target:
        break
else:
    # Executed only if loop completes without break
    print("Not found")
```

## Loop `else` with `return` or Exceptions

The `else` clause only runs if the loop completes normally (no `break`). It does NOT run if the loop exits via `return` or an exception:

```python
def find_item(items: list[int], target: int) -> int:
    for item in items:
        if item == target:
            return item      # return exits function, else does NOT run
    else:
        print("Not found")   # Only runs if no return or break
    return -1

def risky_search(items: list[int]) -> int:
    for item in items:
        if item < 0:
            raise ValueError("Negative value")  # else does NOT run
    else:
        print("All items valid")  # Only runs if loop completes normally
    return len(items)
```

This is the natural behavior from the lowered boolean-flag pattern—the flag is only checked if control flow reaches that point.

## A `break` at any depth suppresses the `else`

The `else` runs only when no `break` **targeting this loop** executed — regardless of how deeply
the `break` is nested. A `break` inside a `match` arm, a `try`, or a `with` block still targets the
enclosing loop and still suppresses its `else`, exactly as in Python:

```python
def main() -> None:
    for i in range(4):
        match i:
            case 2:
                break            # targets the for-loop, not the match
            case _:
                print(i)
    else:
        print("else")           # skipped: a break targeting this loop ran
    print("done")
```

Output:

```
0
1
done
```

A `continue` never suppresses the `else` (the loop still completes normally); a `break` in a
*nested* loop targets that inner loop and leaves this loop's `else` intact.

*Implementation*
- *🔄 Lowered - Boolean flag pattern. The loop declares `bool _loopCompleted = true;`, and each
  `break` that targets this loop clears the flag at the break site — no matter how deeply nested —
  because the loop each `break`/`continue` binds to is recorded during semantic analysis and the
  emitter reads it (#1816). A `match` hosting such a `break` lowers to the `is`-chain rather than a
  C# `switch`, so the `break` reaches the loop instead of the switch.*
```csharp
bool _loopCompleted = true;
foreach (var item in items) {
    if (item == target) { _loopCompleted = false; break; }
}
if (_loopCompleted) { Console.WriteLine("Not found"); }
```
