# Issue Report: execution_failed

**Timestamp:** 2026-01-27T00:38:32.002141
**Type:** execution_failed
**Feature Focus:** access_modifiers
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test access modifiers with a simple configuration class
# Tests: @private field, @protected method, public method (default), method access patterns

class Config:
    @private
    name: str
    
    def __init__(self, name: str):
        self.name = name
    
    @protected
    def get_internal_name(self) -> str:
        return self.name
    
    def get_display_name(self) -> str:
        # Public method can access private field
        return self.name

def main():
    cfg = Config("AppConfig")
    print(cfg.get_display_name())

# EXPECTED OUTPUT:
# AppConfig
```

## Error

```
Compilation failed:
  Compilation failed: Parser error at line 6, column 5: Decorators can only be applied to functions, classes, or structs

```

## Timing

- Generation: 5.40s
- Execution: 0.87s
