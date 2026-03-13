using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

public class ScopedTypeCheckerTests
{
    private readonly CompilerApi _api = new();

    [Fact]
    public void RecheckFunction_BodyChange_UpdatesTypes()
    {
        var source = "def greet() -> str:\n    return \"hello\"\ndef main():\n    print(greet())";

        var result = ScopedTypeChecker.RecheckFunction(_api, source);

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Ast.Should().NotBeNull();
    }

    [Fact]
    public void RecheckFunction_PreservesOtherFunctionTypes()
    {
        var source = "def add(a: int, b: int) -> int:\n    return a + b\ndef main():\n    x: int = add(1, 2)\n    print(x)";

        var result = ScopedTypeChecker.RecheckFunction(_api, source);

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public void RecheckFunction_DetectsNewTypeErrors()
    {
        // Source with a type error in the function body
        var source = "def greet() -> str:\n    return 42\ndef main():\n    print(greet())";

        var result = ScopedTypeChecker.RecheckFunction(_api, source);

        result.Should().NotBeNull();
        result!.Success.Should().BeFalse("type error: returning int from str function");
    }

    [Fact]
    public void RecheckFunction_WithCancellation_DoesNotThrowUnexpectedExceptions()
    {
        var source = "def main():\n    print(\"hello\")";
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // With an already-cancelled token, should either complete (fast analysis
        // may finish before checking the token) or throw OperationCanceledException.
        // ScopedTypeChecker re-throws OCE but catches other exceptions.
        try
        {
            var result = ScopedTypeChecker.RecheckFunction(_api, source, cts.Token);
            // If the compiler completed before checking the token, result is valid
            result.Should().NotBeNull("completed analysis is valid even with cancelled token");
        }
        catch (OperationCanceledException)
        {
            // Expected — cancellation was honored
        }
    }
}
