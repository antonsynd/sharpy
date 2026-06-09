# Testing Framework (unittest)

Sharpy provides a Pythonic testing API that compiles to xUnit infrastructure. Tests are written with `@test` decorators and `assert` statements, and run via `dotnet test`.

## `@test` Decorator

The `@test` decorator marks a function or method as a test case. The compiler transforms it into a `[Fact]` attribute for xUnit discovery.

```python
@test
def test_addition():
    assert 1 + 1 == 2

@test("verifies string concatenation")
def test_concat():
    assert "hello " + "world" == "hello world"
```

Generated C#:
```csharp
[Xunit.FactAttribute]
public void TestAddition()
{
    Xunit.Assert.Equal(2, 1 + 1);
}

[Xunit.FactAttribute(DisplayName = "verifies string concatenation")]
public void TestConcat()
{
    Xunit.Assert.Equal("hello world", "hello " + "world");
}
```

### Syntax

```
test_decorator ::= '@test' [ '(' string_literal ')' ] NEWLINE
```

- `@test` — no arguments, test name derived from function name
- `@test("description")` — sets `DisplayName` on the `[Fact]` attribute

### Validation Rules

| Rule | Diagnostic |
|------|-----------|
| `@test` on a class, struct, interface, enum, property, or event | SPY0448 (error) |
| `@test` combined with `@abstract`, `@virtual`, or `@static` | SPY0449 (error) |
| `@test` on a dunder method (`__init__`, `__str__`, etc.) | SPY0448 (error) |
| `@test` with non-string or multiple arguments | SPY0469 (warning) |

## Assert Rewriting

Inside `@test` functions, `assert` statements are rewritten to xUnit assertions for rich error messages. Outside `@test` functions, `assert` continues to emit `Debug.Assert()`.

| Sharpy | xUnit C# |
|--------|----------|
| `assert a == b` | `Xunit.Assert.Equal(b, a)` |
| `assert a != b` | `Xunit.Assert.NotEqual(b, a)` |
| `assert a > b` | `Xunit.Assert.True(a > b)` |
| `assert a < b` | `Xunit.Assert.True(a < b)` |
| `assert a >= b` | `Xunit.Assert.True(a >= b)` |
| `assert a <= b` | `Xunit.Assert.True(a <= b)` |
| `assert a is None` | `Xunit.Assert.Null(a)` |
| `assert a is not None` | `Xunit.Assert.NotNull(a)` |
| `assert a is b` | `Xunit.Assert.Same(b, a)` |
| `assert a is not b` | `Xunit.Assert.NotSame(b, a)` |
| `assert a in b` | `Xunit.Assert.Contains(a, b)` |
| `assert a not in b` | `Xunit.Assert.DoesNotContain(a, b)` |
| `assert isinstance(a, T)` | `Xunit.Assert.IsType<T>(a)` |
| `assert s.startswith(p)` | `Xunit.Assert.StartsWith(p, s)` (only when `s` is typed `str`) |
| `assert s.endswith(p)` | `Xunit.Assert.EndsWith(p, s)` (only when `s` is typed `str`) |
| `assert x == approx(y)` | `Xunit.Assert.Equal(y, x, 7)` (approximate float equality) |
| `assert not expr` | `Xunit.Assert.False(expr)` |
| `assert expr` (fallback) | `Xunit.Assert.True(expr)` |

When an `assert` has a message (`assert expr, "message"`), it is passed as the last argument where xUnit supports it.

### `startswith` / `endswith` (type-gated)

`assert s.startswith(p)` and `assert s.endswith(p)` rewrite to `Xunit.Assert.StartsWith` /
`Xunit.Assert.EndsWith` **only** when the receiver `s` is typed as `str` (per `SemanticInfo`) and
the call has exactly one positional argument:

```python
@test
def test_prefix():
    name: str = "hello world"
    assert name.startswith("hello")
    assert name.endswith("world")
```

