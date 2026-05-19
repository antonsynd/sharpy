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
| `assert not expr` | `Xunit.Assert.False(expr)` |
| `assert expr` (fallback) | `Xunit.Assert.True(expr)` |

When an `assert` has a message (`assert expr, "message"`), it is passed as the last argument where xUnit supports it.

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
