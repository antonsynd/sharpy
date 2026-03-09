# Skipped Dogfood Run

**Timestamp:** 2026-03-08T19:22:29.167533
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0100]: Unexpected token: Newline
  --> /tmp/tmpgi5wkumb/dogfood_test.spy:21:29
    |
 21 |         case TaskState.IDLE:
    |                             ^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmpgi5wkumb/dogfood_test.spy:23:9
    |
 23 |         case TaskState.PROCESSING:
    |         ^^^^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmpgi5wkumb/dogfood_test.spy:25:9
    |
 25 |         case TaskState.FINISHED:
    |         ^^^^
    |

error[SPY0100]: Unexpected token: Dedent
  --> /tmp/tmpgi5wkumb/dogfood_test.spy:28:1
    |
 28 | def main():
    | ^
    |


**Feature Focus:** match_union_exhaustive
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Exhaustive pattern matching on enums
# Using enum with data carried in parallel variables to simulate tagged union

enum TaskState:
    IDLE = 0
    PROCESSING = 1
    FINISHED = 2

class Task:
    state: TaskState
    step: int
    result: str

    def __init__(self):
        self.state = TaskState.IDLE
        self.step = 0
        self.result = ""

def describe(task: Task) -> str:
    return match task.state:
        case TaskState.IDLE:
            "waiting to start"
        case TaskState.PROCESSING:
            f"step {task.step}"
        case TaskState.FINISHED:
            f"completed: {task.result}"

def main():
    t1: Task = Task()
    t1.state = TaskState.IDLE

    t2: Task = Task()
    t2.state = TaskState.PROCESSING
    t2.step = 3

    t3: Task = Task()
    t3.state = TaskState.FINISHED
    t3.result = "ok"

    print(describe(t1))
    print(describe(t2))
    print(describe(t3))

```

## Timing

- Generation: 263.24s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
