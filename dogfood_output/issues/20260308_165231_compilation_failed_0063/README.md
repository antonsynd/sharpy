# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T16:34:42.033163
**Type:** compilation_failed
**Feature Focus:** try_except_finally
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
class DataError(Exception):
    pass

class ParseError(DataError):
    pass

class RangeError(DataError):
    pass

class Parser:
    property count: int = 0

    def parse_digit(self, s: str) -> int:
        self.count += 1
        if s == "":
            raise ParseError("Empty")
        c: str = str(s[0])
        if c == "0":
            return 0
        elif c == "1":
            return 1
        elif c == "2":
            return 2
        elif c == "3":
            return 3
        elif c == "4":
            return 4
        elif c == "5":
            return 5
        else:
            raise ParseError("Invalid")

class Accumulator:
    _total: int = 0
    _processed: int = 0

    def add(self, v: int) -> int:
        self._processed += 1
        if v < 0:
            raise RangeError("Negative")
        self._total += v
        return self._total

    property get total(self) -> int:
        return self._total

    property get processed(self) -> int:
        return self._processed

class Logger:
    _entries: list[str] = []

    def log(self, m: str) -> None:
        self._entries.append(m)

    property get size(self) -> int:
        return len(self._entries)

def main():
    inputs: list[str] = ["3", "", "5", "x", "2"]
    p = Parser()
    a = Accumulator()
    l = Logger()
    
    for i in range(len(inputs)):
        raw = inputs[i]
        val: int = 0
        ok = False
        
        try:
            print(f"Parse '{raw}'")
            val = p.parse_digit(raw)
        except ParseError as e:
            print("Parse error")
            val = 0
        else:
            print("Parse OK")
        finally:
            l.log(f"item{i}")
            print("Logged")
        
        try:
            print("Process")
            r = a.add(val)
            print(f"Total: {r}")
        except RangeError as e:
            print("Range error")
        else:
            print("Process OK")
            ok = True
        finally:
            print("Item done")
        
        if ok:
            print("Success")
        else:
            print("Failed")
    
    print(f"Calls: {p.count}")
    print(f"Done: {a.processed}")
    print(f"Sum: {a.total}")
    print(f"Logs: {l.size}")

```

## Error

```
Assembly compilation failed:

error[CS1729]: 'DogfoodTest.RangeError' does not contain a constructor that takes 1 arguments
  --> /tmp/tmpv6lnum42/dogfood_test.spy:40:27
    |
 40 |             raise RangeError("Negative")
    |                           ^
    |

error[CS1729]: 'DogfoodTest.ParseError' does not contain a constructor that takes 1 arguments
  --> /tmp/tmpv6lnum42/dogfood_test.spy:16:27
    |
 16 |             raise ParseError("Empty")
    |                           ^
    |

error[CS1729]: 'DogfoodTest.ParseError' does not contain a constructor that takes 1 arguments
  --> /tmp/tmpv6lnum42/dogfood_test.spy:31:27
    |
 31 |             raise ParseError("Invalid")
    |                           ^
    |


```

## Compiler Output

```
warning[SPY0451]: Local variable 'e' is assigned but never used
  --> /tmp/tmpv6lnum42/dogfood_test.spy:86:9
    |
 86 |         except RangeError as e:
    |         ^^^^^^^^^^^^^^^^^^^^^^^
    |


```

## Generated C#

```csharp
warning[SPY0451]: Local variable 'e' is assigned but never used
  --> /tmp/tmpv6lnum42/dogfood_test.spy:86:9
    |
 86 |         except RangeError as e:
    |         ^^^^^^^^^^^^^^^^^^^^^^^
    |

Generated C# code written to: /tmp/tmpv6lnum42/dogfood_test.cs

```

## Timing

- Generation: 1049.21s
- Execution: 5.19s
