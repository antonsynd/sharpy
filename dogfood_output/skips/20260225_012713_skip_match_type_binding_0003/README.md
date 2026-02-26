# Skipped Dogfood Run

**Timestamp:** 2026-02-25T01:14:25.405067
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0104]: Expected Colon, got LeftParen
  --> /tmp/tmp1c04ldvn/dogfood_test.spy:12:19
    |
 12 |         case Point() as pt:
    |                   ^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmp1c04ldvn/dogfood_test.spy:14:9
    |
 14 |         case _:
    |         ^^^^
    |

error[SPY0104]: Expected Colon, got LeftParen
  --> /tmp/tmp1c04ldvn/dogfood_test.spy:19:17
    |
 19 |         case int() as v if v is not None:
    |                 ^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmp1c04ldvn/dogfood_test.spy:21:9
    |
 21 |         case _:
    |         ^^^^
    |

error[SPY0100]: Unexpected token: Dedent
  --> /tmp/tmp1c04ldvn/dogfood_test.spy:24:5
    |
 24 |     n: int = 15
    |     ^
    |


**Feature Focus:** match_type_binding
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
class Point:
    x: int
    y: int

    def __init__(self, x_val: int, y_val: int):
        self.x = x_val
        self.y = y_val

def main():
    p: Point = Point(3, 4)
    match p:
        case Point() as pt:
            print(pt.x + pt.y)
        case _:
            print(0)
    
    val: int? = None()
    match val:
        case int() as v if v is not None:
            print(v.unwrap())
        case _:
            print(42)
    
    n: int = 15
    match n:
        case int() as x if x > 10:
            print(x - 5)
        case int() as x:
            print(x + 5)
        case _:
            print(0)

# EXPECTED OUTPUT:
# 7
# 42
# 10
```

## Timing

- Generation: 750.36s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.18).

This output is saved for inspection to help improve prompting.
