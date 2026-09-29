using FluentAssertions;
using Sharpy.Compiler.Services;
using Xunit;

namespace Sharpy.Compiler.Tests.Services;

public class AstFingerprintTests
{
    private readonly CompilerApi _api = new();

    [Fact]
    public void Classify_NoChange_ReturnsSame()
    {
        var source = "def main():\n    x: int = 1\n    print(x)";
        var ast1 = _api.Parse(source).Ast!;
        var ast2 = _api.Parse(source).Ast!;

        var result = AstFingerprint.Classify(ast1, ast2);

        result.Kind.Should().Be(AstChangeKind.NoChange);
    }

    [Fact]
    public void Classify_FunctionBodyChange_ReturnsBodyOnly()
    {
        var source1 = "def greet() -> str:\n    return \"hello\"\ndef main():\n    print(greet())";
        var source2 = "def greet() -> str:\n    return \"world\"\ndef main():\n    print(greet())";
        var ast1 = _api.Parse(source1).Ast!;
        var ast2 = _api.Parse(source2).Ast!;

        var result = AstFingerprint.Classify(ast1, ast2);

        result.Kind.Should().Be(AstChangeKind.BodyOnly);
        result.FunctionName.Should().Be("greet");
        result.FunctionIndex.Should().Be(0);
    }

    [Fact]
    public void Classify_NewFunction_ReturnsStructural()
    {
        var source1 = "def main():\n    print(\"hello\")";
        var source2 = "def helper() -> int:\n    return 1\ndef main():\n    print(\"hello\")";
        var ast1 = _api.Parse(source1).Ast!;
        var ast2 = _api.Parse(source2).Ast!;

        var result = AstFingerprint.Classify(ast1, ast2);

        result.Kind.Should().Be(AstChangeKind.Structural);
    }

    [Fact]
    public void Classify_ParameterChange_ReturnsStructural()
    {
        var source1 = "def greet(name: str) -> str:\n    return name\ndef main():\n    print(greet(\"hi\"))";
        var source2 = "def greet(name: str, loud: bool) -> str:\n    return name\ndef main():\n    print(greet(\"hi\", True))";
        var ast1 = _api.Parse(source1).Ast!;
        var ast2 = _api.Parse(source2).Ast!;

        var result = AstFingerprint.Classify(ast1, ast2);

        result.Kind.Should().Be(AstChangeKind.Structural);
    }

    [Fact]
    public void Classify_ImportChange_ReturnsStructural()
    {
        var source1 = "def main():\n    print(\"hello\")";
        var source2 = "import math\ndef main():\n    print(\"hello\")";
        var ast1 = _api.Parse(source1).Ast!;
        var ast2 = _api.Parse(source2).Ast!;

        var result = AstFingerprint.Classify(ast1, ast2);

        result.Kind.Should().Be(AstChangeKind.Structural);
    }

    [Fact]
    public void Classify_ReturnTypeChange_ReturnsStructural()
    {
        var source1 = "def greet() -> str:\n    return \"hello\"\ndef main():\n    print(greet())";
        var source2 = "def greet() -> int:\n    return 42\ndef main():\n    print(greet())";
        var ast1 = _api.Parse(source1).Ast!;
        var ast2 = _api.Parse(source2).Ast!;

        var result = AstFingerprint.Classify(ast1, ast2);

        result.Kind.Should().Be(AstChangeKind.Structural);
    }

    [Fact]
    public void Classify_SecondFunctionBodyChange_ReturnsBodyOnly()
    {
        var source1 = "def first() -> int:\n    return 1\ndef second() -> int:\n    return 2\ndef main():\n    print(first())\n    print(second())";
        var source2 = "def first() -> int:\n    return 1\ndef second() -> int:\n    return 99\ndef main():\n    print(first())\n    print(second())";
        var ast1 = _api.Parse(source1).Ast!;
        var ast2 = _api.Parse(source2).Ast!;

        var result = AstFingerprint.Classify(ast1, ast2);

        result.Kind.Should().Be(AstChangeKind.BodyOnly);
        result.FunctionName.Should().Be("second");
        result.FunctionIndex.Should().Be(1);
    }

