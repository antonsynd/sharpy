using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// #1414: "@abstract member in a non-abstract class" was reported twice — once by the TypeChecker as
/// SPY0247 (<c>MissingMethodBody</c>) and again by <c>AbstractMemberValidator</c> as SPY0493, which
/// owns the rule and also covers properties and events. SPY0493 is now the only report.
/// </summary>
/// <remarks>
/// Programmatic rather than a <c>.error</c> fixture, and necessarily so. The fixture format cannot
/// express "and nothing else": <c>FileBasedIntegrationTestsBase</c> matches each expected line with
/// <c>Assert.Contains</c> and has no <c>DoesNotContain</c> and no count assertion, so a sidecar
/// pinning SPY0493's text passes whether or not SPY0247 also fires. The <c>@N:C</c> pin does not
/// rescue it either — it locates the diagnostic by matching the message first, so a SPY0493 pattern
/// only ever matches the SPY0493 diagnostic. <c>errors/abstract_member_non_abstract_class_1307</c>
/// is the standing proof: it is green today, while the duplicate is live. (#1457 tracks the format
/// gap.) The count assertion below is the only thing that can observe this fix.
/// </remarks>
public class AbstractPlacementSingleReportTests
{
    private static DiagnosticBag Check(string source)
    {
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var parser = new global::Sharpy.Compiler.Parser.Parser(lexer.TokenizeAll(), NullLogger.Instance);
        var module = parser.ParseModule();

        var symbolTable = new SymbolTable(new BuiltinRegistry());
        var semanticInfo = new SemanticInfo();
        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance);
        typeChecker.CheckModule(module);
        return typeChecker.Diagnostics;
    }

    /// <summary>The deliverable: one rule, one report.</summary>
    [Fact]
    public void AbstractMethodInNonAbstractClass_ReportsExactlyOneError_AndItIsSpy0493()
    {
        var errors = Check(@"
class Foo:
    @abstract
    def greet(self) -> str:
        ...

def main():
    pass
").GetErrors();

        errors.Should().ContainSingle("one rule reported twice is one report too many")
            .Which.Code.Should().Be(DiagnosticCodes.Validation.AbstractMemberInNonAbstractClass);
    }

    /// <summary>
    /// Control against over-deletion. SPY0247's OTHER arm — an <c>@abstract</c> method whose body is
    /// not <c>...</c> — is <c>MissingMethodBody</c> doing its actual job and must survive. Deleting
    /// both arms would leave this cell unreported and still satisfy the test above.
    /// </summary>
    [Fact]
    public void AbstractMethodWithoutEllipsisBody_StillReportsMissingMethodBody()
    {
        var errors = Check(@"
@abstract
class Foo:
    @abstract
    def greet(self) -> str:
        return ""hi""

def main():
    pass
").GetErrors();

        errors.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.MissingMethodBody,
            "the ellipsis-body requirement is a different rule and keeps SPY0247 Active");
    }

    /// <summary>
    /// Control against over-refusal: the legal placement must stay silent. Without this, dropping
    /// the arm and simultaneously breaking the validator's abstract-class exemption would go unseen.
    /// </summary>
    [Fact]
    public void AbstractMethodInAbstractClass_ReportsNothing()
    {
        Check(@"
@abstract
class Foo:
    @abstract
    def greet(self) -> str:
        ...

def main():
    pass
").GetErrors().Should().BeEmpty();
    }
}
