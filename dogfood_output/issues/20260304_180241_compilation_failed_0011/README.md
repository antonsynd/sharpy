# Issue Report: compilation_failed

**Timestamp:** 2026-03-04T18:00:05.370007
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
from string_utils import reverse, is_palindrome, word_count
from math_utils import factorial, fibonacci, is_prime

def main():
    # Test string utilities
    text: str = "hello"
    print(reverse(text))
    
    palindrome: str = "radar"
    print(is_palindrome(palindrome))
    print(is_palindrome(text))
    
    sentence: str = "the quick brown fox"
    print(word_count(sentence))
    
    # Test math utilities
    n: int = 5
    print(factorial(n))
    
    # Fibonacci sequence
    i: int = 0
    while i < 8:
        print(fibonacci(i))
        i += 1
    
    # Prime checking
    print(is_prime(17))
    print(is_prime(18))

```

## Error

```
Assembly compilation failed:

error[CS0103]: The name 'cache' does not exist in the current context
  --> /tmp/tmp0oggx7wz/math_utils.spy:12:13
    |
 12 |     
    |     ^
    |

error[CS0103]: The name 'cache' does not exist in the current context
  --> /tmp/tmp0oggx7wz/math_utils.spy:13:20
    |
 13 |     sentence: str = "the quick brown fox"
    |                    ^
    |

error[CS0103]: The name 'cache' does not exist in the current context
  --> /tmp/tmp0oggx7wz/math_utils.spy:18:9
    |
 18 |     print(factorial(n))
    |         ^
    |

error[CS0103]: The name 'result_1' does not exist in the current context
  --> /tmp/tmp0oggx7wz/math_utils.spy:18:20
    |
 18 |     print(factorial(n))
    |                    ^
    |

error[CS0103]: The name 'result_1' does not exist in the current context
  --> /tmp/tmp0oggx7wz/math_utils.spy:19:16
    |
 19 |     
    |     ^
    |


```

## Timing

- Generation: 140.33s
- Execution: 4.75s
