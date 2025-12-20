# Contracts (Design by Contract)

Contracts enable formal specification of function preconditions, postconditions, and class invariants, bringing design-by-contract programming to Sharpy.

## Preconditions (`requires`)

Preconditions specify what must be true when a function is called:

```python
def divide(a: double, b: double) -> double:
    requires b != 0, "Divisor cannot be zero"
    return a / b

def sqrt(x: double) -> double:
    requires x >= 0, "Cannot take square root of negative number"
    return x ** 0.5
```

### Multiple Preconditions

```python
def binary_search(items: list[int], target: int) -> int?:
    requires len(items) > 0, "List cannot be empty"
    requires is_sorted(items), "List must be sorted"
    # ... implementation
```

## Postconditions (`ensures`)

Postconditions specify what must be true when a function returns:

```python
def divide(a: double, b: double) -> double:
    requires b != 0
    ensures result * b == a, "Division must be reversible"
    return a / b

def absolute_value(x: int) -> int:
    ensures result >= 0, "Result must be non-negative"
    ensures result == x or result == -x, "Result must be |x|"
    return x if x >= 0 else -x
```

### Using `result` Keyword

The special keyword `result` refers to the function's return value in postconditions:

```python
def factorial(n: int) -> int:
    requires n >= 0
    ensures result > 0, "Factorial is always positive"
    ensures n == 0 implies result == 1
    # ... implementation
```

## Class Invariants (`invariant`)

Invariants specify properties that must hold throughout an object's lifetime:

```python
class BankAccount:
    balance: decimal
    account_id: str
    
    invariant self.balance >= 0, "Balance cannot be negative"
    invariant len(self.account_id) > 0, "Account ID must be non-empty"
    
    def __init__(self, account_id: str):
        requires len(account_id) > 0
        self.account_id = account_id
        self.balance = 0
    
    def withdraw(self, amount: decimal) -> None:
        requires amount > 0
        requires amount <= self.balance, "Insufficient funds"
        self.balance -= amount
    
    def deposit(self, amount: decimal) -> None:
        requires amount > 0
        self.balance += amount
```

### Invariant Checking

Invariants are checked:
- After construction (`__init__`)
- Before and after each public method call
- Not checked for private methods (prefixed with `_`)

```python
class Stack[T]:
    _items: list[T]
    
    invariant len(self._items) >= 0, "Size cannot be negative"
    invariant self.is_empty() == (len(self._items) == 0)
    
    def push(self, item: T) -> None:
        # Invariant checked before and after
        self._items.append(item)
    
    def _internal_operation(self) -> None:
        # Invariant NOT checked for private methods
        pass
```

## Old Values (`old()`)

The `old()` function captures a value's state at function entry for use in postconditions:

```python
class BankAccount:
    balance: decimal
    
    def withdraw(self, amount: decimal) -> None:
        requires amount > 0
        requires amount <= self.balance
        ensures self.balance == old(self.balance) - amount
        self.balance -= amount
    
    def transfer(self, other: BankAccount, amount: decimal) -> None:
        requires amount > 0
        requires amount <= self.balance
        ensures self.balance == old(self.balance) - amount
        ensures other.balance == old(other.balance) + amount
        self.balance -= amount
        other.balance += amount
```

## Contract Inheritance

Derived classes must satisfy the Liskov Substitution Principle:
- Can **weaken** preconditions (accept more inputs)
- Can **strengthen** postconditions (provide more guarantees)
- Must **maintain** invariants

```python
class Animal:
    def make_sound(self, volume: int) -> None:
        requires volume > 0
        requires volume <= 100
        pass

class Dog(Animal):
    @override
    def make_sound(self, volume: int) -> None:
        # Can weaken precondition (accept more values)
        requires volume >= 0  # Accepts 0, base class didn't
        requires volume <= 100
        print("Woof!")
```

## Contract Checking Modes

Contracts can be checked at different levels:

| Mode | Preconditions | Postconditions | Invariants | Performance Impact |
|------|---------------|----------------|------------|-------------------|
| `debug` | ✅ | ✅ | ✅ | High |
| `release-checks` | ✅ | ❌ | ❌ | Medium |
| `release` | ❌ | ❌ | ❌ | None |

Configure via compiler flag: `--contract-checking=<mode>`

## Debug vs Release Behavior

