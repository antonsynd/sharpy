# Skipped Dogfood Run

**Timestamp:** 2026-03-07T05:47:00.987473
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0104]: Expected Colon, got LeftParen
  --> /tmp/tmpd0bve804/dogfood_test.spy:9:23
    |
  9 |         case Task.Done(result):
    |                       ^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmpd0bve804/dogfood_test.spy:11:9
    |
 11 |         case Task.Running(progress):
    |         ^^^^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmpd0bve804/dogfood_test.spy:13:9
    |
 13 |         case Task.Idle:
    |         ^^^^
    |

error[SPY0100]: Unexpected token: Dedent
  --> /tmp/tmpd0bve804/dogfood_test.spy:15:1
    |
 15 | 
    | ^
    |


**Feature Focus:** union_generic
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
union Task[T]:
    case Idle()
    case Running(progress: int)
    case Done(result: T)

def main():
    t: Task[int] = Task.Done(100)
    match t:
        case Task.Done(result):
            print(result)
        case Task.Running(progress):
            print(progress)
        case Task.Idle:
            print(0)

```

## Timing

- Generation: 664.04s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
