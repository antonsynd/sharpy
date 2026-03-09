# Issue Report: output_mismatch

**Timestamp:** 2026-03-08T06:42:00.868589
**Type:** output_mismatch
**Feature Focus:** dunder_unary
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Dunder unary operators with financial positions
# Verifies __neg__, __pos__, and __invert__ on a SignedAmount class

class SignedAmount:
    dollars: int
    cents: int
    
    def __init__(self, dollars: int, cents: int):
        self.dollars = dollars
        self.cents = cents
    
    def __neg__(self) -> SignedAmount:
        # Negate both components (debt becomes credit, vice versa)
        return SignedAmount(-self.dollars, -self.cents)
    
    def __pos__(self) -> SignedAmount:
        # Absolute value - ensure positive
        if self.dollars < 0:
            return SignedAmount(-self.dollars, -self.cents)
        return SignedAmount(self.dollars, self.cents)
    
    def __invert__(self) -> int:
        # Return total cents as inverted sign (flipped debt indicator)
        total_cents = self.dollars * 100 + self.cents
        return -total_cents if total_cents > 0 else total_cents
    
    def __str__(self) -> str:
        sign = "-" if self.dollars < 0 or (self.dollars == 0 and self.cents < 0) else ""
        abs_dollars = self.dollars if self.dollars >= 0 else -self.dollars
        abs_cents = self.cents if self.cents >= 0 else -self.cents
        return f"{sign}${abs_dollars}.{abs_cents:02d}"

def main():
    # Start with a debt (negative position)
    debt: SignedAmount = SignedAmount(-50, 25)
    print(str(debt))
    
    # Negate to get credit position
    credit = -debt
    print(str(credit))
    
    # Positive of debt (absolute value)
    abs_debt = +debt
    print(str(abs_debt))
    
    # Invert gives cents value with sign flipped
    inverted: int = ~debt
    print(inverted)
    
    # Chain negations
    back_to_debt = -credit
    print(str(back_to_debt))

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
-$50.25
$50.25
$50.25
-5025
-$50.25

```

### Actual
```
-$50.25
$50.25
$50.25
-4975
-$50.25
```

## Timing

- Generation: 68.32s
- Execution: 4.96s
