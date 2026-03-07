# Successful Dogfood Run

**Timestamp:** 2026-03-06T22:33:43.639978
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude
**Test Type:** Multi-file (2 files)

## Source Files

### text_utils.spy

```python
class Formatter:
    prefix: str
    suffix: str
    
    def __init__(self, prefix: str = "", suffix: str = ""):
        self.prefix = prefix
        self.suffix = suffix
    
    def format(self, text: str) -> str:
        return self.prefix + text + self.suffix
    
    def format_lines(self, lines: list[str]) -> list[str]:
        result: list[str] = []
        for line in lines:
            result.append(self.format(line))
        return result

def truncate(text: str, max_length: int) -> str:
    if len(text) <= max_length:
        return text
    return text[0:max_length - 3] + "..."

def repeat_char(char: str, n: int) -> str:
    result: str = ""
    i: int = 0
    while i < n:
        result += char
        i += 1
    return result

def pad_center(text: str, width: int) -> str:
    if len(text) >= width:
        return text
    spaces: int = width - len(text)
    left: int = spaces // 2
    right: int = spaces - left
    return repeat_char(" ", left) + text + repeat_char(" ", right)

```

### main.spy

```python
from text_utils import Formatter, truncate, pad_center

def main():
    f: Formatter = Formatter(">> ", " <<")
    print(f.format("hello"))
    
    lines: list[str] = ["one", "two", "three"]
    formatted: list[str] = f.format_lines(lines)
    for line in formatted:
        print(line)
    
    print(truncate("hello world", 8))
    print(truncate("hello world", 20))
    
    centered: str = pad_center("test", 10)
    print("'" + centered + "'")
    
    f2: Formatter = Formatter("[", "]")
    print(f2.format("done"))

```

## Timing

- Generation: 282.31s
- Execution: 5.71s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
