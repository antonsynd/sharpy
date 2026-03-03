# Issue Report: compilation_failed

**Timestamp:** 2026-03-03T08:23:52.056557
**Type:** compilation_failed
**Feature Focus:** match_type_pattern
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Type patterns with binding in match statement
# Demonstrates matching on runtime types and extracting values

def describe(val: object) -> str:
    desc: str = "unknown"
    match val:
        case bool() as b:
            desc = "boolean"
        case int() as n if n < 0:
            desc = "negative int"
        case int() as n:
            desc = f"int={n}"
        case str() as s:
            desc = f"string len={len(s)}"
        case float() as f:
            desc = f"float={f}"
        case _:
            desc = "other"
    return desc

def main():
    print(describe(True))
    print(describe(42))
    print(describe(-7))
    print(describe("hello"))
    print(describe(3.5))
    print(describe([1, 2]))

```

## Error

```
Assembly compilation failed:

error[CS0103]: The name 'n' does not exist in the current context
  --> /tmp/tmp8ksyi78y/dogfood_test.spy:12:60
    |
 12 |             desc = f"int={n}"
    |                              ^
    |


```

## Compiler Output

```
warning[SPY0451]: Local variable 'b' is assigned but never used
  --> /tmp/tmp8ksyi78y/dogfood_test.spy:7:14
    |
  7 |         case bool() as b:
    |              ^^^^^^^^^^^
    |


```

## Generated C#

```csharp
warning[SPY0451]: Local variable 'b' is assigned but never used
  --> /tmp/tmp8ksyi78y/dogfood_test.spy:7:14
    |
  7 |         case bool() as b:
    |              ^^^^^^^^^^^
    |

Generated C# code written to: /tmp/tmp8ksyi78y/dogfood_test.cs

```

## Timing

- Generation: 125.80s
- Execution: 4.62s
