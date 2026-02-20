# Issue Report: compilation_failed

**Timestamp:** 2026-02-19T06:36:18.679881
**Type:** compilation_failed
**Feature Focus:** class_inheritance
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Simple class inheritance with field declaration and inherited method
class Animal:
    name: str  # Field declaration required in Sharpy
    
    def __init__(self, name: str):
        self.name = name
    
    def describe(self) -> str:
        return "Animal: " + self.name

class Dog(Animal):
    # Dog inherits __init__ and describe from Animal
    pass

def main():
    pet: Animal = Dog("Buddy")
    print(pet.describe())
# EXPECTED OUTPUT:
# Animal: Buddy
```

## Error

```
Assembly compilation failed:

error[CS7036]: There is no argument given that corresponds to the required parameter 'name' of 'DogfoodTest.Animal.Animal(string)'
  --> /tmp/tmpf5etyruv/dogfood_test.spy:10:18
    |
 10 | 
    | ^
    |

error[CS1729]: 'DogfoodTest.Dog' does not contain a constructor that takes 1 arguments
  --> /tmp/tmpf5etyruv/dogfood_test.spy:16:26
    |
 16 |     pet: Animal = Dog("Buddy")
    |                          ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpf5etyruv/dogfood_test.cs

```

## Timing

- Generation: 773.45s
- Execution: 4.06s
