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
    public void MustUseFunction_DiscardedResult_Warns()
    {
        var tc = Check(@"
@must_use
def parse() -> int:
    return 5

def main() -> None:
    parse()
");
        Assert.Equal(1, MustUseWarnings(tc));
    }

    [Fact]
    public void MustUseFunction_BoundOrExplicitlyDiscarded_NoWarning()
    {
        var tc = Check(@"
@must_use
def parse() -> int:
    return 5

def main() -> None:
    x = parse()
    _ = parse()
    print(x)
");
        Assert.Equal(0, MustUseWarnings(tc));
    }

    [Fact]
    public void MustUseType_DiscardedValue_Warns()
    {
        var tc = Check(@"
@must_use
class Handle:
    id: int

    def __init__(self, id: int) -> None:
        self.id = id

def open_handle() -> Handle:
    return Handle(1)

def main() -> None:
    open_handle()
");
        Assert.Equal(1, MustUseWarnings(tc));
    }

    [Fact]
    public void PlainType_DiscardedValue_NoWarning()
    {
        // Same shape without @must_use — no warning, proving the flag drives the behavior.
        var tc = Check(@"
class Handle:
    id: int

    def __init__(self, id: int) -> None:
        self.id = id

def open_handle() -> Handle:
    return Handle(1)

def main() -> None:
    open_handle()
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

    [Fact]
    public void QuestionMarkPropagation_NoWarning()
    {
        // `get()?` checks as the *unwrapped* int (QuestionMarkExpression), so discarding it as a
        // bare statement is the sanctioned propagation escape — no warning.
        var tc = Check(Preamble + @"
def caller() -> int?:
    get()?
    return None
");
        Assert.Equal(0, MustUseWarnings(tc));
    }

    [Fact]
    public void CarrierInConditionPosition_NoWarning()
    {
        // A carrier consumed inside an `if` test is not an ExpressionStatement — never flagged.
        var tc = Check(@"
@must_use
def parse() -> int:
    return 5

def main() -> None:
    if parse() > 0:
        print(1)
");
        Assert.Equal(0, MustUseWarnings(tc));
    }

    [Fact]
    public void NestedCarrier_QuestionUnwrapStillWrapped_Warns()
    {
        // `get_nested()?` unwraps the outer Result but the value is *still* an `int !str` —
        // discarding a still-wrapped inner carrier warns.
        var tc = Check(@"
def inner() -> int !str:
    return Ok(42)

def get_nested() -> Result[int !str, str]:
    return Ok(inner())

def process() -> int !str:
    get_nested()?
    return Ok(1)
");
        Assert.Equal(1, MustUseWarnings(tc));
    }

    [Fact]
    public void MaybeStatement_Warns()
    {
        // `maybe expr` produces an OptionalType — discarding it as a bare statement warns.
        var tc = Check(Preamble + @"
def main() -> None:
    maybe get_n()
");
        Assert.Equal(1, MustUseWarnings(tc));
    }

    [Fact]
    public void MustUseStruct_DiscardedValue_Warns()
    {
        var tc = Check(@"
@must_use
struct Pair:
    x: int

    def __init__(self, x: int) -> None:
        self.x = x

def make() -> Pair:
    return Pair(1)

def main() -> None:
    make()
");
        Assert.Equal(1, MustUseWarnings(tc));
    }

    [Fact]
    public void MustUseEnum_DiscardedValue_Warns()
    {
        var tc = Check(@"
@must_use
enum Color:
    RED = 1
    GREEN = 2

def pick() -> Color:
    return Color.RED

def main() -> None:
    pick()
");
        Assert.Equal(1, MustUseWarnings(tc));
    }
}
