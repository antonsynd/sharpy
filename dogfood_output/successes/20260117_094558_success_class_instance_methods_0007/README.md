# Successful Dogfood Run

**Timestamp:** 2026-01-17T09:45:32.585590
**Feature Focus:** class_instance_methods
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Testing class instance methods with a BankAccount class

class BankAccount:
    balance: int
    account_id: int
    
    def __init__(self, account_id: int, initial_balance: int):
        self.account_id = account_id
        self.balance = initial_balance
    
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

account1 = BankAccount(101, 500)
account2 = BankAccount(102, 200)

print(account1.get_balance())
print(account2.get_balance())

account1.deposit(100)
print(account1.get_balance())

success = account1.withdraw(150)
print(success)
print(account1.get_balance())

transferred = account1.transfer_to(account2, 200)
print(transferred)
print(account1.get_balance())
print(account2.get_balance())

# EXPECTED OUTPUT:
# 500
# 200
# 600
# True
# 450
# True
# 250
# 400
```

## Output

```
Successfully compiled to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/dogfood_test_39f23659f8454d11a5955348168193b7.exe

=== Running Program ===

500
200
600
True
450
True
250
400
```

## Timing

- Generation: 7.14s
- Execution: 1.30s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
