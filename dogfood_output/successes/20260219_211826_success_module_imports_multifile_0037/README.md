# Successful Dogfood Run

**Timestamp:** 2026-02-19T21:11:56.265987
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude
**Test Type:** Multi-file (4 files)

## Source Files

### transaction.spy

```python
# Module defining transaction types and interface
enum TxType:
    DEPOSIT = 1
    WITHDRAWAL = 2
    TRANSFER = 3

interface ITransaction:
    def get_type(self) -> TxType: ...
    def get_amount(self) -> float: ...
    def describe(self) -> str: ...

struct TxMetadata:
    timestamp: str
    description: str

    def __init__(self, timestamp: str, description: str):
        self.timestamp = timestamp
        self.description = description

class Transaction(ITransaction):
    _type: TxType
    _amount: float
    _metadata: TxMetadata

    def __init__(self, tx_type: TxType, amount: float, metadata: TxMetadata):
        self._type = tx_type
        self._amount = amount
        self._metadata = metadata

    def get_type(self) -> TxType:
        return self._type

    def get_amount(self) -> float:
        return self._amount

    def describe(self) -> str:
        type_str: str = ""
        if self._type == TxType.DEPOSIT:
            type_str = "Deposit"
        elif self._type == TxType.WITHDRAWAL:
            type_str = "Withdrawal"
        else:
            type_str = "Transfer"
        return type_str + ": " + str(self._amount)
```

### bank_account.spy

```python
# Module with account classes and inheritance
from transaction import TxType, TxMetadata

@abstract
class Account:
    _balance: float
    _account_number: int

    def __init__(self, account_number: int, initial_balance: float):
        self._account_number = account_number
        self._balance = initial_balance

    @virtual
    def get_balance(self) -> float:
        return self._balance

    @abstract
    def get_account_type(self) -> str:
        ...

    def deposit(self, amount: float) -> None:
        self._balance = self._balance + amount

class SavingsAccount(Account):
    _interest_rate: float

    def __init__(self, account_number: int, initial_balance: float, interest_rate: float):
        super().__init__(account_number, initial_balance)
        self._interest_rate = interest_rate

    @override
    def get_account_type(self) -> str:
        return "Savings"

    def get_interest_rate(self) -> float:
        return self._interest_rate

class CheckingAccount(Account):
    _overdraft_limit: float

    def __init__(self, account_number: int, initial_balance: float, overdraft_limit: float):
        super().__init__(account_number, initial_balance)
        self._overdraft_limit = overdraft_limit

    @override
    def get_account_type(self) -> str:
        return "Checking"

    @override
    def get_balance(self) -> float:
        return self._balance + self._overdraft_limit
```

### customer.spy

```python
# Module for customer management and statistics
from bank_account import Account, SavingsAccount, CheckingAccount
from transaction import TxType

class Customer:
    name: str
    accounts: list[Account]

    def __init__(self, name: str):
        self.name = name
        self.accounts = []

    def add_account(self, account: Account) -> None:
        self.accounts.append(account)

    @staticmethod
    def calculate_avg_balance(accounts: list[Account]) -> float:
        if len(accounts) == 0:
            return 0.0
        total: float = 0.0
        for acc in accounts:
            total = total + acc.get_balance()
        return total / float(len(accounts))

def get_tx_type_name(tx_type: TxType) -> str:
    if tx_type == TxType.DEPOSIT:
        return "Deposit"
    elif tx_type == TxType.WITHDRAWAL:
        return "Withdrawal"
    return "Transfer"
```

### main.spy

```python
# Main entry point - cross-module imports and usage
from transaction import TxType, Transaction, TxMetadata
from bank_account import SavingsAccount, CheckingAccount, Account
from customer import Customer, get_tx_type_name

def main():
    # Create accounts from different module classes
    savings: SavingsAccount = SavingsAccount(1001, 5000.0, 0.025)
    checking: CheckingAccount = CheckingAccount(1002, 1000.0, 500.0)

    # Create customer and add accounts
    customer: Customer = Customer("Alice Johnson")
    customer.add_account(savings)
    customer.add_account(checking)

    # Print account types and balances
    print(savings.get_account_type())
    print(savings.get_balance())
    print(checking.get_account_type())
    print(checking.get_balance())

    # Create transaction using enum from another module
    tx: Transaction = Transaction(TxType.DEPOSIT, 250.5, TxMetadata("2024-01-15", "Paycheck deposit"))

    # Print transaction details
    print(tx.describe())
    print(get_tx_type_name(tx.get_type()))

    # Print account info via interface
    acc: Account = checking
    print(acc.get_account_type())

# EXPECTED OUTPUT:
# Savings
# 5000.0
# Checking
# 1500.0
# Deposit: 250.5
# Deposit
# Checking
```

## Timing

- Generation: 359.65s
- Execution: 4.61s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
