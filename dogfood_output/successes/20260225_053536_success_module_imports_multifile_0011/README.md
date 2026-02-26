# Successful Dogfood Run

**Timestamp:** 2026-02-25T05:29:18.095709
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude
**Test Type:** Multi-file (5 files)

## Source Files

### item_types.spy

```python
# Module defining enums, interfaces, and structs for library items

# Enum for item status
enum ItemStatus:
    AVAILABLE = 0
    CHECKED_OUT = 1
    RESERVED = 2
    LOST = 3

# Enum for item condition
enum Condition:
    NEW = 0
    GOOD = 1
    FAIR = 2
    POOR = 3

# Interface for loanable items
interface ILoanable:
    def get_loan_period(self) -> int: ...
    def can_renew(self) -> bool: ...
    property get_late_fee: float

# Struct for representing monetary amounts
struct Money:
    cents: int
    
    def __init__(self, dollars: int, cents_val: int):
        self.cents = dollars * 100 + cents_val
    
    def to_dollars(self) -> float:
        return float(self.cents) / 100.0
```

### library_base.spy

```python
# Base module for library item classes
from item_types import ItemStatus, Condition, ILoanable, Money

# Abstract base class for all library items
@abstract
class LibraryItem:
    title: str
    item_id: int
    status: ItemStatus
    condition: Condition
    
    def __init__(self, title: str, item_id: int):
        self.title = title
        self.item_id = item_id
        self.status = ItemStatus.AVAILABLE
        self.condition = Condition.NEW
    
    @virtual
    def get_description(self) -> str:
        return self.title
    
    @virtual
    def get_item_type(self) -> str:
        return "Unknown"
    
    def check_out(self) -> bool:
        if self.status == ItemStatus.AVAILABLE:
            self.status = ItemStatus.CHECKED_OUT
            return True
        return False
    
    def return_item(self) -> None:
        if self.status == ItemStatus.CHECKED_OUT:
            self.status = ItemStatus.AVAILABLE
    
    def get_status_string(self) -> str:
        if self.status == ItemStatus.AVAILABLE:
            return "Available"
        elif self.status == ItemStatus.CHECKED_OUT:
            return "Checked Out"
        elif self.status == ItemStatus.RESERVED:
            return "Reserved"
        else:
            return "Lost"
```

### library_items.spy

```python
# Concrete library item implementations with inheritance and interface implementation
from item_types import ItemStatus, Condition, ILoanable, Money
from library_base import LibraryItem

# Book class with loanable interface
class Book(LibraryItem, ILoanable):
    author: str
    page_count: int
    isbn: str
    
    def __init__(self, title: str, item_id: int, author: str, pages: int, isbn: str):
        super().__init__(title, item_id)
        self.author = author
        self.page_count = pages
        self.isbn = isbn
    
    @override
    def get_item_type(self) -> str:
        return "Book"
    
    @override
    def get_description(self) -> str:
        return self.title + " by " + self.author
    
    # ILoanable implementation - NO @override for interface methods
    def get_loan_period(self) -> int:
        return 21  # 3 weeks for books
    
    def can_renew(self) -> bool:
        return True
    
    property get_late_fee: float = 0.25  # $0.25 per day

# DVD class with loanable interface and different terms
class DVD(LibraryItem, ILoanable):
    director: str
    runtime_minutes: int
    rating: str
    
    def __init__(self, title: str, item_id: int, director: str, runtime: int, rating: str):
        super().__init__(title, item_id)
        self.director = director
        self.runtime_minutes = runtime
        self.rating = rating
    
    @override
    def get_item_type(self) -> str:
        return "DVD"
    
    @override
    def get_description(self) -> str:
        return self.title + " (" + self.rating + ") directed by " + self.director
    
    # ILoanable implementation - NO @override for interface methods
    def get_loan_period(self) -> int:
        return 7  # 1 week for DVDs
    
    def can_renew(self) -> bool:
        return self.rating != "Reference"
    
    property get_late_fee: float = 1.0  # $1.00 per day

# Magazine class - not loanable, reference only
class Magazine(LibraryItem):
    issue_number: int
    month: int
    year: int
    
    def __init__(self, title: str, item_id: int, issue: int, month: int, year: int):
        super().__init__(title, item_id)
        self.issue_number = issue
        self.month = month
        self.year = year
    
    @override
    def get_item_type(self) -> str:
        return "Magazine"
    
    @override
    def get_description(self) -> str:
        return self.title + " - Issue " + str(self.issue_number) + ", " + str(self.month) + "/" + str(self.year)
```

