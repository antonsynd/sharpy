# Issue Report: internal_compiler_error

**Timestamp:** 2026-03-07T00:16:38.127370
**Type:** internal_compiler_error
**Feature Focus:** overloading_with_inheritance
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
class Base:
    @virtual
    def process(self, x: int) -> str:
        return f"Base int: {x}"
    
    @virtual
    def process(self, s: str) -> str:
        return f"Base str: {s}"

class Child(Base):
    @override
    def process(self, x: int) -> str:
        return f"Child int: {x * 2}"
    
    @override
    def process(self, s: str) -> str:
        return f"Child str: {s.upper()}"

def test_overload_virtual(obj: Base) -> None:
    print(obj.process(5))
    print(obj.process("hello"))

def main():
    b: Base = Base()
    c: Base = Child()
    
    test_overload_virtual(b)
    test_overload_virtual(c)

```

## Error

```
Internal compiler error: Compilation errors:

error[SPY0907]: Internal: type inference produced UnknownType for 'FunctionCall' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmpdsfe09oo/dogfood_test.spy:21:11
    |
 21 |     print(obj.process("hello"))
    |           ^^^^^^^^^^^^^^^^^^^^
    |

error[SPY0907]: Internal: type inference produced UnknownType for 'FunctionCall' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmpdsfe09oo/dogfood_test.spy:20:11
    |
 20 |     print(obj.process(5))
    |           ^^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 249.06s
