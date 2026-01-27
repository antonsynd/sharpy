# Issue Report: compilation_failed

**Timestamp:** 2026-01-26T22:10:49.884936
**Type:** compilation_failed
**Feature Focus:** from_import
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test: from...import statement with multiple items and selective imports
# Tests: from module import syntax, using imported items directly

from system import Console, Environment

def main():
    # Use imported .NET types directly without qualification
    Console.WriteLine("Testing from-import")
    
    # Access static property from imported type
    newline_len: int = len(Environment.NewLine)
    print(newline_len)
    
    # Verify we can use Console methods
    Console.WriteLine("Direct Console access works")

# EXPECTED OUTPUT:
# Testing from-import
# 2
# Direct Console access works
```

## Error

```
Assembly compilation failed:
  dogfood_test.cs(15,30): error CS0266: Cannot implicitly convert type 'uint' to 'int'. An explicit conversion exists (are you missing a cast?)

```

## Timing

- Generation: 5.61s
- Execution: 1.33s
