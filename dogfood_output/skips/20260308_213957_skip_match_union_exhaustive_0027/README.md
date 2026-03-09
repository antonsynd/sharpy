# Skipped Dogfood Run

**Timestamp:** 2026-03-08T21:33:04.438587
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0100]: Unexpected token: Newline
  --> /tmp/tmp5_75zzm_/dogfood_test.spy:8:29
    |
  8 |         case Status.Pending:
    |                             ^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmp5_75zzm_/dogfood_test.spy:10:9
    |
 10 |         case Status.Running:
    |         ^^^^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmp5_75zzm_/dogfood_test.spy:12:9
    |
 12 |         case Status.Completed:
    |         ^^^^
    |

error[SPY0100]: Unexpected token: Dedent
  --> /tmp/tmp5_75zzm_/dogfood_test.spy:15:1
    |
 15 | def main():
    | ^
    |


**Feature Focus:** match_union_exhaustive
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
union Status:
    case Pending
    case Running(progress: int)
    case Completed(result: str)

def describe(status: Status) -> str:
    return match status:
        case Status.Pending:
            "waiting"
        case Status.Running:
            f"loading {status.progress}%"
        case Status.Completed:
            f"done: {status.result}"

def main():
    s1: Status = Status.Pending
    s2: Status = Status.Running(50)
    s3: Status = Status.Completed("success")
    print(describe(s1))
    print(describe(s2))
    print(describe(s3))

```

## Timing

- Generation: 397.35s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
