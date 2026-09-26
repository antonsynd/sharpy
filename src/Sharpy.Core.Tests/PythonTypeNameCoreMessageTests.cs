using System;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// Core runtime messages name the python type through <c>PyFormat.PyTypeName</c>, in CPython's
/// wording (#2035, Decision 26). Comparison refusals live in <see cref="ComparisonRefusalTests"/>. Every expected string is python3 3.12's, recorded beside it.
/// </summary>
public class PythonTypeNameCoreMessageTests
{
    private sealed class Plain
    {
    }

    private static string TypeErrorOf(Action action)
    {
        var ex = Assert.Throws<TypeError>(action);
        return ex.Message;
    }

    // python3: [1] + None / None + [1] / None * 3 / 3 * None
    [Fact]
    public void ListOperators_WithNone_UsePythonTexts()
    {
        var xs = new Sharpy.List<int> { 1 };
        Sharpy.List<int>? none = null;
        TypeErrorOf(() => _ = xs + none).Should().Be("can only concatenate list (not \"NoneType\") to list");
        TypeErrorOf(() => _ = none + xs).Should().Be("unsupported operand type(s) for +: 'NoneType' and 'list'");
        TypeErrorOf(() => _ = none * 3).Should().Be("unsupported operand type(s) for *: 'NoneType' and 'int'");
        TypeErrorOf(() => _ = 3 * none).Should().Be("unsupported operand type(s) for *: 'int' and 'NoneType'");
        TypeErrorOf(() => _ = xs < none).Should().Be("'<' not supported between instances of 'list' and 'NoneType'");
    }

    // python3: len(5) -> TypeError: object of type 'int' has no len()
    [Fact]
    public void Len_NoLength_NamesThePythonType()
    {
        TypeErrorOf(() => Builtins.Len((object)5)).Should().Be("object of type 'int' has no len()");
        TypeErrorOf(() => Builtins.Len(new Plain())).Should().Be("object of type 'Plain' has no len()");
    }

    // python3: repr(ExceptionGroup('eg', [ValueError('a')])) -> ExceptionGroup('eg', [ValueError('a')])
    [Fact]
    public void ExceptionRenderings_NameThePythonType()
    {
        Builtins.Repr(new ValueError("boom")).Should().Be("ValueError('boom')");
        new ExceptionGroup("eg", new Exception[] { new ValueError("a") }).ToString()
            .Should().Be("ExceptionGroup('eg', [ValueError('a')])");
    }

    // python3: str(type(5)) -> <class 'int'>; str(type([])) -> <class 'list'>; str(type(object())) -> <class 'object'>
    [Fact]
    public void ClassObjects_RenderAsPythonClasses()
    {
        Builtins.Str(typeof(int)).Should().Be("<class 'int'>");
        Builtins.Str(typeof(string)).Should().Be("<class 'str'>");
        Builtins.Str(typeof(Sharpy.List<int>)).Should().Be("<class 'list'>");
        Builtins.Repr(typeof(object)).Should().Be("<class 'object'>");
        // A CLR interop type has no python twin: its full CLR name, stated explicitly.
        Builtins.Str(typeof(Guid)).Should().Be("<class 'System.Guid'>");
        PyFormat.PyTypeName(typeof(object)).Should().Be("object");
    }
}
