# Issue Report: internal_compiler_error

**Timestamp:** 2026-02-17T17:56:57.468026
**Type:** internal_compiler_error
**Feature Focus:** module_utils
**Complexity:** complex
**Backend:** copilot

## Generated Sharpy Code

```python
# Main library management system

from book_types import Book, BookStatus
from storage import LibraryStorage
from operations import LibraryOperations

def main():
    # Create storage and operations
    storage: LibraryStorage = LibraryStorage()
    library: LibraryOperations = LibraryOperations("City Library", storage)

    # Register books
    library.register_book("Python Basics", "John Smith", "ISBN-001")
    library.register_book("Advanced Programming", "Jane Doe", "ISBN-002")
    library.register_book("Data Structures", "Bob Johnson", "ISBN-003")

    # Show initial status
    print(library.get_status_summary())
    print(storage.get_available_count())

    # Checkout a book
    success: bool = library.checkout_book("ISBN-001")
    print(success)
    print(storage.get_available_count())

    # Try to checkout same book again (should fail)
    success2: bool = library.checkout_book("ISBN-001")
    print(success2)

    # Return the book
    return_success: bool = library.return_book("ISBN-001")
    print(return_success)
    print(storage.get_available_count())

# EXPECTED OUTPUT:
# City Library has 3 books
# 3
# True
# 2
# False
# True
# 3
```

## Error

```
Internal compiler error: Compilation errors:

error[SPY0907]: Internal: type inference produced UnknownType for 'FunctionCall' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmp00pd4fjq/main.spy:13:5
    |
 13 |     library.register_book("Python Basics", "John Smith", "ISBN-001")
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

error[SPY0907]: Internal: type inference produced UnknownType for 'FunctionCall' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmp00pd4fjq/main.spy:14:5
    |
 14 |     library.register_book("Advanced Programming", "Jane Doe", "ISBN-002")
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

error[SPY0907]: Internal: type inference produced UnknownType for 'FunctionCall' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmp00pd4fjq/main.spy:15:5
    |
 15 |     library.register_book("Data Structures", "Bob Johnson", "ISBN-003")
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 21.78s
