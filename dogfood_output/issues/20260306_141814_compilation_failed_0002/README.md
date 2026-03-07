# Issue Report: compilation_failed

**Timestamp:** 2026-03-06T14:11:01.301963
**Type:** compilation_failed
**Feature Focus:** generic_class
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test generic class with a Stack implementation
class Stack[T]:
    @static
    _items: list[T] = []
    
    def __init__(self):
        self._items = []
    
    def push(self, item: T) -> None:
        self._items.append(item)
    
    def pop(self) -> T:
        return self._items.pop()
    
    def peek(self) -> T:
        return self._items[len(self._items) - 1]
    
    def size(self) -> int:
        return len(self._items)

def main():
    # Test Stack[int]
    int_stack: Stack[int] = Stack[int]()
    int_stack.push(10)
    int_stack.push(20)
    int_stack.push(30)
    print(int_stack.peek())
    print(int_stack.pop())
    print(int_stack.size())
    
    # Test Stack[str]
    str_stack: Stack[str] = Stack[str]()
    str_stack.push("first")
    str_stack.push("second")
    print(str_stack.pop())
    print(str_stack.size())
    
    # Mixed type stack with explicit type
    float_stack: Stack[float] = Stack[float]()
    float_stack.push(1.5)
    print(float_stack.peek())

```

## Error

```
Assembly compilation failed:

error[CS0305]: Using the generic type 'DogfoodTest.Stack<T>' requires 1 type arguments
  --> /tmp/tmpvtja1gjh/dogfood_test.spy:10:25
    |
 10 |         self._items.append(item)
    |                         ^
    |

error[CS0305]: Using the generic type 'DogfoodTest.Stack<T>' requires 1 type arguments
  --> /tmp/tmpvtja1gjh/dogfood_test.spy:13:32
    |
 13 |         return self._items.pop()
    |                                ^
    |

error[CS0305]: Using the generic type 'DogfoodTest.Stack<T>' requires 1 type arguments
  --> /tmp/tmpvtja1gjh/dogfood_test.spy:16:32
    |
 16 |         return self._items[len(self._items) - 1]
    |                                ^
    |

error[CS0305]: Using the generic type 'DogfoodTest.Stack<T>' requires 1 type arguments
  --> /tmp/tmpvtja1gjh/dogfood_test.spy:16:85
    |
 16 |         return self._items[len(self._items) - 1]
    |                                                 ^
    |

error[CS0305]: Using the generic type 'DogfoodTest.Stack<T>' requires 1 type arguments
  --> /tmp/tmpvtja1gjh/dogfood_test.spy:19:60
    |
 19 |         return len(self._items)
    |                                ^
    |

error[CS0176]: Member 'DogfoodTest.Stack<T>._Items' cannot be accessed with an instance reference; qualify it with a type name instead
  --> /tmp/tmpvtja1gjh/dogfood_test.spy:7:13
    |
  7 |         self._items = []
    |             ^
    |


```

## Compiler Output

```
warning[SPY0459]: Accessing static field '_items' via instance. Prefer 'Stack._items'.
  --> /tmp/tmpvtja1gjh/dogfood_test.spy:7:9
    |
  7 |         self._items = []
    |         ^
    |

warning[SPY0459]: Accessing static field '_items' via instance. Prefer 'Stack._items'.
  --> /tmp/tmpvtja1gjh/dogfood_test.spy:10:9
    |
 10 |         self._items.append(item)
    |         ^
    |

warning[SPY0459]: Accessing static field '_items' via instance. Prefer 'Stack._items'.
  --> /tmp/tmpvtja1gjh/dogfood_test.spy:13:16
    |
 13 |         return self._items.pop()
    |                ^
    |

warning[SPY0459]: Accessing static field '_items' via instance. Prefer 'Stack._items'.
  --> /tmp/tmpvtja1gjh/dogfood_test.spy:16:16
    |
 16 |         return self._items[len(self._items) - 1]
    |                ^
    |

warning[SPY0459]: Accessing static field '_items' via instance. Prefer 'Stack._items'.
  --> /tmp/tmpvtja1gjh/dogfood_test.spy:16:32
    |
 16 |         return self._items[len(self._items) - 1]
    |                                ^
    |

warning[SPY0459]: Accessing static field '_items' via instance. Prefer 'Stack._items'.
  --> /tmp/tmpvtja1gjh/dogfood_test.spy:19:20
    |
 19 |         return len(self._items)
    |                    ^
    |


```

## Generated C#

```csharp
warning[SPY0459]: Accessing static field '_items' via instance. Prefer 'Stack._items'.
  --> /tmp/tmpvtja1gjh/dogfood_test.spy:7:9
    |
  7 |         self._items = []
    |         ^
    |

warning[SPY0459]: Accessing static field '_items' via instance. Prefer 'Stack._items'.
  --> /tmp/tmpvtja1gjh/dogfood_test.spy:10:9
    |
 10 |         self._items.append(item)
    |         ^
    |

warning[SPY0459]: Accessing static field '_items' via instance. Prefer 'Stack._items'.
  --> /tmp/tmpvtja1gjh/dogfood_test.spy:13:16
    |
 13 |         return self._items.pop()
    |                ^
    |

warning[SPY0459]: Accessing static field '_items' via instance. Prefer 'Stack._items'.
  --> /tmp/tmpvtja1gjh/dogfood_test.spy:16:16
    |
 16 |         return self._items[len(self._items) - 1]
    |                ^
    |

warning[SPY0459]: Accessing static field '_items' via instance. Prefer 'Stack._items'.
  --> /tmp/tmpvtja1gjh/dogfood_test.spy:16:32
    |
 16 |         return self._items[len(self._items) - 1]
    |                                ^
    |

warning[SPY0459]: Accessing static field '_items' via instance. Prefer 'Stack._items'.
  --> /tmp/tmpvtja1gjh/dogfood_test.spy:19:20
    |
 19 |         return len(self._items)
    |                    ^
    |

Generated C# code written to: /tmp/tmpvtja1gjh/dogfood_test.cs

```

## Timing

- Generation: 415.21s
- Execution: 4.34s
