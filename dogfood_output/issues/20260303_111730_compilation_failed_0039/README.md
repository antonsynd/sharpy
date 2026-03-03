# Issue Report: compilation_failed

**Timestamp:** 2026-03-03T11:10:02.038581
**Type:** compilation_failed
**Feature Focus:** nullable_types
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Nullable types with generics, inheritance, and service pattern

interface Identifiable:
    property id: int

class Entity(Identifiable):
    property id: int
    name: str
    
    def __init__(self, id: int, name: str):
        self.id = id
        self.name = name

class Repository[T: Identifiable]:
    items: dict[int, T]
    
    def __init__(self):
        self.items = {}
    
    def find(self, key: int) -> T?:
        if key in self.items:
            return Some(self.items[key])
        return None()
    
    def save(self, item: T) -> None:
        self.items[item.id] = item

class CacheService[T: Identifiable]:
    repository: Repository[T]
    hits: int
    misses: int
    
    def __init__(self, repo: Repository[T]):
        self.repository = repo
        self.hits = 0
        self.misses = 0
    
    def find_by_id(self, key: int) -> T?:
        result = self.repository.find(key)
        if result is not None:
            self.hits += 1
        else:
            self.misses += 1
        return result
    
    def find_or_default(self, key: int, default: T) -> T:
        result = self.find_by_id(key)
        if result is not None:
            return result.unwrap()
        return default

def safe_divide(a: int, b: int) -> float?:
    if b == 0:
        return None()
    return Some(a / b)

def main():
    repo = Repository[Entity]()
    service = CacheService[Entity](repo)
    
    # Populate repository
    repo.save(Entity(1, "Alpha"))
    repo.save(Entity(2, "Beta"))
    
    # Test successful lookup
    found = service.find_by_id(1)
    if found is not None:
        print(found.unwrap().name)
        print(service.hits)
    
    # Test failed lookup with default fallback
    default = Entity(99, "DefaultEntity")
    result = service.find_or_default(999, default)
    print(result.name)
    print(service.misses)
    
    # Test safe division (nullable return)
    d1 = safe_divide(10, 2)
    if d1 is not None:
        print(d1.unwrap())
    
    # Division by zero returns None
    d2 = safe_divide(5, 0)
    if d2 is not None:
        print(d2.unwrap())
    else:
        print("error")
    
    # Test map on optional value
    val: int? = Some(10)
    doubled = val.map(lambda x: x * 2)
    if doubled is not None:
        print(doubled.unwrap())

```

## Error

```
Assembly compilation failed:

error[CS1929]: 'T' does not contain a definition for 'Unwrap' and the best extension method overload 'TaskExtensions.Unwrap(Task<Task>)' requires a receiver of type 'System.Threading.Tasks.Task<System.Threading.Tasks.Task>'
  --> /tmp/tmpdnjvbg2c/dogfood_test.spy:49:24
    |
 49 |             return result.unwrap()
    |                        ^
    |

error[CS1929]: 'double' does not contain a definition for 'Unwrap' and the best extension method overload 'TaskExtensions.Unwrap(Task<Task>)' requires a receiver of type 'System.Threading.Tasks.Task<System.Threading.Tasks.Task>'
  --> /tmp/tmpdnjvbg2c/dogfood_test.spy:80:43
    |
 80 |         print(d1.unwrap())
    |                           ^
    |

error[CS1929]: 'double' does not contain a definition for 'Unwrap' and the best extension method overload 'TaskExtensions.Unwrap(Task<Task>)' requires a receiver of type 'System.Threading.Tasks.Task<System.Threading.Tasks.Task>'
  --> /tmp/tmpdnjvbg2c/dogfood_test.spy:85:43
    |
 85 |         print(d2.unwrap())
    |                           ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpdnjvbg2c/dogfood_test.cs

```

## Timing

- Generation: 434.52s
- Execution: 4.72s