Generated C#:
```csharp
[Xunit.FactAttribute]
public void TestPrefix()
{
    string name = "hello world";
    Xunit.Assert.StartsWith("hello", name);
    Xunit.Assert.EndsWith("world", name);
}
```

Multi-argument forms (`s.startswith(p, start)`), tuple-prefix forms, and receivers of
user-defined types that happen to define a `startswith`/`endswith` method are **not** rewritten —
they fall through to the `Xunit.Assert.True(...)` fallback so there is no surprising behavior for
non-`str` receivers.

### `approx` — approximate float equality

Use `approx()` on either side of an `==` comparison inside an `assert` for floating-point
comparisons with tolerance. It mirrors `assert_almost_equal` defaults (`places=7`).

```python
from unittest import approx

@test
def test_float():
    assert 0.1 + 0.2 == approx(0.3)                 # default: 7 decimal places
    assert 0.1 + 0.2 == approx(0.3, places=10)      # explicit precision
    assert 0.1 + 0.2 == approx(0.3, abs=1e-9)       # absolute tolerance
    assert approx(0.3) == 0.1 + 0.2                 # approx on either side
```

Generated C#:
```csharp
Xunit.Assert.Equal(0.3, 0.1 + 0.2, 7);
Xunit.Assert.Equal(0.3, 0.1 + 0.2, 10);
Xunit.Assert.Equal(0.3, 0.1 + 0.2, 1e-9);
Xunit.Assert.Equal(0.3, 0.1 + 0.2, 7);
```

- The argument to `approx(...)` is the `expected` value; the other operand of `==` is `actual`.
- `places=n` (an `int`) selects `Xunit.Assert.Equal(double, double, int precision)`.
- `abs=d` (a `double`) selects `Xunit.Assert.Equal(double, double, double tolerance)`.
- If both `places` and `abs` are supplied, `abs` wins (matching `assert_almost_equal`'s
  `delta`-over-`places` precedence).
- Only the `==` form is rewritten; `assert x != approx(y)` falls through to `NotEqual`.

> **Note:** `approx(..., abs=d)` lowers to xUnit's `Assert.Equal(expected, actual, tolerance)`
> overload (richer positional failure output). This differs from the older `assert_almost_equal`
> `delta=` lowering, which emits `Xunit.Assert.True(System.Math.Abs(actual - expected) <= delta)`.
> `rel=` (relative tolerance) is not supported — xUnit has no native overload for it.

## Module-Level Test Functions

Module-level `@test` functions are collected into a generated test class (separate from the module class). This is required because xUnit discovers tests as instance methods on public classes.

```python
x: int = 42

@test
def test_value():
    assert x == 42
```

Generated C#:
```csharp
public static class MyModule
{
    public static int X = 42;
}

public class MyModuleTests
{
    [Xunit.FactAttribute]
    public void TestValue()
    {
        Xunit.Assert.Equal(42, MyModule.X);
    }
}
```

## TestCase Base Class

For test classes with shared setup/teardown, inherit from `unittest.TestCase`:

```python
from unittest import TestCase

class TestCalculator(TestCase):
    def setup(self):
        self.value = 0

    def teardown(self):
        pass

    @test
    def test_initial_value(self):
        assert self.value == 0
```

### Lifecycle Synthesis

The compiler transforms `TestCase` subclasses into xUnit-compatible test classes:

| Sharpy | C# |
|--------|-----|
| `class TestX(TestCase)` | `public class TestX : IDisposable` (if teardown present) |
| `def setup(self):` | Constructor body calls `Setup()` |
| `def teardown(self):` | `Dispose()` calls `Teardown()` |
| `@test` methods | `[Fact] public void ...()` |

`TestCase` is a marker type in `Sharpy.Core` — it has no xUnit dependency. The compiler synthesizes all xUnit integration during code generation.

## `assert_raises` Context Manager

