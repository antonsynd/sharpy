# Object Model

## Objects

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
| Constructor | `Foo(...)` | `def __init__(self, ...)` | `def __init__(self, ...)` | Unlike Python, Sharpy constructors can have overloads. It is a compile-time error to provide a return type as it is implied. |
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
| Boolean conversion | `static operator bool(T value)` | `__bool__(self)` | `def __bool__(self)` | Automatically synthesizes `__true__` and `__false__`. |
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

## Dunder methods

Sharpy inherits dunder methods from Python. Dunder methods
are a closed set of members that are compiler-defined.
Only those supported by the compiler can be implemented,
overridden, and/or overloaded by the user.

Dunder methods typically are associated with a Sharpy
protocol (C# interface), though some are compiler
intrinsic aliases to existing methods on `System.Object`,
e.g. when a type has `__hash__()` defined in Sharpy,
then `GetHashCode()` is overridden to delegate to that
method in the generated C# code.

| Sharpy dunder method | Sharpy user invocation | C#/.NET | Notes |
| - | - | - | - |
| `__add__()` | `x + y` | `operator +()` | - |
| `__and__()` | `x & y` | `operator &()` | - |
| `__bool__()` | `bool(x)`, `if x` | `operator bool()` | - |
| `__call__()` | `x()` | `operator ()()` | - |
| `__contains__()` | `y in x` | `ICollection<T>.Contains()` | - |
| `__copy__()` | - | TODO | - |
| `__deepcopy__()` | - | TODO | - |
| `__del__()` | - | `~Foobar()` | - |
| `__delitem__()` | `del x[i]` | TODO | - |
| `__enter__()` | `with x:` | `IContextManager<T>.EnterContext()` | - |
| `__eq__()` | `==` | `Equals()`, `operator ==()`, `operator !=()` | - |
| `__exit__()` | End of `with`-block | `IContextManager<T>.ExitContext()` | - |
| `__false__()` | `if not x` | `operator false()` | - |
| `__floordiv__()` | `x // y` | N/A | Only used in Sharpy syntax. |
| `__ge__()` | `x >= y` | `operator >=()` | - |
| `__getitem__()` | `x[i]` | `this[]() { get; }` | - |
| `__gt__()` | `x > y` | `operator >()` | - |
| `__hash__()` | `hash(x)` | `GetHashCode()` | - |
| `__index__()` | - | TODO | - |
| `__init__()` | `Foobar()` | `Foobar()` | - |
| `__iter__()` | `iter(x)` | `IEnumerable<T>.GetEnumerator()` | - |
| `__invert__()` | `~x` | `operator ~()` | - |
| `__le__()` | `x <= y` | `operator <=()` | - |
| `__len__()` | `len(x)` | `Count { get; }` | - |
| `__lshift__()` | `x << y` | `operator <<()` | - |
| `__lt__()` | `x < y` | `operator <()` | - |
| `__mul__()` | `x * y` | `operator *()` | - |
| `__mod__()` | `x % y` | `operator %()` | - |
| `__ne__()` | `x != y` | `operator !=()` | - |
| `__neg__()` | `-x` | `operator -()` | Cannot have an argument. |
| `__next__()` | `next(x)` | `IEnumerator<T>.Current { get; }` and `IEnumerator<T>.MoveNext()` | `MoveNext()` is auto-generated to invoke `__next__()` and store the result in an auto-generated private member that is the source of the `Current` property. |
| `__or__()` | `x \| y` | `operator \|()` | - |
| `__pow__()` | `x ** y` | N/A | Only used in Sharpy syntax. |
| `__radd__()` | `y + x` | `operator +()` | - |
| `__reversed__()` | `reversed(x)` | TODO | - |
| `__rfloordiv__()` | `y // x` | N/A | Only used in Sharpy syntax. |
| `__rmul__()` | `y * x` | `operator *()` | - |
| `__rmod__()` | `y % x` | `operator %()` | - |
| `__rpow__()` | `y ** x` | N/A | Only used in Sharpy syntax. |
| `__rshift__()` | `x >> y` | `operator >>()` | - |
| `__rsub__()` | `y - x` | `operator -()` | - |
| `__rtruediv__()` | `y / x` | `operator /()` | - |
| `__setitem__()` | `x[i] = y` | `this[]() { set; }` | - |
| `__str__()` | `str(x)` | `ToString()` | - |
| `__sub__()` | `-` | `operator -()` | Requires one argument. |
| `__sum__()` | `sum(x)` | TODO | - |
| `__true__()` | `if x` | `operator true()` | - |
| `__truediv__()` | `x / y` | `operator /()` | - |
| `__xor__()` | `x ^ y` | `operator ^()` | - |

## Context managers

Sharpy inherits context managers from Python. An object
is a context manager if it implements the
`ContextManager` or the `ContextManager[T]` protocol,
which declares the `__enter__()` and `__exit__()` dunder
methods. The context manager protocols are underlyingly the
`Sharpy.IContextManager` and `Sharpy.IContextManager<T>`
interfaces in generated C# code.

For C# code generation, when a context manager object
enters a `with`-block, its `__enter__()` method is
invoked. If it is an untyped context manager, it should
return nothing. If it a typed context manager with type
parameter `T`, then this method returns an instance of type
`T` which is an object whose scope is bound inside
the `with`-block. The `as`-clause provides the scope-local
name for this object. If no such name is provided, then
the compiler autogenerates a random name. The object
returned here is assigned to its name (either the
user-provided one or the autogenerated name from the
compiler) with a `using`-assignment. This means that if
the object implements the C# interface `IDisposable`, it
will have its `Dispose()` method invoked when the
`with`-block ends.

When the `with`-block ends, as mentioned above, if the object
originally returned by `__enter__()` implements the C#
`IDisposable` or `IAsyncDisposable` interface, that
object's `Dispose()` method will also be invoked by way
of being automatically assigned via a `using var` or
`async using var` statement during code generation.
Afterwards, the `__exit__()` method is invoked on the
context manager object.

In the case of multiple context managers declared at
the start of a `with`-block, the `__enter__()` and
`__exit__()` methods are called in FIFO order for the
`__enter__()` methods and then LIFO-order for the
`__exit__()` methods.

An example C# implementation of the interfaces is shown
below. Note that if the user defines the Sharpy dunder
methods `__enter__()` and/or `__exit__()`, then the C#
interface methods `EnterContext()` and/or `ExitContext()`
should automatically be generated to delegate to them.
It is also possible for the user to define `enter_context()`
and `exit_context()` themselves (prior to name mangling)
to obviate the need for the compiler to synthesize the
delegation.

```C#
namespace Sharpy;

public interface IContextManager {
  void EnterContext();
  bool ExitContext(Sharpy.Optional<Sharpy.Tuple<Exception, StackTrace>> e);
}

public interface IContextManager<T> {
  T EnterContext();
  bool ExitContext(Sharpy.Optional<Sharpy.Tuple<Exception, StackTrace>> e);
}
```

The following Sharpy code:

```Sharpy
class Foobar(ContextManager[int]):
    def __enter__() -> int:
        return 5
    def __exit__(e: Tuple[Exception, StackTrace]?) -> bool:
        return False

with Foobar() as i:
    print(i)

f = Foobar()

with f as i:
    print(i)
```

Will generate this C# code:

```C#
class Foobar : IContextManager<int> {
  int EnterContext() {
    return 5;
  }
  bool ExitContext(Sharpy.Optional<Sharpy.Tuple<Exception, StackTrace>> e) {
    return false;
  }
}

// Block for visual purposes and source mapping in code
// generation. There is no semantic purpose.
{
  var temp_foobar = new Foobar();
  Sharpy.Tuple<Exception, StackTrace>? temp_e = null;
  try {
    // If the object returned by `EnterContext` implements
    // IDisposable or IAsyncDisposable, it is assigned in
    // a `using var` assignment (not shown here).
    var i = temp_foobar.EnterContext();
    Sharpy.Print(i);
  }
  catch (Exception e) {
    temp_e = (e, StackTrace(true));
  }
  finally {
    // Note, implicit conversion of
    // `Sharpy.Tuple<Exception, StackTrace>?` to
    // `Sharpy.Optional<Sharpy.Tuple<Exception, StackTrace>>`
    if (!temp_foobar.ExitContext(temp_e)) {
      throw temp_e.Item0;
    }
  }
}

var f = new Foobar();

{
  Sharpy.Tuple<Exception, StackTrace>? temp_e = null;
  try {
    var i = f.EnterContext();
    Sharpy.Print(i);
  }
  catch (Exception e) {
    temp_e = (e, StackTrace(true));
  }
  finally {
    if (!f.ExitContext(temp_e)) {
      throw temp_e.Item0;
    }
  }
}
```

## Tuples

Tuples in Sharpy operate similarly to Python ones. Tuples
are iterable, but the iterated elements are type-erased.
Specifically, each element is a tuple of its C#/.NET `Type`
record and the actual element itself as a raw `object`.

Auto-generated property names follow `System.ValueTuple`
names, e.g. `Item0`, `Item1`, etc.

```C#
namespace Sharpy;

public static class Exports {
  public static T Next<T>(IEnumerator<T> enumerator) {
    // Enumerators start at the position before the first
    // element
    if (enumerator.MoveNext()) {
      return enumerator.Current;
    }

    throw new StopIteration();
  }
}

public class TupleIterator<T1, T2> : IEnumerator<(Type, object)> {
  private readonly (Type, object)[] source_;
  private int current_ = 0;

  file TupleIterator(Tuple<T1, T2> source) {
    source_ = {
      (typeof(T1), source.Item0),
      (typeof(T2), source.Item1)
    };
  }

  public T Current {
    get {
      return source_[current_];
    }
  }

  public void Dispose() {
    // no-op
  }

  public bool MoveNext()
  {
    ++current_;

    return current_ < source_.Length;
  }

  public void Reset() {
    current_ = 0;
  }

  public int Count { get {
    return source_.Length;
  }
}

public readonly struct Tuple<T1, T2> : IEnumerable<(Type, object)> {
  private readonly (T1, T2) inner_;

  public Tuple(T1 item1, T2 item2)
  {
    inner_ = (item1, item2);
  }

  public T1 Item1 => inner_.Item1;
  public T2 Item2 => inner_.Item2;

  // For destructuring assignment
  public void Deconstruct(out T1 item1, out T2 item2)
  {
      item1 = inner.Item1;
      item2 = inner.Item2;
  }

  public override TupleEnumerator<T1, T2> GetEnumerator() {
    return new TupleEnumerator<T1, T2>(this);
  }

  public override bool Equals(object? obj) =>
      obj is Tuple<T1, T2> other &&
      EqualityComparer<T1>.Default.Equals(Item1, other.Item1) &&
      EqualityComparer<T2>.Default.Equals(Item2, other.Item2);

  public override int GetHashCode() => HashCode.Combine(Item1, Item2);

  public override string ToString() => $"({Item1}, {Item2})";

  public static implicit operator (T1, T2)(Tuple<T1, T2> t) => t.inner_;
  public static implicit operator Tuple<T1, T2>((T1, T2) t) => new(t.Item1, t.Item2);
}
```
