# Events

> **Implementation status:** Implemented in Phase 12.3. Function-style event accessor bodies have a known limitation with `self` parameter scope resolution (see [#260](https://github.com/antonsynd/sharpy/issues/260)).

Events provide a publish-subscribe mechanism for objects to notify subscribers when something of interest occurs. Sharpy events map directly to .NET events, enabling seamless interop with UI frameworks, reactive patterns, and the broader .NET ecosystem.

Event syntax mirrors the established [property](properties.md) pattern: auto-events for simple cases, and separate `event add`/`event remove` declarations for custom logic.

## Event Forms

Sharpy supports two event forms:

| Form | Use Case | Syntax Pattern |
|------|----------|----------------|
| Auto-event | Simple pub-sub with compiler-generated backing delegate | `event name: DelegateType` |
| Function-style event | Custom add/remove logic, user-provided backing | `event (add\|remove) name(self, handler: T):` |

**Key Distinction:**
- **Auto-events** generate a backing delegate field and `add`/`remove` accessors automatically
- **Function-style events** require the user to provide custom `add` and `remove` accessor bodies

This mirrors the [property](properties.md) distinction between auto-properties and function-style properties.

## Auto-Events

Auto-events generate a backing delegate field and accessors automatically. The event type must be a named delegate type (`EventHandler`, `EventHandler[T]`, `Action[T]`, or a custom delegate):

```python
class Button:
    # Standard .NET event pattern
    event on_click: EventHandler
    event on_hover: EventHandler[MouseEventArgs]

    # Custom delegate type
    event on_data: DataReceivedHandler

    def click(self):
        self.on_click?.invoke(self, EventArgs.empty)
```

**Why delegate types, not function types?**

Events must use named delegate types (not inline function types like `(object, EventArgs) -> None`). Inline function types compile to `Action<T>`/`Func<T>`, which are not interop-compatible with the standard .NET `EventHandler` pattern that all frameworks expect. See [SRP-0003](../rejected_proposals/SRP-0003-events-function-type-syntax.md) for details.

*Implementation: ✅ Native*
```csharp
// event on_click: EventHandler
public event EventHandler? OnClick;

// event on_hover: EventHandler[MouseEventArgs]
public event EventHandler<MouseEventArgs>? OnHover;
```

## Function-Style Events

For events requiring custom logic (validation, logging, weak references), use separate `event add` and `event remove` declarations — mirroring `property get` and `property set`:

```python
class SecureButton:
    _handlers: list[EventHandler] = []

    event add on_click(self, handler: EventHandler):
        if handler not in self._handlers:
            self._handlers.append(handler)

    event remove on_click(self, handler: EventHandler):
        self._handlers.remove(handler)
```

Compare side-by-side with property syntax:

```python
# Property with custom logic
property get name(self) -> str:
    return self._name

property set name(self, value: str):
    self._name = value

# Event with custom logic — same pattern
event add on_click(self, handler: EventHandler):
    self._handlers.append(handler)

event remove on_click(self, handler: EventHandler):
    self._handlers.remove(handler)
```

Both `event add` and `event remove` must be declared together for a given event name.

*Implementation: ✅ Native*
```csharp
private List<EventHandler> _handlers = new();

public event EventHandler OnClick
{
    add { _handlers.Add(value); }
    remove { _handlers.Remove(value); }
}
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

For non-standard signatures, declare a [delegate](delegates.md) and use it as the event type:

```python
delegate DataReceivedHandler(sender: object, data: bytes, timestamp: datetime) -> None

class DataStream:
    event on_data_received: DataReceivedHandler
```

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

### Safe Invocation with `?.invoke()`

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

## Decorators

Events support the same decorators as other class members. Decorator placement follows the same rules as properties — decorators appear before each declaration:

### Virtual Events

```python
class BaseControl:
    @virtual
    event on_paint: EventHandler[PaintEventArgs]
```

### Overriding with Custom Accessors

```python
class CustomControl(BaseControl):
    @override
    event add on_paint(self, handler: EventHandler[PaintEventArgs]):
        print("Custom paint handler added")
        super().on_paint += handler

    @override
    event remove on_paint(self, handler: EventHandler[PaintEventArgs]):
        super().on_paint -= handler
```

### Abstract Events

```python
class BasePublisher:
    @abstract
    event on_update: EventHandler[UpdateEventArgs]
```

### Static Events

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

### Access Modifiers

```python
class SecurePublisher:
    # Public subscribe/unsubscribe (default)
    event on_update: EventHandler

    # Custom accessors with different visibility
    @public
    event add on_internal_change(self, handler: EventHandler):
        self._handlers.append(handler)

    @internal
    event remove on_internal_change(self, handler: EventHandler):
        self._handlers.remove(handler)
```

## Interface Events

Interfaces can declare event requirements:

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
var publisher = new SharpyPublisher();
publisher.OnChange += (s, e) => Console.WriteLine("Changed!");
```

## C# Emission

### Auto-events

```python
# Sharpy
class Publisher:
    event on_change: EventHandler[ChangeEventArgs]
    event on_update: Action[str]

    def notify(self, args: ChangeEventArgs):
        self.on_change?.invoke(self, args)
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
```

### Function-style events

```python
# Sharpy
class CustomPublisher:
    _handlers: list[EventHandler] = []

    event add on_action(self, handler: EventHandler):
        self._handlers.append(handler)

    event remove on_action(self, handler: EventHandler):
        self._handlers.remove(handler)
```

```csharp
// C# 9.0
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

### Event subscription emission

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
| `on_event_name` | `OnEventName` | PascalCase via NameMangler |
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

## Event Syntax Summary

**Auto-events (compiler-generated backing delegate):**

| Syntax | C# Equivalent |
|--------|---------------|
| `event name: EventHandler` | `event EventHandler? Name;` |
| `event name: EventHandler[T]` | `event EventHandler<T>? Name;` |
| `event name: CustomDelegate` | `event CustomDelegate? Name;` |

**Function-style events (user-provided custom accessors):**

| Syntax | C# Equivalent |
|--------|---------------|
| `event add name(self, handler: T):` | `event T Name { add { … } … }` |
| `event remove name(self, handler: T):` | `event T Name { … remove { … } }` |

**Decorator placement:**

```python
@virtual
event on_paint: EventHandler[PaintEventArgs]

@override
event add on_paint(self, handler: EventHandler[PaintEventArgs]):
    super().on_paint += handler

@override
event remove on_paint(self, handler: EventHandler[PaintEventArgs]):
    super().on_paint -= handler

@static
event on_startup: EventHandler

@abstract
event on_update: EventHandler[UpdateEventArgs]
```

*Implementation: ✅ Native*
- *Auto-events → C# field-like events*
- *`event add`/`event remove` → C# event accessor syntax*
- *`+=` / `-=` → direct mapping*
- *Safe invocation `?.invoke()` → `?.Invoke()`*
- *Static events → `static event`*
- *Virtual/abstract/override events → corresponding C# modifiers*

## See Also

- [Delegates](delegates.md) — Named delegate types used as event handler types
- [Properties](properties.md) — Analogous accessor pattern (`property get`/`property set`)
- [Interfaces](interfaces.md) — Interface event declarations
- [Dunder Methods](dunder_methods.md) — Special methods
