# Issue Report: compilation_failed

**Timestamp:** 2026-03-03T05:02:20.501139
**Type:** compilation_failed
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point testing module imports with class-based function objects

from utils import StringValidator, ListScaler
from math_utils import find_best, composite_transform, process_strings
from math_utils import GreaterThanComparator, LessThanComparator
from math_utils import Doubler, Tripler, StartsWithA, LengthChecker


def main():
    # Test StringValidator
    sv = StringValidator(3, 8)
    print(sv.validate("hi"))
    print(sv.validate("hello"))
    print(sv.validate("supercalifragilistic"))
    
    # Test ListScaler with static method and instance method
    ls = ListScaler(5)
    print(ListScaler.identity(42))
    
    scaled: list[int] = ls.apply_to_list([1, 2, 3])
    for val in scaled:
        print(val)
    
    # Test comparator using class-based approach
    gt = GreaterThanComparator()
    numbers: list[int] = [3, 7, 2, 8, 4]
    print(find_best(numbers, gt))
    
    # Test composite transformation
    doubler = Doubler()
    tripler = Tripler()
    print(composite_transform(5, doubler, tripler))
    
    # Test string predicate
    starts_a = StartsWithA()
    words: list[str] = ["apple", "banana", "apricot", "cherry"]
    print(process_strings(words, starts_a))

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'object' does not contain a definition for 'Compare' and no accessible extension method 'Compare' accepting a first argument of type 'object' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp89b57efd/math_utils.spy:35:21
    |
 35 |     starts_a = StartsWithA()
    |                     ^
    |

error[CS1061]: 'object' does not contain a definition for 'Transform' and no accessible extension method 'Transform' accepting a first argument of type 'object' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp89b57efd/math_utils.spy:41:32

error[CS1061]: 'object' does not contain a definition for 'Transform' and no accessible extension method 'Transform' accepting a first argument of type 'object' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp89b57efd/math_utils.spy:41:19

error[CS1061]: 'object' does not contain a definition for 'Check' and no accessible extension method 'Check' accepting a first argument of type 'object' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp89b57efd/math_utils.spy:47:22


```

## Compiler Output

```
warning[SPY0452]: Imported name 'LessThanComparator' is never used
  --> /tmp/tmp89b57efd/main.spy:5:47
    |
  5 | from math_utils import GreaterThanComparator, LessThanComparator
    |                                               ^^^^^^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'LengthChecker' is never used
  --> /tmp/tmp89b57efd/main.spy:6:55
    |
  6 | from math_utils import Doubler, Tripler, StartsWithA, LengthChecker
    |                                                       ^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 255.87s
- Execution: 4.70s
