using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// SPY0522 in the LOCAL declaration space (#1276). Every shape that binds a local has to reach the
/// validator: the emitter keys its slot table on the mangled base name, so any binding form it
/// misses is either silent wrong output (the second binding writes the first's storage) or an ICE
/// (CS0128/CS0136 out of the generated C#).
/// </summary>
public class LocalNameCollisionValidatorTests
{
    private static IReadOnlyList<CompilerDiagnostic> Check(string source)
    {
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var parser = new global::Sharpy.Compiler.Parser.Parser(lexer.TokenizeAll(), NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();

        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance);
        typeChecker.CheckModule(module, isEntryPoint: false);

        return typeChecker.Diagnostics.GetErrors();
    }

    private static void ShouldCollide(string source, string secondSpelling, string firstSpelling)
    {
        var errors = Check(source);
        errors.Should().ContainSingle(d => d.Code == DiagnosticCodes.CodeGen.MemberNameCollision
            && d.Message.Contains($"local '{secondSpelling}' and '{firstSpelling}'"));
    }

    private static void ShouldNotCollide(string source)
    {
        Check(source).Should().NotContain(
            d => d.Code == DiagnosticCodes.CodeGen.MemberNameCollision);
    }

    [Fact]
    public void TupleUnpackingTargetCollides()
    {
        ShouldCollide(@"
def main() -> None:
    zed: int = 6
    Zed, other = (7, 8)
    print(zed, Zed, other)
", "Zed", "zed");
    }

    [Fact]
    public void StarredUnpackingTargetCollides()
    {
        ShouldCollide(@"
def main() -> None:
    Rest: int = 5
    a, *rest = (1, 2, 3)
    print(a, rest, Rest)
", "rest", "Rest");
    }

    [Fact]
    public void MatchCaptureCollides()
    {
        ShouldCollide(@"
def main() -> None:
    zed: int = 6
    value: object = 7
    match value:
        case int() as Zed:
            print(Zed)
        case _:
            print(0)
    print(zed)
", "Zed", "zed");
    }

    [Fact]
    public void BareBindingPatternCollides()
    {
        ShouldCollide(@"
def main() -> None:
    zed: int = 6
    value: int = 7
    match value:
        case 7:
            print(70)
        case Zed:
            print(Zed)
    print(zed)
", "Zed", "zed");
    }

    [Fact]
    public void InlineOutDeclarationCollides()
    {
        ShouldCollide(@"
def try_parse(s: str, result: out int) -> bool:
    result = int(s)
    return True

def main() -> None:
    Value: int = 5
    success = try_parse('42', out value: int)
    print(success, value, Value)
", "value", "Value");
    }

    /// <summary>
    /// A constant pattern compares, it does not capture (RFC 3535), so it introduces no local to
    /// collide with. Positive control for the capture arms above.
    /// </summary>
    [Fact]
    public void ConstantPatternDoesNotCollide()
    {
        ShouldNotCollide(@"
const LIMIT: int = 7

def main() -> None:
    limit: int = 3
    value: int = 7
    match value:
        case LIMIT:
            print(1)
        case _:
            print(0)
    print(limit)
");
    }

    [Fact]
    public void DistinctTupleTargetsDoNotCollide()
    {
        ShouldNotCollide(@"
def main() -> None:
    zed: int = 6
    first, second = (7, 8)
    print(zed, first, second)
");
    }

    /// <summary>A nested function has its own slot table, as it does in the emitter.</summary>
    [Fact]
    public void NestedFunctionScopeIsSeparate()
    {
        ShouldNotCollide(@"
def main() -> None:
    zed: int = 6

    def inner() -> None:
        Zed, other = (7, 8)
        print(Zed, other)

    inner()
    print(zed)
");
    }
}
