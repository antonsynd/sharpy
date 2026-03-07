# Successful Dogfood Run

**Timestamp:** 2026-03-07T02:23:38.253116
**Feature Focus:** function_with_print
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Binary tree traversal simulation with function calls and print tracing
# Each recursive call processes a node value and tracks depth level
# When max_depth is reached, leaf nodes return doubled values
# Internal nodes combine results from left (value+1) and right (value+2) branches

def process_node(value: int, depth: int, max_depth: int) -> int:
    # Base case: leaf nodes at max depth
    if depth >= max_depth:
        result = value * 2
        print(f"[Depth {depth}] Leaf: {value} -> {result}")
        return result
    
    # Recursive case: process left and right branches
    left_val = process_node(value + 1, depth + 1, max_depth)
    right_val = process_node(value + 2, depth + 1, max_depth)
    combined = left_val + right_val
    
    print(f"[Depth {depth}] Node {value}: {left_val} + {right_val} = {combined}")
    return combined

def main():
    print("Starting tree traversal...")
    final = process_node(5, 0, 2)
    print(f"Final result: {final}")

```

## Output

```
Starting tree traversal...
[Depth 2] Leaf: 7 -> 14
[Depth 2] Leaf: 8 -> 16
[Depth 1] Node 6: 14 + 16 = 30
[Depth 2] Leaf: 8 -> 16
[Depth 2] Leaf: 9 -> 18
[Depth 1] Node 7: 16 + 18 = 34
[Depth 0] Node 5: 30 + 34 = 64
Final result: 64
```

## Timing

- Generation: 459.50s
- Execution: 4.56s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
