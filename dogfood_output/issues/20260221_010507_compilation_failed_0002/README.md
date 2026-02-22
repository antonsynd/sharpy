# Issue Report: compilation_failed

**Timestamp:** 2026-02-21T01:03:42.675841
**Type:** compilation_failed
**Feature Focus:** list_literal
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Complex nested list literals with slicing and manipulation
# Creates and transforms 2D data structures

def transform_grid(grid: list[list[int]]) -> list[list[int]]:
    result: list[list[int]] = []
    for row in grid:
        new_row: list[int] = [x * 2 for x in row]
        result.append(new_row)
    return result

def main():
    # Nested list literal representing a grid
    grid: list[list[int]] = [[1, 2, 3], [4, 5, 6], [7, 8, 9]]
    
    # List literal with expression elements
    offset: int = 10
    augmented: list[int] = [offset + 1, offset + 2, offset + 3]
    
    # Empty list literal initialization
    buffer: list[str] = []
    buffer.append("first")
    buffer.append("second")
    
    print(grid[1][1])
    print(augmented[2])
    print(len(buffer))
    
    transformed = transform_grid(grid)
    print(transformed[0][0])
    print(transformed[2][2])

# EXPECTED OUTPUT:
# 5
# 13
# 2
# 2
# 18
```

## Error

```
Assembly compilation failed:

error[CS1950]: The best overloaded Add method 'List<List<int>>.Add(List<int>)' for the collection initializer has some invalid arguments
  --> /tmp/tmpjvi2k65m/dogfood_test.spy:17:17
    |
 17 |     augmented: list[int] = [offset + 1, offset + 2, offset + 3]
    |                 ^
    |

error[CS1503]: Argument 1: cannot convert from 'int' to 'Sharpy.List<int>'
  --> /tmp/tmpjvi2k65m/dogfood_test.spy:17:17
    |
 17 |     augmented: list[int] = [offset + 1, offset + 2, offset + 3]
    |                 ^
    |

error[CS1950]: The best overloaded Add method 'List<List<int>>.Add(List<int>)' for the collection initializer has some invalid arguments
  --> /tmp/tmpjvi2k65m/dogfood_test.spy:18:17
    |
 18 |     
    |     ^
    |

error[CS1503]: Argument 1: cannot convert from 'int' to 'Sharpy.List<int>'
  --> /tmp/tmpjvi2k65m/dogfood_test.spy:18:17
    |
 18 |     
    |     ^
    |

error[CS1950]: The best overloaded Add method 'List<List<int>>.Add(List<int>)' for the collection initializer has some invalid arguments
  --> /tmp/tmpjvi2k65m/dogfood_test.spy:19:17
    |
 19 |     # Empty list literal initialization
    |                 ^
    |

error[CS1503]: Argument 1: cannot convert from 'int' to 'Sharpy.List<int>'
  --> /tmp/tmpjvi2k65m/dogfood_test.spy:19:17
    |
 19 |     # Empty list literal initialization
    |                 ^
    |

error[CS1950]: The best overloaded Add method 'List<List<int>>.Add(List<int>)' for the collection initializer has some invalid arguments
  --> /tmp/tmpjvi2k65m/dogfood_test.spy:23:17
    |
 23 |     
    |     ^
    |

error[CS1503]: Argument 1: cannot convert from 'int' to 'Sharpy.List<int>'
  --> /tmp/tmpjvi2k65m/dogfood_test.spy:23:17
    |
 23 |     
    |     ^
    |

error[CS1950]: The best overloaded Add method 'List<List<int>>.Add(List<int>)' for the collection initializer has some invalid arguments
  --> /tmp/tmpjvi2k65m/dogfood_test.spy:24:17
    |
 24 |     print(grid[1][1])
    |                 ^
    |

error[CS1503]: Argument 1: cannot convert from 'int' to 'Sharpy.List<int>'
  --> /tmp/tmpjvi2k65m/dogfood_test.spy:24:17
    |
 24 |     print(grid[1][1])
    |                 ^
    |

error[CS1950]: The best overloaded Add method 'List<List<int>>.Add(List<int>)' for the collection initializer has some invalid arguments
  --> /tmp/tmpjvi2k65m/dogfood_test.spy:25:17
    |
 25 |     print(augmented[2])
    |                 ^
    |

error[CS1503]: Argument 1: cannot convert from 'int' to 'Sharpy.List<int>'
  --> /tmp/tmpjvi2k65m/dogfood_test.spy:25:17
    |
 25 |     print(augmented[2])
    |                 ^
    |

error[CS1950]: The best overloaded Add method 'List<List<int>>.Add(List<int>)' for the collection initializer has some invalid arguments
  --> /tmp/tmpjvi2k65m/dogfood_test.spy:29:17
    |
 29 |     print(transformed[0][0])
    |                 ^
    |

error[CS1503]: Argument 1: cannot convert from 'int' to 'Sharpy.List<int>'
  --> /tmp/tmpjvi2k65m/dogfood_test.spy:29:17
    |
 29 |     print(transformed[0][0])
    |                 ^
    |

error[CS1950]: The best overloaded Add method 'List<List<int>>.Add(List<int>)' for the collection initializer has some invalid arguments
  --> /tmp/tmpjvi2k65m/dogfood_test.spy:30:17
    |
 30 |     print(transformed[2][2])
    |                 ^
    |

error[CS1503]: Argument 1: cannot convert from 'int' to 'Sharpy.List<int>'
  --> /tmp/tmpjvi2k65m/dogfood_test.spy:30:17
    |
 30 |     print(transformed[2][2])
    |                 ^
    |

error[CS1950]: The best overloaded Add method 'List<List<int>>.Add(List<int>)' for the collection initializer has some invalid arguments
  --> /tmp/tmpjvi2k65m/dogfood_test.spy:31:17
    |
 31 | 
    | ^
    |

error[CS1503]: Argument 1: cannot convert from 'int' to 'Sharpy.List<int>'
  --> /tmp/tmpjvi2k65m/dogfood_test.spy:31:17
    |
 31 | 
    | ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpjvi2k65m/dogfood_test.cs

```

## Timing

- Generation: 70.23s
- Execution: 4.83s