    [Fact]
    public void Classify_TwoFunctionBodiesChanged_ReturnsStructural()
    {
        var source1 = "def first() -> int:\n    return 1\ndef second() -> int:\n    return 2\ndef main():\n    print(first())\n    print(second())";
        var source2 = "def first() -> int:\n    return 10\ndef second() -> int:\n    return 20\ndef main():\n    print(first())\n    print(second())";
        var ast1 = _api.Parse(source1).Ast!;
        var ast2 = _api.Parse(source2).Ast!;

        var result = AstFingerprint.Classify(ast1, ast2);

        result.Kind.Should().Be(AstChangeKind.Structural);
    }

    [Fact]
    public void Classify_ClassBodyChange_ReturnsStructural()
    {
        var source1 = "class Point:\n    x: int\n    y: int\ndef main():\n    p = Point()\n    print(p.x)";
        var source2 = "class Point:\n    x: int\n    y: int\n    z: int\ndef main():\n    p = Point()\n    print(p.x)";
        var ast1 = _api.Parse(source1).Ast!;
        var ast2 = _api.Parse(source2).Ast!;

        var result = AstFingerprint.Classify(ast1, ast2);

        result.Kind.Should().Be(AstChangeKind.Structural);
    }

    [Fact]
    public void Classify_FunctionRemoved_ReturnsStructural()
    {
        var source1 = "def helper() -> int:\n    return 1\ndef main():\n    print(helper())";
        var source2 = "def main():\n    print(1)";
        var ast1 = _api.Parse(source1).Ast!;
        var ast2 = _api.Parse(source2).Ast!;

        var result = AstFingerprint.Classify(ast1, ast2);

        result.Kind.Should().Be(AstChangeKind.Structural);
    }

    // #1974: `let x = e` introduces a fresh binding where `x = e` may write through, so flipping
    // the keyword on an otherwise identical file changes the program and MUST change the
    // fingerprint. The same-source cases are the positive control that a `let` body compares equal
    // to itself (the flag is compared, not merely present). A tuple-target row is deliberately
    // absent: ExpressionEquals has no TupleLiteral arm, so any tuple store is conservatively
    // "changed" even against itself and could not show the flag being compared.

    [Theory]
    [InlineData("def main():\n    let x = 1\n    print(x)", "def main():\n    x = 1\n    print(x)")]
    [InlineData("def main():\n    let x: int = 1\n    print(x)", "def main():\n    x: int = 1\n    print(x)")]
    public void Classify_LetFlippedInAFunctionBody_ReturnsBodyOnly(string withLet, string withoutLet)
    {
        AstFingerprint.Classify(_api.Parse(withLet).Ast!, _api.Parse(withLet).Ast!)
            .Kind.Should().Be(AstChangeKind.NoChange);

        var result = AstFingerprint.Classify(_api.Parse(withLet).Ast!, _api.Parse(withoutLet).Ast!);

        result.Kind.Should().Be(AstChangeKind.BodyOnly);
        result.FunctionName.Should().Be("main");
    }

    [Fact]
    public void Classify_LetFlippedAtTopLevel_ReturnsStructural()
    {
        // A top-level `let` is refused with a different diagnostic from a top-level `x = e`, and a
        // top-level `let x: T = e` from `x: T = e` — the flag is part of the top-level signature.
        var bare = _api.Parse("let x = 1").Ast!;
        var keywordless = _api.Parse("x = 1").Ast!;
        AstFingerprint.Classify(bare, _api.Parse("let x = 1").Ast!).Kind.Should().Be(AstChangeKind.NoChange);
        AstFingerprint.Classify(bare, keywordless).Kind.Should().Be(AstChangeKind.Structural);

        var annotated = _api.Parse("let x: int = 1").Ast!;
        AstFingerprint.Classify(annotated, _api.Parse("x: int = 1").Ast!).Kind.Should().Be(AstChangeKind.Structural);
    }
}
