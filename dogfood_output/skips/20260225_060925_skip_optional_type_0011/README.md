# Skipped Dogfood Run

**Timestamp:** 2026-02-25T06:00:23.092093
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0107]: Invalid type annotation target
  --> /tmp/tmp_zjsxyr8/dogfood_test.spy:3:19
    |
  3 |         self.value: int? = value
    |                   ^
    |

error[SPY0107]: Invalid type annotation target
  --> /tmp/tmp_zjsxyr8/dogfood_test.spy:12:19
    |
 12 |         self.value: int? = value
    |                   ^
    |


**Feature Focus:** optional_type
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
class Container:
    def __init__(self, value: int?):
        self.value: int? = value
    
    def get_or_default(self, default_value: int) -> int:
        if self.value is not None:
            return self.value
        return default_value

class IntBox:
    def __init__(self, value: int?):
        self.value: int? = value
    
    def unwrap_or_else(self, default_fn: () -> int) -> int:
        if self.value is not None:
            return self.value
        return default_fn()

def get_fallback() -> int:
    return 42

def main():
    opt1: int? = Some(10)
    opt2: int? = None()
    
    print(opt1.unwrap_or(0))
    print(opt2.unwrap_or(99))
    
    # Test Container class with get_or_default
    c1: Container = Container(Some(25))
    c2: Container = Container(None())
    
    print(c1.get_or_default(100))
    print(c2.get_or_default(100))
    
    # Test IntBox with unwrap_or_else
    box1: IntBox = IntBox(Some(50))
    box2: IntBox = IntBox(None())
    
    print(box1.unwrap_or_else(get_fallback))
    print(box2.unwrap_or_else(get_fallback))
    
    opt3: int? = Some(7)
    mapped: int? = opt3.map(lambda x: x * 3)
    print(mapped.unwrap_or(0))
    
    val: str? = Some("hello")
    if val is not None:
        print(1)
    else:
        print(0)
    
    chain1: int? = Some(5)
    result1: int? = chain1.map(lambda x: x + 10)
    final1: int = result1.unwrap_or(0)
    print(final1)
    
    chain2: int? = None()
    result2: int? = chain2.map(lambda x: x + 10)
    final2: int = result2.unwrap_or(0)
    print(final2)
    
    empty: int? = None()
    print(empty.unwrap_or(777))
    
    def process_optional(opt: int?) -> int:
        if opt is not None:
            return opt * 2
        return 0
    
    print(process_optional(Some(15)))
    print(process_optional(None()))
    
    name: str? = Some("world")
    print(name.unwrap_or("default"))
    
    empty_name: str? = None()
    print(empty_name.unwrap_or("default"))

# EXPECTED OUTPUT:
# 10
# 99
# 25
# 100
# 50
# 42
# 21
# 1
# 15
# 0
# 777
# 30
# 0
# world
# default
```

## Timing

- Generation: 527.38s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.18).

This output is saved for inspection to help improve prompting.
