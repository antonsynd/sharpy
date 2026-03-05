# Successful Dogfood Run

**Timestamp:** 2026-03-04T15:27:03.323774
**Feature Focus:** event_with_lambda_subscribe
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test event with lambda subscription
delegate IntTransformer(x: int) -> int

class EventSource:
    event on_transform: IntTransformer

    def trigger(self, value: int) -> None:
        handler = self.on_transform
        if handler is not None:
            result = handler.invoke(value)
            print(result)

def main():
    source = EventSource()
    source.on_transform += lambda x: x * x
    source.trigger(5)

```

## Output

```
25
```

## Timing

- Generation: 74.95s
- Execution: 4.98s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
