# Skipped Dogfood Run

**Timestamp:** 2026-02-21T04:12:44.746850
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0104]: Expected RightParen, got Dot
  --> /tmp/tmpbmz8_d6f/dogfood_test.spy:60:28
    |
 60 |             case (TaskState.PENDING, TaskState.RUNNING):
    |                            ^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmpbmz8_d6f/dogfood_test.spy:62:13
    |
 62 |             case (TaskState.PENDING, TaskState.CANCELLED):
    |             ^^^^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmpbmz8_d6f/dogfood_test.spy:64:13
    |
 64 |             case (TaskState.RUNNING, TaskState.COMPLETED):
    |             ^^^^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmpbmz8_d6f/dogfood_test.spy:66:13
    |
 66 |             case (TaskState.RUNNING, TaskState.FAILED):
    |             ^^^^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmpbmz8_d6f/dogfood_test.spy:68:13
    |
 68 |             case (TaskState.RUNNING, TaskState.CANCELLED):
    |             ^^^^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmpbmz8_d6f/dogfood_test.spy:70:13
    |
 70 |             case _:
    |             ^^^^
    |

error[SPY0100]: Unexpected token: Dedent
  --> /tmp/tmpbmz8_d6f/dogfood_test.spy:73:1
    |
 73 | class StateLogger(IStateObserver):
    | ^
    |


**Feature Focus:** match_literal
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Complex state machine using enums, inheritance, and generics
# Tests: enums, match statements, inheritance, generic container
# State transitions, properties, and simple pattern matching
enum TaskState:
    PENDING = 0
    RUNNING = 1
    COMPLETED = 2
    FAILED = 3
    CANCELLED = 4

interface IStateObserver:
    def on_state_change(previous: TaskState, current: TaskState) -> None

type StateTransition = tuple[from_state: TaskState, to_state: TaskState]

class BaseTask:
    name: str
    state: TaskState
    _priority: int

    def __init__(self, name: str):
        self.name = name
        self.state = TaskState.PENDING
        self._priority = 0

    property get priority(self) -> int:
        return self._priority

class PriorityTask(BaseTask):
    def __init__(self, name: str, priority_val: int):
        super().__init__(name)
        self._priority = priority_val

    property get priority(self) -> int:
        return self._priority

class TaskScheduler:
    observers: list[IStateObserver]

    def __init__(self):
        self.observers = []

    def add_observer(self, obs: IStateObserver) -> None:
        self.observers.append(obs)

    def can_transition(self, from_s: TaskState, to_s: TaskState) -> bool:
        # Use if/else chains instead of nested match with enum patterns
        # Valid transitions: PENDING->RUNNING/CANCELLED, RUNNING->COMPLETED/FAILED/CANCELLED
        if from_s == TaskState.PENDING:
            return to_s == TaskState.RUNNING or to_s == TaskState.CANCELLED
        elif from_s == TaskState.RUNNING:
            return to_s == TaskState.COMPLETED or to_s == TaskState.FAILED or to_s == TaskState.CANCELLED
        else:
            return False

    def describe_transition(self, from_s: TaskState, to_s: TaskState) -> str:
        # Use flat match with tuple patterns instead of nested match
        transition: tuple[TaskState, TaskState] = (from_s, to_s)
        match transition:
            case (TaskState.PENDING, TaskState.RUNNING):
                return "starting"
            case (TaskState.PENDING, TaskState.CANCELLED):
                return "abandoning"
            case (TaskState.RUNNING, TaskState.COMPLETED):
                return "finishing"
            case (TaskState.RUNNING, TaskState.FAILED):
                return "failing"
            case (TaskState.RUNNING, TaskState.CANCELLED):
                return "cancelling"
            case _:
                return "unknown"

class StateLogger(IStateObserver):
    transition_count: int

    def __init__(self):
        self.transition_count = 0

    def on_state_change(self, previous: TaskState, current: TaskState) -> None:
        self.transition_count += 1
        # Use if/else instead of match on enum values
        if current == TaskState.COMPLETED:
            print(f"Task completed (transition #{self.transition_count})")
        elif current == TaskState.FAILED:
            print(f"Task failed (transition #{self.transition_count})")
        elif current == TaskState.CANCELLED:
            print(f"Task cancelled (transition #{self.transition_count})")

def run_task_lifecycle(scheduler: TaskScheduler, task: BaseTask, logger: StateLogger) -> None:
    states_to_simulate: list[TaskState] = [TaskState.PENDING, TaskState.RUNNING, TaskState.COMPLETED]
    prev: TaskState = task.state
    for next_state in states_to_simulate[1:]:
        if scheduler.can_transition(prev, next_state):
            desc: str = scheduler.describe_transition(prev, next_state)
            print(f"Transition: {desc}")
            task.state = next_state
            logger.on_state_change(prev, next_state)
            prev = next_state

def main():
    scheduler: TaskScheduler = TaskScheduler()
    logger: StateLogger = StateLogger()
    scheduler.add_observer(logger)

    # Test with priority task
    test_task: PriorityTask = PriorityTask("important-job", 5)
    print(f"Task: {test_task.name}")
    print(f"Priority: {test_task.priority}")

    # Run the task lifecycle
    test_task.state = TaskState.PENDING
    run_task_lifecycle(scheduler, test_task, logger)

    # Test invalid transition
    from_s: TaskState = TaskState.COMPLETED
    to_s: TaskState = TaskState.RUNNING
    can_go: bool = scheduler.can_transition(from_s, to_s)
    if can_go:
        print("Can transition")
    else:
        print("Cannot transition: completed->running")

    # Final state
    if test_task.state == TaskState.COMPLETED:
        print("Final: success")

# EXPECTED OUTPUT:
# Task: important-job
# Priority: 5
# Transition: starting
# Transition: finishing
# Task completed (transition #1)
# Cannot transition: completed->running
# Final: success
```

## Timing

- Generation: 508.18s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.18).

This output is saved for inspection to help improve prompting.
