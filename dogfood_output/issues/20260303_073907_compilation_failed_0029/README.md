# Issue Report: compilation_failed

**Timestamp:** 2026-03-03T07:34:48.952722
**Type:** compilation_failed
**Feature Focus:** module_imports
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating complex module interactions
from utils import Status, SearchResult
from entities import Product, Service, BaseEntity
from services import EntityService, SearchService

def main():
    # Create service instances
    entity_service = EntityService()
    search_service = SearchService()

    # Create products
    laptop = Product(1, "Laptop Pro", 999.99, "Electronics")
    mouse = Product(2, "Wireless Mouse", 29.99, "Electronics")
    desk = Product(3, "Standing Desk", 499.99, "Furniture")

    # Create services
    support = Service(4, "Premium Support", 24)
    setup = Service(5, "Installation Setup", 4)

    # Add to entity service
    entity_service.add(laptop)
    entity_service.add(mouse)
    entity_service.add(desk)
    entity_service.add(support)
    entity_service.add(setup)

    # Print total entities
    print(len(entity_service.entities))

    # Find active entities using the generic filter function
    active_entities = entity_service.find_by_status(Status.ACTIVE)
    print(len(active_entities))

    # Search for items containing "Pro"
    # Fix: Declare as list[ISearchable] and add items individually
    searchable_items: list[ISearchable] = []
    searchable_items.append(laptop)
    searchable_items.append(mouse)
    searchable_items.append(desk)
    searchable_items.append(support)
    searchable_items.append(setup)
    results = search_service.search(searchable_items, "Pro")
    print(len(results))

    # Test generic SearchResult container
    search_result = SearchResult[str](["a", "b", "c"])
    print(search_result.total_count)

    # Test find_by_id with Optional result
    found = entity_service.find_by_id(2)
    if found is not None:
        print(found.get_id())

    # Test optional unwrap_or
    not_found = entity_service.find_by_id(99)
    default_value: BaseEntity = Product(0, "Default", 0.0, "None")
    result_entity = not_found.unwrap_or(default_value)
    print(result_entity.get_id())

    # Test enum iteration and print
    for s in Status:
        print(s.value)

```

## Error

```
Assembly compilation failed:

error[CS0535]: 'Entities.Service' does not implement interface member 'Utils.ISearchable.GetId()'
  --> /tmp/tmpc91gplns/entities.spy:34:49
    |
 34 |     # Search for items containing "Pro"
    |                                        ^
    |

error[CS0119]: 'Utils.ISearchable' is a type, which is not valid in the given context
  --> /tmp/tmpc91gplns/services.spy:25:38
    |
 25 |     entity_service.add(setup)
    |                              ^
    |

error[CS0021]: Cannot apply indexing with [] to an expression of type 'method group'
  --> /tmp/tmpc91gplns/services.spy:25:20
    |
 25 |     entity_service.add(setup)
    |                    ^
    |

error[CS1061]: 'Entities.BaseEntity' does not contain a definition for 'GetId' and no accessible extension method 'GetId' accepting a first argument of type 'Entities.BaseEntity' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpc91gplns/main.spy:52:58
    |
 52 |         print(found.get_id())
    |                              ^
    |

error[CS1061]: 'Entities.BaseEntity' does not contain a definition for 'GetId' and no accessible extension method 'GetId' accepting a first argument of type 'Entities.BaseEntity' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpc91gplns/main.spy:58:52
    |
 58 |     print(result_entity.get_id())
    |                                  ^
    |

error[CS0119]: 'Entities.BaseEntity' is a type, which is not valid in the given context
  --> /tmp/tmpc91gplns/services.spy:15:38
    |
 15 | 
    | ^
    |

error[CS0021]: Cannot apply indexing with [] to an expression of type 'method group'
  --> /tmp/tmpc91gplns/services.spy:15:20
    |
 15 | 
    | ^
    |


```

## Compiler Output

```
warning[SPY0452]: Imported name 'Product' is never used
  --> /tmp/tmpc91gplns/services.spy:4:10
    |
  4 | from services import EntityService, SearchService
    |          ^^^^^^^
    |

warning[SPY0452]: Imported name 'Service' is never used
  --> /tmp/tmpc91gplns/services.spy:4:19
    |
  4 | from services import EntityService, SearchService
    |                   ^^^^^^^
    |


```

## Timing

- Generation: 226.56s
- Execution: 4.77s
