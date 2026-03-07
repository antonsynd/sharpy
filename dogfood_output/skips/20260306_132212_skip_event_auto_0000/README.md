# Skipped Dogfood Run

**Timestamp:** 2026-03-06T13:19:56.792881
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0107]: Invalid type annotation target
  --> /tmp/tmpe5s4m_js/dogfood_test.spy:9:19
    |
  9 |         self.value: int = 0
    |                   ^
    |


**Feature Focus:** event_auto
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test auto-event with multiple subscribers using a counter class
delegate EventHandler(sender: object, args: EventArgs)

class EventArgs:
    pass

class Counter:
    def __init__(self) -> None:
        self.value: int = 0

class Button:
    # Auto-event - compiler generates backing field and accessors
    event on_click: EventHandler
    
    def click(self) -> None:
        # Raise event using thread-safe ?.invoke()
        self.on_click?.invoke(self, EventArgs())

def handler1(sender: object, args: EventArgs) -> None:
    print("Handler1 called")

def handler2(sender: object, args: EventArgs) -> None:
    print("Handler2 called")

def main() -> None:
    button: Button = Button()
    
    # Subscribe multiple handlers
    button.on_click += handler1
    button.on_click += handler2
    
    # Trigger event
    print("First click:")
    button.click()
    
    # Unsubscribe one handler
    button.on_click -= handler1
    print("Second click (after unsubscribe):")
    button.click()
    
    # Test with mutable counter object
    button2: Button = Button()
    counter: Counter = Counter()
    
    def increment(sender: object, args: EventArgs) -> None:
        counter.value += 1
    
    button2.on_click += increment
    
    print("Before click:")
    print(counter.value)
    
    button2.click()
    
    print("After click:")
    print(counter.value)

```

## Timing

- Generation: 125.62s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
