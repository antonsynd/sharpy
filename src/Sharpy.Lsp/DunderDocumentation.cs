namespace Sharpy.Lsp;

/// <summary>
/// Provides documentation strings for Python dunder (magic) methods.
/// Used as fallback documentation when Symbol.Documentation is not available.
/// </summary>
internal static class DunderDocumentation
{
    private static readonly Dictionary<string, string> Docs = new()
    {
        // Construction / lifecycle
        ["__init__"] = "Constructor. Called when creating a new instance of the class.",
        ["__del__"] = "Destructor. Called when the instance is about to be destroyed.",

        // String representations
        ["__str__"] = "String representation. Called by `str()` and `print()`.",
        ["__repr__"] = "Debug representation. Called by `repr()`.",
        ["__format__"] = "Custom string formatting. Called by `format()` and f-strings.",

        // Comparison operators
        ["__eq__"] = "Equality comparison (`==`).",
        ["__ne__"] = "Inequality comparison (`!=`).",
        ["__lt__"] = "Less-than comparison (`<`).",
        ["__le__"] = "Less-than-or-equal comparison (`<=`).",
        ["__gt__"] = "Greater-than comparison (`>`).",
        ["__ge__"] = "Greater-than-or-equal comparison (`>=`).",

        // Arithmetic operators
        ["__add__"] = "Addition (`+`). Returns the sum of self and other.",
        ["__sub__"] = "Subtraction (`-`). Returns the difference of self and other.",
        ["__mul__"] = "Multiplication (`*`). Returns the product of self and other.",
        ["__truediv__"] = "True division (`/`). Returns the quotient of self and other.",
        ["__floordiv__"] = "Floor division (`//`). Returns the floored quotient.",
        ["__mod__"] = "Modulo (`%`). Returns the remainder of division.",
        ["__pow__"] = "Power (`**`). Returns self raised to the power of other.",
        ["__matmul__"] = "Matrix multiplication (`@`).",

        // Reflected arithmetic
        ["__radd__"] = "Reflected addition. Called when `other + self` and other doesn't support `__add__`.",
        ["__rsub__"] = "Reflected subtraction.",
        ["__rmul__"] = "Reflected multiplication.",
        ["__rtruediv__"] = "Reflected true division.",
        ["__rfloordiv__"] = "Reflected floor division.",
        ["__rmod__"] = "Reflected modulo.",
        ["__rpow__"] = "Reflected power.",

        // In-place arithmetic
        ["__iadd__"] = "In-place addition (`+=`).",
        ["__isub__"] = "In-place subtraction (`-=`).",
        ["__imul__"] = "In-place multiplication (`*=`).",
        ["__itruediv__"] = "In-place true division (`/=`).",
        ["__ifloordiv__"] = "In-place floor division (`//=`).",
        ["__imod__"] = "In-place modulo (`%=`).",
        ["__ipow__"] = "In-place power (`**=`).",

        // Unary operators
        ["__neg__"] = "Unary negation (`-self`).",
        ["__pos__"] = "Unary positive (`+self`).",
        ["__abs__"] = "Absolute value. Called by `abs()`.",
        ["__invert__"] = "Bitwise inversion (`~self`).",

        // Bitwise operators
        ["__and__"] = "Bitwise AND (`&`).",
        ["__or__"] = "Bitwise OR (`|`).",
        ["__xor__"] = "Bitwise XOR (`^`).",
        ["__lshift__"] = "Left shift (`<<`).",
        ["__rshift__"] = "Right shift (`>>`).",

        // Container / sequence
        ["__len__"] = "Length. Called by `len()`.",
        ["__getitem__"] = "Subscript access (`self[key]`).",
        ["__setitem__"] = "Subscript assignment (`self[key] = value`).",
        ["__delitem__"] = "Subscript deletion (`del self[key]`).",
        ["__contains__"] = "Membership test (`item in self`). Called by the `in` operator.",
        ["__missing__"] = "Called when a key is not found in a dict subclass.",

        // Iteration
        ["__iter__"] = "Returns an iterator. Called by `iter()` and `for` loops.",
        ["__next__"] = "Returns the next item from the iterator. Called by `next()`.",
        ["__reversed__"] = "Reverse iteration. Called by `reversed()`.",

        // Context manager
        ["__enter__"] = "Context manager entry. Called when entering a `with` block.\nMust return the value to bind to the `as` variable.",
        ["__exit__"] = "Context manager exit. Called when leaving a `with` block.\n\nSupports two forms:\n- `def __exit__(self) -> None` — simple cleanup; always invoked in `finally`.\n- `def __exit__(self, exc_type: object?, exc_val: Exception?, exc_tb: object?) -> bool` — exception-aware; returns `True` to suppress the exception, `False` to propagate.",

        // Async context manager
        ["__aenter__"] = "Async context manager entry. Called when entering an `async with` block.\nMust return the value to bind to the `as` variable.",
        ["__aexit__"] = "Async context manager exit. Called when leaving an `async with` block.\n\nSupports two forms:\n- `async def __aexit__(self) -> None` — simple cleanup; always awaited in `finally`.\n- `async def __aexit__(self, exc_type: object?, exc_val: Exception?, exc_tb: object?) -> bool` — exception-aware; returns `True` to suppress the exception, `False` to propagate.",

        // Async iteration
        ["__aiter__"] = "Returns an async iterator. Called by `async for` loops.",
        ["__anext__"] = "Returns the next item from the async iterator.",

        // Hashing / truthiness
        ["__hash__"] = "Hash value. Called by `hash()`. Required for use as dict key or set member.",
        ["__bool__"] = "Truthiness. Called by `bool()` and in boolean contexts (`if x:`).",

        // Callable
        ["__call__"] = "Makes the instance callable (`self(...)`).",

        // Attribute access
        ["__getattr__"] = "Called when attribute lookup fails. Provides fallback attribute access.",
        ["__setattr__"] = "Called on every attribute assignment (`self.attr = value`).",
        ["__delattr__"] = "Called on attribute deletion (`del self.attr`).",

        // Numeric type conversion
        ["__int__"] = "Integer conversion. Called by `int()`.",
        ["__float__"] = "Float conversion. Called by `float()`.",
        ["__complex__"] = "Complex number conversion. Called by `complex()`.",
        ["__index__"] = "Integer index conversion. Used for slicing and `bin()`/`hex()`/`oct()`.",
        ["__round__"] = "Rounding. Called by `round()`.",
        ["__trunc__"] = "Truncation. Called by `math.trunc()`.",
        ["__floor__"] = "Floor. Called by `math.floor()`.",
        ["__ceil__"] = "Ceiling. Called by `math.ceil()`.",

        // Descriptor protocol
        ["__get__"] = "Descriptor get. Called when the attribute is accessed on an instance.",
        ["__set__"] = "Descriptor set. Called when the attribute is assigned on an instance.",
        ["__delete__"] = "Descriptor delete. Called when the attribute is deleted on an instance.",
        ["__set_name__"] = "Called when the descriptor is assigned to a class attribute.",

        // Class creation
        ["__init_subclass__"] = "Called when a subclass is created. Allows customization of subclass behavior.",
        ["__class_getitem__"] = "Called for class subscription (`MyClass[int]`). Enables generic syntax.",

        // Pickling
        ["__reduce__"] = "Serialization support. Returns reconstruction info for pickle.",
        ["__reduce_ex__"] = "Extended serialization support with protocol version.",
        ["__getstate__"] = "Returns the state to be pickled.",
        ["__setstate__"] = "Restores state from pickle data.",
    };

    /// <summary>
    /// Gets documentation for a dunder method by name, or null if not found.
    /// </summary>
    public static string? GetDocumentation(string name) =>
        Docs.TryGetValue(name, out var doc) ? doc : null;
}
