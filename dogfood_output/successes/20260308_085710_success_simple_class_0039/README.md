# Successful Dogfood Run

**Timestamp:** 2026-03-08T08:56:05.040818
**Feature Focus:** simple_class
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Simple class with properties, static tracking, and tuple unpacking
# Combines auto-properties, static fields, and __str__ for a library system

class Book:
    @static
    total_books: int = 0
    
    property title: str
    property author: str
    property get display_info(self) -> str:
        return f"'{self.title}' by {self.author}"
    
    def __init__(self, title: str, author: str):
        self.title = title
        self.author = author
        Book.total_books += 1
    
    def __str__(self) -> str:
        return f"[Book: {self.title}]"
    
    def get_details(self) -> tuple[str, str]:
        return (self.title, self.author)

def main():
    # Create instances
    book1 = Book("Python Guide", "Alice Smith")
    book2 = Book("Code Patterns", "Bob Jones")
    
    # Print using __str__
    print(book1)
    
    # Property access
    print(book2.title)
    
    # Computed property
    info: str = book1.display_info
    print(info)
    
    # Tuple unpacking from method
    title, author = book2.get_details()
    print(title)
    print(author)
    
    # Static field tracking
    print(Book.total_books)

```

## Output

```
[Book: Python Guide]
Code Patterns
'Python Guide' by Alice Smith
Code Patterns
Bob Jones
2
```

## Timing

- Generation: 54.53s
- Execution: 5.27s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
