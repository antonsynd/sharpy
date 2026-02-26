# Issue Report: compilation_failed

**Timestamp:** 2026-02-26T01:56:27.184904
**Type:** compilation_failed
**Feature Focus:** class_static_methods
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test complex static method patterns with abstract factory and inheritance

type Price = float
type DiscountRate = float

@abstract
class InventoryItem:
    _item_count: int = 0
    
    @static
    def get_next_sku() -> str:
        InventoryItem._item_count += 1
        return f"SKU-{InventoryItem._item_count:04d}"
    
    @static
    def reset_count() -> None:
        InventoryItem._item_count = 0
    
    @static
    def get_item_count() -> int:
        return InventoryItem._item_count
    
    @abstract
    def get_value(self) -> Price: ...

class Electronics(InventoryItem):
    brand: str
    cost: Price
    markup: DiscountRate
    
    def __init__(self, brand: str, cost: Price):
        self.brand = brand
        self.cost = cost
        self.markup = 1.2
    
    @static
    def create_with_markup(brand: str, cost: Price, markup_pct: float) -> Electronics:
        item = Electronics(brand, cost)
        item.markup = 1.0 + markup_pct
        return item
    
    @static
    def create_discounted(brand: str, cost: Price, discount: float) -> Electronics:
        final = cost * (1.0 - discount)
        return Electronics(brand, final)
    
    @override
    def get_value(self) -> Price:
        return self.cost * self.markup

class Book(InventoryItem):
    title: str
    base_price: Price
    is_bulk: bool
    
    def __init__(self, title: str, price: Price):
        self.title = title
        self.base_price = price
        self.is_bulk = False
    
    @static
    def create_bulk_priced(title: str, price: Price, qty: int) -> Book:
        book = Book(title, price)
        if qty >= 10:
            book.base_price = price * 0.9
            book.is_bulk = True
        return book
    
    @static
    def compare_prices(b1: Book, b2: Book) -> str:
        if b1.base_price < b2.base_price:
            return b1.title
        return b2.title
    
    @override
    def get_value(self) -> Price:
        return self.base_price

def main():
    InventoryItem.reset_count()
    
    laptop = Electronics.create_discounted("TechCorp", 1000.0, 0.15)
    phone = Electronics.create_with_markup("OtherBrand", 500.0, 0.1)
    
    print(laptop.brand)
    print(laptop.get_value())
    print(phone.get_value())
    
    book1 = Book.create_bulk_priced("Python Guide", 50.0, 12)
    book2 = Book("Regular Book", 45.0)
    
    print(book1.title)
    print(book1.get_value())
    print(InventoryItem.compare_prices(book1, book2))
    
    print(InventoryItem.get_next_sku())
    print(InventoryItem.get_item_count())
```

## Error

```
Assembly compilation failed:

error[CS0120]: An object reference is required for the non-static field, method, or property 'DogfoodTest.InventoryItem._ItemCount'
  --> /tmp/tmpkpa2t2ag/dogfood_test.spy:12:13
    |
 12 |         InventoryItem._item_count += 1
    |             ^
    |

error[CS0120]: An object reference is required for the non-static field, method, or property 'DogfoodTest.InventoryItem._ItemCount'
  --> /tmp/tmpkpa2t2ag/dogfood_test.spy:12:40
    |
 12 |         InventoryItem._item_count += 1
    |                                       ^
    |

error[CS0120]: An object reference is required for the non-static field, method, or property 'DogfoodTest.InventoryItem._ItemCount'
  --> /tmp/tmpkpa2t2ag/dogfood_test.spy:13:56
    |
 13 |         return f"SKU-{InventoryItem._item_count:04d}"
    |                                                      ^
    |

error[CS0117]: 'DogfoodTest.InventoryItem' does not contain a definition for 'ComparePrices'
  --> /tmp/tmpkpa2t2ag/dogfood_test.spy:94:53
    |
 94 |     print(InventoryItem.compare_prices(book1, book2))
    |                                                     ^
    |

error[CS0120]: An object reference is required for the non-static field, method, or property 'DogfoodTest.InventoryItem._ItemCount'
  --> /tmp/tmpkpa2t2ag/dogfood_test.spy:17:13
    |
 17 |         InventoryItem._item_count = 0
    |             ^
    |

error[CS0120]: An object reference is required for the non-static field, method, or property 'DogfoodTest.InventoryItem._ItemCount'
  --> /tmp/tmpkpa2t2ag/dogfood_test.spy:21:20
    |
 21 |         return InventoryItem._item_count
    |                    ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpkpa2t2ag/dogfood_test.cs

```

## Timing

- Generation: 141.21s
- Execution: 4.27s
