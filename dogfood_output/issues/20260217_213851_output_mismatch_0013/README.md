# Issue Report: output_mismatch

**Timestamp:** 2026-02-17T21:35:20.845965
**Type:** output_mismatch
**Feature Focus:** virtual_override
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test virtual/override with payment processing hierarchy
class PaymentProcessor:
    processor_name: str
    
    def __init__(self, name: str):
        self.processor_name = name
    
    @virtual
    def process(self, amount: float) -> str:
        return f"Processing ${amount} via generic"
    
    @virtual
    def calculate_fee(self, amount: float) -> float:
        return 0.0
    
    def get_name(self) -> str:
        return self.processor_name

class CreditCardProcessor(PaymentProcessor):
    last_four: str
    
    def __init__(self, name: str, card_tail: str):
        super().__init__(name)
        self.last_four = card_tail
    
    @override
    def process(self, amount: float) -> str:
        base = super().process(amount)
        return f"{base} using card ...{self.last_four}"
    
    @override
    def calculate_fee(self, amount: float) -> float:
        return amount * 0.029

class BankTransferProcessor(PaymentProcessor):
    minimum_fee: float
    
    def __init__(self, name: str, min_fee: float):
        super().__init__(name)
        self.minimum_fee = min_fee
    
    @override
    def calculate_fee(self, amount: float) -> float:
        calculated = amount * 0.01
        if calculated < self.minimum_fee:
            return self.minimum_fee
        return calculated

def main():
    credit = CreditCardProcessor("Stripe", "4242")
    bank = BankTransferProcessor("ACH", 5.0)
    amount = 100.0
    
    print(credit.get_name())
    print(credit.process(amount))
    print(credit.calculate_fee(amount))
    print(bank.process(amount))
    print(bank.calculate_fee(amount))
    
    # EXPECTED OUTPUT:
    # Stripe
    # Processing $100.0 via generic using card ...4242
    # 2.9
    # Processing $100.0 via generic
    # 5.0
```

## Error

```
AI verification backend failure
```

## Output Comparison

### Expected
```
Stripe
Processing $100.0 via generic using card ...4242
2.9
Processing $100.0 via generic
5.0

```

### Actual
```
Stripe
Processing $100 via generic using card ...4242
2.9000000000000004
Processing $100 via generic
5.0
```

## Timing

- Generation: 110.98s
- Execution: 4.52s
