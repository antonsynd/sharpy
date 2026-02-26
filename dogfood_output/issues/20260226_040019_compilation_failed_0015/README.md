# Issue Report: compilation_failed

**Timestamp:** 2026-02-26T03:58:19.132797
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - imports from multiple modules and orchestrates

from models import Person
from services import Repository, PersonFormatter

def main():
    repo = Repository()
    
    p1 = Person(1, "Alice", "Smith")
    p2 = Person(2, "Bob", "Johnson")
    
    repo.add(p1)
    repo.add(p2)
    
    print(PersonFormatter.format_full(p1))
    print(PersonFormatter.format_with_id(p2))
    
    found = repo.find_by_id(1)
    if found is not None:
        print(found.unwrap().get_display_name())
    
    all_names = repo.get_all_display_names()
    for name in all_names:
        print(name)
```

## Error

```
Assembly compilation failed:

error[CS1929]: 'Models.Entity' does not contain a definition for 'Unwrap' and the best extension method overload 'TaskExtensions.Unwrap(Task<Task>)' requires a receiver of type 'System.Threading.Tasks.Task<System.Threading.Tasks.Task>'
  --> /tmp/tmp9869g_fh/main.spy:20:43
    |
 20 |         print(found.unwrap().get_display_name())
    |                                           ^
    |

error[CS0029]: Cannot implicitly convert type 'Sharpy.List<object>' to 'Sharpy.List<string>'
  --> /tmp/tmp9869g_fh/services.spy:21:20
    |
 21 |     
    |     ^
    |


```

## Timing

- Generation: 105.48s
- Execution: 4.51s
