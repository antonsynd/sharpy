# SRP-0004: Events with Nested Accessor Syntax

> **Status:** Rejected
> **Superseded by:** [events.md](../language_specification/events.md) (property-aligned delegate-typed events)
> **Primary conflict:** Consistency — nested `def add`/`def remove` blocks are inconsistent with the established property accessor pattern (`property get`/`property set`)

## Original Proposal

A comprehensive event design using delegate-typed events with nested `def add`/`def remove` for custom accessors:

### Field-like events (auto)

```python
class Button:
    event on_click: EventHandler
    event on_hover: EventHandler[MouseEventArgs]
    event on_key_press: Action[str]
```

### Property-like events (custom accessors)

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

### Subscribing/unsubscribing

```python
button.on_click += handle_click
button.on_click -= handle_click
button.on_click += lambda sender, e: print("Clicked!")
```

### Raising events

```python
self.on_click?.invoke(self, EventArgs.empty)
```

### Virtual events with nested override

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

### Access modifiers on nested accessors

```python
event on_internal_change(self) -> EventHandler:
    @public
    def add(handler: EventHandler):
        self._handlers.append(handler)

    @internal
    def remove(handler: EventHandler):
        self._handlers.remove(handler)
```

The proposal also covered static events, interface events, struct events, weak event patterns, observable integration, and C# interop.

## Motivation

- Comprehensive .NET coverage (field-like, custom accessors, virtual, static, interface events)
- Correct delegate type usage (`EventHandler`, `EventHandler[T]`, custom delegates)
- Familiar to C# developers

## What Was Retained

Much of this proposal's content was carried forward into the accepted design:

- Field-like event syntax `event name: DelegateType` (identical)
- `EventHandler` / `EventHandler[T]` / custom delegate types as event types
- `?.invoke()` for thread-safe raising
- `+=` / `-=` for subscribe/unsubscribe
- Virtual, static, abstract, interface event support
- Lambda subscription caveats
- Protected raise method pattern
- Event restrictions (no external invocation, no assignment)
- C# interop semantics

## Reasons for Rejection

### Inconsistency with property accessor pattern

Properties use **separate declarations** with the accessor keyword between `property` and the name:

```python
property get name(self) -> str:
    return self._name

property set name(self, value: str):
    self._name = value
```

The nested accessor syntax uses a fundamentally different structure — a single `event` block containing `def add`/`def remove`:

```python
event on_click(self) -> EventHandler:
    def add(handler: EventHandler):
        ...
    def remove(handler: EventHandler):
        ...
```

In the accepted design, events follow the property pattern with separate `event add`/`event remove` declarations:

```python
event add on_click(self, handler: EventHandler):
    self._handlers.append(handler)

event remove on_click(self, handler: EventHandler):
    self._handlers.remove(handler)
```

This is more consistent with the established grammar and easier to implement in the parser.

### Decorator placement ambiguity

The nested syntax places access modifiers (`@public`, `@internal`) inside the event block on individual `def` statements. In the property pattern, decorators appear before each separate declaration, which is simpler and consistent with how all other decorators work in Sharpy.

## See Also

- [SRP-0003](SRP-0003-events-function-type-syntax.md) — Rejected function type event syntax
- [Events](../language_specification/events.md) — Accepted property-aligned event design
- [Properties](../language_specification/properties.md) — The accessor pattern events now mirror
- [Delegates](../language_specification/delegates.md) — Named delegate types
