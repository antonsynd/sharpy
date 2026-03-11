# Skipped Dogfood Run

**Timestamp:** 2026-03-10T14:20:41.841875
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0203]: Type 'ConfigValue' has no member 'unwrap'
  --> /tmp/tmp89_9x0md/dogfood_test.spy:47:14
    |
 47 |         tv = timeout_val.unwrap()
    |              ^^^^^^^^^^^^^^^^^^
    |

error[SPY0202]: Unknown type 'IntValue' in positional pattern
  --> /tmp/tmp89_9x0md/dogfood_test.spy:49:18
    |
 49 |             case IntValue(v):
    |                  ^^^^^^^^^^^
    |

error[SPY0202]: Unknown type 'StrValue' in positional pattern
  --> /tmp/tmp89_9x0md/dogfood_test.spy:51:18
    |
 51 |             case StrValue(v):
    |                  ^^^^^^^^^^^
    |

error[SPY0202]: Type 'IntValue' not found
  --> /tmp/tmp89_9x0md/dogfood_test.spy:49:18
    |
 49 |             case IntValue(v):
    |                  ^^^^^^^^
    |

error[SPY0202]: Type 'StrValue' not found
  --> /tmp/tmp89_9x0md/dogfood_test.spy:51:18
    |
 51 |             case StrValue(v):
    |                  ^^^^^^^^
    |


**Feature Focus:** type_alias
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
union ConfigValue:
    case IntValue(value: int)
    case StrValue(value: str)

type ConfigMap = dict[str, ConfigValue]

class ConfigManager:
    _settings: ConfigMap
    _handler: Optional[(str) -> None]

    def __init__(self):
        self._settings = {}
        self._handler = None()

    def set(self, key: str, value: ConfigValue) -> None:
        self._settings[key] = value
        if self._handler is not None:
            self._handler.unwrap()(key)

    def get(self, key: str) -> ConfigValue?:
        if key in self._settings:
            return Some(self._settings[key])
        return None()

    def register_handler(self, handler: (str) -> None) -> None:
        self._handler = Some(handler)

def log_change(key: str) -> None:
    print(f"Changed: {key}")

def main():
    manager = ConfigManager()

    # Store simple values using tagged union constructors
    manager.set("timeout", ConfigValue.IntValue(30))
    manager.set("host", ConfigValue.StrValue("localhost"))

    # Register handler after initial setup
    manager.register_handler(log_change)

    # Now changes trigger handler
    manager.set("retries", ConfigValue.IntValue(3))

    # Lookup existing and missing keys
    timeout_val = manager.get("timeout")
    if timeout_val is not None:
        tv = timeout_val.unwrap()
        match tv:
            case IntValue(v):
                print(v)
            case StrValue(v):
                print(v)

    missing = manager.get("missing")
    if missing is None:
        print("Not found")

```

## Timing

- Generation: 411.14s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
