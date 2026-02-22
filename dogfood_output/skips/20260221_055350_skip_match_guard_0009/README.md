# Skipped Dogfood Run

**Timestamp:** 2026-02-21T05:33:32.174702
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0103]: Expected end of statement, got For
  --> /tmp/tmpqksnyg_o/dogfood_test.spy:44:5
    |
 44 |     for r in results:
    |     ^^^
    |


**Feature Focus:** match_guard
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Match guards with type patterns and complex conditions
# Guards allow additional constraints beyond the pattern itself
# Using isinstance() checks in guards since case int() as n: is not supported

def categorize(value: object) -> str:
    match value:
        # Zero case (literal pattern) - check before int guard
        case 0:
            return "exactly zero"
        # Guard with positive integers
        case n if isinstance(n, int) and n > 0 and n < 10:
            return f"small positive digit: {n}"
        # Guard with larger positive integers
        case n if isinstance(n, int) and n >= 10 and n < 100:
            return f"medium positive: {n}"
        # Guard with negative integers
        case n if isinstance(n, int) and n < 0:
            return f"negative: {n}"
        # Guard with float ranges
        case f if isinstance(f, float) and f >= 0.0 and f <= 1.0:
            return f"unit float: {f:.2f}"
        # Guard with string length check
        case s if isinstance(s, str) and len(s) <= 3:
            return f"short text ({len(s)} chars): {s}"
        # Guard with string content check
        case s if isinstance(s, str) and "urgent" in s:
            return f"urgent message: {s}"
        # Wildcard fallback
        case _:
            return f"other: {value}"

def main():
    results: list[str] = [
        categorize(5),
        categorize(42),
        categorize(-7),
        categorize(0),
        categorize(0.5),
        categorize(2.71),
        categorize("hi"),
        categorize("urgent: action needed"),
        categorize("something else entirely")
    ]
    for r in results:
        print(r)

# EXPECTED OUTPUT:
# small positive digit: 5
# medium positive: 42
# negative: -7
# exactly zero
# unit float: 0.50
# other: 2.71
# short text (2 chars): hi
# urgent message: urgent: action needed
# other: something else entirely
```

## Timing

- Generation: 1202.13s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.18).

This output is saved for inspection to help improve prompting.
