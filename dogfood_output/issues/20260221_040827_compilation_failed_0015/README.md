# Issue Report: compilation_failed

**Timestamp:** 2026-02-21T04:00:10.128479
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - demonstrates cross-module class usage
from models import Entity, Person, Product, create_test_person, create_test_product
from utils import describe_entity, show_identifiable, format_printable, process_entity_list, create_person_clone

def main():
    # Create instances using constructors from models module
    person: Person = Person(42, "Bob")
    product: Product = Product(7, "MOUSE-023", 29.99)
    
    # Test methods from models module
    print(person.to_display())
    print(product.to_display())
    
    # Test functions from utils module operating on models
    print(describe_entity(person))
    print(describe_entity(product))
    
    # Test interface usage across modules
    print(show_identifiable(person))
    
    # Test list processing with cross-module types
    entities: list[Entity] = [person, product]
    descriptions: list[str] = process_entity_list(entities)
    print(descriptions[0])
    print(descriptions[1])
    
    # Test cloning function from utils
    clone: Person = create_person_clone(person)
    print(clone.to_display())
    
    # Test factory functions from models
    test_person: Person = create_test_person()
    test_product: Product = create_test_product()
    print(format_printable(test_person))
    print(format_printable(test_product))

# EXPECTED OUTPUT:
# Person(Bob, id=42)
# Product(MOUSE-023, $29.99)
# Entity with ID: 4200
# Entity with ID: 700
# ID=4200
# Person(Bob, id=42)
# Person(Alice, id=1)
# Product(LAPTOP-001, $999.99)
```

## Error

```
Assembly compilation failed:

error[CS1061]: 'Models.Person' does not contain a definition for 'Name' and no accessible extension method 'Name' accepting a first argument of type 'Models.Person' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmphq64hfjn/utils.spy:20:47
    |
 20 |     
    |     ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Product' is never used
  --> /tmp/tmphq64hfjn/utils.spy:2:44
    |
  2 | from models import Entity, Person, Product, create_test_person, create_test_product
    |                                            ^^^^^^^
    |


```

## Timing

- Generation: 458.86s
- Execution: 4.77s
