# Issue Report: compilation_failed

**Timestamp:** 2026-02-17T21:16:30.410090
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating cross-module inheritance
# Tests importing from multiple modules and using inherited classes

from text_utils import TextProcessor, word_count
from formatters import TitleFormatter, Truncator, format_report

def main():
    test_text: str = "   hello WORLD from sharpy   "
    
    # Test base class
    base: TextProcessor = TextProcessor(test_text)
    print(base.process())
    stats: dict[str, int] = base.get_stats()
    print(stats["length"])
    
    # Test cross-module inheritance
    title: TitleFormatter = TitleFormatter(test_text, "[PROCESSED] ")
    print(title.process())
    
    # Test word count utility
    count: int = word_count("one two three four")
    print(count)
    
    # Test multi-file report
    items: list[str] = ["  Alpha  ", "BETA", "  gamma  "]
    report: str = format_report(items)
    print(report)

# EXPECTED OUTPUT:
#    hello world from sharpy   
# 43
# [PROCESSED]    Hello World From Sharpy   
# 4
# 1. alpha
# 2. beta
# 3. gamma
```

## Error

```
Assembly compilation failed:

error[CS0103]: The name 'Enumerate' does not exist in the current context
  --> /tmp/tmpoa5rjj2j/formatters.spy:40:33

error[CS8130]: Cannot infer the type of implicitly-typed deconstruction variable 'i'.
  --> /tmp/tmpoa5rjj2j/formatters.spy:40:22

error[CS8130]: Cannot infer the type of implicitly-typed deconstruction variable 'item'.
  --> /tmp/tmpoa5rjj2j/formatters.spy:40:25

error[CS0103]: The name 'clean_text' does not exist in the current context
  --> /tmp/tmpoa5rjj2j/formatters.spy:41:66


```

## Compiler Output

```
warning[SPY0452]: Imported name 'clean_text' is never used
  --> /tmp/tmpoa5rjj2j/formatters.spy:4:6
    |
  4 | from text_utils import TextProcessor, word_count
    |      ^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'Truncator' is never used
  --> /tmp/tmpoa5rjj2j/main.spy:5:40
    |
  5 | from formatters import TitleFormatter, Truncator, format_report
    |                                        ^^^^^^^^^
    |


```

## Timing

- Generation: 128.41s
- Execution: 4.40s
