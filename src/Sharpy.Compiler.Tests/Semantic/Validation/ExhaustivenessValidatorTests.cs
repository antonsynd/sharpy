using Xunit;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

public class ExhaustivenessValidatorTests
{
    private (Module module, SemanticContext context) Parse(string code)
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        var context = new SemanticContext(symbolTable, semanticInfo, typeResolver);
        return (module, context);
    }

    [Fact]
    public void ExhaustiveEnumMatch_NoDiagnostics()
    {
        var code = @"
enum Color:
    RED = 0
    GREEN = 1
    BLUE = 2

def describe(c: Color) -> str:
    match c:
        case Color.RED:
            return ""red""
        case Color.GREEN:
            return ""green""
        case Color.BLUE:
            return ""blue""
    return """"
";
        var (module, context) = Parse(code);
        var validator = new ExhaustivenessValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        var warnings = context.Diagnostics.GetWarnings();
        Assert.DoesNotContain(warnings, w => w.Message.Contains("not exhaustive") || w.Message.Contains("Not exhaustive"));
    }

    [Fact]
    public void NonExhaustiveEnumMatch_EmitsWarning()
    {
        var code = @"
enum Color:
    RED = 0
    GREEN = 1
    BLUE = 2

def describe(c: Color) -> str:
    match c:
        case Color.RED:
            return ""red""
        case Color.GREEN:
            return ""green""
    return ""other""
";
        var (module, context) = Parse(code);
        var validator = new ExhaustivenessValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        var warnings = context.Diagnostics.GetWarnings();
        Assert.Contains(warnings, w => w.Message.Contains("not exhaustive"));
        Assert.Contains(warnings, w => w.Message.Contains("BLUE"));
    }

    [Fact]
    public void ExhaustiveBoolMatch_NoDiagnostics()
    {
        var code = @"
def describe(b: bool) -> str:
    match b:
        case True:
            return ""yes""
        case False:
            return ""no""
    return """"
";
        var (module, context) = Parse(code);
        var validator = new ExhaustivenessValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        var warnings = context.Diagnostics.GetWarnings();
        Assert.DoesNotContain(warnings, w => w.Message.Contains("not exhaustive"));
    }

    [Fact]
    public void NonExhaustiveBoolMatch_EmitsWarning()
    {
        var code = @"
def describe(b: bool) -> str:
    match b:
        case True:
            return ""yes""
    return ""other""
";
        var (module, context) = Parse(code);
        var validator = new ExhaustivenessValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        var warnings = context.Diagnostics.GetWarnings();
        Assert.Contains(warnings, w => w.Message.Contains("not exhaustive"));
        Assert.Contains(warnings, w => w.Message.Contains("False"));
    }

    [Fact]
    public void ExhaustiveUnionMatch_NoDiagnostics()
    {
        var code = @"
union Shape:
    case Circle(radius: float)
    case Square(side: float)

def area(s: Shape) -> float:
    match s:
        case Circle(r):
            return 3.14 * r * r
        case Square(side):
            return side * side
    return 0.0
";
        var (module, context) = Parse(code);
        var validator = new ExhaustivenessValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        var warnings = context.Diagnostics.GetWarnings();
        Assert.DoesNotContain(warnings, w => w.Message.Contains("not exhaustive"));
    }

    [Fact]
    public void NonExhaustiveUnionMatchExpression_EmitsError()
    {
        var code = @"
enum Color:
    RED = 0
    GREEN = 1
    BLUE = 2

def describe(c: Color) -> str:
    return match c:
        case Color.RED: ""red""
        case Color.GREEN: ""green""
";
        var (module, context) = Parse(code);
        var validator = new ExhaustivenessValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("not exhaustive"));
    }

    [Fact]
    public void WildcardArmMakesMatchExhaustive()
    {
        var code = @"
enum Color:
    RED = 0
    GREEN = 1
    BLUE = 2

def describe(c: Color) -> str:
    match c:
        case Color.RED:
            return ""red""
        case _:
            return ""other""
    return """"
";
        var (module, context) = Parse(code);
        var validator = new ExhaustivenessValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        var warnings = context.Diagnostics.GetWarnings();
        Assert.DoesNotContain(warnings, w => w.Message.Contains("not exhaustive"));
    }

    [Fact]
    public void GuardedArmDoesNotCountAsExhaustive()
    {
        var code = @"
def describe(b: bool) -> str:
    match b:
        case True:
            return ""yes""
        case False if True:
            return ""no""
    return ""other""
";
        var (module, context) = Parse(code);
        var validator = new ExhaustivenessValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        var warnings = context.Diagnostics.GetWarnings();
        Assert.Contains(warnings, w => w.Message.Contains("not exhaustive"));
        Assert.Contains(warnings, w => w.Message.Contains("False"));
    }
}
