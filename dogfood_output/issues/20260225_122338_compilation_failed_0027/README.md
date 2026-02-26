# Issue Report: compilation_failed

**Timestamp:** 2026-02-25T12:16:23.829190
**Type:** compilation_failed
**Feature Focus:** null_conditional
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Null conditional operator with optional chaining
# Tests: Safe navigation through nullable properties, method calls, null coalescing

class Logger:
    prefix: str
    
    def __init__(self, p: str):
        self.prefix = p
    
    def format(self, msg: str) -> str:
        return f"[{self.prefix}] {msg}"

class Module:
    name: str
    logger: Logger?
    
    def __init__(self, n: str):
        self.name = n

def format_log(mod: Module?, message: str) -> str:
    # Chain: module -> logger -> format(), with fallback
    return mod?.logger?.format(message) ?? f"[{mod?.name ?? 'unknown'}] {message}"

def main():
    core = Module("core")
    http = Module("http")
    http.logger = Some(Logger("HTTP"))
    
    print(core.logger?.prefix ?? "none")
    
    print(format_log(core, "init"))
    print(format_log(http, "request"))
    
    print(format_log(None(), "test"))
    
    print(http.logger?.format("done") ?? "fail")
    print(core.logger?.format("skip") ?? "nolog")

# EXPECTED OUTPUT:
# none
# [core] init
# [HTTP] request
# [unknown] test
# [HTTP] done
# nolog
```

## Error

```
Assembly compilation failed:

error[CS0173]: Type of conditional expression cannot be determined because there is no implicit conversion between 'Sharpy.Optional<DogfoodTest.Logger>' and 'Sharpy.Optional<string>'
  --> /tmp/tmpi5ekcdb_/dogfood_test.spy:22:17
    |
 22 |     return mod?.logger?.format(message) ?? f"[{mod?.name ?? 'unknown'}] {message}"
    |                 ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpi5ekcdb_/dogfood_test.cs

```

## Timing

- Generation: 422.47s
- Execution: 4.13s
