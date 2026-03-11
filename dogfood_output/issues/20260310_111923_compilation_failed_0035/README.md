# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T11:16:01.886553
**Type:** compilation_failed
**Feature Focus:** custom_decorator_dotted
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test dotted custom decorators with various .NET attributes
# Simple attribute on class
@system.serializable
class Configuration:
    name: str
    version: int
    
    def __init__(self, name: str, version: int):
        self.name = name
        self.version = version
    
    def get_display_name(self) -> str:
        return f"{self.name} v{self.version}"

# Attribute with message argument on function
@system.obsolete("Use calculate_area() instead")
def get_area_legacy(width: int, height: int) -> int:
    return width * height

def calculate_area(width: int, height: int) -> int:
    return width * height

# Attribute with error=True on class
@system.obsolete("Use ServiceV2 instead", error=True)
class LegacyService:
    pass

def process_config(config: Configuration) -> str:
    return config.get_display_name()

def main():
    config = Configuration("MyApp", 2)
    print(config.name)
    print(config.version)
    print(config.get_display_name())
    
    # Call the obsolete function
    result: int = get_area_legacy(5, 3)
    print(result)
    
    print("done")

```

## Error

```
Assembly compilation failed:

error[CS0246]: The type or namespace name 'Error' could not be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp5j5z1cf8/dogfood_test.spy:24:47
    |
 24 | @system.obsolete("Use ServiceV2 instead", error=True)
    |                                               ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmp5j5z1cf8/dogfood_test.cs

```

## Timing

- Generation: 181.59s
- Execution: 4.67s
