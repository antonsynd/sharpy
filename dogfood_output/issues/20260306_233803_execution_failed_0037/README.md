# Issue Report: execution_failed

**Timestamp:** 2026-03-06T23:35:49.106397
**Type:** execution_failed
**Feature Focus:** raise_exception
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test custom exceptions with multiple except blocks and re-raise pattern

class ParseError(Exception):
    line: int
    column: int
    
    def __init__(self, line: int, column: int):
        self.line = line
        self.column = column

class TimeoutError(Exception):
    seconds: int
    
    def __init__(self, seconds: int):
        self.seconds = seconds

def parse_number(text: str, pos: int) -> int:
    if pos >= len(text):
        raise ParseError(1, pos)
    if text[pos] == "x":
        raise TimeoutError(5)
    return int(text[pos])

def process_with_fallback(text: str) -> int:
    result: int = 0
    try:
        result = parse_number(text, 0)
        result += parse_number(text, 1)
    except ParseError as e:
        print(e.line)
        result = 999
    except TimeoutError as e:
        print(e.seconds)
        result = 888
    return result

def main():
    print(process_with_fallback("37"))
    print(process_with_fallback("3"))
    print(process_with_fallback("ax"))

```

## Error

```
Unhandled exception. Sharpy.ValueError: invalid literal for int() with base 10: 'a'
   at Sharpy.Builtins.Int(String s)
   at DogfoodTest.ParseNumber(String text, Int32 pos) in /tmp/tmpevibgew4/dogfood_test.spy:line 22
   at DogfoodTest.ProcessWithFallback(String text) in /tmp/tmpevibgew4/dogfood_test.spy:line 27
   at DogfoodTest.Main() in /tmp/tmpevibgew4/dogfood_test.spy:line 40

```

## Compiler Output

```
10
1
999

```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpevibgew4/dogfood_test.cs

```

## Timing

- Generation: 120.29s
- Execution: 5.79s
