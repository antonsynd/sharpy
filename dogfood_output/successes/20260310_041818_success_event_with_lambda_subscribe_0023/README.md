# Successful Dogfood Run

**Timestamp:** 2026-03-10T04:16:56.683218
**Feature Focus:** event_with_lambda_subscribe
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Define a delegate type for click events
delegate ClickHandler() -> None

class Button:
    event on_click: ClickHandler
    
    def click(self) -> None:
        self.on_click?.invoke()

def main():
    b = Button()
    # Subscribe multiple lambdas to the event
    b.on_click += lambda: print("Clicked!")
    b.on_click += lambda: print("Also clicked!")
    # Raise the event
    b.click()

```

## Output

```
Clicked!
Also clicked!
```

## Timing

- Generation: 64.49s
- Execution: 5.05s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
