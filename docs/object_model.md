# Object Model

# System.Object

The .NET top-level object `System.Object` is the base class of all Sharpy
builtin types, with `ValueType` as an intermediary for `struct` (a.k.a. value
types) in Sharpy.

Member operators that are defined and have a C# static equivalent, e.g.
`__add__` and `operator +`, will cause the C# static equivalent to be
automatically synthesized. This allows virtual dispatch at runtime to subclass
overrides of an operator.

| Concept | C#/.NET | Python | Sharpy | Notes |
| - | - | - | - | - |
| Constructor | `public Foo(...)` | `def __init__(self, ...)` | `def __init__(self, ...)` | - |
| Destructor/finalizer | `~Foo()` | `__del__(self)` | `__del__(self)` | ??? |
| String representation | `string ToString()` | `def __str__(self) -> str` | `def __str__(self) -> str` | - |
| Representation | N/A | `def __repr__(self) -> str` | N/A | - |
| Equality check | `bool Equals(object?)` | `def __eq__(self, other: object \| None) -> bool` | `def __eq__(self, other: object?) -> bool` | Must override both `__eq__` and `__hash__` |
| Inequality check | N/A | `def __ne__(self, other: object \| None) -> bool` | N/A | - |
| Equality check with `T` | `bool Equals(T)` | N/A | `def __eq__(self, other: T) -> bool` | ??? |
| Reference equality | `static bool System.Object.ReferenceEquals(T a, T b)` | `a is b` | `a is b` | - |
| Hashing | `int GetHashCode()` | `def __hash__(self) -> int` | `def __hash__(self) -> int` | Must override both `__eq__` and `__hash__` |
| Less than | `static bool operator <(object lhs, object rhs)` | `def __lt__(self, other: object) -> bool` | `def __lt__(self, other: object) -> bool` | - |
| Less than or equal | `static bool operator <=(object lhs, object rhs)` | `__le__(self)` | `def __le__(self, other: object) -> bool` | - |
| Greater than or equal | `static bool operator >(object lhs, object rhs)` | `__gt__(self)` | `def __gt__(self, other: object) -> bool` | - |
| Greater than or equal | `static bool operator >=(object lhs, object rhs)` | `__ge__(self)` | `def __ge__(self, other: object) -> bool` | - |
| Addition | `static T operator +(T lhs, T rhs)` | `__add__(self)` | `def __add__(self, other: object) -> bool` | - |
| In-place addition | N/A | `__iadd__(self)` | N/A | - |
| Subtraction | `static T operator -(T lhs, T rhs)` | `__sub__(self)` | `def __sub__(self, other: object) -> bool` | Note that this is an overload of `def __-` with 1 argument |
| In-place subtraction | N/A | `__isub__(self)` | N/A | - |
| Multiplication | `static T operator *(T lhs, T rhs)` | `__mul__(self)` | `def __mul__(self, other: object) -> bool` | - |
| In-place multiplication | N/A | `__imul__(self)` | N/A | - |
| True division | `static T operator /(T lhs, T rhs)` | `__div__(self)` | `def __div__(self, other: object) -> bool` | - |
| In-place true division | N/A | `__idiv__(self)` | N/A | - |
| Negation | `static T operator -(T value)` | `__neg__(self)` | `def __neg__(self) -> T` | Note that this is an overload of `def __-` with 2 arguments |
| Inversion | `static T operator !(T value)` | `__invert__(self)` | `def __invert__(self) -> T` | - |
| Boolean conversion | `static operator bool(T value)` | `__bool__(self)` | `def __bool(self)` | Automatically synthesizes `__true__` and `__false__` |
| True conversion | `static bool operator true(T value)` | - | `def __true__(self) -> bool` | Must override both `__true__` and `__false__` |
| False conversion | `static bool operator false(T value)` | - | `def __false__(self) -> bool` | Must override both `__true__` and `__false__` |
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