Use `assert_raises` to verify that code raises a specific exception:

```python
from unittest import assert_raises

@test
def test_division_by_zero():
    with assert_raises(ZeroDivisionError):
        x = 1 / 0
```

Generated C#:
```csharp
Xunit.Assert.Throws<ZeroDivisionError>(() =>
{
    var x = 1 / 0;
});
```

`assert_raises` is a codegen transform — the compiler replaces the entire `with` block with `Assert.Throws<T>(() => { ... })`, which is the correct xUnit idiom for exception assertions.

### Capturing the exception (`as`)

`with assert_raises(E) as exc:` captures the thrown exception into a local so its attributes can
be asserted after the block:

```python
@test
def test_message():
    with assert_raises(ValueError) as exc:
        raise ValueError("bad input")
    assert exc.message == "bad input"
```

Generated C#:
```csharp
var exc = Xunit.Assert.Throws<ValueError>((System.Action)(() =>
{
    throw new ValueError("bad input");
}));
Xunit.Assert.Equal("bad input", exc.Message);
```

### Matching the exception message (`match=`)

`assert_raises` accepts an optional `match=` keyword (or a second positional string) that asserts
the exception's message matches a regular expression. Semantics follow Python's
`pytest.raises(match=...)` / `re.search` — the pattern matches anywhere in `str(exc)` (not
anchored):

```python
@test
def test_match():
    with assert_raises(ValueError, match="bad.*input"):
        raise ValueError("bad input")
```

Generated C#:
```csharp
var __ex0 = Xunit.Assert.Throws<ValueError>((System.Action)(() =>
{
    throw new ValueError("bad input");
}));
Xunit.Assert.Matches("bad.*input", __ex0.Message);
```

`match=` combines with `as`: the captured name is reused for the `Assert.Matches` call.

```python
with assert_raises(ValueError, match="bad") as exc:
    raise ValueError("bad input")
assert exc.message == "bad input"
```

Generated C#:
```csharp
var exc = Xunit.Assert.Throws<ValueError>((System.Action)(() =>
{
    throw new ValueError("bad input");
}));
Xunit.Assert.Matches("bad", exc.Message);
```

## `assert_almost_equal`

For floating-point comparisons with precision:

```python
from unittest import assert_almost_equal

@test
def test_float_precision():
    assert_almost_equal(0.1 + 0.2, 0.3, places=10)
```

Generated C#:
```csharp
Xunit.Assert.Equal(0.3, 0.1 + 0.2, 10);
```

## `assert_count_equal`

`assert_count_equal(a, b)` asserts that two collections contain the same elements regardless of
order, **respecting multiplicity** (matching Python's `unittest.TestCase.assertCountEqual` —
`[1, 2, 2]` is not equal to `[1, 2]`):

```python
from unittest import assert_count_equal

@test
def test_same_elements():
    assert_count_equal([3, 1, 2], [1, 2, 3])
    assert_count_equal([1, 2, 2], [2, 1, 2])
```

Generated C#:
```csharp
Xunit.Assert.Equal(global::Sharpy.Builtins.Sorted([1, 2, 3]), global::Sharpy.Builtins.Sorted([3, 1, 2]));
Xunit.Assert.Equal(global::Sharpy.Builtins.Sorted([2, 1, 2]), global::Sharpy.Builtins.Sorted([1, 2, 2]));
```

The lowering sorts both operands and compares them with `Xunit.Assert.Equal`, which produces a
rich positional sequence diff on failure. Because the rewrite sorts the elements, they must be
**comparable** — `Builtins.Sorted` carries no compile-time constraint, so a collection of
non-comparable elements compiles but throws at runtime (the same failure mode as the `sorted()`
builtin).

## `assert_regex`

`assert_regex(text, pattern)` asserts that `text` matches a regular expression (Python's
`assertRegex` argument order — text first):

