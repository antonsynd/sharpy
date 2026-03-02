# SRP-0003: Events with Function Type Syntax

> **Status:** Rejected
> **Superseded by:** [events.md](../language_specification/events.md) (property-aligned delegate-typed events)
> **Primary conflict:** Axiom 1 (.NET) — function types compile to `Action<T>`/`Func<T>`, not `EventHandler`, breaking .NET interop

## Original Proposal

Events declared using inline function type syntax instead of named delegate types:

```python
class Button:
    event clicked: (object, EventArgs) -> None

    def click(self):
        if self.clicked is not None:
            self.clicked(self, EventArgs())

button = Button()
button.clicked += on_clicked
button.clicked -= on_clicked
```

Thread-safe invocation via `?.invoke()`:

```python
class Button:
    event clicked: (object, EventArgs) -> None

    def click(self):
        self.clicked?.invoke(self, EventArgs())
```

Custom EventArgs:

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

## Motivation

- Simple syntax reusing existing function type notation
- Minimal new concepts to learn
- Pythonic feel — looks like a typed field

## Reasons for Rejection

### Axiom 1 (.NET): Incorrect delegate type mapping

Function types `(object, EventArgs) -> None` compile to `Action<object, EventArgs>` or equivalent `Func<…>` types in C#, **not** `EventHandler` or `EventHandler<T>`. The standard .NET event pattern requires `EventHandler`-typed events for:

- Framework interop (WPF, WinForms, ASP.NET all expect `EventHandler<T>`)
- C# consumers subscribing to Sharpy events
- Serialization and reflection-based tooling

Using `Action<…>` instead of `EventHandler` means Sharpy events would be incompatible with the .NET ecosystem without hidden, surprising conversions.

### No support for named delegate types

The proposal provides no path for events using custom delegate types (e.g., `DataReceivedHandler`), which are common in real .NET codebases for non-standard event signatures.

### No custom accessor syntax

The proposal has no mechanism for custom add/remove accessors, which are needed for weak event patterns, logging, validation, and other advanced scenarios.

## See Also

- [SRP-0004](SRP-0004-events-nested-accessor-syntax.md) — Rejected nested accessor syntax
- [Events](../language_specification/events.md) — Accepted property-aligned event design
- [Delegates](../language_specification/delegates.md) — Named delegate types
