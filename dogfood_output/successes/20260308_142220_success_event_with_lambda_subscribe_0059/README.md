# Successful Dogfood Run

**Timestamp:** 2026-03-08T14:13:30.149334
**Feature Focus:** event_with_lambda_subscribe
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Event subscription with lambda expressions
# Verifies that lambdas can subscribe to events and capture values

delegate EventHandler() -> None

class Button:
    event on_click: EventHandler
    label: str
    
    def __init__(self, name: str):
        self.label = name
    
    def click(self) -> None:
        self.on_click?.invoke()

# Counter class to hold state since nested functions aren't allowed
class Counter:
    count: int = 0
    
    def increment(self) -> None:
        self.count = self.count + 1

def main():
    button = Button("Submit")
    counter = Counter()
    
    # Store lambda in typed variable so we can unsubscribe it later
    track_handler: EventHandler = lambda: counter.increment()
    
    # Subscribe handlers
    button.on_click += lambda: print(f"Button '{button.label}' clicked!")
    button.on_click += track_handler
    
    button.click()
    button.click()
    print(f"Count: {counter.count}")
    
    # Unsubscribe the tracking handler
    button.on_click -= track_handler
    button.click()
    print(f"Final: {counter.count}")

```

## Output

```
Button 'Submit' clicked!
Button 'Submit' clicked!
Count: 2
Button 'Submit' clicked!
Final: 2
```

## Timing

- Generation: 513.71s
- Execution: 5.13s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