```python
from unittest import assert_regex

@test
def test_format():
    assert_regex("2026-06-09", r"\d{4}-\d{2}-\d{2}")
```

Generated C#:
```csharp
Xunit.Assert.Matches(@"\d{4}-\d{2}-\d{2}", "2026-06-09");
```

Note the argument swap: Sharpy follows Python's `(text, pattern)` order, while
`Xunit.Assert.Matches` takes `(pattern, actual)`.

## Capturing Output (`captured_output`)

`captured_output()` is a context manager that redirects `Console.Out` to an in-memory buffer so a
test can verify what code prints. `getvalue()` returns the accumulated text (mirroring
`io.StringIO.getvalue`). It restores the previous writer on exit.

```python
from unittest import captured_output

@test
def test_print():
    with captured_output() as output:
        print("hello")
        assert output.getvalue() == "hello\n"
```

Generated C#:
```csharp
[Xunit.FactAttribute]
public void TestPrint()
{
    using (var output = global::Sharpy.Unittest.CapturedOutput())
    {
        global::Sharpy.Builtins.Print("hello");
        Xunit.Assert.Equal("hello\n", output.Getvalue());
    }
}
```

`captured_output` is a plain `Sharpy.Stdlib` runtime type (`Sharpy.Unittest.CapturedOutput`,
implementing `System.IDisposable`); it lowers through the standard `with` → `using` path with no
special codegen.

> **Parallelism caveat:** `Console.Out` is process-global. xUnit parallelizes across test
> collections, so a test using `captured_output` can race with another collection that also
> prints. If this matters, serialize affected tests into a single `@test.collection`.

## Fixtures (`@test.fixture`)

A `@test.fixture` function provides a reusable, set-up value to tests that declare a parameter of
the same name. The compiler turns the fixture function into a public C# class and injects it into
consuming test classes via xUnit's `IClassFixture<T>` (shared once per test class).

```python
@test.fixture
def greeting() -> str:
    return "hello"

@test
def test_uses_greeting(greeting: str):
    assert greeting == "hello"
```

Generated C#:
```csharp
public class GreetingFixture
{
    public string Value { get; private set; } = default!;

    public GreetingFixture()
    {
        Value = "hello";
    }
}

public partial class MyModuleTests : Xunit.IClassFixture<GreetingFixture>
{
    private readonly GreetingFixture _greetingFixture;

    public MyModuleTests(GreetingFixture greetingFixture)
    {
        _greetingFixture = greetingFixture;
    }

    [Xunit.FactAttribute]
    public void TestUsesGreeting()
    {
        string greeting = _greetingFixture.Value;
        Xunit.Assert.Equal("hello", greeting);
    }
}
```

- The fixture function's `return` expression becomes the `Value` property (return annotation is
  required).
- A test parameter is matched to a fixture **by name**; the parameter is stripped from the
  emitted `[Fact]` signature and replaced with a `T name = _nameFixture.Value;` prelude.
- Fixture consumption applies to **module-level** `@test` functions (not `TestCase` classes).

### Yield-based fixtures (setup / teardown)

A fixture that `yield`s exposes the yielded value and runs the statements after the `yield` as
teardown. The generated class implements `System.IDisposable`; teardown runs in `Dispose()`:

```python
@test.fixture
def counter() -> list[int]:
    data: list[int] = [0]
    yield data
    data.clear()
```

Generated C#:
```csharp
public class CounterFixture : global::System.IDisposable
{
    public Sharpy.List<int> Value { get; private set; } = default!;
    private global::System.Action? _teardown;

    public CounterFixture()
    {
        Sharpy.List<int> data = new Sharpy.List<int>() { 0 };
        Value = data;
        _teardown = () =>
        {
            data.Clear();
        };
    }

    public void Dispose()
    {
        _teardown?.Invoke();
    }
}
```

### Async fixtures (`IAsyncLifetime`)

