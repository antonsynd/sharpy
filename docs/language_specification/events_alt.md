# Events

> **Implementation status:** Not yet implemented — planned for Phase 12 (v0.2.6). This is an alternate design document; see also [events.md](events.md).

Events provide a mechanism for objects to notify subscribers when something of interest occurs. Sharpy events map directly to .NET events, enabling seamless interop with UI frameworks, reactive patterns, and the broader .NET ecosystem.

## Basic Syntax

```python
class Publisher:
    # Field-like event declaration
    event on_change: EventHandler[ChangeEventArgs]

    def notify(self):
        # Raise the event (if subscribers exist)
        self.on_change?.invoke(self, ChangeEventArgs())
```

## Event Declaration Forms

### Field-Like Events (Simple)

The most common form — compiler generates backing delegate and accessors:

```python
class Button:
    event on_click: EventHandler
    event on_hover: EventHandler[MouseEventArgs]
    event on_key_press: Action[str]
```

### Property-Like Events (Custom Logic)

For validation, logging, or custom subscription management:

```python
class Button:
    _click_handlers: list[EventHandler] = []

    event on_click(self) -> EventHandler:
        def add(handler: EventHandler):
            if handler not in self._click_handlers:
                self._click_handlers.append(handler)
        def remove(handler: EventHandler):
            self._click_handlers.remove(handler)
```

## Event Handler Types

### Standard EventHandler

For events without custom data:

```python
from system import EventHandler

class Timer:
    event on_tick: EventHandler

    def tick(self):
        self.on_tick?.invoke(self, EventArgs.empty)
```

### Generic EventHandler[TEventArgs]

For events with custom data:

```python
from system import EventHandler, EventArgs

class ProgressEventArgs(EventArgs):
    property percent: int
    property message: str

    def __init__(self, percent: int, message: str):
        self.percent = percent
        self.message = message

class Downloader:
    event on_progress: EventHandler[ProgressEventArgs]

    def report_progress(self, percent: int, msg: str):
        self.on_progress?.invoke(self, ProgressEventArgs(percent, msg))
```

### Custom Delegate Types

For non-standard signatures:

```python
delegate DataReceivedHandler(sender: object, data: bytes, timestamp: datetime) -> None

class DataStream:
    event on_data_received: DataReceivedHandler
```

### Function Type Syntax

Events can also be declared using inline function type syntax:

```python
class Button:
    # Using function type instead of named delegate
    event on_click: (object, EventArgs) -> None

    def click(self):
        self.on_click?.invoke(self, EventArgs())

## Subscribing to Events

### Using `+=` Operator

```python
def handle_click(sender: object, e: EventArgs):
    print("Button clicked!")

button = Button()
button.on_click += handle_click
```

### Using Lambda Expressions

```python
button.on_click += lambda sender, e: print("Clicked!")
```

### Using Method References

```python
class ClickCounter:
    _count: int = 0

    def handle_click(self, sender: object, e: EventArgs):
        self._count += 1
        print(f"Click count: {self._count}")

counter = ClickCounter()
button.on_click += counter.handle_click
```

## Unsubscribing from Events

Use `-=` to remove a handler:

```python
button.on_click -= handle_click
```

**Important:** Lambda expressions cannot be unsubscribed (no reference equality):

```python
# ❌ This won't work — different lambda instance
button.on_click += lambda s, e: print("A")
button.on_click -= lambda s, e: print("A")  # Does nothing!

# ✅ Store reference to unsubscribe later
handler = lambda s, e: print("A")
button.on_click += handler
button.on_click -= handler  # Works
```

## Raising Events

### Safe Invocation with `?.`

Always use null-conditional to handle the case of no subscribers:

```python
class Publisher:
    event on_change: EventHandler[ChangeEventArgs]

    def notify_change(self, change: ChangeEventArgs):
        # Safe: does nothing if no subscribers
        self.on_change?.invoke(self, change)
```

**Why `?.invoke()` is thread-safe:**

The null-conditional invoke pattern maps to C#'s `?.Invoke()`, which atomically checks for null and invokes. This prevents race conditions where a subscriber could unsubscribe between a null check and the invocation:

```python
# ❌ Not thread-safe — explicit null check
if self.on_change is not None:
    self.on_change(self, args)  # Race condition: subscriber could unsubscribe here!

# ✅ Thread-safe — null-conditional invoke (atomic check-and-invoke)
self.on_change?.invoke(self, args)
```

### Protected Raise Methods (Pattern)

For derived classes to raise events:

```python
class Control:
    event on_click: EventHandler

    @protected
    def raise_click(self, e: EventArgs):
        """Allow derived classes to raise the click event."""
        self.on_click?.invoke(self, e)

class Button(Control):
    def do_click(self):
        self.raise_click(EventArgs.empty)
```

## Event Accessors

### Access Modifiers

```python
class SecurePublisher:
    # Public subscribe, but only class can raise
    event on_update: EventHandler

    # Explicit accessor visibility
    event on_internal_change(self) -> EventHandler:
        @public
        def add(handler: EventHandler):
            self._handlers.append(handler)

        @internal
        def remove(handler: EventHandler):
            self._handlers.remove(handler)
```

### Virtual Events

```python
class BaseControl:
    @virtual
    event on_paint: EventHandler[PaintEventArgs]

class CustomControl(BaseControl):
    @override
    event on_paint(self) -> EventHandler[PaintEventArgs]:
        def add(handler: EventHandler[PaintEventArgs]):
            print("Custom paint handler added")
            super().on_paint += handler
        def remove(handler: EventHandler[PaintEventArgs]):
            super().on_paint -= handler
