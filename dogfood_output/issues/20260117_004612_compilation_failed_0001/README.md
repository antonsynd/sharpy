# Issue Report: compilation_failed

**Timestamp:** 2026-01-17T00:45:49.924506
**Type:** compilation_failed
**Feature Focus:** class_instance_methods
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Testing class instance methods with a BankAccount class

class BankAccount:
    balance: int
    account_name: str

    def __init__(self, name: str, initial: int):
        self.account_name = name
        self.balance = initial

    def deposit(self, amount: int) -> None:
        self.balance += amount

    def withdraw(self, amount: int) -> bool:
        if amount <= self.balance:
            self.balance -= amount
            return True
        return False

    def get_balance(self) -> int:
        return self.balance

    def transfer_to(self, other: BankAccount, amount: int) -> bool:
        if self.withdraw(amount):
            other.deposit(amount)
            return True
        return False

account1 = BankAccount("Alice", 100)
account2 = BankAccount("Bob", 50)

print(account1.get_balance())
print(account2.get_balance())

account1.deposit(25)
print(account1.get_balance())

success: bool = account1.transfer_to(account2, 75)
print(success)
print(account1.get_balance())
print(account2.get_balance())

# EXPECTED OUTPUT:
# 100
# 50
# 125
# True
# 50
# 125
```

## Error

```
Assembly compilation failed:
  dogfood_test.cs(55,38): error CS0103: The name 'account1' does not exist in the current context
  dogfood_test.cs(55,58): error CS0103: The name 'account2' does not exist in the current context

```

## Timing

- Generation: 6.36s
- Execution: 1.30s
