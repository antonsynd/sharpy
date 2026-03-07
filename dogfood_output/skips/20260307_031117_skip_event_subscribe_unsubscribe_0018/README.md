# Skipped Dogfood Run

**Timestamp:** 2026-03-07T02:59:25.029582
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

Type errors:
error[SPY0203]: Type '' has no member '_on_status_change'
  --> /tmp/tmpm87lvb5d/dogfood_test.spy:22:9
    |
 22 |         self._on_status_change?.invoke(self._id, old, self._status)
    |         ^^^^^^^^^^^^^^^^^^^^^^
    |

error[SPY0203]: Type '' has no member '_on_status_change'
  --> /tmp/tmpm87lvb5d/dogfood_test.spy:27:9
    |
 27 |         self._on_status_change?.invoke(self._id, old, self._status)
    |         ^^^^^^^^^^^^^^^^^^^^^^
    |

Validation errors:
error[SPY0283]: Cannot access protected member '_log' of 'SystemMonitor' from outside the class hierarchy
  --> /tmp/tmpm87lvb5d/dogfood_test.spy:82:18
    |
 82 |     for entry in monitor._log:
    |                  ^^^^^^^^^^^^
    |

error[SPY0283]: Cannot access protected member '_log' of 'SystemMonitor' from outside the class hierarchy
  --> /tmp/tmpm87lvb5d/dogfood_test.spy:85:5
    |
 85 |     monitor._log.clear()
    |     ^^^^^^^^^^^^
    |

error[SPY0283]: Cannot access protected member '_log' of 'SystemMonitor' from outside the class hierarchy
  --> /tmp/tmpm87lvb5d/dogfood_test.spy:95:18
    |
 95 |     for entry in monitor._log:
    |                  ^^^^^^^^^^^^
    |


**Feature Focus:** event_subscribe_unsubscribe
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Complex event subscription test with custom delegates and inheritance
delegate StatusChangeHandler(component_id: int, old_status: str, new_status: str) -> None

@abstract
class Component:
    _id: int
    _status: str

    property get id(self) -> int:
        return self._id

    property get status(self) -> str:
        return self._status

    def __init__(self, component_id: int):
        self._id = component_id
        self._status = "initialized"

    def activate(self) -> None:
        old = self._status
        self._status = "active"
        self._on_status_change?.invoke(self._id, old, self._status)

    def deactivate(self) -> None:
        old = self._status
        self._status = "inactive"
        self._on_status_change?.invoke(self._id, old, self._status)

    @virtual
    event on_status_change: StatusChangeHandler

class MonitoredComponent(Component):
    priority: int

    def __init__(self, component_id: int, priority: int):
        super().__init__(component_id)
        self.priority = priority

class SystemMonitor:
    _active_handlers: dict[int, StatusChangeHandler]
    _log: list[str]

    def __init__(self):
        self._active_handlers = {}
        self._log = []

    def create_handler(self, name: str) -> StatusChangeHandler:
        return lambda cid, old, new: self._log.append(f"[{name}] {cid}: {old}->{new}")

    def monitor_high_priority(self, components: list[MonitoredComponent]) -> None:
        for comp in components:
            if comp.priority >= 5:
                handler = self.create_handler("HIGH")
                self._active_handlers[comp.id] = handler
                comp.on_status_change += handler

    def unmonitor_by_id(self, component_id: int, component: MonitoredComponent) -> None:
        if component_id in self._active_handlers:
            component.on_status_change -= self._active_handlers[component_id]
            self._active_handlers.pop(component_id)

def main():
    monitor = SystemMonitor()
    components: list[MonitoredComponent] = []

    # Create 4 components with varying priorities
    components.append(MonitoredComponent(1, 2))
    components.append(MonitoredComponent(2, 7))
    components.append(MonitoredComponent(3, 5))
    components.append(MonitoredComponent(4, 3))

    # Subscribe only to high priority components (>= 5)
    monitor.monitor_high_priority(components)
    print(f"Subscribed: {len(monitor._active_handlers)}")

    # Activate all components
    for c in components:
        c.activate()

    # Print event log
    print(f"Events: {len(monitor._log)}")
    for entry in monitor._log:
        print(entry)

    monitor._log.clear()

    # Unsubscribe from component 3 and deactivate all
    monitor.unmonitor_by_id(3, components[2])
    print("Unsubscribed: 3")

    for c in components:
        c.deactivate()

    print(f"Events: {len(monitor._log)}")
    for entry in monitor._log:
        print(entry)

```

## Timing

- Generation: 697.31s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
