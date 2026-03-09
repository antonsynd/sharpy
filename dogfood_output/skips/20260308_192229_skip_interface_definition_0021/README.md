# Skipped Dogfood Run

**Timestamp:** 2026-03-08T19:16:22.547110
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0220]: Cannot pass argument of type 'EventFilter' to parameter of type 'IFilter[str]'
  --> /tmp/tmppvls230o/dogfood_test.spy:98:29
    |
 98 |     result = matches_filter(processor, "ERROR: Connection failed")
    |                             ^^^^^^^^^
    |

error[SPY0220]: Cannot pass argument of type 'EventFilter' to parameter of type 'IFilter[str]'
  --> /tmp/tmppvls230o/dogfood_test.spy:102:29
     |
 102 |     result = matches_filter(processor, "DEBUG: Details")
     |                             ^^^^^^^^^
     |

error[SPY0220]: Cannot pass argument of type 'EventFilter' to parameter of type 'IFilter[str]'
  --> /tmp/tmppvls230o/dogfood_test.spy:124:29
     |
 124 |     result = matches_filter(processor, "WARNING: Low memory")
     |                             ^^^^^^^^^
     |

error[SPY0220]: Cannot pass argument of type 'EventFilter' to parameter of type 'IFilter[str]'
  --> /tmp/tmppvls230o/dogfood_test.spy:132:29
     |
 132 |     result = matches_filter(processor, "ANYTHING")
     |                             ^^^^^^^^^
     |


**Feature Focus:** interface_definition
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Complex interface definitions with inheritance, generics, and multiple implementation
# Demonstrates: interface inheritance (IFilter -> IEventProcessor), generic interfaces (IFilter[T]),
# multiple interface implementation (5+ methods across 3 interfaces), and
# polymorphic dispatch through interface-typed function parameters

# Base generic interface for filtering any type
interface IFilter[T]:
    def accept(self, item: T) -> bool: ...

# Interface extending another interface - IEventProcessor IS-A IFilter[str]
interface IEventProcessor(IFilter[str]):
    def process(self, evt: str) -> int: ...
    property source: str

# Interface with property and stateful operations
interface IConfigurable:
    property name: str
    def reset(self) -> None: ...

# Interface for metrics/statistics tracking
interface IMetrics:
    def get_count(self) -> int: ...
    def increment(self) -> None: ...

# Class implementing multiple interfaces from different hierarchies
class EventFilter(IEventProcessor, IConfigurable, IMetrics):
    _source: str
    _processed: int
    _name: str
    _pattern: str

    def __init__(self, pattern: str, source: str):
        self._pattern = pattern
        self._source = source
        self._processed = 0
        self._name = "EventFilter"

    # IFilter[str] implementation
    def accept(self, item: str) -> bool:
        return self._pattern in item

    # IEventProcessor implementation
    def process(self, evt: str) -> int:
        if self.accept(evt):
            self._processed += 1
            return len(evt)
        return 0

    # IEventProcessor property implementation
    property get source(self) -> str:
        return self._source

    # IConfigurable property implementation
    property get name(self) -> str:
        return self._name

    # IConfigurable method implementation
    def reset(self) -> None:
        self._processed = 0
        self._pattern = ""

    # IMetrics implementations
    def get_count(self) -> int:
        return self._processed

    def increment(self) -> None:
        self._processed += 1

    # Helper for testing different patterns
    def set_pattern(self, pattern: str) -> None:
        self._pattern = pattern

# Polymorphic function accepting interface type
def process_event(processor: IEventProcessor, evt: str) -> int:
    return processor.process(evt)

# Function accepting base interface - tests that EventFilter IS-A IFilter[str]
def matches_filter(filter: IFilter[str], item: str) -> bool:
    return filter.accept(item)

# Function accepting metrics interface
def verify_count(metrics: IMetrics, expected: int) -> bool:
    return metrics.get_count() == expected

def main():
    # Initialize with ERROR pattern
    processor = EventFilter("ERROR", "system_logs")

    # Test: Process matching event returns length
    result = process_event(processor, "ERROR: Disk full")
    print(result)

    # Test: Non-matching event returns 0
    result = process_event(processor, "INFO: System ready")
    print(result)

    # Test: Verify via IFilter[str] base interface
    result = matches_filter(processor, "ERROR: Connection failed")
    print(result)

    # Test: Non-match via IFilter
    result = matches_filter(processor, "DEBUG: Details")
    print(result)

    # Test: Property from IEventProcessor
    print(processor.source)

    # Test: Property from IConfigurable
    print(processor.name)

    # Test: IMetrics count after processing
    print(processor.get_count())

    # Test: Process more events
    process_event(processor, "ERROR: Memory leak")
    process_event(processor, "WARNING: Network slow")

    # Verify count is still 2 (only one more ERROR processed)
    result = verify_count(processor, 2)
    print(result)

    # Test: Change pattern and verify via IFilter
    processor.set_pattern("WARN")
    result = matches_filter(processor, "WARNING: Low memory")
    print(result)

    # Test: Reset clears metrics and pattern
    processor.reset()
    print(processor.get_count())

    # Test: After reset, empty pattern matches everything
    result = matches_filter(processor, "ANYTHING")
    print(result)

```

## Timing

- Generation: 349.67s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