An `async def` `@test.fixture` emits a fixture class implementing xUnit's
`Xunit.IAsyncLifetime` instead of `System.IDisposable`. Setup (including `await`s) moves into
`InitializeAsync`; teardown after the `yield` runs in `DisposeAsync`:

```python
@test.fixture
async def db() -> Connection:
    conn = await open_connection()
    yield conn
    await conn.close()
```

Generated C#:
```csharp
public class DbFixture : Xunit.IAsyncLifetime
{
    public Connection Value { get; private set; } = default!;
    private System.Func<System.Threading.Tasks.Task>? _teardown;

    public async System.Threading.Tasks.Task InitializeAsync()
    {
        Connection conn = await OpenConnection();
        Value = conn;
        _teardown = async () =>
        {
            await conn.Close();
        };
    }

    public async System.Threading.Tasks.Task DisposeAsync()
    {
        if (_teardown != null)
        {
            await _teardown();
        }
    }
}
```

- The constructor stays empty; xUnit drives `InitializeAsync`/`DisposeAsync` automatically through
  `IClassFixture<T>`.
- A return-based async fixture (no `yield`) emits `InitializeAsync` with `Value = ...;` and a
  `DisposeAsync` returning `System.Threading.Tasks.Task.CompletedTask`.
- Consuming test classes are unchanged from the sync case.

## `tmp_path` — per-test temporary directory

A module-level `@test` function that declares a `tmp_path` parameter receives the path of a fresh
temporary directory, created before the test and recursively deleted afterward (best-effort).
This mirrors pytest's `tmp_path` — the directory is **per test**, not shared:

```python
import os

@test
def test_writes_file(tmp_path: str):
    target: str = os.path.join(tmp_path, "data.txt")
    with open(target, "w") as f:
        f.write("content")
    assert os.path.exists(target)
```

Generated C#:
```csharp
public partial class MyModuleTests : System.IDisposable
{
    private readonly global::Sharpy.TmpPathFixture _tmpPathFixture = new global::Sharpy.TmpPathFixture();

    [Xunit.FactAttribute]
    public void TestWritesFile()
    {
        string tmp_path = _tmpPathFixture.Value;
        // ... test body ...
    }

    public void Dispose()
    {
        _tmpPathFixture.Dispose();
    }
}
```

- No import is required — `tmp_path` is recognized by parameter name (the same convention as user
  fixtures).
- Unlike user fixtures, `tmp_path` is **not** an `IClassFixture<T>` (which would be shared across
  the class); instead the test class holds a `TmpPathFixture` instance field and implements
  `System.IDisposable`. xUnit constructs the test class once per test method and disposes it after
  each, giving exactly pytest's per-test lifecycle.
- A user-defined `@test.fixture def tmp_path() -> str:` overrides the built-in (the fixture
  registry is consulted first).
- `TmpPathFixture` creates a unique directory under the system temp path and swallows
  `IOException`/`UnauthorizedAccessException` during cleanup, so a cleanup failure never fails the
  test.

## Project Setup

Test projects need xUnit packages in their `.spyproj`:

```xml
<Project>
  <PropertyGroup>
    <RootNamespace>MyTests</RootNamespace>
    <OutputType>Library</OutputType>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <SpyFile Include="**/*.spy" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.0.2" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
  </ItemGroup>
</Project>
```

The compiler resolves `<PackageReference>` elements from the NuGet global cache and generates a test runner scaffold for `dotnet test` discovery.

## Axiom Alignment

| Axiom | How Testing Aligns |
|-------|--------------------|
| Axiom 1 (.NET) | Compiles to standard xUnit `[Fact]` — full .NET test tooling compatibility |
| Axiom 2 (Python) | `@test`, `assert`, `setup`/`teardown` — Pythonic testing vocabulary |
| Axiom 3 (Types) | Assert rewriting is type-aware — `assert a == b` uses `Assert.Equal` for proper type dispatch |
