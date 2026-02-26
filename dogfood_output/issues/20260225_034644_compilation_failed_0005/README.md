# Issue Report: compilation_failed

**Timestamp:** 2026-02-25T03:40:07.486797
**Type:** compilation_failed
**Feature Focus:** property_inheritance
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Property inheritance with virtual/override and polymorphic dispatch
class Temperature:
    _celsius: float
    
    def __init__(self, c: float):
        self._celsius = c
    
    @virtual
    property get display(self) -> str:
        return f"{self._celsius}C"

class FahrenheitTemperature(Temperature):
    def __init__(self, f: float):
        c: float = (f - 32.0) * 5.0 / 9.0
        super().__init__(c)
    
    @override
    property get display(self) -> str:
        f: float = self._celsius * 9.0 / 5.0 + 32.0
        return f"{f:.1f}F"

class KelvinTemperature(Temperature):
    def __init__(self, k: float):
        c: float = k - 273.15
        super().__init__(c)

def main():
    temp_c: Temperature = Temperature(25.0)
    temp_f: FahrenheitTemperature = FahrenheitTemperature(77.0)
    temp_k: KelvinTemperature = KelvinTemperature(298.15)
    
    print(temp_c.display)
    print(temp_f.display)
    print(temp_k.display)
    
    poly_temps: list[Temperature] = [temp_c, temp_f, temp_k]
    for t in poly_temps:
        print(t.display)

# EXPECTED OUTPUT:
# 25.0C
# 77.0F
# 25.0C
# 25.0C
# 77.0F
# 25.0C
```

## Error

```
Assembly compilation failed:

error[CS0103]: The name 'c' does not exist in the current context
  --> /tmp/tmpz_v0h250/dogfood_test.spy:20:51
    |
 20 |         return f"{f:.1f}F"
    |                           ^
    |

error[CS0103]: The name 'c' does not exist in the current context
  --> /tmp/tmpz_v0h250/dogfood_test.spy:24:55
    |
 24 |         c: float = k - 273.15
    |                              ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpz_v0h250/dogfood_test.cs

```

## Timing

- Generation: 378.88s
- Execution: 4.28s
