# Issue Report: compilation_failed

**Timestamp:** 2026-03-03T03:58:17.301347
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - imports from both modules
import task_manager
from report_generator import generate_summary_report, generate_detailed_report

def main():
    # Add tasks via task_manager module
    task_manager.add_task("Code Review", 60, 4)
    task_manager.add_task("Bug Fix", 30, 5)
    task_manager.add_task("Documentation", 45, 2)
    
    # Get summary from report_generator
    summary = generate_summary_report()
    print(summary)
    
    # Get detailed report
    details = generate_detailed_report()
    for line in details:
        print(line)
    
    # Add more tasks and show updated state
    task_manager.add_task("Testing", 90, 3)
    updated_summary = generate_summary_report()
    print(updated_summary)

```

## Error

```
Assembly compilation failed:

error[CS0103]: The name '_tasks' does not exist in the current context
  --> /tmp/tmpbvconme4/task_manager.spy:19:9
    |
 19 |     
    |     ^
    |

error[CS0103]: The name '_tasks' does not exist in the current context
  --> /tmp/tmpbvconme4/task_manager.spy:22:16
    |
 22 |     updated_summary = generate_summary_report()
    |                ^
    |

error[CS0103]: The name '_tasks' does not exist in the current context
  --> /tmp/tmpbvconme4/task_manager.spy:26:37

error[CS0103]: The name '_tasks' does not exist in the current context
  --> /tmp/tmpbvconme4/task_manager.spy:32:37

error[CS0103]: The name '_tasks' does not exist in the current context
  --> /tmp/tmpbvconme4/task_manager.spy:38:9


```

## Timing

- Generation: 107.51s
- Execution: 4.72s
