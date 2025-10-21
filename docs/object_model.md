# Object Model

# System.Object

The .NET top-level object `System.Object` is the base class
of all Sharpy builtin types, with `ValueType` as an
intermediary for `struct` (a.k.a. value types) in Sharpy.

Member operators that are defined and have a C# static
equivalent, e.g. `__add__` and `operator +`, will cause the
C# static equivalent to be automatically synthesized. This
allows virtual dispatch at runtime to subclass overrides of
an operator. It is possible for the user to manually define
the corresponding static operator.

No user-defined object members or methods can take the
form of `__[A-Za-z0-9_]*__`, i.e. no user-defined member
or method on a object can look like a dunder method. The
exception to this is if it is used as an override or
overload of the corresponding dunder method, since
most dunder methods are virtual (e.g. the operators) and
can be overloaded.

All Sharpy objects have an automatically synthesized
static `==` operator delegating to the `equals()` method
from the .NET object model. If there are any overloads,
their static equivalents are also synthesized.

This creates one subtle difference for .NET programmers
where even though a Sharpy type doesn't explicitly define
the static `==` operator, using `==` with it will invoke
equality checking (assuming `equals()` is overridden with
equality checking behavior), rather than reference checking.

For Python developers, this difference does not exist
because it is exactly how it works in Python.

| Concept | C#/.NET | Python | Sharpy | Notes |
| - | - | - | - | - |
| Constructor | `Foo(...)` | `def __init__(self, ...)` | `def __init__(self, ...)` | Can have overloads. Has no return type. |
| Destructor/finalizer | `~Foo()` | `__del__(self)` | `__del__(self)` | TODO |
| String representation | `string ToString()` | `def __str__(self) -> str` | `def __str__(self) -> str` | This is a compiler intrinsic alias to `to_string()`. |
| Representation | N/A | `def __repr__(self) -> str` | N/A | Not supported in .NET. This method in Python is designed for use in code generation for REPLs. |
| Equality check | `bool Equals(object?)` | `def __eq__(self, other: object \| None) -> bool` | `def __eq__(self, other: object?) -> bool` | Must override both `__eq__` and `__hash__`. This is a compiler intrinsic alias to `equals()`. This automatically synthesizes the equivalent static `==` operator. |
| Inequality check | N/A | `def __ne__(self, other: object \| None) -> bool` | N/A | Not supported in .NET. |
| Equality check with `T` | `bool Equals(T)` | N/A | `def __eq__(self, other: T) -> bool` | Automatically synthesizes the equivalent static `==` operator. |
| Reference equality | `static bool System.Object.ReferenceEquals(T a, T b)` | `a is b` | `a is b` | - |
| Hashing | `int GetHashCode()` | `def __hash__(self) -> int` | `def __hash__(self) -> int` | Must override both `__eq__` and `__hash__`. This is a compiler intrinsic alias to `get_hash_code()`. |
| Less than | `static bool operator <(object lhs, object rhs)` | `def __lt__(self, other: object) -> bool` | `def __lt__(self, other: object) -> bool` | Can have overloads. Automatically synthesizes the equivalent static `<` operator. |
| Less than or equal | `static bool operator <=(object lhs, object rhs)` | `__le__(self)` | `def __le__(self, other: object) -> bool` | Can have overloads. Automatically synthesizes the equivalent static `<=` operator. |
| Greater than or equal | `static bool operator >(object lhs, object rhs)` | `__gt__(self)` | `def __gt__(self, other: object) -> bool` | Can have overloads. Automatically synthesizes the equivalent static `>` operator. |
| Greater than or equal | `static bool operator >=(object lhs, object rhs)` | `__ge__(self)` | `def __ge__(self, other: object) -> bool` | Can have overloads. Automatically synthesizes the equivalent static `>=` operator. |
| Addition | `static T operator +(T lhs, T rhs)` | `__add__(self)` | `def __add__(self, other: object) -> bool` | Can have overloads. Automatically synthesizes the equivalent static `+` operator. |
| In-place addition | N/A | `__iadd__(self)` | N/A | Not supported in .NET. |
| Subtraction | `static T operator -(T lhs, T rhs)` | `__sub__(self)` | `def __sub__(self, other: object) -> bool` | Can have overloads. Automatically synthesizes the equivalent static `-` operator. |
| In-place subtraction | N/A | `__isub__(self)` | N/A | Not supported in .NET. |
| Multiplication | `static T operator *(T lhs, T rhs)` | `__mul__(self)` | `def __mul__(self, other: object) -> bool` | Can have overloads. Automatically synthesizes the equivalent static `*` operator. |
| In-place multiplication | N/A | `__imul__(self)` | N/A | Not supported in .NET. |
| True division | `static T operator /(T lhs, T rhs)` | `__div__(self)` | `def __div__(self, other: object) -> bool` | Can have overloads. Automatically synthesizes the equivalent static `/` operator. |
| In-place true division | N/A | `__idiv__(self)` | N/A | Not supported in .NET. |
| Negation | `static T operator -(T value)` | `__neg__(self)` | `def __neg__(self) -> T` | Automatically synthesizes the equivalent static `-` operator. |
| Inversion | `static T operator !(T value)` | `__invert__(self)` | `def __invert__(self) -> T` | Automatically synthesizes the equivalent static `!` operator. |
| Boolean conversion | `static operator bool(T value)` | `__bool__(self)` | `def __bool(self)` | Automatically synthesizes `__true__` and `__false__`. |
| True conversion | `static bool operator true(T value)` | - | `def __true__(self) -> bool` | Automatically synthesizes `__false__`. |
| False conversion | `static bool operator false(T value)` | - | `def __false__(self) -> bool` | Automatically synthesizes `__true__`. |
| Get indexing | `T this[K] { get; }` | `def __getitem__(self, key: K) -> T` | `def __getitem__(key: K) -> T` | - |
| Set indexing | `T this[K] { set; }` | `def __setitem__(self, key: K, value: T)` | `def __setitem__(key: K, value: T)` | - |
| Delete indexing | N/A | `def __delitem__(self, key: K)` | TODO | - |
| Get slice indexing | N/A | `def __getitem__(self, key: K) -> T` | `def __getitem__(slice: slice) -> S[T]` | - |
| Set slice indexing | N/A | `def __setitem__(self, slice: slice, value: T)` | `def __setitem__(slice: slice, value: S[T])` | - |
| Delete slice indexing | N/A | `def __delitem__(self, slice: slice)` | TODO | - |
| Callable objects | `Invoke(...)` | `def __call__(self, ...)` | TODO | - |
| Enumerable | `System.Collections.Generic.IEnumerator<T> GetEnumerator()` implementing `System.Collections.Generic.IEnumerable<T>` | `def __iter__(self) -> Iterable[T]` | `def __iter__(self) -> Iterator[T]` implementing `Iterable[T]` | - |
| Enumerator | `System.Collections.Generic.IEnumerator<T> GetEnumerator()` implementing `System.Collections.Generic.IEnumerable<T>` | `def __next__(self) -> T` | `def __next__(self) -> T` implementing `Iterator[T]` | ??? |
| Context manager | `IDisposable` | `def __enter__(self) -> T` | `def __enter__(self) -> T` implementing `ContextManager[T]` | ??? |
| Context manager | `IDisposable` | `def __exit__(self) -> T` | `def __exit__(self)` implementing `ContextManager[T]` | ??? |
| Cloning | `ICloneable.Clone()` | `def __copy__(self) -> T` | ??? | ??? |
| Deep Cloning | `ICloneable.Clone()` | `def __deepcopy__(self) -> T` | ??? | ??? |
