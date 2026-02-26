# Successful Dogfood Run

**Timestamp:** 2026-02-25T07:08:22.357212
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude
**Test Type:** Multi-file (3 files)

## Source Files

### math_utils.spy

```python
# Mathematical utility functions

def square(x: int) -> int:
    """Calculate the square of a number."""
    return x * x

def sum_of_squares(n: int) -> int:
    """Calculate sum of squares from 0 to n."""
    total: int = 0
    i: int = 0
    while i <= n:
        total = total + square(i)
        i = i + 1
    return total

def is_even(n: int) -> bool:
    """Check if a number is even."""
    return n % 2 == 0
```

### text_utils.spy

```python
# Text formatting utility class

class TextFormatter:
    text: str
    
    def __init__(self, text: str):
        self.text = text
    
    def to_upper(self) -> str:
        """Convert text to uppercase."""
        return self.text.upper()
    
    def word_count(self) -> int:
        """Count the number of words in the text."""
        words: list[str] = self.text.split(" ")
        return len(words)
    
    def reverse_words(self) -> str:
        """Reverse the order of words."""
        words: list[str] = self.text.split(" ")
        result: str = ""
        i: int = len(words) - 1
        while i >= 0:
            if result != "":
                result = result + " "
            result = result + words[i]
            i = i - 1
        return result
```

### main.spy

```python
# Main entry point - tests module utility imports
from math_utils import square, sum_of_squares, is_even
from text_utils import TextFormatter

def main():
    # Test math utilities
    n: int = 5
    result: int = sum_of_squares(n)
    print(result)
    
    # Check if 16 is even (square of 4)
    val: int = square(4)
    print(is_even(val))
    
    # Test text formatter
    formatter: TextFormatter = TextFormatter("hello world example")
    
    # Uppercase conversion
    upper: str = formatter.to_upper()
    print(upper)
    
    # Word count
    count: int = formatter.word_count()
    print(count)
    
    # Reverse words
    reversed_text: str = formatter.reverse_words()
    print(reversed_text)

# EXPECTED OUTPUT:
# 55
# True
# HELLO WORLD EXAMPLE
# 3
# example world hello
```

## Timing

- Generation: 91.98s
- Execution: 4.48s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
