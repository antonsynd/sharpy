# Issue Report: compilation_failed

**Timestamp:** 2026-02-25T08:14:42.557731
**Type:** compilation_failed
**Feature Focus:** nullable_types
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Nullable types with null coalescing, conditional access, and type narrowing

def process_score(raw_input: str?) -> int:
    value: str = raw_input ?? "0"
    
    if value == "":
        return 0
    result: int = 0
    for c in value:
        result = result * 10 + (ord(c) - 48)  # Simple digit parsing
    return result

def get_display_name(name: str?) -> str:
    formatted: str? = name?.strip()
    
    if formatted is not None:
        if len(formatted) > 0:
            return formatted
    return "Anonymous"

def main():
    a: int? = Some(42)
    b: int? = None()
    
    result1: int = a ?? 0  # Should be 42
    result2: int = b ?? 100  # Should be 100 (default)
    
    print(result1)
    print(result2)
    
    greeting: str? = Some("  hello  ")
    empty: str? = None()
    
    upper1: str? = greeting?.upper()
    upper2: str? = empty?.upper()
    
    # unwrap_or for safe access
    final1: str = upper1?.strip() ?? "NO GREETING"
    final2: str = upper2 ?? "NO VALUE"
    
    print(final1)
    print(final2)
    
    print(get_display_name("  Alice  "))
    print(get_display_name(None()))
    print(get_display_name(""))
    
    print(process_score(Some("123")))
    print(process_score(None()))

# EXPECTED OUTPUT:
# 42
# 100
# HELLO
# NO VALUE
# Alice
# Anonymous
# Anonymous
# 123
# 0
```

## Error

```
Assembly compilation failed:

error[CS0266]: Cannot implicitly convert type 'object' to 'Sharpy.Optional<string>'. An explicit conversion exists (are you missing a cast?)
  --> /tmp/tmp5pifzsmb/dogfood_test.spy:14:38
    |
 14 |     formatted: str? = name?.strip()
    |                                    ^
    |

error[CS0266]: Cannot implicitly convert type 'object' to 'Sharpy.Optional<string>'. An explicit conversion exists (are you missing a cast?)
  --> /tmp/tmp5pifzsmb/dogfood_test.spy:34:35
    |
 34 |     upper1: str? = greeting?.upper()
    |                                   ^
    |

error[CS0266]: Cannot implicitly convert type 'object' to 'Sharpy.Optional<string>'. An explicit conversion exists (are you missing a cast?)
  --> /tmp/tmp5pifzsmb/dogfood_test.spy:35:35
    |
 35 |     upper2: str? = empty?.upper()
    |                                  ^
    |

error[CS0266]: Cannot implicitly convert type 'object' to 'string'. An explicit conversion exists (are you missing a cast?)
  --> /tmp/tmp5pifzsmb/dogfood_test.spy:38:25
    |
 38 |     final1: str = upper1?.strip() ?? "NO GREETING"
    |                         ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmp5pifzsmb/dogfood_test.cs

```

## Timing

- Generation: 71.14s
- Execution: 4.34s
