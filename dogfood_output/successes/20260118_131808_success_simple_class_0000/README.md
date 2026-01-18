# Successful Dogfood Run

**Timestamp:** 2026-01-18T13:17:48.304779
**Feature Focus:** simple_class
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test simple class with field initialization and method operations

class BankAccount:
    balance: int
    account_holder: str
    is_active: bool

    def __init__(self, holder: str, initial_balance: int):
        self.account_holder = holder
        self.balance = initial_balance
        self.is_active = True

    def deposit(self, amount: int) -> None:
        if self.is_active:
            self.balance += amount

    def withdraw(self, amount: int) -> bool:
        if self.is_active and self.balance >= amount:
            self.balance -= amount
            return True
        return False

    def deactivate(self) -> None:
        self.is_active = False

    def get_balance(self) -> int:
        return self.balance

account = BankAccount("Alice", 1000)
print(account.get_balance())

account.deposit(500)
print(account.get_balance())

success: bool = account.withdraw(300)
print(success)
print(account.get_balance())

account.deactivate()
account.deposit(100)
print(account.get_balance())

# EXPECTED OUTPUT:
# 1000
# 1500
# True
# 1200
# 1200
```

## Output

```
Successfully compiled to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/dogfood_test_3e1a344e8ba04fe6a0cfaf53b90353dd.exe

=== Running Program ===

1000
1500
True
1200
1200
```

## Timing

- Generation: 4.49s
- Execution: 1.31s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
