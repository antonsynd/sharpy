# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T17:11:52.703823
**Type:** compilation_failed
**Feature Focus:** nullable_types
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
class UserProfile:
    _display_name: str?
    _age: int?
    
    def __init__(self, name: str?, age: int?):
        self._display_name = name
        self._age = age
    
    property get formatted_name(self) -> str:
        # Null coalescing with string fallback
        return self._display_name ?? "Anonymous User"
    
    def get_name_length(self) -> int:
        # Type narrowing with is not None check
        if self._display_name is not None:
            return len(self._display_name)
        return 0
    
    def get_aged_years(self) -> int:
        # Type narrowing followed by arithmetic
        if self._age is not None:
            years: int = self._age
            years += 5
            return years
        return 25

def main():
    # Different nullable patterns
    user1 = UserProfile(None, None)
    user2 = UserProfile("Alice", 30)
    
    print(user1.formatted_name)
    print(user2.formatted_name)
    print(user1.get_name_length())
    print(user2.get_name_length())
    print(user1.get_aged_years())
    print(user2.get_aged_years())

```

## Error

```
Assembly compilation failed:

error[CS1503]: Argument 2: cannot convert from '<null>' to 'Sharpy.Optional<int>'
  --> /tmp/tmpqw_v6z_a/dogfood_test.spy:29:43
    |
 29 |     user1 = UserProfile(None, None)
    |                                    ^
    |

error[CS0019]: Operator '??' cannot be applied to operands of type 'Optional<string>' and 'string'
  --> /tmp/tmpqw_v6z_a/dogfood_test.spy:11:24
    |
 11 |         return self._display_name ?? "Anonymous User"
    |                        ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpqw_v6z_a/dogfood_test.cs

```

## Timing

- Generation: 77.44s
- Execution: 4.99s
