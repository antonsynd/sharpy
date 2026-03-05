# Skipped Dogfood Run

**Timestamp:** 2026-03-04T17:39:20.415255
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0203]: Type 'SignedNumber' has no member 'unwrap'
  --> /tmp/tmpdk2zqili/dogfood_test.spy:107:15
     |
 107 |         print(maybe_num.unwrap().value)
     |               ^^^^^^^^^^^^^^^^
     |


**Feature Focus:** dunder_unary
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test dunder_unary operators (__neg__, __pos__, __invert__)
# with inheritance and clamping behavior
type SignedByte = int

enum SignCategory:
    NEGATIVE = -1
    ZERO = 0
    POSITIVE = 1

@abstract
class Signable:
    @abstract
    def category(self) -> SignCategory:
        ...

class SignedNumber(Signable):
    value: SignedByte

    def __init__(self, v: SignedByte):
        self.value = v

    @virtual
    def __neg__(self) -> SignedByte:
        """Unary minus: negate the value"""
        return -self.value

    @virtual
    def __pos__(self) -> SignedByte:
        """Unary plus: return absolute value"""
        if self.value < 0:
            return -self.value
        return self.value

    @virtual
    def __invert__(self) -> SignedByte:
        """Bitwise NOT, masked to 8 bits"""
        return ~self.value & 0xFF

    @override
    def category(self) -> SignCategory:
        if self.value < 0:
            return SignCategory.NEGATIVE
        elif self.value > 0:
            return SignCategory.POSITIVE
        return SignCategory.ZERO

class ClampedNumber(SignedNumber):
    limit: SignedByte

    def __init__(self, v: SignedByte, lim: SignedByte):
        super().__init__(v)
        self.limit = lim

    @override
    def __neg__(self) -> SignedByte:
        """Bounded negation"""
        raw = super().__neg__()
        if raw > self.limit:
            return self.limit
        if raw < -self.limit:
            return -self.limit
        return raw

    @override
    def __pos__(self) -> SignedByte:
        """Bounded absolute value"""
        raw = super().__pos__()
        if raw > self.limit:
            return self.limit
        return raw

def test_cases() -> tuple[SignedByte, SignedByte]:
    """Generator yielding test value pairs (input, limit)"""
    yield (10, 5)
    yield (-30, 20)
    yield (100, 50)

def main():
    # Basic SignedNumber tests
    a = SignedNumber(-8)
    b = SignedNumber(42)

    # Test __neg__, __pos__, __invert__ on SignedNumber
    print(-a)
    print(+a)
    print(~a)
    print(a.category().value)

    # Test with positive number
    print(-b)
    print(+b)
    print(~b)
    print(b.category().value)

    # ClampedNumber tests with control flow
    for val, lim in test_cases():
        c = ClampedNumber(val, lim)
        negated = -c
        absolute = +c
        print(negated)
        print(absolute)

    # Test with Optional type - use unwrap() after narrowing
    maybe_num: SignedNumber? = None()
    if maybe_num is not None:
        # This is one way - but still need unwrap() for unary operators
        print(maybe_num.unwrap().value)
    # Instead, assign a concrete value then test
    num = SignedNumber(-15)
    print(num.value)
    print(-num)
    print(+num)

```

## Timing

- Generation: 818.37s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
