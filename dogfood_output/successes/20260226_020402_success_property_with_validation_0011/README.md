# Successful Dogfood Run

**Timestamp:** 2026-02-26T02:02:25.054964
**Feature Focus:** property_with_validation
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Properties with validation and inheritance
# Validates computed properties, backing fields, and cross-field validation

enum AccountStatus:
    ACTIVE = 0
    FROZEN = 1
    CLOSED = 2

type Balance = float

class BankAccount:
    _balance: Balance
    _status: AccountStatus
    
    property get status(self) -> AccountStatus:
        return self._status
    
    property set status(self, value: AccountStatus):
        self._status = value
    
    property get balance(self) -> Balance:
        if self._status == AccountStatus.FROZEN:
            print("Warning: account frozen")
        return self._balance
    
    property set balance(self, value: Balance):
        if value < 0.0:
            raise ValueError("Balance cannot be negative")
        self._balance = value
    
    def __init__(self, initial: Balance):
        self._balance = 0.0
        self._status = AccountStatus.ACTIVE
        self.balance = initial  # Uses setter with validation
    
    @virtual
    def can_withdraw(self, amount: Balance) -> bool:
        return self._status == AccountStatus.ACTIVE and amount <= self._balance

class SavingsAccount(BankAccount):
    _interest_rate: float
    
    property get interest_rate(self) -> float:
        return self._interest_rate
    
    property set interest_rate(self, value: float):
        if value < 0.0 or value > 1.0:
            raise ValueError("Rate must be between 0.0 and 1.0")
        self._interest_rate = value
    
    property get projected_annual(self) -> Balance:
        return self.balance * (1.0 + self._interest_rate)
    
    def __init__(self, initial: Balance, rate: float):
        super().__init__(initial)
        self._interest_rate = 0.0
        self.interest_rate = rate  # Uses setter with validation
    
    @override
    def can_withdraw(self, amount: Balance) -> bool:
        minimum: Balance = 100.0
        if self.balance - amount < minimum:
            return False
        return super().can_withdraw(amount)

def main():
    # Test basic property with validation
    account = SavingsAccount(500.0, 0.05)
    print(account.balance)
    
    # Test computed property
    print(account.projected_annual)
    
    # Test validation in setter
    account.balance = 300.0
    print(account.balance)
    
    # Test status property
    account.status = AccountStatus.FROZEN
    print(account.status.value)
    
    # Test warning on frozen account
    print(account.balance)
    
    # Test can_withdraw with inheritance
    account.status = AccountStatus.ACTIVE
    print(account.can_withdraw(450.0))
    print(account.can_withdraw(50.0))
    
    # Update interest rate
    account.interest_rate = 0.08
    print(account.projected_annual)
```

## Output

```
500.0
525.0
300.0
1
Warning: account frozen
300.0
False
True
324.0
```

## Timing

- Generation: 87.17s
- Execution: 4.52s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
