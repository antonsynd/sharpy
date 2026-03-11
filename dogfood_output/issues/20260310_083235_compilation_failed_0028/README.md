# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T08:30:54.901863
**Type:** compilation_failed
**Feature Focus:** custom_decorator_on_class
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test custom decorators on classes with various argument types
# Comprehensive test of compile-time attribute generation

@system.serializable
class ConfigData:
    name: str
    version: int
    
    def __init__(self, name: str, version: int):
        self.name = name
        self.version = version

@obsolete("Use EnhancedSettings instead")
class DeprecatedSettings:
    value: int
    
    def __init__(self, v: int):
        self.value = v

@system.diagnostics.conditional("DEBUG")
class DebugHelper:
    message: str
    
    def __init__(self, msg: str):
        self.message = msg

class EnhancedSettings:
    config: ConfigData
    debug_mode: bool
    
    def __init__(self, cfg: ConfigData, debug: bool = False):
        self.config = cfg
        self.debug_mode = debug
    
    def describe(self) -> str:
        if self.debug_mode:
            return f"{self.config.name} (v{self.config.version}) - DEBUG"
        return f"{self.config.name} (v{self.config.version})"

def main():
    # Create instances and verify basic functionality
    cfg = ConfigData("TestApp", 2)
    settings = EnhancedSettings(cfg, True)
    
    # Test inheritance pattern with decorated parent
    deprecated = DeprecatedSettings(100)
    
    # Control flow: if with walrus in condition
    if (s := settings.describe()) == "TestApp (v2) - DEBUG":
        print("Debug mode active")
    else:
        print("Release mode")
    
    print(cfg.name)
    print(cfg.version)
    
    # Loop with pattern
    items: list[int] = [deprecated.value, cfg.version * 2, 42]
    total = 0
    for i, val in enumerate(items):
        total += val
        print(f"item {i}: {val}")
    
    print(total)

```

## Error

```
Assembly compilation failed:

error[CS1689]: Attribute 'System.Diagnostics.Conditional' is only valid on methods or attribute classes
  --> /tmp/tmpgx83r9wn/dogfood_test.spy:22:6
    |
 22 |     message: str
    |      ^
    |


```

## Compiler Output

```
warning[SPY0451]: Local variable 's' is assigned but never used
  --> /tmp/tmpgx83r9wn/dogfood_test.spy:49:9
    |
 49 |     if (s := settings.describe()) == "TestApp (v2) - DEBUG":
    |         ^^^^^^^^^^^^^^^^^^^^^^^^
    |


```

## Generated C#

```csharp
warning[SPY0451]: Local variable 's' is assigned but never used
  --> /tmp/tmpgx83r9wn/dogfood_test.spy:49:9
    |
 49 |     if (s := settings.describe()) == "TestApp (v2) - DEBUG":
    |         ^^^^^^^^^^^^^^^^^^^^^^^^
    |

Generated C# code written to: /tmp/tmpgx83r9wn/dogfood_test.cs

```

## Timing

- Generation: 85.99s
- Execution: 4.76s