```

## Static Events

For class-level notifications:

```python
class Application:
    @static
    event on_startup: EventHandler

    @static
    event on_shutdown: EventHandler

    @staticmethod
    def start():
        Application.on_startup?.invoke(None, EventArgs.empty)
```

## Interface Events

Interfaces can declare events:

```python
interface INotifyPropertyChanged:
    event property_changed: EventHandler[PropertyChangedEventArgs]

class ObservableObject(INotifyPropertyChanged):
    event property_changed: EventHandler[PropertyChangedEventArgs]

    def set_property[T](self, field: ref[T], value: T, name: str):
        if field != value:
            field = value
            self.property_changed?.invoke(
                self,
                PropertyChangedEventArgs(name)
            )
```

## Events in Structs

Structs can have events, but with limitations:

```python
struct Counter:
    event on_increment: EventHandler
    _value: int

    def increment(self: ref[Counter]):  # Must be ref for mutation
        self._value += 1
        self.on_increment?.invoke(self, EventArgs.empty)
```

**Warning:** Value-type semantics mean subscribers might not see updates if the struct is copied.

## Event Patterns

### Weak Event Pattern

Prevent memory leaks with weak references:

```python
class WeakEventSource[TEventArgs]:
    _handlers: list[WeakReference[EventHandler[TEventArgs]]] = []

    def add(self, handler: EventHandler[TEventArgs]):
        self._handlers.append(WeakReference(handler))

    def remove(self, handler: EventHandler[TEventArgs]):
        self._handlers = [
            h for h in self._handlers
            if h.is_alive and h.target != handler
        ]

    def invoke(self, sender: object, args: TEventArgs):
        for weak_ref in self._handlers:
            if weak_ref.is_alive:
                weak_ref.target(sender, args)
```

### Observable Pattern Integration

Events work alongside observables:

```python
from system.reactive import Observable

class DataService:
    event on_data_changed: EventHandler[DataChangedEventArgs]

    @property
    def data_changed_observable(self) -> Observable[DataChangedEventArgs]:
        return Observable.from_event(
            lambda h: self.on_data_changed += h,
            lambda h: self.on_data_changed -= h
        )
```

## C# Interop

### Subscribing to C# Events

```python
from system.windows.forms import Button as WinButton

button = WinButton()
button.click += lambda s, e: print("WinForms button clicked!")
```

### Exposing Events to C#

Sharpy events are fully compatible with C# event consumers:

```csharp
// C# code consuming Sharpy class
var publisher = new SharppyPublisher();
publisher.OnChange += (s, e) => Console.WriteLine("Changed!");
```

## C# Emission

```python
# Sharpy
class Publisher:
    event on_change: EventHandler[ChangeEventArgs]
    event on_update: Action[str]

    def notify(self, args: ChangeEventArgs):
        self.on_change?.invoke(self, args)

# Custom accessor
class CustomPublisher:
    _handlers: list[EventHandler] = []

    event on_action(self) -> EventHandler:
        def add(handler: EventHandler):
            self._handlers.append(handler)
        def remove(handler: EventHandler):
            self._handlers.remove(handler)
```

```csharp
// C# 9.0
public class Publisher
{
    public event EventHandler<ChangeEventArgs>? OnChange;
    public event Action<string>? OnUpdate;

    public void Notify(ChangeEventArgs args)
    {
        OnChange?.Invoke(this, args);
    }
}

public class CustomPublisher
{
    private List<EventHandler> _handlers = new();

    public event EventHandler OnAction
    {
        add { _handlers.Add(value); }
        remove { _handlers.Remove(value); }
    }
}
```

**Event subscription emission:**

```python
# Sharpy
obj.on_change += my_handler
obj.on_change -= my_handler
```

```csharp
// C# 9.0
obj.OnChange += myHandler;
obj.OnChange -= myHandler;
```

## Naming Conventions

| Sharpy Convention | C# Emission | Notes |
|-------------------|-------------|-------|
| `on_event_name` | `OnEventName` | PascalCase in C# |
| `event_name_changed` | `EventNameChanged` | Common for property changes |
| `before_event` | `BeforeEvent` | Pre-event notification |
| `after_event` | `AfterEvent` | Post-event notification |

## Restrictions

1. **Events can only be raised from within the declaring class** (or derived via protected method)
2. **Cannot assign to events** — only `+=` and `-=` are allowed from outside
3. **Cannot invoke events directly from outside** — must use raise method
4. **Event handlers should not throw exceptions** — unhandled exceptions propagate to caller

```python
class Foo:
    event on_bar: EventHandler

foo = Foo()

# ✅ Valid: subscribe/unsubscribe
foo.on_bar += handler
foo.on_bar -= handler

# ❌ Invalid: direct assignment
foo.on_bar = handler  # ERROR: Cannot assign to event

# ❌ Invalid: invoke from outside
foo.on_bar.invoke(foo, args)  # ERROR: Cannot invoke event from outside class
```

*Implementation: ✅ Native*
- *Field-like events → C# field-like events*
- *Custom accessors → C# event accessor syntax*
- *`+=` / `-=` → direct mapping*
- *Safe invocation `?.invoke()` → `?.Invoke()`*
- *Static events → `static event`*
- *Virtual events → `virtual event`*

## See Also

- [Delegates](delegates.md) — Named delegate types
- [Properties](properties.md) — Similar accessor syntax
- [Interfaces](interfaces.md) — Interface event declarations
- [Dunder Methods](dunder_methods.md) — Special methods
