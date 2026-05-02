using Xunit;
using FluentAssertions;

namespace Sharpy.Compiler.Tests.CodeGen;

[Collection("Sequential")]
public class NamespaceOptionTests
{
    [Fact]
    public void Compile_WithNamespace_WrapsOutputInNamespace()
    {
        var source = @"def main():
    print(42)
";
        var options = new CompilerOptions { Namespace = "Game.Scripts" };
        var compiler = new Compiler(options);
        var result = compiler.Compile(source, "test.spy");

        result.Success.Should().BeTrue(
            $"compilation should succeed, errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(e => e.Message))}");
        result.GeneratedCSharpCode.Should().Contain("namespace Game.Scripts");
    }

    [Fact]
    public void Compile_WithoutNamespace_NoNamespaceWrapper()
    {
        var source = @"def main():
    print(42)
";
        var compiler = new Compiler();
        var result = compiler.Compile(source, "test.spy");

        result.Success.Should().BeTrue();
        result.GeneratedCSharpCode.Should().NotContain("namespace ");
    }

    [Fact]
    public void Compile_WithSingleSegmentNamespace_Works()
    {
        var source = @"def main():
    print(42)
";
        var options = new CompilerOptions { Namespace = "MyGame" };
        var compiler = new Compiler(options);
        var result = compiler.Compile(source, "test.spy");

        result.Success.Should().BeTrue(
            $"compilation should succeed, errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(e => e.Message))}");
        result.GeneratedCSharpCode.Should().Contain("namespace MyGame");
    }
}
