using Xunit;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Tests.Diagnostics;

public class InternalCompilerErrorExceptionTests
{
    [Fact]
    public void Constructor_WithNode_CapturesNodeAndDerivesSpan()
    {
        var span = new TextSpan(3, 4);
        var node = new Identifier { Name = "x", Span = span };
        var exception = new InternalCompilerErrorException("RoslynEmitter", "boom", node: node);

        Assert.Same(node, exception.Node);
        Assert.Equal(span, exception.Span);
    }

    [Fact]
    public void Constructor_WithNodeAndInner_CapturesBoth()
    {
        var inner = new InvalidOperationException("root");
        var span = new TextSpan(10, 2);
        var node = new Identifier { Name = "y", Span = span };
        var exception = new InternalCompilerErrorException("TypeChecker", "boom", inner, node: node);

        Assert.Same(inner, exception.InnerException);
        Assert.Same(node, exception.Node);
        Assert.Equal(span, exception.Span);
    }

    [Fact]
    public void Constructor_WithoutNode_LeavesNodeAndSpanNull()
    {
        var exception = new InternalCompilerErrorException("Parser", "boom");

        Assert.Null(exception.Node);
        Assert.Null(exception.Span);
    }

    [Fact]
    public void Constructor_WithInnerException_FormatsMessageCorrectly()
    {
        var inner = new InvalidOperationException("Something went wrong");
        var exception = new InternalCompilerErrorException("TypeChecker", "Failed to resolve type", inner);

        Assert.Equal("Internal compiler error in TypeChecker: Failed to resolve type", exception.Message);
        Assert.Equal("TypeChecker", exception.Component);
        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void Constructor_WithoutInnerException_FormatsMessageCorrectly()
    {
        var exception = new InternalCompilerErrorException("RoslynEmitter", "Invalid syntax tree");

        Assert.Equal("Internal compiler error in RoslynEmitter: Invalid syntax tree", exception.Message);
        Assert.Equal("RoslynEmitter", exception.Component);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void Component_IsSetCorrectly()
    {
        var exception = new InternalCompilerErrorException("ValidationPipeline", "Validator crashed");

        Assert.Equal("ValidationPipeline", exception.Component);
    }

    [Fact]
    public void InnerException_PreservesChain()
    {
        var root = new ArgumentNullException("param");
        var middle = new InvalidOperationException("Operation failed", root);
        var exception = new InternalCompilerErrorException("Parser", "Parse error", middle);

        Assert.Same(middle, exception.InnerException);
        Assert.Same(root, exception.InnerException?.InnerException);
    }

    [Fact]
    public void ToString_IncludesStackTraceAndInnerException()
    {
        var inner = new NullReferenceException("Null reference");
        var exception = new InternalCompilerErrorException("CodeGen", "Generation failed", inner);

        var str = exception.ToString();

        // ToString should include the message
        Assert.Contains("Internal compiler error in CodeGen: Generation failed", str);
        // ToString should include the inner exception
        Assert.Contains("NullReferenceException", str);
        Assert.Contains("Null reference", str);
    }
}
