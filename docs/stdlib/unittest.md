# unittest

The unittest module provides a Pythonic testing API that the Sharpy compiler
transforms into xUnit test infrastructure during code generation.

```python
import unittest
```

## Properties

| Name | Type | Description |
|------|------|-------------|
| `value` | `str` | The absolute path to this fixture's unique temporary directory. |

## Functions

### `unittest.getvalue() -> str`

Return the text captured so far, mirroring `io.StringIO.getvalue()`.

### `unittest.assert_raises(exception_type: Type, match: str | None = None) -> AssertRaisesMarker`

Marker for assert_raises context manager. The compiler transforms
`with assert_raises(ExceptionType): body` into
`Xunit.Assert.Throws&lt;ExceptionType&gt;(() =&gt; { body })`.

**Parameters:**

- `exception_type` (Type)
- `match` (str | None) -- Optional regular expression applied to the exception message with
`re.search` semantics. When provided, the compiler appends a
`Xunit.Assert.Matches(match, exception.Message)` check after the
`Xunit.Assert.Throws&lt;ExceptionType&gt;` call.

!!! note
    This method exists for type resolution only. It should never be called at runtime.
    If called outside a compiler-transformed context, it throws NotSupportedException.

### `unittest.approx(expected: float, places: int = 7, abs: float = 0.0) -> float`

Marker for approx. The compiler transforms
`assert x == approx(y)` into a tolerance-based
`Xunit.Assert.Equal(expected, actual, precision)` (when
`places` is used) or `Xunit.Assert.Equal(expected, actual, tolerance)`
(when `abs` is used).

!!! note
    This method exists for type resolution only — it returns `double`
    so that `x == approx(y)` type-checks as numeric equality. It should never
    be called at runtime. Defaults mirror `AssertAlmostEqual`:
    `places=7`; if both `places` and `abs` are supplied,
    `abs` takes precedence.

### `unittest.assert_count_equal(first: object, second: object)`

Marker for assert_count_equal. The compiler transforms calls to this method
into an order-insensitive comparison
`Xunit.Assert.Equal(Sharpy.Builtins.Sorted(second), Sharpy.Builtins.Sorted(first))`,
which preserves element multiplicity (matching Python's
`unittest.TestCase.assertCountEqual`).

!!! note
    This method exists for type resolution only. It should never be called at runtime.
    Requires comparable elements; non-comparable elements fail at runtime when the
    sorted comparison executes.

### `unittest.assert_regex(text: str, pattern: str)`

Marker for assert_regex. The compiler transforms calls to this method
into `Xunit.Assert.Matches(pattern, text)` (note the argument swap:
Sharpy follows Python's `assertRegex(text, pattern)` order, while xUnit
takes the pattern first).

!!! note
    This method exists for type resolution only. It should never be called at runtime.

### `unittest.assert_almost_equal(actual: float, expected: float, places: int = 7, delta: float = 0.0)`

Marker for assert_almost_equal. The compiler transforms calls to this method
into `Xunit.Assert.Equal(expected, actual, precision)` (digits) or, when
the `delta` keyword is provided, into an absolute-tolerance check.

!!! note
    This method exists for type resolution only. Both `places` and `delta`
    are accepted; if both are passed at the same call site, `delta` takes
    precedence.

### `unittest.assert_true(value: object)`

Marker for assert_true. The compiler transforms calls to
`Xunit.Assert.True(value)`.

### `unittest.assert_false(value: object)`

Marker for assert_false. The compiler transforms calls to
`Xunit.Assert.False(value)`.

### `unittest.assert_is_none(value: object)`

Marker for assert_is_none. The compiler transforms calls to
`Xunit.Assert.Null(value)`.

### `unittest.assert_is_not_none(value: object)`

Marker for assert_is_not_none. The compiler transforms calls to
`Xunit.Assert.NotNull(value)`.

### `unittest.assert_greater(a: object, b: object)`

Marker for assert_greater. The compiler transforms calls to
`Xunit.Assert.True(a &gt; b, ...)`.

### `unittest.assert_less(a: object, b: object)`

Marker for assert_less. The compiler transforms calls to
`Xunit.Assert.True(a &lt; b, ...)`.

### `unittest.assert_in(item: object, collection: object)`

Marker for assert_in. The compiler transforms calls to
`Xunit.Assert.Contains(item, collection)`.

### `unittest.assert_not_in(item: object, collection: object)`

Marker for assert_not_in. The compiler transforms calls to
`Xunit.Assert.DoesNotContain(item, collection)`.

### `unittest.captured_output() -> CapturedOutput`

Create a `Sharpy.CapturedOutput` context manager that captures
everything written to the console while active. Exposed to Sharpy as
`captured_output()` and intended for use in a `with` statement:
`with captured_output() as out: ...`.

!!! note
    Unlike the assertion markers, this is a real runtime helper — it is not
    rewritten by the compiler and may be called directly.

## AssertRaisesMarker

Marker type returned by unittest.assert_raises(). Implements IDisposable
so the with-statement type checking passes. The compiler replaces the
entire with-block with Xunit.Assert.Throws during codegen.

## TestCase

Base class for unittest-style test classes. The Sharpy compiler detects
inheritance from TestCase and synthesizes xUnit lifecycle code:

  setup() → constructor body that calls Setup()
  teardown() → IDisposable.Dispose() that calls Teardown()
  @test methods → [Fact] public methods
