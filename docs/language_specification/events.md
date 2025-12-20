## Events

Events provide a publish-subscribe pattern:

```python
class Button:
    # Event declaration
    event clicked: (object, EventArgs) -> None

    def click(self):
        if self.clicked is not None:
            self.clicked(self, EventArgs())

# Subscription
button = Button()

def on_clicked(sender: object, args: EventArgs):
    print("Button clicked!")

button.clicked += on_clicked  # Subscribe
button.click()                # Triggers event
button.clicked -= on_clicked  # Unsubscribe
```

**Thread-Safe Event Invocation:**

For thread-safe event invocation that avoids race conditions, use the null-conditional call pattern:

```python
class Button:
    event clicked: (object, EventArgs) -> None

    def click(self):
        # Thread-safe pattern using ?.
        self.clicked?.invoke(self, EventArgs())
```

This maps to C#'s `clicked?.Invoke(...)` pattern, which atomically checks for null and invokes, preventing race conditions where a subscriber unsubscribes between the null check and invocation.

```python
# These are equivalent:

# Explicit null check (not thread-safe)
if self.clicked is not None:
    self.clicked(self, EventArgs())  # Race condition possible here

# Null-conditional invoke (thread-safe)
self.clicked?.invoke(self, EventArgs())  # Atomic check-and-invoke
```

### Custom EventArgs

```python
class ValueChangedArgs(EventArgs):
    old_value: int
    new_value: int

    def __init__(self, old_value: int, new_value: int):
        self.old_value = old_value
        self.new_value = new_value

class Counter:
    event value_changed: (object, ValueChangedArgs) -> None
    _value: int = 0

    property get value(self) -> int:
        return self._value

    property set value(self, new_value: int):
        old = self._value
        self._value = new_value
        if self.value_changed is not None:
            self.value_changed(self, ValueChangedArgs(old, new_value))
```

**Event Rules:**
- Events can only be invoked from the declaring class
- `+=` subscribes, `-=` unsubscribes
- Multiple subscribers are called in subscription order

*Implementation*
- *✅ Native - `event EventHandler Name;`*
