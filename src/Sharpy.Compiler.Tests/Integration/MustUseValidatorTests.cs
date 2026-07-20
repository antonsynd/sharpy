using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Tests for the must-use carrier warning (SPY0480, #1022): discarding a Result/Optional as a
/// bare expression statement warns; binding, explicit '_ =' discard, and '?' propagation do not.
/// Runs the full NameResolver + TypeChecker (which drives the validation pipeline) because the
/// validator reads effective types from SemanticInfo.
/// </summary>
[Collection("HeavyCompilation")]
public class MustUseValidatorTests
{
    private static TypeChecker Check(string code)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(code);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var typeResolver = new TypeResolver(symbolTable, semanticInfo);

        var nameResolver = new NameResolver(symbolTable);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver);
        typeChecker.CheckModule(module);
        return typeChecker;
    }

    private static int MustUseWarnings(TypeChecker tc) =>
        tc.Diagnostics.GetWarnings().Count(w => w.Code == DiagnosticCodes.Validation.MustUseValueDiscarded);

    // `int?` in Sharpy source is the strict OptionalType (a must-use carrier); `int | None` is the
    // loose NullableType (ordinary .NET-interop shape, NOT a carrier). The two are distinct here.
    private const string Preamble = @"
def risky() -> int:
    return 5

def get() -> int?:
    return None

def get_n() -> int | None:
    return None
";

    [Fact]
    public void BareResultStatement_Warns()
    {
        var tc = Check(Preamble + @"
def main() -> None:
    try risky()
");
        Assert.Equal(1, MustUseWarnings(tc));
    }

    [Fact]
    public void BareOptionalStatement_Warns()
    {
        // get() returns int? (OptionalType), discarded as a bare statement.
        var tc = Check(Preamble + @"
def main() -> None:
    get()
");
        Assert.Equal(1, MustUseWarnings(tc));
    }

    [Fact]
    public void BoundResult_NoWarning()
    {
        var tc = Check(Preamble + @"
def main() -> None:
    r = try risky()
    print(r.is_ok)
");
        Assert.Equal(0, MustUseWarnings(tc));
    }

    [Fact]
    public void ExplicitDiscard_NoWarning()
    {
        // `_ = expr` parses as an Assignment, never an ExpressionStatement, so it never reaches
        // the walker as a carrier — the sanctioned throw-away escape.
        var tc = Check(Preamble + @"
def main() -> None:
    _ = try risky()
");
        Assert.Equal(0, MustUseWarnings(tc));
    }

    [Fact]
    public void PlainNonCarrierCall_NoWarning()
    {
        // risky() returns a bare int (not a carrier) — discarding it is fine.
        var tc = Check(Preamble + @"
def main() -> None:
    risky()
");
        Assert.Equal(0, MustUseWarnings(tc));
    }

    [Fact]
    public void NullableValueStatement_NotFlagged()
    {
        // A loose C#-interop nullable (int | None) is NOT a must-use carrier — flagging it would
        // punish ordinary .NET calls (Axiom 1). A bare nullable expression statement stays clean.
        var tc = Check(Preamble + @"
def main() -> None:
    n: int | None = get_n()
    n
");
        Assert.Equal(0, MustUseWarnings(tc));
    }
}
