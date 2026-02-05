using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Compiler.Tests.Diagnostics;

/// <summary>
/// Tests for Phase 1.2: Verifying that critical assertions are promoted
/// to release builds and emit proper diagnostics.
/// </summary>
/// <remarks>
/// These tests verify that <see cref="CompilerInvariants.AssertGeneratedCSharpParses"/>
/// is always-on (not DEBUG-only) and emits proper diagnostic codes.
/// </remarks>
public class AssertionPromotionTests
{
    [Fact]
    public void AssertGeneratedCSharpParses_ValidCode_NoDiagnostics()
    {
        var diagnostics = new DiagnosticBag();
        var validCSharp = @"
using System;
namespace Test
{
    public class Program
    {
        public static void Main() { Console.WriteLine(""hello""); }
    }
}";
        // Call the consolidated CompilerInvariants method directly
        CompilerInvariants.AssertGeneratedCSharpParses(validCSharp, diagnostics);

        diagnostics.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void AssertGeneratedCSharpParses_InvalidCode_EmitsDiagnostic()
    {
        var diagnostics = new DiagnosticBag();
        var invalidCSharp = @"
using System;
namespace Test
{
    public class Program
    {
        public static void Main() { Console.WriteLine(""hello"" // missing closing paren and semicolon
    }
}";
        CompilerInvariants.AssertGeneratedCSharpParses(invalidCSharp, diagnostics);

        diagnostics.HasErrors.Should().BeTrue();
        var errors = diagnostics.GetErrors();
        errors.Should().ContainSingle();
        errors[0].Code.Should().Be(DiagnosticCodes.CodeGen.InternalGeneratedCSharpParseError);
        errors[0].Message.Should().Contain("Internal error");
        errors[0].Message.Should().Contain("compiler bug");
        errors[0].Phase.Should().Be(CompilerPhase.CodeGeneration);
    }

    [Fact]
    public void AssertGeneratedCSharpParses_MultipleErrors_ReportsCount()
    {
        var diagnostics = new DiagnosticBag();
        var badCSharp = @"
class {  // missing class name
    void foo( {  // malformed method
    }
";
        CompilerInvariants.AssertGeneratedCSharpParses(badCSharp, diagnostics);

        diagnostics.HasErrors.Should().BeTrue();
        var error = diagnostics.GetErrors().First();
        error.Message.Should().Contain("syntax error");
    }
}
