# Successful Dogfood Run

**Timestamp:** 2026-03-03T02:39:07.655784
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude
**Test Type:** Multi-file (3 files)

## Source Files

### math_helpers.spy

```python
# Math helper module - provides mathematical utilities

const PI: float = 3.14159

def square(x: int) -> int:
    return x * x

def cube(x: int) -> int:
    return x * x * x

def factorial(n: int) -> int:
    if n <= 1:
        return 1
    return n * factorial(n - 1)

def average(values: list[int]) -> float:
    if len(values) == 0:
        return 0.0
    total: int = sum(values)
    return float(total) / float(len(values))

```

### string_helpers.spy

```python
# String helper module - provides string manipulation utilities

def reverse(s: str) -> str:
    result: str = ""
    i: int = len(s) - 1
    while i >= 0:
        result = result + str(s[i])
        i = i - 1
    return result

def is_palindrome(s: str) -> bool:
    return s == reverse(s)

def word_count(text: str) -> int:
    words: list[str] = text.split(" ")
    return len(words)

def capitalize_words(text: str) -> str:
    return text.title()

```

### main.spy

```python
# Main entry point - demonstrates module imports

from math_helpers import square, cube, factorial, average, PI
from string_helpers import reverse, is_palindrome, word_count, capitalize_words

def main():
    # Test math imports
    x: int = 5
    print(f"Square of {x}: {square(x)}")
    print(f"Cube of {x}: {cube(x)}")
    print(f"Factorial of {x}: {factorial(x)}")
    print(f"PI constant: {PI}")
    
    numbers: list[int] = [10, 20, 30, 40, 50]
    avg: float = average(numbers)
    print(f"Average of list: {avg}")
    
    # Test string imports
    text: str = "hello world"
    print(f"Original: {text}")
    print(f"Reversed: {reverse(text)}")
    print(f"Word count: {word_count(text)}")
    print(f"Capitalized: {capitalize_words(text)}")
    
    palindrome: str = "radar"
    print(f"'{palindrome}' is palindrome: {is_palindrome(palindrome)}")
    
    not_palindrome: str = "hello"
    print(f"'{not_palindrome}' is palindrome: {is_palindrome(not_palindrome)}")

```

## Timing

- Generation: 296.27s
- Execution: 4.88s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
