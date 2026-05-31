# unittest

The unittest module provides a Pythonic testing API that the Sharpy compiler
transforms into xUnit test infrastructure during code generation.

```python
import unittest
```

## Functions

### `unittest.assert_raises(exception_type: Type) -> AssertRaisesMarker`

Marker for assert_raises context manager. The compiler transforms
`with assert_raises(ExceptionType): body` into
`Xunit.Assert.Throws&lt;ExceptionType&gt;(() =&gt; { body })`.

!!! note
    This method exists for type resolution only. It should never be called at runtime.
    If called outside a compiler-transformed context, it throws NotSupportedException.

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
