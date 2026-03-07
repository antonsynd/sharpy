# Issue Report: compilation_failed

**Timestamp:** 2026-03-06T19:32:41.301147
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating cross-module inheritance and interfaces
from types_module import Priority, Point3D
from utils_module import IntProcessor, create_default_point, get_priority_level, format_processor_info

def main():
    # Create processor with HIGH priority
    processor: IntProcessor = IntProcessor("MyProc", Priority.HIGH, 7)

    # Test virtual method from abstract base class
    print(processor.get_label())

    # Test abstract method implementation
    proc_result: str = processor.process(6)
    print(proc_result)

    # Test IValue interface method
    multiplier_val: int = processor.get_value()
    print(multiplier_val)

    # Test enum name access via utility function
    level_name: str = get_priority_level(processor.priority)
    print(level_name)

    # Test struct magnitude calculation
    p1: Point3D = Point3D(3.0, 4.0, 0.0)
    mag: float = p1.magnitude()
    print(mag)

    # Test default point creation
    p2: Point3D = create_default_point()
    mag2: float = p2.magnitude()
    print(mag2)

    # Test utility function with processor
    info: str = format_processor_info(processor)
    print(info)

    # Count enum values
    count: int = 0
    for _ in Priority:
        count = count + 1
    print(count)

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'TypesModule.Priority' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'TypesModule.Priority' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpd5sslswq/utils_module.spy:46:41


```

## Timing

- Generation: 140.88s
- Execution: 4.49s
