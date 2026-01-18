# Issue Report: output_mismatch

**Timestamp:** 2026-01-18T18:36:32.361584
**Type:** output_mismatch
**Feature Focus:** access_modifiers
**Complexity:** complex
**Backend:** claude

## Generated Sharpy Code

```python
# Test access modifiers with banking system hierarchy
# Tests: @private, @protected, @internal, public (default), static methods, inheritance

@abstract
class Account:
    balance: float
    account_id: int
    
    def __init__(self, id: int, initial_balance: float):
        self.account_id = id
        self.balance = initial_balance
    
    @private
    def log_transaction(self, amount: float) -> None:
        # Private method - only accessible within this class
        print(1000)
    
    @protected
    def validate_amount(self, amount: float) -> bool:
        # Protected - accessible in subclasses
        if amount > 0.0:
            return True
        return False
    
    @internal
    def get_internal_id(self) -> int:
        # Internal - accessible within assembly
        return self.account_id
    
    @virtual
    def deposit(self, amount: float) -> None:
        if self.validate_amount(amount):
            self.balance += amount
            self.log_transaction(amount)
    
    @abstract
    def withdraw(self, amount: float) -> bool:
        ...

class SavingsAccount(Account):
    withdrawal_limit: float
    withdrawal_count: int
    
    def __init__(self, id: int, balance: float, limit: float):
        super().__init__(id, balance)
        self.withdrawal_limit = limit
        self.withdrawal_count = 0
    
    @override
    def withdraw(self, amount: float) -> bool:
        # Uses protected validate_amount from parent
        if self.validate_amount(amount):
            if amount <= self.withdrawal_limit:
                if self.balance >= amount:
                    self.balance -= amount
                    self.withdrawal_count += 1
                    return True
        return False
    
    @private
    @static
    def calculate_interest_rate(balance: float) -> float:
        # Private static - only within SavingsAccount
        if balance > 10000.0:
            return 2.5
        return 1.5

class CheckingAccount(Account):
    overdraft_limit: float
    
    def __init__(self, id: int, balance: float, overdraft: float):
        super().__init__(id, balance)
        self.overdraft_limit = overdraft
    
    @override
    def withdraw(self, amount: float) -> bool:
        if self.validate_amount(amount):
            available = self.balance + self.overdraft_limit
            if amount <= available:
                self.balance -= amount
                return True
        return False
    
    @override
    def deposit(self, amount: float) -> None:
        # Override parent's virtual method
        super().deposit(amount)
        print(2000)

# Test public access (default)
savings = SavingsAccount(101, 5000.0, 1000.0)
print(savings.account_id)
print(savings.get_internal_id())

savings.deposit(500.0)
print(savings.balance)

success = savings.withdraw(800.0)
if success:
    print(4200)

print(savings.withdrawal_count)

# Test checking account with overdraft
checking = CheckingAccount(102, 1000.0, 500.0)
checking.deposit(250.0)
print(checking.balance)

withdraw_success = checking.withdraw(1100.0)
if withdraw_success:
    print(150)

# EXPECTED OUTPUT:
# 101
# 101
# 1000
# 5500.0
# 1000
# 4200
# 1
# 2000
# 1250.0
# 150
```

## Output Comparison

### Expected
```
101
101
1000
5500.0
1000
4200
1
2000
1250.0
150

```

### Actual
```
101
101
1000
5500
4200
1
1000
2000
1250
150
```

## Timing

- Generation: 19.47s
- Execution: 1.55s
