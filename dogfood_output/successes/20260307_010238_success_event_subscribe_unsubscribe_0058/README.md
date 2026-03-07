# Successful Dogfood Run

**Timestamp:** 2026-03-07T00:57:55.243985
**Feature Focus:** event_subscribe_unsubscribe
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
delegate TaskHandler(task_id: int, result: str)

class TaskRunner:
    event on_finish: TaskHandler
    _next_id: int

    def __init__(self):
        self._next_id = 0

    def execute(self, task_name: str) -> None:
        self._next_id += 1
        print(f"EXEC:{task_name}")
        # Simulate different completion results
        if self._next_id == 1:
            self.on_finish?.invoke(self._next_id, "ok")
        elif self._next_id == 2:
            self.on_finish?.invoke(self._next_id, "done")
        else:
            self.on_finish?.invoke(self._next_id, "finished")

def tracer_alpha(tid: int, res: str) -> None:
    print(f"[ALPHA] task={tid} res={res}")

def tracer_beta(tid: int, res: str) -> None:
    print(f"[BETA] task={tid} res={res}")

def main():
    runner = TaskRunner()

    # Start with one subscriber
    runner.on_finish += tracer_alpha
    runner.execute("job1")

    # Add second subscriber
    runner.on_finish += tracer_beta
    runner.execute("job2")

    # Remove first, keep second - classic swap pattern
    runner.on_finish -= tracer_alpha
    runner.execute("job3")

    # Remove all subscribers
    runner.on_finish -= tracer_beta
    runner.execute("job4")

    # Re-add first for a final task
    runner.on_finish += tracer_alpha
    runner.execute("job5")

```

## Output

```
EXEC:job1
[ALPHA] task=1 res=ok
EXEC:job2
[ALPHA] task=2 res=done
[BETA] task=2 res=done
EXEC:job3
[BETA] task=3 res=finished
EXEC:job4
EXEC:job5
[ALPHA] task=5 res=finished
```

## Timing

- Generation: 267.68s
- Execution: 4.84s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
