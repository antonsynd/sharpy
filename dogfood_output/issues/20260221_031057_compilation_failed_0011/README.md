# Issue Report: compilation_failed

**Timestamp:** 2026-02-21T03:08:41.632682
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - imports from multiple modules
from data_structures import TaskManager, WorkTask, PersonalTask, create_default_manager
from utils import sort_by_priority, task_summary

def main():
    # Get pre-populated manager
    manager: TaskManager = create_default_manager()
    
    # Print initial state
    print(f"Total tasks in manager: {{len(manager.tasks)}}")
    
    # Sort and display tasks
    sorted_tasks: list[Task] = sort_by_priority(manager.tasks)
    print(f"Tasks sorted by priority:")
    for t in sorted_tasks:
        print(f"  {{t}}")
    
    # Get high priority tasks
    high_priority: list[Task] = manager.get_high_priority()
    print(f"High priority count: {{len(high_priority)}}")
    
    # Get task summary
    summary: tuple[int, int] = task_summary(manager.tasks)
    total, work = summary
    print(f"Summary: {{total}} total, {{work}} work tasks")

# EXPECTED OUTPUT:
# Total tasks in manager: 3
# Tasks sorted by priority:
#   Review code (P1) -> Alice
#   Fix bug (P2) -> Bob
#   Buy groceries (P3) by Friday
# High priority count: 2
# Summary: 3 total, 2 work tasks
```

## Error

```
Assembly compilation failed:

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmp6ziutotm/data_structures.spy:12:51
    |
 12 |     # Sort and display tasks
    |                             ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmp6ziutotm/data_structures.spy:12:66
    |
 12 |     # Sort and display tasks
    |                             ^
    |

error[CS0103]: The name 'len' does not exist in the current context
  --> /tmp/tmp6ziutotm/main.spy:10:94
    |
 10 |     print(f"Total tasks in manager: {{len(manager.tasks)}}")
    |                                                             ^
    |

error[CS1061]: 'DataStructures.TaskManager' does not contain a definition for 'tasks' and no accessible extension method 'tasks' accepting a first argument of type 'DataStructures.TaskManager' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp6ziutotm/main.spy:10:106
    |
 10 |     print(f"Total tasks in manager: {{len(manager.tasks)}}")
    |                                                             ^
    |

error[CS0103]: The name 'len' does not exist in the current context
  --> /tmp/tmp6ziutotm/main.spy:20:91
    |
 20 |     print(f"High priority count: {{len(high_priority)}}")
    |                                                          ^
    |

error[CS0103]: The name 'high_priority' does not exist in the current context
  --> /tmp/tmp6ziutotm/main.spy:20:95
    |
 20 |     print(f"High priority count: {{len(high_priority)}}")
    |                                                          ^
    |

error[CS0175]: Use of keyword 'base' is not valid in this context
  --> /tmp/tmp6ziutotm/data_structures.spy:23:51
    |
 23 |     summary: tuple[int, int] = task_summary(manager.tasks)
    |                                                   ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmp6ziutotm/data_structures.spy:23:61
    |
 23 |     summary: tuple[int, int] = task_summary(manager.tasks)
    |                                                           ^
    |

error[CS0175]: Use of keyword 'base' is not valid in this context
  --> /tmp/tmp6ziutotm/data_structures.spy:34:51
    |
 34 | # Summary: 3 total, 2 work tasks
    |                                 ^
    |

error[CS0103]: The name 'self' does not exist in the current context
  --> /tmp/tmp6ziutotm/data_structures.spy:34:61
    |
 34 | # Summary: 3 total, 2 work tasks
    |                                 ^
    |


```

## Compiler Output

```
warning[SPY0451]: Local variable 'base' is assigned but never used
  --> /tmp/tmp6ziutotm/data_structures.spy:16:16
    |
 16 |         print(f"  {{t}}")
    |                ^^^^^^^^^^
    |

warning[SPY0451]: Local variable 'base' is assigned but never used
  --> /tmp/tmp6ziutotm/data_structures.spy:25:39
    |
 25 |     print(f"Summary: {{total}} total, {{work}} work tasks")
    |                                       ^^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0451]: Local variable 'wt' is assigned but never used
  --> /tmp/tmp6ziutotm/utils.spy:37:9

warning[SPY0452]: Imported name 'PersonalTask' is never used
  --> /tmp/tmp6ziutotm/utils.spy:2:34
    |
  2 | from data_structures import TaskManager, WorkTask, PersonalTask, create_default_manager
    |                                  ^^^^^^^^^^^^
    |

warning[SPY0451]: Local variable 'high_priority' is assigned but never used
  --> /tmp/tmp6ziutotm/main.spy:19:5
    |
 19 |     high_priority: list[Task] = manager.get_high_priority()
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0451]: Local variable 'total' is assigned but never used
  --> /tmp/tmp6ziutotm/main.spy:24:5
    |
 24 |     total, work = summary
    |     ^^^^^
    |

warning[SPY0451]: Local variable 'work' is assigned but never used
  --> /tmp/tmp6ziutotm/main.spy:24:12
    |
 24 |     total, work = summary
    |            ^^^^
    |

warning[SPY0452]: Imported name 'WorkTask' is never used
  --> /tmp/tmp6ziutotm/main.spy:2:42
    |
  2 | from data_structures import TaskManager, WorkTask, PersonalTask, create_default_manager
    |                                          ^^^^^^^^
    |

warning[SPY0452]: Imported name 'PersonalTask' is never used
  --> /tmp/tmp6ziutotm/main.spy:2:52
    |
  2 | from data_structures import TaskManager, WorkTask, PersonalTask, create_default_manager
    |                                                    ^^^^^^^^^^^^
    |


```

## Timing

- Generation: 119.66s
- Execution: 4.87s
