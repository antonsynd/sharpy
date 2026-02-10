using Xunit;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

/// <summary>
/// Tests for EqualityContractValidator, which warns when a class defines __eq__
/// but no __eq__(self, other: object) overload (SPY0454).
/// </summary>
public class EqualityContractValidatorTests
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

        // Run name resolution
        var nameResolver = new NameResolver(symbolTable);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        var context = new SemanticContext(symbolTable, semanticInfo, typeResolver);
        return (module, context);
    }

    [Fact]
    public void EqWithObjectOverload_NoWarning()
    {
        var code = @"
class Foo:
    def __eq__(self, other: object) -> bool:
        return True

    def __hash__(self) -> int:
        return 0
";
        var (module, context) = Parse(code);

        var validator = new EqualityContractValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        Assert.Empty(context.Diagnostics.GetWarnings());
    }

    [Fact]
    public void EqWithTypedOverloadOnly_WarnsAboutMissingObjectOverload()
    {
        var code = @"
class Foo:
    def __eq__(self, other: Foo) -> bool:
        return True

    def __hash__(self) -> int:
        return 0
";
        var (module, context) = Parse(code);

        var validator = new EqualityContractValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        var warnings = context.Diagnostics.GetWarnings();
        Assert.Single(warnings);
        Assert.Contains("__eq__", warnings[0].Message);
        Assert.Contains("__eq__(self, other: object)", warnings[0].Message);
        Assert.Equal(DiagnosticCodes.Validation.EqWithoutObjectOverload, warnings[0].Code);
    }

    [Fact]
    public void EqWithBothTypedAndObjectOverloads_NoWarning()
    {
        var code = @"
class Foo:
    def __eq__(self, other: Foo) -> bool:
        return True

    def __eq__(self, other: object) -> bool:
        return False

    def __hash__(self) -> int:
        return 0
";
        var (module, context) = Parse(code);

        var validator = new EqualityContractValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        Assert.Empty(context.Diagnostics.GetWarnings());
    }

    [Fact]
    public void NoEqDefined_NoWarning()
    {
        var code = @"
class Foo:
    x: int

    def __init__(self, x: int):
        self.x = x
";
        var (module, context) = Parse(code);

        var validator = new EqualityContractValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        Assert.Empty(context.Diagnostics.GetWarnings());
    }

    [Fact]
    public void StructWithTypedEqOnly_WarnsAboutMissingObjectOverload()
    {
        var code = @"
struct Point:
    x: int
    y: int

    def __eq__(self, other: Point) -> bool:
        return self.x == other.x and self.y == other.y

    def __hash__(self) -> int:
        return self.x * 31 + self.y
";
        var (module, context) = Parse(code);

        var validator = new EqualityContractValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        var warnings = context.Diagnostics.GetWarnings();
        Assert.Single(warnings);
        Assert.Contains("Point", warnings[0].Message);
        Assert.Equal(DiagnosticCodes.Validation.EqWithoutObjectOverload, warnings[0].Code);
    }

    [Fact]
    public void StructWithObjectEq_NoWarning()
    {
        var code = @"
struct Point:
    x: int
    y: int

    def __eq__(self, other: object) -> bool:
        return False

    def __hash__(self) -> int:
        return self.x * 31 + self.y
";
        var (module, context) = Parse(code);

        var validator = new EqualityContractValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        Assert.Empty(context.Diagnostics.GetWarnings());
    }
}
