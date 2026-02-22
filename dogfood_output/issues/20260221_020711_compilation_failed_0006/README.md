# Issue Report: compilation_failed

**Timestamp:** 2026-02-21T01:57:29.528499
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates complex cross-module imports and usage
from core_types import Status, Priority, IDataProcessor, priority_name
from data_models import TextProcessor, AdvancedProcessor, create_processor
from config_utils import Config, ConfigManager, Point, default_config, ConfigPair

def main():
    # Create and test basic processor
    processor: TextProcessor = create_processor()
    
    # Process some data
    result1: str = processor.process("hello")
    print(result1)
    
    # Check validation
    valid: bool = processor.is_valid()
    print(valid)
    
    # Create advanced processor with HIGH priority
    advanced: AdvancedProcessor = AdvancedProcessor(Priority.HIGH)
    result2: str = advanced.process("world")
    print(result2)
    
    # Use priority name helper
    print(priority_name(Priority.CRITICAL))
    
    # Create and use Config struct
    config: Config = default_config()
    print(config.is_active())
    
    # Create ConfigManager and add configs
    manager: ConfigManager = ConfigManager()
    manager.add(config)
    manager.add(Config(50, False))
    manager.add(Config(200, True))
    
    # Count active configs
    active_count: int = manager.count_active()
    print(active_count)
    
    # Create a point and calculate distance
    point: Point = Point(3.5, 4.5)
    dist: float = point.distance_from_origin()
    print(dist)
    
    # Work with enum values
    current_status: Status = Status.SUCCESS
    print(current_status == Status.SUCCESS)
    
    # Use ConfigPair struct (replaces named tuple)
    pair1: ConfigPair = ConfigPair("test", 42)
    print(pair1.name)
# EXPECTED OUTPUT:
# HELLO
# True
# [2] WORLD
# critical
# True
# 2
# 8.0
# True
# test
```

## Error

```
Assembly compilation failed:

error[CS0104]: 'ISized' is an ambiguous reference between 'CoreTypes.ISized' and 'Sharpy.ISized'
  --> /tmp/tmprgh6tfw_/data_models.spy:15:84
    |
 15 |     valid: bool = processor.is_valid()
    |                                       ^
    |

error[CS0506]: 'DataModels.AdvancedProcessor.Process(string)': cannot override inherited member 'DataModels.TextProcessor.Process(string)' because it is not marked virtual, abstract, or override
  --> /tmp/tmprgh6tfw_/data_models.spy:33:32
    |
 33 |     manager.add(Config(50, False))
    |                                ^
    |

error[CS0115]: 'DataModels.AdvancedProcessor.GetInfo()': no suitable method found to override
  --> /tmp/tmprgh6tfw_/data_models.spy:53:32
    |
 53 | # HELLO
    |        ^
    |

error[CS1061]: 'DataModels.AdvancedProcessor' does not contain a definition for 'PriorityName' and no accessible extension method 'PriorityName' accepting a first argument of type 'DataModels.AdvancedProcessor' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmprgh6tfw_/data_models.spy:48:67
    |
 48 |     
    |     ^
    |

error[CS1061]: 'DataModels.AdvancedProcessor' does not contain a definition for 'PriorityName' and no accessible extension method 'PriorityName' accepting a first argument of type 'DataModels.AdvancedProcessor' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmprgh6tfw_/data_models.spy:54:88
    |
 54 | # True
    |       ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Priority' is never used
  --> /tmp/tmprgh6tfw_/config_utils.spy:2:8
    |
  2 | from core_types import Status, Priority, IDataProcessor, priority_name
    |        ^^^^^^^^
    |

warning[SPY0452]: Imported name 'IDataProcessor' is never used
  --> /tmp/tmprgh6tfw_/main.spy:2:42
    |
  2 | from core_types import Status, Priority, IDataProcessor, priority_name
    |                                          ^^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 549.82s
- Execution: 4.75s