### library_utils.spy

```python
# Utility module for library operations
from library_items import Book, DVD, Magazine
from item_types import ILoanable
from library_base import LibraryItem

# Utility class for library statistics
class LibraryStats:
    total_items: int
    loanable_items: int
    
    def __init__(self):
        self.total_items = 0
        self.loanable_items = 0
    
    def analyze_item(self, item: LibraryItem) -> None:
        self.total_items = self.total_items + 1
        # Check if item implements ILoanable using isinstance
        if isinstance(item, ILoanable):
            self.loanable_items = self.loanable_items + 1
    
    def get_ratio(self) -> float:
        if self.total_items == 0:
            return 0.0
        return float(self.loanable_items) / float(self.total_items) * 100.0

# Function to calculate total late fees for loanable items
def calculate_potential_fees(items: list[LibraryItem]) -> float:
    total: float = 0.0
    for item in items:
        if isinstance(item, ILoanable):
            loanable: ILoanable = item
            total = total + loanable.get_late_fee
    return total
```

### main.spy

```python
# Main entry point demonstrating complex multi-file imports
from item_types import ItemStatus, Condition, ILoanable, Money
from library_base import LibraryItem
from library_items import Book, DVD, Magazine
from library_utils import LibraryStats, calculate_potential_fees

def main():
    # Create a collection of library items
    items: list[LibraryItem] = []
    
    # Add books
    book1: Book = Book("The Great Gatsby", 1001, "F. Scott Fitzgerald", 180, "978-0743273565")
    book2: Book = Book("1984", 1002, "George Orwell", 328, "978-0451524935")
    
    # Add DVDs
    dvd1: DVD = DVD("The Godfather", 2001, "Francis Ford Coppola", 175, "R")
    dvd2: DVD = DVD("Citizen Kane", 2002, "Orson Welles", 119, "PG")
    
    # Add magazines
    mag1: Magazine = Magazine("National Geographic", 3001, 245, 6, 2024)
    
    # Populate list
    items.append(book1)
    items.append(book2)
    items.append(dvd1)
    items.append(dvd2)
    items.append(mag1)
    
    # Print 1: Total items count
    print(len(items))
    
    # Analyze items with stats utility
    stats: LibraryStats = LibraryStats()
    for item in items:
        stats.analyze_item(item)
    
    # Print 2: Stats summary
    print(stats.total_items)
    print(stats.loanable_items)
    
    # Test interface dispatch with ILoanable
    loanable_count: int = 0
    for item in items:
        if isinstance(item, ILoanable):
            loanable_count = loanable_count + 1
    
    # Print 4: Count of loanable items (should be 4 - 2 books + 2 DVDs)
    print(loanable_count)
    
    # Test loan periods for different item types
    # Print 5-6: Loan periods for book and DVD
    print(book1.get_loan_period())
    print(dvd1.get_loan_period())
    
    # Test late fees
    # Print 7: Potential late fees total
    total_fees: float = calculate_potential_fees(items)
    print(total_fees)
    
    # Test check out functionality
    status1: bool = book1.check_out()
    status2: bool = mag1.check_out()
    
    # Print 8-9: Check out results
    print(status1)  # True - books can be checked out
    print(status2)  # True - magazines can be checked out
    
    # Print 10-11: Status strings
    print(book1.get_status_string())
    print(mag1.get_status_string())
    
    # Print 12: Description using overridden method
    print(book1.get_description())
    
    # Print 13: Item type for different classes
    print(dvd1.get_item_type())
    
    # Test struct functionality
    price1: Money = Money(10, 99)
    # Print 14: Money struct to_dollars
    print(price1.to_dollars())

# EXPECTED OUTPUT:
# 5
# 5
# 4
# 4
# 21
# 7
# 2.5
# True
# True
# Checked Out
# Checked Out
# The Great Gatsby by F. Scott Fitzgerald
# DVD
# 10.99
```

## Timing

- Generation: 343.72s
- Execution: 4.62s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
