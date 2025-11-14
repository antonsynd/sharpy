# Sharpy Language Enhancements: C# 9-14 Features Analysis

This document analyzes features from C# 9 through C# 14 that could be interpreted in the context of Sharpy and implemented with Pythonic syntax. Features are evaluated for their applicability to Sharpy's design goals and ease of code generation.

## Executive Summary

**Priority Levels:**
- **P0 (v0.5)**: Core features required for C# interop and Unity development
- **P1 (v1.0)**: Enhanced features and Pythonic additions
- **P2 (v1.5+)**: Advanced features and language extensions

**Quick Wins** (Easy to implement, high value):
1. Init-only properties (C# 9)
2. Record types (C# 9)
3. Top-level statements (C# 9) - Already supported
4. Global using directives (C# 10)
5. File-scoped namespace (C# 10)
6. Raw string literals (C# 11)
7. Required members (C# 11)
8. Primary constructors (C# 12)
9. Collection expressions (C# 12)

---

## C# 9 Features (.NET 5)

### ✅ Top-level Statements
**Status**: Already supported in Sharpy v0.5

Sharpy already supports top-level statements as documented:
```python
# Current Sharpy
print("Hello, World!")
```

### 🟢 Records (HIGH PRIORITY - P1)
**Applicability**: Excellent fit for Sharpy
**Code Generation**: Easy
**Value**: High

Records provide immutable data structures with value-based equality. This fits perfectly with Sharpy's Pythonic philosophy.

**Proposed Sharpy Syntax**:
```python
# Option 1: Using class with @record decorator
@record
class Point:
    x: int
    y: int

# Option 2: Using 'record' keyword (more explicit)
record Point:
    x: int
    y: int

# Usage
p1 = Point(10, 20)
p2 = Point(10, 20)
assert p1 == p2  # Value-based equality
```

**Generated C# Code**:
```csharp
public record Point(int X, int Y);
```

**Benefits**:
- Natural fit for data classes
- Reduces boilerplate
- Value semantics align with Python's tuple behavior
- Built-in equality, hashing, and string representation

**Implementation Notes**:
- Map to C# `record` types
- Support both positional and nominal records
- Automatic `__eq__`, `__hash__`, `__str__` generation
- Immutable by default (unless fields marked mutable)

---

### 🟢 Init-only Properties (HIGH PRIORITY - P1)
**Applicability**: Excellent fit for Sharpy
**Code Generation**: Easy
**Value**: High

Init-only properties enable immutable object design while allowing initialization flexibility.

**Proposed Sharpy Syntax**:
```python
class Person:
    # Using 'readonly' keyword to indicate init-only
    readonly first_name: str
    readonly last_name: str
    age: int  # Mutable
    
    def __init__(self, first_name: str, last_name: str, age: int):
        self.first_name = first_name
        self.last_name = last_name
        self.age = age

# Usage
person = Person("Alice", "Smith", 30)
person.age = 31  # OK - mutable
person.first_name = "Bob"  # ERROR - init-only
```

**Alternative with properties**:
```python
class Circle:
    property radius: double:
        get: return self._radius
        init: self._radius = value  # Init-only setter
```

**Generated C# Code**:
```csharp
public class Person
{
    public string FirstName { get; init; }
    public string LastName { get; init; }
    public int Age { get; set; }
}
```

**Benefits**:
- Supports immutable design patterns
- Clear distinction between mutable and immutable fields
- Works well with object initializers

---

### 🟡 Pattern Matching Improvements (MEDIUM PRIORITY - P1)
**Applicability**: Good fit for Sharpy
**Code Generation**: Medium complexity
**Value**: Medium-High

C# 9 enhanced pattern matching with relational patterns and logical patterns.

**Proposed Sharpy Syntax**:
```python
# Already planned for v1.0 match statements
match temperature:
    case < 0:
        print("Freezing")
    case >= 0 and < 20:
        print("Cold")
    case >= 20 and < 30:
        print("Warm")
    case >= 30:
        print("Hot")

# Type patterns with guards
match value:
    case int() as i if i > 0:
        print("Positive integer")
    case str() as s if len(s) > 0:
        print("Non-empty string")
```

**Benefits**:
- More expressive than if/elif chains
- Aligns with Python 3.10+ match statements
- Natural C# code generation

---

### 🔵 Covariant Returns (LOW PRIORITY - P2)
**Applicability**: Moderate
**Code Generation**: Easy (direct mapping)
**Value**: Low-Medium

Methods can override with more specific return types.

**Proposed Sharpy Syntax**:
```python
class Animal:
    def clone(self) -> Animal:
        ...

class Dog(Animal):
    def clone(self) -> Dog:  # More specific return type
        return Dog()
```

**Generated C# Code**:
```csharp
public class Animal
{
    public virtual Animal Clone() => new Animal();
}

public class Dog : Animal
{
    public override Dog Clone() => new Dog();
}
```

**Benefits**:
- Type safety improvements
- Natural OOP pattern
- Direct C# mapping

---

## C# 10 Features (.NET 6)

### 🟢 Global Using Directives (HIGH PRIORITY - P1)
**Applicability**: Excellent fit for Sharpy
**Code Generation**: Trivial
**Value**: High

Define common imports once for entire project.

**Proposed Sharpy Syntax**:
```python
# In a special file: global_imports.spy or project config
global from system import *
global from system.collections.generic import List, Dictionary
global from system.linq import Enumerable

# Other files automatically have these imports
# No need to repeat them
```

**Alternative: Project-level configuration**:
```json5
// In sharpy.json or pyproject.toml
{
  "global_imports": [
    "from system import *",
    "from system.collections.generic import List, Dictionary",
    "from system.linq import Enumerable"
  ]
}
```

**Generated C# Code**:
```csharp
global using System;
global using System.Collections.Generic;
global using System.Linq;
```

**Benefits**:
- Reduces import boilerplate
- Project-wide consistency
- Easy to implement

---

### 🟢 File-scoped Namespace (HIGH PRIORITY - P1)
**Applicability**: Perfect for Sharpy
**Code Generation**: Trivial
**Value**: High

Reduces indentation by making namespace declaration apply to entire file.

**Current Sharpy (inferred)**:
```python
# File: my_module.spy
# Automatically becomes namespace MyModule
class MyClass:
    pass
```

**Explicit namespace control**:
```python
# File: services/user_service.spy
namespace MyApp.Services  # File-scoped

class UserService:
    pass

# Generated as: namespace MyApp.Services; without braces
```

**Generated C# Code**:
```csharp
namespace MyApp.Services;

public class UserService
{
}
```

**Benefits**:
- Less indentation in generated code
- Already natural in Sharpy's module system
- One-to-one mapping

---

### 🟢 Record Structs (HIGH PRIORITY - P1)
**Applicability**: Excellent fit for Sharpy
**Code Generation**: Easy
**Value**: High

Value-type records for performance-critical scenarios.

**Proposed Sharpy Syntax**:
```python
# Using struct with @record decorator
@record
struct Point2D:
    x: double
    y: double

# Or explicit record struct
record struct Point2D:
    x: double
    y: double

# Usage
p1 = Point2D(1.0, 2.0)
p2 = Point2D(1.0, 2.0)
assert p1 == p2  # Value equality
```

**Generated C# Code**:
```csharp
public readonly record struct Point2D(double X, double Y);
```

**Benefits**:
- Zero-allocation for small data structures
- Perfect for game development (Unity)
- Natural Pythonic syntax

---

### 🟡 Extended Property Patterns (MEDIUM PRIORITY - P1)
**Applicability**: Good with match statements
**Code Generation**: Medium
**Value**: Medium

**Proposed Sharpy Syntax**:
```python
# Property pattern matching
match point:
    case Point(x=0, y=0):
        print("Origin")
    case Point(x=0):
        print("On Y axis")
    case Point(y=0):
        print("On X axis")
```

**Benefits**:
- Clean destructuring syntax
- Integrates with v1.0 match statements

---

## C# 11 Features (.NET 7)

### 🟢 Raw String Literals (HIGH PRIORITY - P1)
**Applicability**: Perfect for Sharpy
**Code Generation**: Trivial
**Value**: High

Multi-line strings without escape characters.

**Proposed Sharpy Syntax**:
```python
# Already has triple-quoted strings, but enhance with raw prefix
json_data = r"""
{
    "name": "Alice",
    "path": "C:\Users\Alice\Documents"
}
"""

# Or use existing triple-quote with automatic raw interpretation
sql_query = """
SELECT *
FROM users
WHERE name = 'Alice'
  AND path LIKE 'C:\%'
"""
```

**Generated C# Code**:
```csharp
string jsonData = """
{
    "name": "Alice",
    "path": "C:\Users\Alice\Documents"
}
""";
```

**Benefits**:
- Already partially supported
- Natural for JSON, SQL, regex
- Direct mapping to C# 11 raw strings

---

### 🟢 Required Members (HIGH PRIORITY - P1)
**Applicability**: Excellent fit for Sharpy
**Code Generation**: Easy
**Value**: High

Enforce mandatory initialization of properties.

**Proposed Sharpy Syntax**:
```python
class Person:
    required name: str
    required age: int
    email: str?  # Optional

# Usage
person = Person(name="Alice", age=30)  # OK
person2 = Person(name="Bob")  # ERROR: missing required 'age'
```

**Alternative decorator syntax**:
```python
class Person:
    @required
    name: str
    
    @required
    age: int
```

**Generated C# Code**:
```csharp
public class Person
{
    public required string Name { get; set; }
    public required int Age { get; set; }
    public string? Email { get; set; }
}
```

**Benefits**:
- Compile-time safety
- Clear API contracts
- Prevents incomplete initialization

---

### 🟡 UTF-8 String Literals (MEDIUM PRIORITY - P1)
**Applicability**: Moderate for Sharpy
**Code Generation**: Easy
**Value**: Low-Medium (performance scenarios)

**Proposed Sharpy Syntax**:
```python
# Use prefix for UTF-8 encoded strings
utf8_bytes = u8"Hello, World!"

# Type: ReadOnlySpan[byte]
```

**Generated C# Code**:
```csharp
ReadOnlySpan<byte> utf8Bytes = "Hello, World!"u8;
```

**Benefits**:
- Performance optimization
- Useful for network/file I/O
- Easy to implement

---

### 🟡 Generic Math Support (MEDIUM PRIORITY - P1)
**Applicability**: Good for Sharpy
**Code Generation**: Medium
**Value**: Medium

Native support for math operations on generic types.

**Proposed Sharpy Syntax**:
```python
# Generic function with arithmetic constraints
def add[T: IAdditionOperators[T, T, T]](a: T, b: T) -> T:
    return a + b

# Usage
result1 = add(5, 10)        # int
result2 = add(3.14, 2.86)   # double
```

**Benefits**:
- Type-safe generic math
- Reduces code duplication
- Natural operator overloading support

---

### 🟡 List Patterns (MEDIUM PRIORITY - P1)
**Applicability**: Excellent with match statements
**Code Generation**: Medium
**Value**: Medium-High

Pattern match on list/array structure.

**Proposed Sharpy Syntax**:
```python
# List pattern matching (v1.0 with match statements)
match numbers:
    case []:
        print("Empty list")
    case [x]:
        print(f"Single element: {x}")
    case [first, *rest]:
        print(f"First: {first}, Rest: {rest}")
    case [*init, last]:
        print(f"Last: {last}")
```

**Benefits**:
- Pythonic unpacking syntax
- Already familiar to Python developers
- Maps well to C# list patterns

---

## C# 12 Features (.NET 8)

### 🟢 Primary Constructors (HIGH PRIORITY - P1)
**Applicability**: Perfect for Sharpy
**Code Generation**: Easy
**Value**: Very High

Declare constructor parameters directly in class/struct header.

**Proposed Sharpy Syntax**:
```python
# Primary constructor syntax - very Pythonic!
class Person(name: str, age: int):
    # Parameters automatically become fields
    
    def greet(self):
        print(f"Hello, I'm {self.name} and I'm {self.age} years old")

# Record with primary constructor (already covered)
record Point(x: double, y: double)

# Struct with primary constructor
struct Vector2(x: float, y: float):
    def magnitude(self) -> float:
        return (self.x ** 2 + self.y ** 2) ** 0.5
```

**Generated C# Code**:
```csharp
public class Person(string name, int age)
{
    public void Greet()
    {
        Console.WriteLine($"Hello, I'm {name} and I'm {age} years old");
    }
}
```

**Benefits**:
- **Extremely Pythonic** - matches Python class syntax
- Massive reduction in boilerplate
- Natural fit for data classes
- Easy code generation

**Implementation Notes**:
- Primary constructor parameters available in entire class scope
- Can be combined with additional fields
- Auto-generates backing fields

---

### 🟢 Collection Expressions (HIGH PRIORITY - P1)
**Applicability**: Perfect for Sharpy
**Code Generation**: Easy
**Value**: High

Concise syntax for collection initialization.

**Current Sharpy**:
```python
numbers = [1, 2, 3]
```

**Enhanced with spread operator**:
```python
# Spread operator for collections
numbers1 = [1, 2, 3]
numbers2 = [4, 5, 6]
combined = [*numbers1, *numbers2]  # [1, 2, 3, 4, 5, 6]

# Collection expressions
evens = [x for x in range(10) if x % 2 == 0]  # List comprehension (v1.0)
```

**Generated C# Code**:
```csharp
int[] combined = [.. numbers1, .. numbers2];
```

**Benefits**:
- Already partially supported
- Python-like spread operator
- Aligns with comprehensions (v1.0)

---

### 🟡 Default Lambda Parameters (MEDIUM PRIORITY - P1)
**Applicability**: Good for Sharpy
**Code Generation**: Easy
**Value**: Medium

Lambdas with default parameter values.

**Proposed Sharpy Syntax**:
```python
# Lambda with default parameters
multiply = lambda x, y=2: x * y

result1 = multiply(5)     # 10
result2 = multiply(5, 3)  # 15
```

**Generated C# Code**:
```csharp
var multiply = (int x, int y = 2) => x * y;
```

**Benefits**:
- Consistent with function defaults
- Convenient for callbacks

---

### 🔵 Alias Any Type (LOW PRIORITY - P1)
**Applicability**: Moderate
**Code Generation**: Easy
**Value**: Low-Medium

**Proposed Sharpy Syntax**:
```python
# Type aliases (already planned for v1.0)
type UserId = int
type Coordinate = tuple[double, double]
type StringList = list[str]

# Can now alias any type including tuples, pointers, etc.
```

**Benefits**:
- Already planned for v1.0
- Enhanced with C# 12's broader aliasing

---

### 🔵 Inline Arrays (LOW PRIORITY - P2)
**Applicability**: Low for typical Sharpy use cases
**Code Generation**: Medium
**Value**: Low (performance scenarios only)

Fixed-size buffers for high-performance scenarios.

**Proposed Sharpy Syntax**:
```python
# Inline array for performance-critical code
struct Buffer:
    @inline_array(16)
    data: byte

# Usage
buffer = Buffer()
buffer.data[0] = 255
```

**Benefits**:
- Performance optimization
- Useful for game development
- Advanced feature

---

## C# 13 Features (.NET 9)

### 🟢 Expanded `params` Collections (HIGH PRIORITY - P1)
**Applicability**: Excellent for Sharpy
**Code Generation**: Easy
**Value**: High

`params` works with any collection type, including `Span<T>`.

**Proposed Sharpy Syntax**:
```python
# Variable arguments (already supported)
def sum_all(*numbers: int) -> int:
    return sum(numbers)

# Enhanced to work with any collection type
def process_items(*items: str) -> None:
    for item in items:
        print(item)

# Generated code can use Span<T> for performance
```

**Generated C# Code**:
```csharp
public static void ProcessItems(params ReadOnlySpan<string> items)
{
    foreach (var item in items)
    {
        Console.WriteLine(item);
    }
}
```

**Benefits**:
- Performance improvement (Span allocation)
- Already Pythonic with *args
- Direct mapping to C# 13

---

### 🟡 New `Lock` Type (MEDIUM PRIORITY - P1)
**Applicability**: Good for Sharpy
**Code Generation**: Easy
**Value**: Medium

Improved thread synchronization.

**Proposed Sharpy Syntax**:
```python
from system.threading import Lock

class Counter:
    _lock = Lock()
    _count = 0
    
    def increment(self):
        with self._lock:
            self._count += 1
```

**Generated C# Code**:
```csharp
using System.Threading;

public class Counter
{
    private Lock _lock = new();
    private int _count;
    
    public void Increment()
    {
        lock (_lock)
        {
            _count++;
        }
    }
}
```

**Benefits**:
- Better than traditional lock(object)
- Disposable scope pattern
- Works with context managers

---

### 🟡 Escape Sequence `\e` (MEDIUM PRIORITY - P1)
**Applicability**: Low impact
**Code Generation**: Trivial
**Value**: Low

ASCII escape character for terminal control.

**Proposed Sharpy Syntax**:
```python
# ANSI escape codes
RED = "\e[31m"
RESET = "\e[0m"
print(f"{RED}Error{RESET}")
```

**Benefits**:
- Useful for CLI applications
- Easy to implement
- Direct mapping

---

### 🔵 Ref Structs Improvements (LOW PRIORITY - P2)
**Applicability**: Low for typical Sharpy
**Code Generation**: Medium
**Value**: Low (advanced scenarios)

Ref structs can implement interfaces and be used in generics.

**Benefits**:
- Advanced performance optimization
- Low-level programming
- Niche use cases

---

### 🔵 Partial Properties/Indexers (LOW PRIORITY - P1)
**Applicability**: Moderate
**Code Generation**: Easy
**Value**: Low-Medium

**Proposed Sharpy Syntax**:
```python
# Partial class with partial properties
partial class User:
    partial property name: str

# In another file
partial class User:
    partial property name: str:
        get: return self._name
        set: self._name = value
```

**Benefits**:
- Code generation scenarios
- Source generators support
- Useful for tooling

---

## C# 14 Features (.NET 10)

### 🟢 The `field` Keyword for Properties (HIGH PRIORITY - P1)
**Applicability**: Perfect for Sharpy
**Code Generation**: Easy
**Value**: Very High

Access auto-property's backing field directly without declaring it.

**Proposed Sharpy Syntax**:
```python
class Person:
    property name: str:
        get: return field
        set:
            if value is None:
                raise ValueError("Name cannot be null")
            field = value

# More Pythonic alternative with @property decorator
class Person:
    @property
    def name(self) -> str:
        return field
    
    @name.setter
    def name(self, value: str):
        if value is None:
            raise ValueError("Name cannot be null")
        field = value
```

**Generated C# Code**:
```csharp
public class Person
{
    public string Name
    {
        get => field;
        set => field = value ?? throw new ArgumentNullException(nameof(value));
    }
}
```

**Benefits**:
- Eliminates backing field boilerplate
- Clean property validation
- Very easy to implement

---

### 🟢 Extension Members (HIGH PRIORITY - P1)
**Applicability**: Excellent for Sharpy
**Code Generation**: Medium
**Value**: Very High

Extension properties and grouped extensions.

**Proposed Sharpy Syntax**:
```python
# Extension methods (already in discussion for v1.0+)
# Extension properties and static extensions are new

# Extension block - group related extensions
extension list[str]:
    @property
    def is_empty(self) -> bool:
        return len(self) == 0
    
    def count_starting_with(self, prefix: str) -> int:
        return sum(1 for s in self if s.startswith(prefix))
    
    @staticmethod
    def from_csv(csv: str) -> list[str]:
        return csv.split(',')

# Usage
names = ["Alice", "Bob", "Andrew"]
print(names.is_empty)  # False
print(names.count_starting_with("A"))  # 2
parsed = list[str].from_csv("a,b,c")
```

**Alternative syntax using decorators**:
```python
@extend(list[str])
class ListStringExtensions:
    @property
    def is_empty(self) -> bool:
        return len(self) == 0
```

**Generated C# Code**:
```csharp
public static class ListStringExtensions
{
    public static bool IsEmpty(this List<string> source) => !source.Any();
    
    public static int CountStartingWith(this List<string> source, string prefix)
        => source.Count(x => x.StartsWith(prefix));
    
    public static List<string> FromCsv(string csv)
        => csv.Split(',').ToList();
}
```

**Benefits**:
- **Huge win for API design**
- Groups related extensions
- Extension properties are very useful
- Static extensions add factory methods

---

### 🟢 Implicit Span Conversions (HIGH PRIORITY - P1)
**Applicability**: Good for performance-critical Sharpy code
**Code Generation**: Easy
**Value**: Medium-High

Implicit conversions to `Span<T>` and `ReadOnlySpan<T>`.

**Proposed Sharpy Syntax**:
```python
from system import Span, ReadOnlySpan

def process_data(data: ReadOnlySpan[int]) -> int:
    total = 0
    for value in data:
        total += value
    return total

# Implicit conversion from array or list
numbers = [1, 2, 3, 4, 5]
result = process_data(numbers)  # Automatic conversion to ReadOnlySpan
```

**Generated C# Code**:
```csharp
public static int ProcessData(ReadOnlySpan<int> data)
{
    int total = 0;
    foreach (var value in data)
    {
        total += value;
    }
    return total;
}

// Usage
int[] numbers = [1, 2, 3, 4, 5];
int result = ProcessData(numbers);  // Implicit conversion
```

**Benefits**:
- Performance optimization
- Zero-copy semantics
- Natural in Sharpy

---

### 🟡 Lambda Parameter Modifiers (MEDIUM PRIORITY - P1)
**Applicability**: Moderate
**Code Generation**: Easy
**Value**: Medium

Lambdas support `ref`, `out`, `in` parameter modifiers.

**Proposed Sharpy Syntax**:
```python
# Lambda with ref parameters
increment = lambda ref x: x += 1

value = 10
increment(ref value)  # value is now 11

# Lambda with in parameter (read-only reference)
process = lambda in x: print(x * 2)
```

**Generated C# Code**:
```csharp
var increment = (ref int x) => x += 1;

int value = 10;
increment(ref value);
```

**Benefits**:
- Performance (avoid copying)
- More expressive lambdas
- Advanced use cases

---

### 🟡 nameof with Unbound Generics (MEDIUM PRIORITY - P1)
**Applicability**: Moderate
**Code Generation**: Trivial
**Value**: Low-Medium

**Proposed Sharpy Syntax**:
```python
# Get name of generic type without specifying type argument
from system.collections.generic import List

# nameof for reflection/logging
type_name = nameof(List<>)  # "List"
```

**Benefits**:
- Useful for diagnostics
- Reflection scenarios
- Easy to implement

---

### 🟡 Null-Conditional Assignment (MEDIUM PRIORITY - P1)
**Applicability**: Good for Sharpy
**Code Generation**: Easy
**Value**: Medium

**Proposed Sharpy Syntax**:
```python
# Null-conditional on left side of assignment
user?.name = "Alice"  # Only assigns if user is not None

# Equivalent to:
if user is not None:
    user.name = "Alice"
```

**Generated C# Code**:
```csharp
user?.Name = "Alice";
```

**Benefits**:
- Reduces null-check boilerplate
- Consistent with `?.` operator
- Natural extension

---

### 🔵 Partial Constructors and Events (LOW PRIORITY - P1)
**Applicability**: Low
**Code Generation**: Easy
**Value**: Low (tooling scenarios)

**Benefits**:
- Code generation support
- Source generators
- Niche use cases

---

### 🔵 User-Defined Compound Assignment (LOW PRIORITY - P2)
**Applicability**: Low for typical Sharpy
**Code Generation**: Medium
**Value**: Low

Custom operators like `+=`, `*=` for user types.

**Benefits**:
- Advanced operator overloading
- Performance optimization
- Low priority

---

## Implementation Roadmap

### Phase 1: v1.0 Quick Wins (Highest Value, Easiest Implementation)

1. **Primary Constructors** - Massive boilerplate reduction
   ```python
   class Person(name: str, age: int):
       pass
   ```

2. **Records** - Immutable data classes
   ```python
   record Point(x: int, y: int)
   ```

3. **Init-only Properties** - Immutability support
   ```python
   readonly name: str
   ```

4. **Required Members** - Compile-time safety
   ```python
   required email: str
   ```

5. **Raw String Literals** - Already mostly supported
   ```python
   sql = """SELECT * FROM users"""
   ```

6. **Global Imports** - Project-level imports
   ```python
   global from system import *
   ```

7. **File-scoped Namespace** - Natural in Sharpy

8. **The `field` Keyword** - Clean property validation
   ```python
   property name:
       set: field = value if value else "default"
   ```

### Phase 2: v1.0 Enhanced Features

9. **Collection Expressions** - Enhanced spread operator
10. **Record Structs** - Value-type records
11. **Pattern Matching** - Already planned
12. **List Patterns** - Destructuring in match
13. **Extension Members** - Grouped extensions with properties

### Phase 3: v1.5+ Advanced Features

14. **Generic Math Support**
15. **Span Improvements**
16. **Lambda Parameter Modifiers**
17. **Partial Properties/Constructors**

### Phase 4: Performance & Niche (v2.0+)

18. **Inline Arrays**
19. **User-Defined Compound Assignment**
20. **Ref Struct Improvements**

---

## Code Generation Strategy

### Easy Wins (Direct C# Mapping)
- Init-only properties → `{ get; init; }`
- Records → `record Type(...)`
- Primary constructors → `class Type(...)`
- File-scoped namespace → `namespace X;`
- Raw strings → `""" ... """`
- Global imports → `global using`
- Required members → `required`

### Medium Complexity (Syntax Transformation)
- Extension members → Extension class generation
- Pattern matching → C# switch expressions
- Collection expressions → C# collection expressions
- Record structs → `record struct`

### Higher Complexity (Advanced Features)
- Generic math → Interface constraints
- Partial properties → Multi-file code gen
- Ref struct interfaces → Advanced type system

---

## Recommendations

### Immediate Priorities for v1.0

1. **Primary Constructors** ⭐⭐⭐⭐⭐
   - Extremely Pythonic
   - Massive value
   - Easy to implement
   - Natural fit

2. **Records** ⭐⭐⭐⭐⭐
   - Data classes are core to modern development
   - Value semantics
   - Easy implementation

3. **Init-only Properties** ⭐⭐⭐⭐
   - Immutability pattern
   - Good defaults
   - Easy win

4. **Required Members** ⭐⭐⭐⭐
   - Compile-time safety
   - Prevents bugs
   - Clear API contracts

5. **Extension Members** ⭐⭐⭐⭐⭐
   - Huge value for library authors
   - Extension properties are powerful
   - Natural grouping

6. **The `field` Keyword** ⭐⭐⭐⭐
   - Eliminates boilerplate
   - Clean validation
   - Easy to implement

### Strong Candidates for v1.0

7. **Global Imports** - Reduces boilerplate
8. **Collection Expressions** - Python-like
9. **Raw String Literals** - Natural for Sharpy
10. **Record Structs** - Performance optimization

### Consider for v1.5+

- Generic Math Support
- Span Improvements
- Lambda Parameter Modifiers
- Null-Conditional Assignment

### Low Priority

- Inline Arrays (niche)
- Partial Constructors (tooling)
- User-Defined Compound Assignment (advanced)

---

## Conclusion

C# 9-14 introduces many features that align beautifully with Sharpy's Pythonic philosophy. The highest-value features for Sharpy are:

1. **Primary Constructors** - Most Pythonic, highest value
2. **Records** - Essential for modern data-oriented programming
3. **Extension Members** - Powerful API design tool
4. **Init-only Properties** - Immutability support
5. **Required Members** - Type safety

These features require minimal syntax additions to Sharpy and map cleanly to C# code generation, making them excellent candidates for the v1.0 release.

The Pythonic syntax for these features feels natural and maintains Sharpy's design philosophy while leveraging the latest C# capabilities for performance and safety.
