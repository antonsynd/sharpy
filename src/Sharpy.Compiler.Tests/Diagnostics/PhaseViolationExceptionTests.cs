using Xunit;
using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Compiler.Tests.Diagnostics;

public class PhaseViolationExceptionTests
{
    [Fact]
    public void Constructor_WithSymbolName_FormatsMessageCorrectly()
    {
        var exception = new PhaseViolationException("set variable type", "type checking", "myVariable");

        Assert.Contains("Phase violation", exception.Message);
        Assert.Contains("set variable type", exception.Message);
        Assert.Contains("'myVariable'", exception.Message);
        Assert.Contains("type checking", exception.Message);
        Assert.Contains("compiler bug", exception.Message);
    }

    [Fact]
    public void Constructor_WithoutSymbolName_FormatsMessageCorrectly()
    {
        var exception = new PhaseViolationException("set base type", "inheritance resolution");

        Assert.Contains("Phase violation", exception.Message);
        Assert.Contains("set base type", exception.Message);
        Assert.Contains("inheritance resolution", exception.Message);
        Assert.DoesNotContain("for '", exception.Message);
    }

    [Fact]
    public void Operation_IsSetCorrectly()
    {
        var exception = new PhaseViolationException("add interface", "inheritance resolution", "MyClass");

        Assert.Equal("add interface", exception.Operation);
    }

    [Fact]
    public void ExpectedPhase_IsSetCorrectly()
    {
        var exception = new PhaseViolationException("set CodeGenInfo", "code generation", "myFunction");

        Assert.Equal("code generation", exception.ExpectedPhase);
    }

    [Fact]
    public void SymbolName_IsSetCorrectly_WhenProvided()
    {
        var exception = new PhaseViolationException("set variable type", "type checking", "x");

        Assert.Equal("x", exception.SymbolName);
    }

    [Fact]
    public void SymbolName_IsNull_WhenNotProvided()
    {
        var exception = new PhaseViolationException("set base type", "inheritance resolution");

        Assert.Null(exception.SymbolName);
    }

    [Fact]
    public void InheritsFrom_InvalidOperationException()
    {
        var exception = new PhaseViolationException("set variable type", "type checking", "x");

        Assert.IsAssignableFrom<InvalidOperationException>(exception);
    }

    [Fact]
    public void CanBeCaughtAs_InvalidOperationException()
    {
        // Should be catchable as InvalidOperationException for backwards compatibility
        InvalidOperationException? caught = null;

        try
        {
            throw new PhaseViolationException("set variable type", "type checking", "x");
        }
        catch (InvalidOperationException ex)
        {
            caught = ex;
        }

        Assert.NotNull(caught);
        Assert.IsType<PhaseViolationException>(caught);
    }

    [Fact]
    public void ToString_IncludesFullMessage()
    {
        var exception = new PhaseViolationException("set variable type", "type checking", "myVar");

        var str = exception.ToString();

        Assert.Contains("Phase violation", str);
        Assert.Contains("set variable type", str);
        Assert.Contains("myVar", str);
        Assert.Contains("type checking", str);
    }
}
