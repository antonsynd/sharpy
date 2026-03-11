# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T09:04:08.953409
**Type:** compilation_failed
**Feature Focus:** custom_decorator_on_class
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Custom decorator on class with method chaining
# Verifies custom .NET attributes work on classes and don't affect runtime behavior
# Uses system.obsolete with error flag to test argument passing

@system.obsolete("Use ConfigManager instead", True)
class LegacyConfig:
    name: str
    value: int
    
    def __init__(self, name: str, value: int):
        self.name = name
        self.value = value
    
    def update(self, new_value: int) -> LegacyConfig:
        self.value = new_value
        return self

@system.serializable
class DataPacket:
    payload: str
    
    def __init__(self, payload: str):
        self.payload = payload
    
    def prepend(self, prefix: str) -> str:
        return prefix + self.payload

def main():
    # Test legacy config with method chaining (fluent API pattern)
    cfg = LegacyConfig("test", 100).update(200).update(300)
    print(cfg.name)
    print(cfg.value)
    
    # Test serializable data container
    pkt = DataPacket("hello")
    result = pkt.prepend("GREETING: ")
    print(result)
    
    # Verify final state
    print(pkt.payload)

```

## Error

```
Assembly compilation failed:

error[CS0619]: 'DogfoodTest.LegacyConfig' is obsolete: 'Use ConfigManager instead'
  --> /tmp/tmpy7iem_vf/dogfood_test.spy:30:23
    |
 30 |     cfg = LegacyConfig("test", 100).update(200).update(300)
    |                       ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpy7iem_vf/dogfood_test.cs

```

## Timing

- Generation: 152.42s
- Execution: 4.85s
