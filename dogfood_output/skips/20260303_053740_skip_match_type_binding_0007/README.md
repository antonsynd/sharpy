# Skipped Dogfood Run

**Timestamp:** 2026-03-03T05:31:56.746032
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0220]: Cannot assign type 'int' to variable of type 'int'
  --> /tmp/tmph58o8xqs/dogfood_test.spy:16:9
    |
 16 |         num: int = v
    |         ^^^^^^^^^^^^
    |

error[SPY0220]: Cannot assign type 'float' to variable of type 'float'
  --> /tmp/tmph58o8xqs/dogfood_test.spy:22:9
    |
 22 |         fval: float = v
    |         ^^^^^^^^^^^^^^^
    |

error[SPY0220]: Cannot assign type 'str' to variable of type 'str'
  --> /tmp/tmph58o8xqs/dogfood_test.spy:28:9
    |
 28 |         text: str = v
    |         ^^^^^^^^^^^^^
    |

error[SPY0220]: Cannot assign type 'bool' to variable of type 'bool'
  --> /tmp/tmph58o8xqs/dogfood_test.spy:34:9
    |
 34 |         flag: bool = v
    |         ^^^^^^^^^^^^^^
    |


**Feature Focus:** match_type_binding
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test type-based value parsing with isinstance checks
# Validates and converts different config value types with appropriate defaults

class ConfigEntry:
    key: str
    value: object
    
    def __init__(self, key: str, value: object):
        self.key = key
        self.value = value

def parse_config_value(entry: ConfigEntry) -> str:
    v: object = entry.value
    
    if isinstance(v, int):
        num: int = v
        if num < 0:
            return f"[{entry.key}] negative int: {num}"
        else:
            return f"[{entry.key}] positive int: {num}"
    elif isinstance(v, float):
        fval: float = v
        if fval > 100.0:
            return f"[{entry.key}] large float: {fval:.2f}"
        else:
            return f"[{entry.key}] small float: {fval:.1f}"
    elif isinstance(v, str):
        text: str = v
        if len(text) == 0:
            return f"[{entry.key}] empty string"
        else:
            return f"[{entry.key}] string ({len(text)} chars): '{text}'"
    elif isinstance(v, bool):
        flag: bool = v
        return f"[{entry.key}] boolean: {flag}"
    else:
        return f"[{entry.key}] unknown type"

def main():
    # Create entries one at a time to avoid complex nested list initialization issues
    e1: ConfigEntry = ConfigEntry("timeout", 30)
    e2: ConfigEntry = ConfigEntry("delay", -5)
    e3: ConfigEntry = ConfigEntry("pi", 3.14159)
    e4: ConfigEntry = ConfigEntry("scale", 250.5)
    e5: ConfigEntry = ConfigEntry("message", "hello world")
    e6: ConfigEntry = ConfigEntry("empty", "")
    e7: ConfigEntry = ConfigEntry("enabled", True)
    e8: ConfigEntry = ConfigEntry("data", None)
    
    entries: list[ConfigEntry] = [e1, e2, e3, e4, e5, e6, e7, e8]
    
    i: int = 0
    while i < len(entries):
        entry: ConfigEntry = entries[i]
        print(parse_config_value(entry))
        i += 1

```

## Timing

- Generation: 327.20s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