```python
def divide(a: double, b: double) -> double:
    requires b != 0, "Divisor cannot be zero"
    return a / b

# In debug mode:
divide(10, 0)  # Throws ContractViolationException

# In release mode (--contract-checking=release):
divide(10, 0)  # Contracts removed, may crash or return invalid result
```

## Contract Violation Exceptions

When a contract is violated:

```python
class ContractViolationException(Exception):
    kind: str  # "precondition", "postcondition", or "invariant"
    message: str
    location: str

# Example violation
try:
    sqrt(-5)
except ContractViolationException as e:
    print(f"{e.kind} violated: {e.message}")
    # "precondition violated: Cannot take square root of negative number"
```

## Pure Functions

Functions used in contracts should be **pure** (no side effects):

```python
# ✅ Pure function - safe for contracts
def is_sorted(items: list[int]) -> bool:
    return all(items[i] <= items[i+1] for i in range(len(items)-1))

def binary_search(items: list[int], target: int) -> int?:
    requires is_sorted(items)  # OK - is_sorted is pure
    # ...

# ❌ Impure function - NOT safe for contracts
counter: int = 0

def increment_counter() -> int:
    counter += 1  # Side effect!
    return counter

def bad_example(x: int) -> int:
    requires increment_counter() > 0  # BAD - has side effects
    return x * 2
```

## Common Patterns

**Range Validation:**
```python
def set_age(self, age: int) -> None:
    requires age >= 0
    requires age <= 150, "Age must be reasonable"
    self.age = age
```

**Non-null Parameters:**
```python
def process(data: str) -> None:
    requires data is not None
    requires len(data) > 0, "Data cannot be empty"
    # ...
```

**State Validation:**
```python
class Connection:
    is_open: bool
    
    def send(self, data: bytes) -> None:
        requires self.is_open, "Connection must be open"
        # ...
```

**Result Validation:**
```python
def find_max(items: list[int]) -> int:
    requires len(items) > 0
    ensures result in items, "Result must be from the list"
    ensures all(result >= x for x in items), "Result must be maximum"
    return max(items)
```

## Static Analysis

The Sharpy compiler performs limited static analysis of contracts:

```python
def example(x: int) -> int:
    requires x > 0
    requires x < 0  # WARNING: contradictory requirements
    return x

def another(x: int) -> int:
    requires x > 0
    ensures result < 0  # WARNING: impossible with positive input
    return x
```

## C# Mapping

Contracts are implemented using guard checks and assertions:

```python
# Sharpy
def divide(a: double, b: double) -> double:
    requires b != 0, "Divisor cannot be zero"
    ensures result * b == a
    return a / b
```
```csharp
// C# 9.0 (debug mode)
public static double Divide(double a, double b)
{
    // Precondition
    if (!(b != 0))
        throw new ContractViolationException(
            "precondition", 
            "Divisor cannot be zero",
            "divide at line 2"
        );
    
    var __result = a / b;
    
    // Postcondition
    if (!(__result * b == a))
        throw new ContractViolationException(
            "postcondition",
            "Division must be reversible",
            "divide at line 3"
        );
    
    return __result;
}

// C# 9.0 (release mode - contracts removed)
public static double Divide(double a, double b)
{
    return a / b;
}
```

**Invariants:**
```python
# Sharpy
class BankAccount:
    balance: decimal
    invariant self.balance >= 0
    
    def withdraw(self, amount: decimal) -> None:
        self.balance -= amount
```
```csharp
// C# 9.0 (debug mode)
public class BankAccount
{
    private decimal _balance;
    
    private void CheckInvariant()
    {
        if (!(_balance >= 0))
            throw new ContractViolationException(
                "invariant",
                "Balance cannot be negative",
                "BankAccount"
            );
    }
    
    public void Withdraw(decimal amount)
    {
        CheckInvariant();  // Check before
        _balance -= amount;
        CheckInvariant();  // Check after
    }
}
```

## Limitations

- Contracts cannot modify program state
- No quantifiers (`forall`, `exists`) in C# 9 target
- Limited to expressions that can be evaluated at runtime
- Checking has performance cost (use release mode for production)

*Implementation: 🔄 Lowered - Transformed to runtime checks:*
- `requires`: Guard clause with exception throw
- `ensures`: Assertion after return value computation
- `invariant`: Method wrapper checks for public methods
- All checks removed in `release` mode

---
