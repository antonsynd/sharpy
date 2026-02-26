# Skipped Dogfood Run

**Timestamp:** 2026-02-25T04:23:53.748568
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0200]: Undefined identifier 'factorial'
  --> /tmp/tmp6d1ksz2n/dogfood_test.spy:41:20
    |
 41 |         return n * factorial(n - 1)
    |                    ^^^^^^^^^
    |

error[SPY0200]: Undefined identifier 'factorial'
  --> /tmp/tmp6d1ksz2n/dogfood_test.spy:43:19
    |
 43 |     fact_5: int = factorial(5)
    |                   ^^^^^^^^^
    |

error[SPY0200]: Undefined identifier 'min'. Did you mean 'main'?
  --> /tmp/tmp6d1ksz2n/dogfood_test.spy:74:20
    |
 74 |     min_val: int = min([50, 20, 80, 10, 60])
    |                    ^^^
    |

error[SPY0200]: Undefined identifier 'max'. Did you mean 'x'?
  --> /tmp/tmp6d1ksz2n/dogfood_test.spy:75:20
    |
 75 |     max_val: int = max([50, 20, 80, 10, 60])
    |                    ^^^
    |


**Feature Focus:** integer_variables
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
def main():
    x: int = 42
    y: int = -17
    z: int = 123456789

    print(x)
    print(y)
    print(z)

    sum_val: int = x + y
    diff_val: int = x - y
    prod_val: int = x * y
    div_val: int = z // x
    mod_val: int = z % x

    print(sum_val)
    print(diff_val)
    print(prod_val)
    print(div_val)
    print(mod_val)

    counter: int = 0
    counter += 10
    counter -= 3
    counter *= 2
    counter //= 5

    print(counter)

    total: int = 0
    i: int = 0
    while i < 5:
        total = total + i
        i = i + 1

    print(total)

    def factorial(n: int) -> int:
        if n <= 1:
            return 1
        return n * factorial(n - 1)

    fact_5: int = factorial(5)
    print(fact_5)

    def div_mod(a: int, b: int) -> tuple[quot: int, rem: int]:
        return (quot=a // b, rem=a % b)

    result: tuple[quot: int, rem: int] = div_mod(17, 5)
    print(result.quot)
    print(result.rem)

    bit_and: int = 0b1100 & 0b1010
    bit_or: int = 0b1100 | 0b1010
    bit_xor: int = 0b1100 ^ 0b1010
    bit_not: int = ~0b00001111
    bit_lsh: int = 1 << 8
    bit_rsh: int = 256 >> 4

    print(bit_and)
    print(bit_or)
    print(bit_xor)
    print(bit_not)
    print(bit_lsh)
    print(bit_rsh)

    power_val: int = 2 ** 10
    print(power_val)

    abs_val: int = abs(-100)
    print(abs_val)

    # Min/max with integers
    min_val: int = min([50, 20, 80, 10, 60])
    max_val: int = max([50, 20, 80, 10, 60])
    print(min_val)
    print(max_val)

    neg_zero: int = -0
    print(neg_zero)

    large: int = 999999999
    small: int = -999999999
    print(large)
    print(small)

    is_greater: bool = x > y
    print(is_greater)

    if (val := 25) > 20:
        print(val)

    squares: list[int] = [n * n for n in [1, 2, 3, 4, 5]]
    print(squares[0])
    print(squares[4])

    list_sum: int = sum([1, 2, 3, 4, 5, 6, 7, 8, 9, 10])
    print(list_sum)

# EXPECTED OUTPUT:
# 42
# -17
# 123456789
# 25
# 59
# -714
# 2939209
# 27
# 2
# 10
# 120
# 3
# 2
# 8
# 14
# 2
# -16
# 256
# 16
# 1024
# 100
# 10
# 80
# 0
# 999999999
# -999999999
# True
# 25
# 1
# 25
# 55
```

## Timing

- Generation: 589.10s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.18).

This output is saved for inspection to help improve prompting.
