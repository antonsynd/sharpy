# Issue Report: compilation_failed

**Timestamp:** 2026-03-07T00:43:10.989608
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point testing cross-module static factory and class usage
# Tests: Static factory methods, cross-module method access, version constants

from settings import Settings, VERSION, get_prefix
from database import DatabaseConfig

def main():
    # Test 1: Static factory method across modules
    settings: Settings = Settings.get_instance()
    print(get_prefix())
    
    # Test 2: Static constant across modules
    print(VERSION)
    
    # Test 3: Class from module with constructor chaining
    config1: DatabaseConfig = DatabaseConfig("db.example.com", 3306)
    print(config1.get_host())
    print(config1.get_port())
    
    # Test 4: Default constructor (constructor chaining)
    config2: DatabaseConfig = DatabaseConfig()
    print(config2.get_host())
    
    # Test 5: Method using static from another module
    print(config1.connect_string())
    
    # Test 6: Cross-module state sharing via singleton pattern
    config1.configure("mydb", "admin")
    print(config1.get_config("db_name"))
    print(config1.get_config("user"))
    
    # Verify config2 sees same settings
    print(config2.get_config("db_name"))

```

## Error

```
Assembly compilation failed:

error[CS0229]: Ambiguity between 'Settings.Version' and 'Version'
  --> /tmp/tmpmp48grnr/main.spy:13:39
    |
 13 |     print(VERSION)
    |                   ^
    |

error[CS0149]: Method name expected
  --> /tmp/tmpmp48grnr/settings.spy:30:45
    |
 30 |     print(config1.get_config("user"))
    |                                      ^
    |

error[CS0103]: The name 'configPrefix' does not exist in the current context
  --> /tmp/tmpmp48grnr/settings.spy:39:16


```

## Compiler Output

```
warning[SPY0451]: Local variable 'result' is assigned but never used
  --> /tmp/tmpmp48grnr/settings.spy:9:48
    |
  9 |     settings: Settings = Settings.get_instance()
    |                                                ^
    |

warning[SPY0451]: Local variable 'settings' is assigned but never used
  --> /tmp/tmpmp48grnr/main.spy:9:5
    |
  9 |     settings: Settings = Settings.get_instance()
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 338.16s
- Execution: 4.47s
