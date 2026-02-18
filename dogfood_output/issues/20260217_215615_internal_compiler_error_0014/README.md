# Issue Report: internal_compiler_error

**Timestamp:** 2026-02-17T21:44:48.755162
**Type:** internal_compiler_error
**Feature Focus:** dict_comprehension
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
def main():
    # Test basic dict comprehension with filtering (SIMPLE - allowed)
    nums = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]
    squares = {n: n * n for n in nums if n % 2 == 0}
    print(len(squares))
    
    # Test dict building with arithmetic (using loop instead of comprehension unpacking)
    source = {1: 10, 2: 20, 3: 30}
    doubled = {}
    for k in source:
        doubled[k] = source[k] * 2
    print(doubled[1])
    print(doubled[2])
    print(doubled[3])
    
    # Test nested conditional logic
    categories = {}
    for n in nums:
        if n % 2 == 0:
            if n % 4 == 0:
                categories[n] = "even_div_by_4"
            else:
                categories[n] = "even"
        else:
            categories[n] = "odd"
    
    # Filter categories using loop (NO unpacking in comprehension)
    div_by_4 = {}
    for k in categories:
        if categories[k] == "even_div_by_4":
            div_by_4[k] = categories[k]
    print(len(div_by_4))
    print(4 in div_by_4)
    print(8 in div_by_4)
    
    # Test dict comprehension from list (SIMPLE - allowed)
    words = ["apple", "banana", "cherry"]
    word_lengths = {w: len(w) for w in words if len(w) > 5}
    print(len(word_lengths))
    print(word_lengths["banana"])
    print(word_lengths["cherry"])
    
    # Test with range (SIMPLE - allowed)
    range_dict = {i: i * i for i in range(5) if i > 2}
    print(len(range_dict))
    print(range_dict[3])
    print(range_dict[4])
    
    # Test complex chaining: dict from dict from list
    # Use dict comprehension for first step (simple)
    base = {x: x * 2 for x in range(10) if x > 3}
    # Use loop for second step (no unpacking in comprehension)
    transformed = {}
    for k in base:
        v = base[k]
        if v > 10:
            transformed[k] = v + k
    print(len(transformed))
    print(transformed[5])
    print(transformed[6])

# EXPECTED OUTPUT:
# 5
# 20
# 40
# 60
# 2
# True
# True
# 1
# 6
# 6
# 2
# 9
# 16
# 5
# 15
# 18
```

## Error

```
Internal compiler error: Compilation errors:

error[SPY0907]: Internal: type inference produced UnknownType for 'IndexAccess' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmpskfesidl/dogfood_test.spy:11:9
    |
 11 |         doubled[k] = source[k] * 2
    |         ^^^^^^^^^^
    |

error[SPY0907]: Internal: type inference produced UnknownType for 'IndexAccess' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmpskfesidl/dogfood_test.spy:12:11
    |
 12 |     print(doubled[1])
    |           ^^^^^^^^^^
    |

error[SPY0907]: Internal: type inference produced UnknownType for 'IndexAccess' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmpskfesidl/dogfood_test.spy:13:11
    |
 13 |     print(doubled[2])
    |           ^^^^^^^^^^
    |

error[SPY0907]: Internal: type inference produced UnknownType for 'IndexAccess' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmpskfesidl/dogfood_test.spy:14:11
    |
 14 |     print(doubled[3])
    |           ^^^^^^^^^^
    |

error[SPY0907]: Internal: type inference produced UnknownType for 'IndexAccess' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmpskfesidl/dogfood_test.spy:21:17
    |
 21 |                 categories[n] = "even_div_by_4"
    |                 ^^^^^^^^^^^^^
    |

error[SPY0907]: Internal: type inference produced UnknownType for 'IndexAccess' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmpskfesidl/dogfood_test.spy:23:17
    |
 23 |                 categories[n] = "even"
    |                 ^^^^^^^^^^^^^
    |

error[SPY0907]: Internal: type inference produced UnknownType for 'IndexAccess' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmpskfesidl/dogfood_test.spy:25:13
    |
 25 |             categories[n] = "odd"
    |             ^^^^^^^^^^^^^
    |

error[SPY0907]: Internal: type inference produced UnknownType for 'IndexAccess' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmpskfesidl/dogfood_test.spy:57:13
    |
 57 |             transformed[k] = v + k
    |             ^^^^^^^^^^^^^^
    |

error[SPY0907]: Internal: type inference produced UnknownType for 'IndexAccess' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmpskfesidl/dogfood_test.spy:59:11
    |
 59 |     print(transformed[5])
    |           ^^^^^^^^^^^^^^
    |

error[SPY0907]: Internal: type inference produced UnknownType for 'IndexAccess' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmpskfesidl/dogfood_test.spy:60:11
    |
 60 |     print(transformed[6])
    |           ^^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 671.46s
