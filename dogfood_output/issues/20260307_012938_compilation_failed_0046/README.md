# Issue Report: compilation_failed

**Timestamp:** 2026-03-07T01:27:34.813614
**Type:** compilation_failed
**Feature Focus:** custom_decorator_keyword_args
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Custom decorator with multiple keyword arguments of diverse types
# Tests combining positional args with multiple named args (enum, bool, type)
# Verifies proper code generation for named parameter assignments in attributes

enum LogLevel:
    DEBUG = 0
    INFO = 1
    WARN = 2
    ERROR = 3

class ApiClient:
    endpoint: str

    @system.component_model.description("Legacy API client", category="Network", type_id=type(str))
    def __init__(self, endpoint: str):
        self.endpoint = endpoint

@system.component_model.default_property("Name")
@system.diagnostics.conditional("DEBUG")
class ConfigSection:
    name: str
    priority: int = 100

    def __init__(self, name: str):
        self.name = name

    @system.obsolete("Use get_config_v2 instead", error=True)
    def get_config(self) -> str:
        return self.name

def main():
    client = ApiClient("https://api.example.com")
    print(client.endpoint)

    cfg = ConfigSection("app-settings")
    print(cfg.name)
    print(cfg.priority)

```

## Error

```
Assembly compilation failed:

error[CS0234]: The type or namespace name 'DescriptionAttribute' does not exist in the namespace 'System.ComponentModel' (are you missing an assembly reference?)
  --> dogfood_test.cs:22:32
    |
 22 |     priority: int = 100
    |                        ^
    |

error[CS0234]: The type or namespace name 'DefaultPropertyAttribute' does not exist in the namespace 'System.ComponentModel' (are you missing an assembly reference?)
  --> /tmp/tmpolrpkk52/dogfood_test.spy:20:28
    |
 20 | class ConfigSection:
    |                     ^
    |

error[CS1689]: Attribute 'System.Diagnostics.Conditional' is only valid on methods or attribute classes
  --> /tmp/tmpolrpkk52/dogfood_test.spy:21:6
    |
 21 |     name: str
    |      ^
    |

error[CS0246]: The type or namespace name 'Error' could not be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpolrpkk52/dogfood_test.spy:26:55
    |
 26 | 
    | ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpolrpkk52/dogfood_test.cs

```

## Timing

- Generation: 110.17s
- Execution: 4.17s
