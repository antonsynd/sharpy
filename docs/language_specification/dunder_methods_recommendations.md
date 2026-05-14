# Dunder Methods: Design Recommendations

This document captures design issues, edge cases, and recommendations for Sharpy's dunder method implementation. Each section identifies a concern, provides examples of problematic scenarios, and offers concrete recommendations.

---

## Table of Contents

1. [Equality Contract (`__eq__` / `__hash__`)](#1-equality-contract-__eq__--__hash__)
2. [Equals Overload Dispatch Synthesis](#2-equals-overload-dispatch-synthesis)
3. [Reflected Operators (`__radd__`, etc.)](#3-reflected-operators-__radd__-etc)
4. [String Conversion (`__str__`)](#4-string-conversion-__str__)
5. [Boolean Conversion (`__bool__` / `__len__`)](#5-boolean-conversion-__bool__--__len__)
6. [Override Requirements for Object Methods](#6-override-requirements-for-object-methods)
7. [Iterator Protocol (`__iter__` / `__next__`)](#7-iterator-protocol-__iter__--__next__)
8. [Callable Objects (`__call__`)](#8-callable-objects-__call__)
9. [Context Managers (`__enter__` / `__exit__`)](#9-context-managers-__enter__--__exit__)
10. [Indexing and Slicing (`__getitem__` / `__setitem__`)](#10-indexing-and-slicing-__getitem__--__setitem__)
11. [Cross-Dunder Synthesis Rules](#11-cross-dunder-synthesis-rules)
12. [Implicit Interface Implementation](#12-implicit-interface-implementation)
13. [Comparison Operator Synthesis](#13-comparison-operator-synthesis)
14. [Interop Scenarios](#14-interop-scenarios)

---

## 1. Equality Contract (`__eq__` / `__hash__`)

### Issue

.NET requires that if two objects are equal (`Equals()` returns `true`), they must have the same hash code. Defining `__eq__` without `__hash__` violates this contract.

### Problematic Scenarios

```python
# Scenario 1: Missing __hash__
class Point:
    x: int
    y: int

    def __eq__(self, other: Point) -> bool:
        return self.x == other.x and self.y == other.y

    # No __hash__ defined - .NET contract violated

# Usage that breaks:
points = set[Point]()
p1 = Point(1, 2)
points.add(p1)
p2 = Point(1, 2)
print(p2 in points)  # Undefined behavior: p1 == p2 but hash(p1) != hash(p2)
```

```python
# Scenario 2: Mutable fields in equality
class MutablePoint:
    x: int
    y: int

    def __eq__(self, other: MutablePoint) -> bool:
        return self.x == other.x and self.y == other.y

    def __hash__(self) -> int:
        return hash((self.x, self.y))  # Hash based on mutable state

# Usage that breaks:
points = dict[MutablePoint, str]()
p = MutablePoint(1, 2)
points[p] = "hello"
p.x = 3  # Mutate after insertion
print(points[p])  # KeyError: hash changed, can't find the key
```

### Cross-Module Edge Case

```python
# module_a.spy
class Base:
    value: int

    def __eq__(self, other: Base) -> bool:
        return self.value == other.value

# module_b.spy
from module_a import Base

class Derived(Base):
    extra: str

    @override
    def __eq__(self, other: Base) -> bool:
        if not isinstance(other, Derived):
            return False
        return super().__eq__(other) and self.extra == other.extra

    # Must also override __hash__ to maintain contract
```

### Interop Edge Case

```csharp
// C# code consuming Sharpy type
public class CSharpDerived : SharpyBase
{
    public override bool Equals(object obj) => /* custom logic */;
    // If SharpyBase has __eq__ but CSharpDerived doesn't override GetHashCode,
    // the contract may still be violated
}
```

### Recommendations

| Option | Description | Trade-off |
|--------|-------------|-----------|
| **A. Error** | Compiler error if `__eq__` defined without `__hash__` | Strictest; may annoy users for simple cases |
| **B. Warning + Default** | Warning if missing; synthesize identity-based `__hash__` | Allows compilation but warns of potential bugs |
| **C. Synthesize from fields** | Auto-generate `__hash__` from all fields used in `__eq__` | Complex; requires analyzing `__eq__` body |
| **D. Warning only** | Warning but no synthesis; use inherited `GetHashCode()` | Minimal intervention; relies on user awareness |

**Recommended: Option A (Error)** with clear error message explaining the .NET contract. Users can explicitly opt-out by defining `__hash__` that returns `base.__hash__()` if they understand the implications.

**Additional rule:** If a class is marked `@frozen` or is a `struct`, consider auto-synthesizing `__hash__` from all fields (like Python's `@dataclass(frozen=True)`).

---

## 2. Equals Overload Dispatch Synthesis

### Decision: No Automatic Synthesis

Each `__eq__` overload maps 1:1 to a corresponding `Equals` overload with matching parameter type.
The compiler does **not** synthesize an `Equals(object)` dispatcher. Users who want `Equals(object)`
define `__eq__(self, other: object)` explicitly.

- `__eq__(self, other: Foo)` generates `public bool Equals(Foo rhs)` (new overload, not override)
- `__eq__(self, other: object)` generates `public override bool Equals(object rhs)` (overrides `System.Object`)
- `operator==` calls `left.Equals(right)` — C# overload resolution picks the right `Equals` at compile time

**Warning SPY0454**: If any `__eq__` overload exists but none has parameter type `object`, the compiler
warns that collections (`set`, `dict`) will use reference equality. This encourages users to define
`__eq__(self, other: object)` when collection behavior matters.

### Previous Design (Rejected)

The original design considered synthesizing an `Equals(object)` dispatcher with `is` checks.
This was rejected because:

1. **Dispatch ordering is ambiguous** — overlapping type hierarchies create unreachable branches
2. **Partial orderings are wrong** — synthesis assumes total ordering of types
3. **Implicit behavior is surprising** — users should explicitly opt into `Equals(object)` semantics
4. **1:1 mapping is simpler** — matches how all other dunder methods work (direct translation)

---

## 3. Reflected Operators (`__radd__`, etc.)

### Issue

Python's reflected operators (`__radd__`, `__rsub__`, etc.) are a fallback mechanism: `a + b` tries `a.__add__(b)`, and if that returns `NotImplemented`, tries `b.__radd__(a)`. C# has no equivalent — operator overload resolution is purely static.

### Problematic Scenarios

```python
# Scenario 1: Both types define operators
class Vector:
    def __add__(self, other: Scalar) -> Vector: ...

class Scalar:
    def __radd__(self, other: Vector) -> Vector: ...  # Intended as fallback

# In Python: Vector() + Scalar() calls Vector.__add__
# In Python: if Vector.__add__ returns NotImplemented, calls Scalar.__radd__
# In Sharpy: Vector() + Scalar() has TWO valid operator+ overloads - ambiguous?
```

```python
# Scenario 2: Asymmetric operations
class Matrix:
    def __mul__(self, other: int) -> Matrix: ...      # Matrix * int

class Scalar:
    def __rmul__(self, other: Matrix) -> Matrix: ...  # int * Matrix (from Scalar's perspective)

# Problem: Scalar.__rmul__ generates operator*(int, Scalar), not operator*(Matrix, Scalar)
# The 'other' parameter type doesn't match the intended use case
```

### Cross-Module Edge Case

```python
# module_a.spy - published library
class Vector:
    def __add__(self, other: int) -> Vector: ...

# module_b.spy - consumer trying to extend
from module_a import Vector

class MyScalar:
    def __radd__(self, other: Vector) -> Vector: ...
    # Generates: operator+(Vector, MyScalar)
    # This works! But user might expect Python's fallback semantics
```

### Interop Edge Case

```csharp
// C# library defines:
public static Vector operator +(Vector v, int i) => ...;

// Sharpy code:
class MyInt:
    def __radd__(self, other: Vector) -> Vector: ...
    # Generates: operator+(Vector, MyInt)
    # Now Vector + MyInt has overloads in BOTH assemblies
    # C# picks based on "better conversion" rules
```

### Recommendations

1. **Rename or clarify semantics:** Consider whether `__radd__` should be called `__rhs_add__` or similar to clarify it means "define operator where self is RHS" rather than "fallback if LHS fails."

2. **Document explicitly** that Sharpy's reflected operators are NOT Python's fallback mechanism:

   ```
   # In Python: __radd__ is called when __add__ returns NotImplemented
   # In Sharpy: __radd__ simply defines an operator with reversed operand positions
   # There is no fallback behavior - C# picks one overload at compile time
   ```

3. **Compiler warning for potential ambiguity:**

   ```
   warning SPY0201: Both 'Vector.__add__(Scalar)' and 'Scalar.__radd__(Vector)'
   define 'operator+(Vector, Scalar)'. The overload from 'Vector' will be preferred
   by C# overload resolution when both are visible.
   ```

4. **Consider prohibiting duplicate signatures:** If `A.__add__(B)` and `B.__radd__(A)` both generate `operator+(A, B)`, this could be an error rather than relying on C# resolution rules.

5. **NotImplemented pattern:** Do NOT support `NotImplemented` return type. It doesn't fit C#'s static dispatch model and would require runtime checks that defeat the purpose of static typing.

---

## 4. String Conversion (`__str__`)

### Issue

With `str` being `System.String`, the mapping simplifies to `__str__` → `ToString()`. However, Python has both `__str__` (user-friendly) and `__repr__` (developer-friendly, unambiguous).

### Problematic Scenarios

```python
# Scenario: Debugging vs display
class User:
    id: int
    name: str

    def __str__(self) -> str:
        return self.name  # User-friendly

    # No __repr__ - how to get "User(id=42, name='Alice')" for debugging?
```

```python
# Scenario: String interpolation
user = User(42, "Alice")
print(f"Debug: {user!r}")  # Python uses __repr__ for !r
print(f"Display: {user}")   # Python uses __str__
# What does Sharpy do for {user!r}?
```

### Inheritance Edge Case

```python
# base.spy
class Base:
    def __str__(self) -> str:
        return "Base"

# derived.spy
class Derived(Base):
    # Inherits __str__ -> ToString()
    # To override, needs @override
    @override
    def __str__(self) -> str:
        return "Derived"
```

### Interop Edge Case

```csharp
// C# class with custom ToString
public class CSharpClass
{
    public override string ToString() => "CSharp";
}

// Sharpy inheriting:
class SharpyDerived(CSharpClass):
    @override
    def __str__(self) -> str:
        return f"Sharpy wrapping {super().__str__()}"
```

### Recommendations

1. **Single `__str__` mapping:** `__str__` → `public override string ToString()`

2. **`str()` built-in:** Calls `ToString()` on any type, providing uniform syntax.

3. **`repr()` built-in (optional):** If supporting `__repr__`:
   - `__repr__` → synthesized method `string ToRepr()` (not a .NET standard)
   - `repr(x)` calls `ToRepr()` if available, else falls back to `ToString()`
   - f-string `{x!r}` calls `repr(x)`

   **Alternative:** Don't support `__repr__`. Document that `__str__` serves both purposes in Sharpy. Users needing debug representations can define a regular `debug_str()` method.

4. **Format strings:** `f"{x:format}"` should map to `IFormattable.ToString(format, provider)` if implemented. Consider `__format__(self, spec: str) -> str` → `IFormattable` implementation.

---

## 5. Boolean Conversion (`__bool__` / `__len__`)

### Issue

Python's truthiness rules: `bool(x)` calls `__bool__()`, falling back to `__len__() != 0`, falling back to `True`. This is runtime dispatch that doesn't fit C#'s static model cleanly.

### Problematic Scenarios

```python
# Scenario 1: Only __len__ defined
class MyList:
    items: list[int]

    def __len__(self) -> int:
        return len(self.items)

# Python: bool(MyList()) is False if empty, True otherwise
# Sharpy: What does `if my_list:` do?
```

```python
# Scenario 2: __bool__ returns non-bool in Python (error in Sharpy)
class Weird:
    def __bool__(self) -> int:  # Python allows this (truthy if non-zero)
        return 42
# Sharpy: Must return bool - this is a type error (good)
```

### Inheritance Edge Case

```python
class Base:
    def __len__(self) -> int:
        return 0

class Derived(Base):
    def __bool__(self) -> bool:
        return True  # Override truthiness independent of length

# bool(Derived()) should be True, not based on len()
```

### Interop Edge Case

```csharp
// C# type with Count but no operator true/false
public class CSharpCollection : ICollection<int>
{
    public int Count => 5;
    // No operator true/false
}

// Sharpy: bool(csharp_collection) should work via Count
```

### Recommendations

1. **Compiler synthesis:**
   - `__bool__(self) -> bool` → `public static bool operator true(T x)` + `public static bool operator false(T x)`
   - `__len__(self) -> int` → `public int Count { get; }` (and implement `ISized` interface)

2. **`bool()` built-in dispatch order (in Sharpy.Core):**
   ```
   1. If T has implicit conversion to bool, use it
   2. If T has operator true, use it
   3. If T implements ISized, return Count != 0
   4. Return true (objects are truthy by default)
   ```

3. **Interface for discoverability:**
   ```csharp
   // In Sharpy.Core
   public interface ISized
   {
       int Count { get; }
   }

   public interface ITruthy
   {
       bool IsTrue { get; }  // Or just rely on operator true
   }
   ```

4. **`if x:` statement:** The compiler should emit code equivalent to `if (Builtins.@bool(x))` to get the full dispatch chain, OR inline the appropriate check based on static type analysis.

5. **Optimization:** For types where the compiler can statically determine the truthiness path (e.g., type has `operator true`), emit direct code without runtime dispatch.

---

## 6. Override Requirements for Object Methods

### Issue

`__str__`, `__eq__`, `__hash__` map to `System.Object` virtual methods. C# requires `override` keyword. Requiring `@override` decorator in Sharpy is technically correct but un-Pythonic.

### Problematic Scenarios

```python
# Scenario 1: New Sharpy user from Python
class Point:
    x: int
    y: int

    def __str__(self) -> str:  # ERROR: missing @override
        return f"({self.x}, {self.y})"

# User confusion: "But I'm defining __str__, not overriding anything!"
```

```python
# Scenario 2: Inheriting from Sharpy class
class Base:
    def __str__(self) -> str:  # Already has @override (implicit or explicit)
        return "Base"

class Derived(Base):
    def __str__(self) -> str:  # Also needs @override
        return "Derived"
```

### Interop Edge Case

```csharp
// C# class that seals ToString
public class SealedToString
{
    public sealed override string ToString() => "Sealed";
}

// Sharpy:
class SharpyDerived(SealedToString):
    def __str__(self) -> str:  # ERROR: Cannot override sealed method
        return "Oops"
```

### Recommendations

| Option | Description | Trade-off |
|--------|-------------|-----------|
| **A. Always require @override** | Explicit, matches C# | Un-Pythonic, confuses new users |
| **B. Implicit @override for Object methods** (**Chosen**) | `__str__`, `__eq__`, `__hash__` auto-override | Magic behavior, inconsistent with other dunders |
| **C. @override only when base explicitly defines** | If parent has `__str__`, need `@override`; if inheriting raw from `object`, no decorator needed | Complex rule, but matches intuition |
| **D. Warning, not error** | Warn if @override missing but compile anyway | Allows gradual adoption |

**Decision: Option B (Implicit @override)** for the specific dunders that map to `System.Object` methods:
- `__str__` → `ToString()`
- `__eq__(self, other: object)` → `Equals(object)` (only when parameter type is `object`)
- `__hash__` → `GetHashCode()`

The `@override` decorator is accepted but never required for these three dunders, at any inheritance depth. The compiler implicitly treats them as overrides.

**Rationale:** These three are special-cased in every language that targets .NET. Making them implicit acknowledges that "every class inherits from object" is an implementation detail that shouldn't leak into Pythonic syntax.

**For other dunders** (comparison operators, arithmetic, etc.), require explicit `@override` when the base class defines them, since those represent intentional polymorphism.

---

## 7. Iterator Protocol (`__iter__` / `__next__`)

### Issue

Python's iterator protocol (`__iter__` returns iterator, `__next__` returns next item or raises `StopIteration`) differs fundamentally from C#'s (`IEnumerable<T>.GetEnumerator()` returns `IEnumerator<T>`, `MoveNext()` returns bool, `Current` property holds value).

### Problematic Scenarios

```python
# Scenario 1: Self-iterating class (common Python pattern)
class Counter:
    current: int
    max: int

    def __init__(self, max: int):
        self.current = 0
        self.max = max

    def __iter__(self) -> Counter:  # Returns self
        return self

    def __next__(self) -> int:
        if self.current >= self.max:
            raise StopIteration()  # How does this map to MoveNext() -> false?
        result = self.current
        self.current += 1
        return result

# Problems:
# 1. Counter is both IEnumerable<int> AND IEnumerator<int>
# 2. StopIteration must map to MoveNext() returning false
# 3. State management differs (Python resets on __iter__, C# creates new enumerator)
```

```python
# Scenario 2: Separate iterator class
class Numbers:
    data: list[int]

    def __iter__(self) -> NumbersIterator:
        return NumbersIterator(self.data)

class NumbersIterator:
    data: list[int]
    index: int

    def __init__(self, data: list[int]):
        self.data = data
        self.index = 0

    def __next__(self) -> int:
        if self.index >= len(self.data):
            raise StopIteration()
        result = self.data[self.index]
        self.index += 1
        return result

    # Missing: __iter__ returning self (required for Python iterator protocol)
```

### Inheritance Edge Case

```python
# base.spy
class BaseIterable:
    def __iter__(self) -> Iterator[int]:
        yield 1
        yield 2

# derived.spy
class DerivedIterable(BaseIterable):
    @override
    def __iter__(self) -> Iterator[int]:
        yield from super().__iter__()
        yield 3
```

### Interop Edge Case

```csharp
// C# consuming Sharpy iterator
foreach (var item in sharpyIterable)  // Works if IEnumerable<T> implemented
{
    Console.WriteLine(item);
}

// C# implementing IEnumerable, consumed by Sharpy
public class CSharpIterable : IEnumerable<int>
{
    public IEnumerator<int> GetEnumerator() => ...;
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

// Sharpy:
for item in csharp_iterable:  # Should work
    print(item)
```

### Recommendations

1. **`StopIteration` handling:** The compiler or runtime must catch `StopIteration` and convert to `MoveNext() -> false`. This requires wrapping user's `__next__` logic.

2. **Generated code for `__next__`:**
   ```csharp
   // User writes:
   // def __next__(self) -> int:
   //     if done: raise StopIteration()
   //     return value

   // Compiler generates:
   private int _current;
   private bool _hasNext;

   public bool MoveNext()
   {
       try
       {
           _current = __next__impl();  // User's logic
           _hasNext = true;
           return true;
       }
       catch (StopIterationException)
       {
           _hasNext = false;
           return false;
       }
   }

   public int Current => _hasNext ? _current : throw new InvalidOperationException();
   ```

3. **Self-iterating types:** If a class defines both `__iter__` returning `self` and `__next__`, implement both `IEnumerable<T>` and `IEnumerator<T>`:
   ```csharp
   public class Counter : IEnumerable<int>, IEnumerator<int>
   {
       public IEnumerator<int> GetEnumerator() => this;  // Returns self
       // ... MoveNext, Current, Reset, Dispose ...
   }
   ```

   **Warning:** This pattern means the same instance is reused, which can cause issues with nested iteration. Consider warning users.

4. **`Iterator[T]` type:** Define as:
   ```csharp
   public interface Iterator<T> : IEnumerable<T>, IEnumerator<T>
   {
       // Combines both interfaces for Python-style iterators
   }
   ```

5. **Generator functions (`yield`):** If supporting generator syntax, the compiler should generate a state machine class similar to C#'s iterator methods. This is a significant undertaking.

6. **`for` loop compilation:**
   ```python
   for item in iterable:
       process(item)
   ```
   Compiles to:
   ```csharp
   foreach (var item in iterable)  // Works for any IEnumerable<T>
   {
       Process(item);
   }
   ```

---

## 8. Callable Objects (`__call__`)

### Issue

Python's `__call__` makes instances callable: `obj()` invokes `obj.__call__()`. C# has no direct equivalent — objects are not callable unless they're delegates.

### Problematic Scenarios

```python
# Scenario 1: Function-like objects
class Adder:
    amount: int

    def __init__(self, amount: int):
        self.amount = amount

    def __call__(self, x: int) -> int:
        return x + self.amount

add_five = Adder(5)
result = add_five(10)  # Python: 15. Sharpy: ???
```

```python
# Scenario 2: Decorators that return callable objects
class Memoize:
    func: Callable[[int], int]
    cache: dict[int, int]

    def __init__(self, func: Callable[[int], int]):
        self.func = func
        self.cache = {}

    def __call__(self, x: int) -> int:
        if x not in self.cache:
            self.cache[x] = self.func(x)
        return self.cache[x]

@Memoize
def fib(n: int) -> int:
    if n <= 1:
        return n
    return fib(n - 1) + fib(n - 2)

print(fib(10))  # Calls Memoize.__call__
```

### Interop Edge Case

```csharp
// C# code receiving a Sharpy "callable" object
public void Process(Func<int, int> func)
{
    Console.WriteLine(func(5));
}

// How does Sharpy pass an Adder instance to this?
// Adder is not a Func<int, int>
```

### Recommendations

| Option | Description | Trade-off |
|--------|-------------|-----------|
| **A. Not supported** (current) | Use explicit `Invoke()` method | Breaks Python idioms significantly |
| **B. Invoke method + syntax sugar** | `__call__` → `Invoke()`, compiler treats `obj()` as `obj.Invoke()` | Works for Sharpy code, breaks .NET interop |
| **C. Implicit delegate conversion** | `__call__` generates implicit conversion to matching `Func<>` or `Action<>` | Complex generic matching, allocation overhead |
| **D. ICallable<TResult> interface** | Define interface, generate implementation | Enables pattern matching in Sharpy ecosystem |

**Recommended: Option B + D hybrid:**

1. `__call__` generates `public TResult Invoke(TArgs...)` method
2. Compiler recognizes `obj(args)` syntax and emits `obj.Invoke(args)` for types with `__call__`
3. Define `ICallable<T1, ..., TResult>` interfaces for interop scenarios
4. Optionally generate implicit conversion to `Func<>/Action<>` (with allocation warning)

**Example generated code:**
```csharp
public class Adder : ICallable<int, int>
{
    public int Invoke(int x) => x + _amount;

    // Optional: implicit conversion for delegate interop
    public static implicit operator Func<int, int>(Adder a) => a.Invoke;
}
```

**Limitation to document:** Unlike Python, you cannot pass an `Adder` directly to a method expecting `Func<int, int>` without the implicit conversion. This is a .NET limitation.

---

## 9. Context Managers (`__enter__` / `__exit__`)

### Issue

Python's context managers (`with` statement) have richer semantics than C#'s `IDisposable` (`using` statement):
- `__enter__` returns a value bound by `as`
- `__exit__` receives exception info and can suppress exceptions by returning `True`

### Problematic Scenarios

```python
# Scenario 1: Exception suppression
class SuppressErrors:
    def __enter__(self) -> SuppressErrors:
        return self

    def __exit__(self, exc_type: type?, exc_val: Exception?, exc_tb: object?) -> bool:
        if exc_type is not None:
            print(f"Suppressed: {exc_val}")
            return True  # Suppress the exception
        return False

with SuppressErrors():
    raise ValueError("oops")  # Should be suppressed

print("Continues normally")  # Should print
```

```python
# Scenario 2: Return value from __enter__
class Connection:
    def __enter__(self) -> Cursor:  # Returns different type!
        self._conn = open_connection()
        return self._conn.cursor()

    def __exit__(self, *args) -> bool:
        self._conn.close()
        return False

with Connection() as cursor:  # cursor is Cursor, not Connection
    cursor.execute("SELECT 1")
```

### Interop Edge Case

```csharp
// C# IDisposable doesn't have enter/exit semantics
public class CSharpResource : IDisposable
{
    public void Dispose() => /* cleanup */;
}

// Sharpy using C# IDisposable:
with CSharpResource() as r:  # What does 'r' bind to? The resource itself?
    use(r)
```

### Recommendations

1. **`with` statement for `IDisposable`:** If a type implements `IDisposable` but not `__enter__`/`__exit__`:
   ```python
   with resource as r:
       use(r)
   ```
   Compiles to:
   ```csharp
   using (var r = resource)
   {
       Use(r);
   }
   ```
   The bound variable `r` is the resource itself.

2. **Full context manager protocol:** For types with `__enter__`/`__exit__`:
   - Generate wrapper methods that the compiler recognizes
   - `with` statement compiles to try/finally with exception handling

   ```csharp
   // Generated for: with manager as value:
   var __mgr = manager;
   var value = __mgr.__enter__();
   bool __suppress = false;
   try
   {
       // body
   }
   catch (Exception __ex)
   {
       __suppress = __mgr.__exit__(__ex.GetType(), __ex, null);
       if (!__suppress) throw;
   }
   finally
   {
       if (!__suppress)
           __mgr.__exit__(null, null, null);
   }
   ```

3. **Interface definition:**
   ```csharp
   public interface IContextManager<T>
   {
       T Enter();
       bool Exit(Type? excType, Exception? excVal, object? excTb);
   }
   ```

4. **`__enter__` return type:** The return type of `__enter__` determines the type of the `as` binding. If omitted, defaults to `Self`.

5. **Traceback object:** Python's `exc_tb` is a traceback object. In .NET, stack traces are part of `Exception`. Consider:
   - Pass `null` always for `exc_tb`
   - Or pass `exc_val.StackTrace` as string
   - Or define a `Traceback` wrapper type

---

## 10. Indexing and Slicing (`__getitem__` / `__setitem__`)

### Issue

Python's indexing supports integers, slices (`x[1:3]`), and arbitrary keys. C# indexers are more limited, especially before C# 8's range support.

### Problematic Scenarios

```python
# Scenario 1: Slice notation
class MyList:
    items: list[int]

    def __getitem__(self, index: int) -> int:
        return self.items[index]

    def __getitem__(self, index: slice) -> list[int]:  # Slice overload
        return self.items[index.start:index.stop:index.step]

my_list = MyList([1, 2, 3, 4, 5])
print(my_list[1:3])  # How does this work?
```

```python
# Scenario 2: Multi-dimensional indexing
class Matrix:
    def __getitem__(self, indices: tuple[int, int]) -> float:
        row, col = indices
        return self._data[row][col]

m = Matrix()
print(m[1, 2])  # Python passes (1, 2) tuple to __getitem__
```

```python
# Scenario 3: Negative indexing
class MySequence:
    def __getitem__(self, index: int) -> int:
        # Python convention: -1 means last element
        if index < 0:
            index = len(self) + index
        return self._data[index]
```

### Interop Edge Case

```csharp
// C# 8+ Range/Index support
public class CSharpCollection
{
    public int this[Index index] => /* ... */;
    public int[] this[Range range] => /* ... */;
}

// Sharpy consuming:
csharp_coll[-1]    // Should use Index.FromEnd(1)
csharp_coll[1..3]  // Should use Range
```

### Recommendations

1. **Integer indexing:** `__getitem__(self, index: int) -> T` → `public T this[int index] { get; }`

2. **Negative indexing (Sharpy types):** The compiler should NOT automatically handle negative indices in the indexer. Instead:
   - Sharpy.Core collection types handle it in their indexer implementation
   - User-defined types must handle it explicitly in `__getitem__`
   - Document this clearly

3. **Slice syntax:** `x[start:stop:step]` should:
   - For Sharpy types: call a `Slice(start, stop, step)` method (not `__getitem__`)
   - For .NET types with `Range` indexer: convert to `Range` (C# 8+)
   - Define a `Slice` type in Sharpy.Core if needed

4. **Multi-dimensional indexing:**
   ```python
   def __getitem__(self, row: int, col: int) -> float: ...
   ```
   Generates multi-parameter indexer:
   ```csharp
   public float this[int row, int col] { get; }
   ```

   Alternatively, tuple overload:
   ```python
   def __getitem__(self, indices: tuple[int, int]) -> float: ...
   ```
   Requires unpacking at call site or tuple indexer.

5. **Key-based indexing (dict-like):**
   ```python
   def __getitem__(self, key: str) -> int: ...
   ```
   Generates:
   ```csharp
   public int this[string key] { get; }
   ```

6. **Read-only vs read-write:**
   - `__getitem__` only → `{ get; }` indexer
   - `__setitem__` only → `{ set; }` indexer (unusual but valid)
   - Both → `{ get; set; }` indexer

---

## 11. Cross-Dunder Synthesis Rules

### Issue

The spec allows calling dunders on `self` within dunder methods for synthesis (e.g., `__le__` calling `__lt__` and `__eq__`). This requires context-sensitive parsing and semantic analysis.

### Problematic Scenarios

```python
# Scenario 1: Valid cross-dunder call
class Ordered:
    def __lt__(self, other: Ordered) -> bool: ...
    def __eq__(self, other: object) -> bool: ...

    def __le__(self, other: Ordered) -> bool:
        return self.__lt__(other) or self.__eq__(other)  # OK

# Scenario 2: Invalid - not immediate call
class Bad:
    def __eq__(self, other: object) -> bool: ...

    def __ne__(self, other: object) -> bool:
        eq_func = self.__eq__  # ERROR: Cannot capture dunder reference
        return not eq_func(other)

# Scenario 3: Invalid - wrong receiver
class Also_Bad:
    other_obj: Ordered

    def __lt__(self, other: Ordered) -> bool:
        return self.other_obj.__lt__(other)  # ERROR: Not self or super()
```

### Inheritance Edge Case

```python
class Base:
    def __eq__(self, other: object) -> bool:
        return True

class Derived(Base):
    def __ne__(self, other: object) -> bool:
        return not super().__eq__(other)  # OK: super() is allowed

    def __lt__(self, other: Derived) -> bool:
        return not self.__eq__(other)  # OK: self dunder call
```

### Recommendations

1. **Formal rules for valid cross-dunder calls:**

   A dunder call `receiver.__dunder__(args)` is valid if and only if:
   - The call site is inside a dunder method body
   - The receiver is exactly `self` or `super()`
   - The call is an immediate invocation (call expression), not:
     - Assigned to a variable
     - Passed as an argument
     - Used in any non-call expression context
   - The target dunder is defined on the same class or a base class

2. **Compiler implementation:**

   ```
   // Pseudo-code for semantic checker
   function checkDunderCall(call):
       if not isInsideDunderMethod(currentContext):
           error("Dunder calls only allowed inside dunder methods")

       if call.receiver is not (SelfExpression or SuperExpression):
           error("Dunder calls must be on 'self' or 'super()'")

       if call.parent is not CallExpression:
           error("Dunders cannot be captured or passed, only called immediately")
   ```

3. **Alternative: Use operators inside dunders:**

   Consider allowing operators as an alternative to cross-dunder calls:
   ```python
   def __le__(self, other: Ordered) -> bool:
       return self < other or self == other  # Use operators instead
   ```

   **Concern:** This could cause infinite recursion if not careful:
   ```python
   def __lt__(self, other: Ordered) -> bool:
       return not (self >= other)  # Calls __ge__, which might call __lt__!
   ```

4. **Document the recursion risk:** Users must ensure their cross-dunder calls don't create cycles.

5. **Consider a lint/warning for potential cycles:** Static analysis could detect simple cases like `__lt__` → `__ge__` → `__lt__`.

---

## 12. Implicit Interface Implementation

### Issue

Several dunders synthesize interface implementations (`IEnumerable<T>`, `IEquatable<T>`, etc.). Users may not realize their types now implement these interfaces.

### Problematic Scenarios

```python
# Scenario 1: Surprise interface compliance
class MyContainer:
    def __iter__(self) -> Iterator[int]:
        yield 1
        yield 2

# User may not realize MyContainer : IEnumerable<int>
# Now valid: IEnumerable<int> x = MyContainer()  # Implicit upcast
```

```python
# Scenario 2: Conflicting inheritance
from dotnet import SomeBaseClass  # Already implements IEnumerable<string>

class MyClass(SomeBaseClass):
    def __iter__(self) -> Iterator[int]:  # Wants IEnumerable<int>
        yield 1

# Conflict: Base has IEnumerable<string>, derived wants IEnumerable<int>
```

### Interop Edge Case

```csharp
// C# code that checks for interface
public void Process(object obj)
{
    if (obj is IEnumerable<int> enumerable)
    {
        foreach (var i in enumerable)
            Console.WriteLine(i);
    }
}

// Sharpy type with __iter__ passes this check unexpectedly
```

### Recommendations

1. **Explicit interface list:** Require users to explicitly declare interface implementation:
   ```python
   class MyContainer(IEnumerable[int]):  # Explicit
       def __iter__(self) -> Iterator[int]:
           yield 1
   ```

   If `__iter__` is defined without the interface in the inheritance list, generate the method but NOT the interface implementation.

2. **Alternative: Always implicit with documentation:** Accept that dunders imply interfaces, but document this clearly:
   ```
   Defining __iter__ automatically implements IEnumerable<T>.
   Your type becomes assignable to IEnumerable<T> variables.
   ```

3. **Conflict detection:** If a base class already implements `IEnumerable<T>` with a different `T`, this is an error:
   ```
   error SPY0301: Cannot implement IEnumerable<int> because base class
   SomeBaseClass already implements IEnumerable<string>
   ```

4. **Explicit vs implicit interface implementation:** Use explicit implementation to avoid conflicts:
   ```csharp
   // Generated for __iter__
   IEnumerator<int> IEnumerable<int>.GetEnumerator() => /* ... */;

   // Not: public IEnumerator<int> GetEnumerator()
   ```

   This means `obj.GetEnumerator()` won't compile — must cast to `IEnumerable<int>` first. This is more restrictive but avoids name conflicts.

5. **Interface mapping table:**

   | Dunder | Synthesized Interface | Notes |
   |--------|----------------------|-------|
   | `__iter__` | `IEnumerable<T>` | Return type determines `T` |
   | `__next__` | `IEnumerator<T>` | Usually combined with `__iter__` |
   | `__len__` | `ISized` (custom) or none | .NET has no standard "has Count" interface |
   | `__contains__` | None (just method) | Could map to `ICollection<T>.Contains` but risky |
   | `__eq__` | `IEquatable<T>` | For each overload type `T` |
   | `__hash__` | None | `GetHashCode()` is not interface-based |
   | `__enter__`/`__exit__` | `IDisposable` + custom | See context manager section |

---

## 13. Comparison Operator Synthesis

### Issue

Python's `functools.total_ordering` decorator synthesizes comparison operators from `__eq__` and one of `__lt__`, `__le__`, `__gt__`, `__ge__`. Should Sharpy do this automatically?

### Problematic Scenarios

```python
# Scenario 1: Partial comparison operators
class Version:
    major: int
    minor: int

    def __eq__(self, other: Version) -> bool:
        return self.major == other.major and self.minor == other.minor

    def __lt__(self, other: Version) -> bool:
        if self.major != other.major:
            return self.major < other.major
        return self.minor < other.minor

    # User expects <=, >, >= to work automatically

v1 = Version(1, 0)
v2 = Version(2, 0)
print(v1 <= v2)  # ERROR if __le__ not defined and no synthesis
```

```python
# Scenario 2: Non-total ordering
class PartiallyOrdered:
    # Not all instances are comparable
    def __lt__(self, other: PartiallyOrdered) -> bool:
        # Returns False for incomparable pairs, not error
        ...

    # Auto-synthesis of __le__ as __lt__ or __eq__ would be WRONG
    # for partial orderings
```

### Recommendations

1. **No automatic synthesis by default:** Don't automatically synthesize missing comparison operators. This avoids bugs for partial orderings.

2. **`@total_ordering` decorator:**
   ```python
   @total_ordering
   class Version:
       def __eq__(self, other: Version) -> bool: ...
       def __lt__(self, other: Version) -> bool: ...
       # __le__, __gt__, __ge__ synthesized
   ```

3. **`IComparable<T>` integration:** If implementing `IComparable<T>`, synthesize all comparison operators from `CompareTo()`:
   ```python
   class Version(IComparable[Version]):
       def compare_to(self, other: Version) -> int:
           # Return <0, 0, or >0
           ...

       # All comparison operators synthesized from compare_to
   ```

4. **Synthesis rules for `@total_ordering`:**
   - Requires `__eq__` and exactly one of `__lt__`, `__le__`, `__gt__`, `__ge__`
   - Synthesis formulas:
     - `__le__` = `__lt__ or __eq__`
     - `__ge__` = `not __lt__`
     - `__gt__` = `not __le__`
     - (Adjust based on which one is provided)

5. **C# `IComparable` awareness:** If a base class implements `IComparable<T>`, suggest using that rather than defining individual comparison operators.

---

## 14. Interop Scenarios

This section consolidates complex interop scenarios that span multiple dunder methods.

### Scenario A: Sharpy Type Inherited in C#, Then Back to Sharpy

```python
# sharpy_lib.spy (compiled to SharpyLib.dll)
class SharpyBase:
    value: int

    def __init__(self, value: int):
        self.value = value

    def __str__(self) -> str:
        return f"SharpyBase({self.value})"

    def __eq__(self, other: SharpyBase) -> bool:
        return self.value == other.value

    def __hash__(self) -> int:
        return hash(self.value)
```

```csharp
// CSharpMiddle.dll, references SharpyLib.dll
public class CSharpMiddle : SharpyBase
{
    public string Extra { get; set; }

    public CSharpMiddle(int value, string extra) : base(value)
    {
        Extra = extra;
    }

    public override string ToString() => $"CSharpMiddle({Value}, {Extra})";

    public override bool Equals(object obj)
    {
        if (obj is CSharpMiddle other)
            return base.Equals(other) && Extra == other.Extra;
        return false;
    }

    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Extra);
}
```

```python
# sharpy_consumer.spy, references CSharpMiddle.dll
from CSharpMiddle import CSharpMiddle

class SharpyFinal(CSharpMiddle):
    extra2: str

    def __init__(self, value: int, extra: str, extra2: str):
        super().__init__(value, extra)
        self.extra2 = extra2

    @override
    def __str__(self) -> str:
        return f"SharpyFinal({self.value}, {self.extra}, {self.extra2})"

    @override
    def __eq__(self, other: object) -> bool:
        if not isinstance(other, SharpyFinal):
            return False
        return super().__eq__(other) and self.extra2 == other.extra2

    @override
    def __hash__(self) -> int:
        return hash((super().__hash__(), self.extra2))
```

**Issues to verify:**
1. `super().__init__` chains correctly through C# constructor
2. `super().__str__()` calls C#'s `ToString()` override
3. `super().__eq__()` calls C#'s `Equals()` override
4. `super().__hash__()` calls C#'s `GetHashCode()` override
5. Polymorphism works: `SharpyBase b = SharpyFinal(...)` then `str(b)` calls `SharpyFinal.__str__`

### Scenario B: C# Code Using Sharpy Operators

```python
# vectors.spy
class Vector:
    x: float
    y: float

    def __add__(self, other: Vector) -> Vector:
        return Vector(self.x + other.x, self.y + other.y)

    def __mul__(self, scalar: float) -> Vector:
        return Vector(self.x * scalar, self.y * scalar)

    def __rmul__(self, scalar: float) -> Vector:
        return self * scalar
```

```csharp
// C# consumer
var v1 = new Vector(1, 2);
var v2 = new Vector(3, 4);

var sum = v1 + v2;        // Uses operator+(Vector, Vector)
var scaled = v1 * 2.0;    // Uses operator*(Vector, double)
var scaled2 = 2.0 * v1;   // Uses operator*(double, Vector) from __rmul__
```

**Issues to verify:**
1. All operators are visible as `public static`
2. Overload resolution picks correct operator
3. No ambiguity between `__mul__` and `__rmul__` operators

### Scenario C: Sharpy Iterator Consumed by LINQ

```python
# iterable.spy
class Fibonacci:
    limit: int

    def __init__(self, limit: int):
        self.limit = limit

    def __iter__(self) -> Iterator[int]:
        a, b = 0, 1
        while a < self.limit:
            yield a
            a, b = b, a + b
```

```csharp
// C# consumer
var fib = new Fibonacci(100);

// LINQ operations
var evenFibs = fib.Where(x => x % 2 == 0).ToList();
var sumFibs = fib.Sum();
var firstFive = fib.Take(5).ToArray();
```

**Issues to verify:**
1. `Fibonacci` implements `IEnumerable<int>`
2. Generator state machine works correctly
3. Multiple enumeration creates fresh iterators
4. LINQ methods chain correctly

### Scenario D: Sharpy Context Manager with C# Exceptions

```python
# transaction.spy
class Transaction:
    committed: bool

    def __init__(self):
        self.committed = False

    def __enter__(self) -> Transaction:
        print("Begin transaction")
        return self

    def __exit__(self, exc_type: type?, exc_val: Exception?, exc_tb: object?) -> bool:
        if exc_type is None:
            print("Commit transaction")
            self.committed = True
        else:
            print(f"Rollback transaction due to {exc_val}")
        return False  # Don't suppress exceptions

    def execute(self, sql: str) -> None:
        if "DROP" in sql:
            raise PermissionError("DROP not allowed")
        print(f"Executing: {sql}")
```

```csharp
// C# consumer
using var tx = new Transaction();  // Calls __enter__, __exit__ via IDisposable?
tx.Execute("SELECT 1");
// What happens here? C# using doesn't support __enter__/__exit__ semantics
```

**Issue:** C#'s `using` only calls `Dispose()`, not `__enter__`/`__exit__`. Options:
1. Generate `IDisposable.Dispose()` that calls `__exit__(null, null, null)`
2. Require explicit Sharpy `with` statement for full context manager semantics
3. Document that C# `using` is limited compared to Sharpy `with`

---

## Summary: Priority Recommendations

### Must Fix Before 1.0

1. **`__eq__` without `__hash__`** — Error or strong warning
2. **`Equals(object)` synthesis** — Document dispatch order, warn on shadowing
3. **Reflected operators** — Document they're NOT Python fallback semantics
4. **Iterator `StopIteration` handling** — Must map to `MoveNext() -> false`

### Should Fix Before 1.0

5. **`@override` for Object methods** — Make implicit for `__str__`/`__eq__`/`__hash__`
6. **Cross-dunder call rules** — Document formal rules, implement in semantic checker
7. **Interface synthesis** — Decide explicit vs implicit, document behavior
8. **`bool()` dispatch** — Implement `ISized` or equivalent

### Can Defer Past 1.0

9. **`__call__` support** — Complex but high-value for Pythonic code
10. **Full `__enter__`/`__exit__`** — Complex exception semantics
11. **`@total_ordering`** — Nice-to-have decorator
12. **`__repr__`** — Nice-to-have, not critical

---

## Appendix: Decision Log Template

For each issue, track the decision:

| Issue | Decision | Rationale | Spec Section to Update |
|-------|----------|-----------|------------------------|
| `@override` for Object methods | Option B: Implicit `@override` for `__str__`, `__eq__`, `__hash__` | These always override `System.Object`; requiring `@override` is un-Pythonic friction without safety benefit | `dunder_invocation_rules.md`, `dunder_methods.md` |
| `__eq__` without `__hash__` | | | |
| Equals dispatch order | No automatic synthesis. 1:1 mapping of `__eq__` to `Equals`. Only `__eq__(self, other: object)` generates `override`. Warning SPY0454 when no `object` overload. | Simpler, predictable, avoids ambiguous dispatch ordering | `dunder_methods.md` |
| Reflected operator semantics | | | |
| ... | | | |
