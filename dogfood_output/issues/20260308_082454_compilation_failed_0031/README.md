# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T08:14:05.237427
**Type:** compilation_failed
**Feature Focus:** dotnet_type_usage
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
from system import Console
from system.collections.generic import List as DotNetList

# Define a delegate for callbacks
delegate TypedCallback(value: int) -> str

class DataProcessor:
    _values: list[int]
    
    def __init__(self):
        self._values = []
    
    def add_value(self, val: int) -> None:
        self._values.append(val)
    
    def get_values(self) -> list[int]:
        return self._values
    
    def process_with_callback(self, cb: TypedCallback) -> list[str]:
        result: list[str] = []
        for v in self._values:
            result.append(cb(v))
        return result

def safe_parse_int(s: str) -> int !str:
    try:
        x: int = int(s)
        return Ok(x)
    except ValueError as e:
        return Err(str(e))

def find_value(values: list[int], target: int) -> int?:
    for v in values:
        if v == target:
            return Some(v)
    return None()

def main():
    # Basic .NET Console usage
    Console.WriteLine("Starting .NET type usage demo")
    
    # Working with .NET List
    dn_list: DotNetList[str] = DotNetList[str]()
    dn_list.Add("First")
    dn_list.Add("Second")
    dn_list.Add("Third")
    Console.WriteLine(f".NET List count: {dn_list.get_Count()}")
    
    # Using custom DataProcessor with delegates
    processor: DataProcessor = DataProcessor()
    processor.add_value(10)
    processor.add_value(20)
    processor.add_value(30)
    
    # Custom callback via delegate
    custom_cb: TypedCallback = lambda n: f"Value is {n * 2}"
    processed: list[str] = processor.process_with_callback(custom_cb)
    for item in processed:
        Console.WriteLine(item)
    
    # Result type with methods (not pattern matching)
    parse_result1: int !str = safe_parse_int("42")
    parse_result2: int !str = safe_parse_int("invalid")
    
    # Use Result methods instead of pattern matching
    print(parse_result1.is_ok())
    print(parse_result1.is_err())
    print(parse_result1.unwrap())
    print(parse_result1.unwrap_or(0))
    
    print(parse_result2.is_ok())
    print(parse_result2.is_err())
    print(parse_result2.unwrap_or(-1))
    
    # Optional type with methods (not pattern matching)
    values: list[int] = [5, 10, 15, 20]
    found: int? = find_value(values, 15)
    not_found: int? = find_value(values, 100)
    
    # Use Optional methods instead of pattern matching
    print(found.is_some())
    print(found.is_none())
    print(found.unwrap())
    print(found.unwrap_or(0))
    
    print(not_found.is_some())
    print(not_found.is_none())
    print(not_found.unwrap_or(-1))

```

## Error

```
Assembly compilation failed:

error[CS0305]: Using the generic type 'List<T>' requires 1 type arguments
  --> dogfood_test.cs:9:55
    |
  9 |     
    |     ^
    |

error[CS0234]: The type or namespace name 'DotNetList<>' does not exist in the namespace 'System.Collections.Generic' (are you missing an assembly reference?)
  --> /tmp/tmpsmgu67ui/dogfood_test.spy:43:36
    |
 43 |     dn_list: DotNetList[str] = DotNetList[str]()
    |                                    ^
    |

error[CS0307]: The using alias 'DotNetList' cannot be used with type arguments
  --> /tmp/tmpsmgu67ui/dogfood_test.spy:43:68
    |
 43 |     dn_list: DotNetList[str] = DotNetList[str]()
    |                                                 ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpsmgu67ui/dogfood_test.cs

```

## Timing

- Generation: 627.29s
- Execution: 5.46s
