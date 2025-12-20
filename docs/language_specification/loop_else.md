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

*Implementation: 🔄 Lowered - Boolean flag pattern:*
```csharp
bool _loopCompleted = true;
foreach (var item in items) {
    if (item == target) { _loopCompleted = false; break; }
}
if (_loopCompleted) { Console.WriteLine("Not found"); }
```
