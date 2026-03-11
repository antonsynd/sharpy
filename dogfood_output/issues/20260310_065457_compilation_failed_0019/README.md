# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T06:44:56.851086
**Type:** compilation_failed
**Feature Focus:** arithmetic_operators
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Complex arithmetic with operator overloading in expressions
# Tests: arithmetic operators +, -, *, /, //, %, **, negation

class ConstantExpression:
    _value: float

    property get value(self) -> float:
        return self._value

    def __init__(self, value: float):
        self._value = value

    def evaluate(self) -> float:
        return self._value

class BinaryExpression:
    left: ConstantExpression
    right: ConstantExpression
    op: str

    def __init__(self, left: ConstantExpression, right: ConstantExpression, op: str):
        self.left = left
        self.right = right
        self.op = op

    def evaluate(self) -> float:
        left_val = self.left.value
        right_val = self.right.value

        if self.op == "+":
            return left_val + right_val
        elif self.op == "-":
            return left_val - right_val
        elif self.op == "*":
            return left_val * right_val
        elif self.op == "/":
            return left_val / right_val
        elif self.op == "//":
            return float(int(left_val // right_val))
        elif self.op == "%":
            return left_val % right_val
        elif self.op == "**":
            return left_val ** right_val
        else:
            return 0.0

class NegatedExpression:
    inner: ConstantExpression

    def __init__(self, inner: ConstantExpression):
        self.inner = inner

    def evaluate(self) -> float:
        return -self.inner.value

def compute_stats(values: list[float]) -> tuple[float, float, float]:
    if len(values) == 0:
        return (0.0, 0.0, 0.0)

    min_val: float = values[0]
    max_val: float = values[0]
    sum_val: float = 0.0
    count: int = 0

    for val in values:
        sum_val += val
        count += 1
        if val < min_val:
            min_val = val
        if val > max_val:
            max_val = val

    avg_val: float = sum_val / float(count)
    return (min_val, max_val, avg_val)

def main():
    # Build expression: (10 * 3 - 15) ** 2 / 5 + (-4) % 3 = 45 + (-1) = 44
    step1 = ConstantExpression(10.0)
    step2 = ConstantExpression(3.0)
    step3 = ConstantExpression(15.0)
    step4 = ConstantExpression(2.0)
    step5 = ConstantExpression(5.0)
    step6 = ConstantExpression(4.0)
    step7 = ConstantExpression(3.0)

    inner = BinaryExpression(step1, step2, "*")
    minus = BinaryExpression(inner, step3, "-")
    power = BinaryExpression(minus, step4, "**")
    divide = BinaryExpression(power, step5, "/")
    neg = NegatedExpression(step6)
    modulo = BinaryExpression(neg, step7, "%")
    final = BinaryExpression(divide, modulo, "+")

    print(final.evaluate())

    # Floor division test: 17 // 5 = 3
    fd1 = ConstantExpression(17.0)
    fd2 = ConstantExpression(5.0)
    floor_test = BinaryExpression(fd1, fd2, "//")
    print(floor_test.evaluate())

    # Stats computation
    values: list[float] = [5.0, 10.0, 15.0, 8.0, -3.0]
    stats = compute_stats(values)
    print(stats[0])
    print(stats[1])
    print(stats[2])

    # Precedence test: (2 + 3) * (4 * 5) = 5 * 20 = 100
    p1a = ConstantExpression(2.0)
    p1b = ConstantExpression(3.0)
    p1 = BinaryExpression(p1a, p1b, "+")
    p2a = ConstantExpression(4.0)
    p2b = ConstantExpression(5.0)
    p2 = BinaryExpression(p2a, p2b, "*")
    precedence = BinaryExpression(p1, p2, "*")
    print(precedence.evaluate())

```

## Error

```
Assembly compilation failed:

error[CS1503]: Argument 1: cannot convert from 'DogfoodTest.BinaryExpression' to 'DogfoodTest.ConstantExpression'
  --> /tmp/tmpjaxjjbuk/dogfood_test.spy:87:42
    |
 87 |     minus = BinaryExpression(inner, step3, "-")
    |                                          ^
    |

error[CS1503]: Argument 1: cannot convert from 'DogfoodTest.BinaryExpression' to 'DogfoodTest.ConstantExpression'
  --> /tmp/tmpjaxjjbuk/dogfood_test.spy:88:42
    |
 88 |     power = BinaryExpression(minus, step4, "**")
    |                                          ^
    |

error[CS1503]: Argument 1: cannot convert from 'DogfoodTest.BinaryExpression' to 'DogfoodTest.ConstantExpression'
  --> /tmp/tmpjaxjjbuk/dogfood_test.spy:89:43
    |
 89 |     divide = BinaryExpression(power, step5, "/")
    |                                           ^
    |

error[CS1503]: Argument 1: cannot convert from 'DogfoodTest.NegatedExpression' to 'DogfoodTest.ConstantExpression'
  --> /tmp/tmpjaxjjbuk/dogfood_test.spy:91:43
    |
 91 |     modulo = BinaryExpression(neg, step7, "%")
    |                                           ^
    |

error[CS1503]: Argument 1: cannot convert from 'DogfoodTest.BinaryExpression' to 'DogfoodTest.ConstantExpression'
  --> /tmp/tmpjaxjjbuk/dogfood_test.spy:92:42
    |
 92 |     final = BinaryExpression(divide, modulo, "+")
    |                                          ^
    |

error[CS1503]: Argument 2: cannot convert from 'DogfoodTest.BinaryExpression' to 'DogfoodTest.ConstantExpression'
  --> /tmp/tmpjaxjjbuk/dogfood_test.spy:92:50
    |
 92 |     final = BinaryExpression(divide, modulo, "+")
    |                                                  ^
    |

error[CS1503]: Argument 1: cannot convert from 'DogfoodTest.BinaryExpression' to 'DogfoodTest.ConstantExpression'
  --> /tmp/tmpjaxjjbuk/dogfood_test.spy:116:47
     |
 116 |     precedence = BinaryExpression(p1, p2, "*")
     |                                               ^
     |

error[CS1503]: Argument 2: cannot convert from 'DogfoodTest.BinaryExpression' to 'DogfoodTest.ConstantExpression'
  --> /tmp/tmpjaxjjbuk/dogfood_test.spy:116:51
     |
 116 |     precedence = BinaryExpression(p1, p2, "*")
     |                                               ^
     |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpjaxjjbuk/dogfood_test.cs

```

## Timing

- Generation: 574.73s
- Execution: 4.85s
