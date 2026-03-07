# Successful Dogfood Run

**Timestamp:** 2026-03-07T07:16:49.752372
**Feature Focus:** class_with_init
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
class Product:
    name: str
    price: int
    quantity: int

    def __init__(self, n: str, p: int, q: int):
        self.name = n
        self.price = p
        self.quantity = q

    def total_value(self) -> int:
        return self.price * self.quantity

    def restock(self, amount: int) -> None:
        self.quantity += amount

def main():
    item = Product("Widget", 25, 4)
    print(item.total_value())
    item.restock(2)
    print(item.total_value())

```

## Output

```
100
150
```

## Timing

- Generation: 35.50s
- Execution: 4.42s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
