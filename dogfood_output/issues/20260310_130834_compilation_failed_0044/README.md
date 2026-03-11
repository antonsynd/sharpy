# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T13:05:25.970628
**Type:** compilation_failed
**Feature Focus:** access_modifiers
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test access modifiers with protected inheritance and encapsulation

class BankAccount:
    _balance: float
    _transaction_count: int

    def __init__(self, initial: float):
        self._balance = initial
        self._transaction_count = 0

    @protected
    def record_transaction(self) -> None:
        self._transaction_count += 1

    @protected
    def get_balance(self) -> float:
        return self._balance

    @private
    def audit_log(self, msg: str) -> None:
        print(f"Audit: {msg}")

    @virtual
    def deposit(self, amount: float) -> None:
        self._balance += amount
        self.record_transaction()

class SavingsAccount(BankAccount):
    interest_rate: float

    def __init__(self, initial: float, rate: float):
        super().__init__(initial)
        self.interest_rate = rate

    @override
    def deposit(self, amount: float) -> None:
        # Can access protected members from subclass
        current = self.get_balance()
        self._balance = current + amount
        self.record_transaction()

    def apply_interest(self) -> None:
        interest = self.get_balance() * self.interest_rate
        self._balance += interest

def main():
    account = SavingsAccount(100.0, 0.05)
    account.deposit(50.0)
    account.apply_interest()
    # Verify protected access works (visible within class hierarchy)
    print(account.get_balance())

```

## Error

```
Assembly compilation failed:

error[CS0122]: 'DogfoodTest.BankAccount.GetBalance()' is inaccessible due to its protection level
  --> /tmp/tmppkn79wpt/dogfood_test.spy:51:47
    |
 51 |     print(account.get_balance())
    |                                 ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmppkn79wpt/dogfood_test.cs

```

## Timing

- Generation: 167.99s
- Execution: 4.97s
