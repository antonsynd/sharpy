using Xunit;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

public class DefaultParameterValidatorV2Tests
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

        // Run type checking to populate semantic info
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver);
        typeChecker.CheckModule(module);

        var context = new SemanticContext(symbolTable, semanticInfo, typeResolver);
        return (module, context);
    }

    [Fact]
    public void LiteralDefault_IsValid()
    {
        var code = @"
def foo(x: int = 42) -> None:
    pass
";
        var (module, context) = Parse(code);

        var validator = new DefaultParameterValidatorV2();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void StringLiteralDefault_IsValid()
    {
        var code = @"
def greet(name: str = ""World"") -> None:
    pass
";
        var (module, context) = Parse(code);

        var validator = new DefaultParameterValidatorV2();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void BoolLiteralDefault_IsValid()
    {
        var code = @"
def check(enabled: bool = True) -> None:
    pass
";
        var (module, context) = Parse(code);

        var validator = new DefaultParameterValidatorV2();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void NoneLiteralDefault_ForNullableType_IsValid()
    {
        var code = @"
def process(value: int? = None) -> None:
    pass
";
        var (module, context) = Parse(code);

        var validator = new DefaultParameterValidatorV2();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void NoneLiteralDefault_ForNonNullableType_ReportsError()
    {
        var code = @"
def process(value: int = None) -> None:
    pass
";
        var (module, context) = Parse(code);

        var validator = new DefaultParameterValidatorV2();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("Cannot use 'None' as default value for non-nullable parameter"));
    }

    [Fact]
    public void EmptyListDefault_ReportsError()
    {
        var code = @"
def foo(items: list[int] = []) -> None:
    pass
";
        var (module, context) = Parse(code);

        var validator = new DefaultParameterValidatorV2();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("Mutable default value is not allowed"));
    }

    [Fact]
    public void ListWithElementsDefault_ReportsError()
    {
        var code = @"
def foo(items: list[int] = [1, 2, 3]) -> None:
    pass
";
        var (module, context) = Parse(code);

        var validator = new DefaultParameterValidatorV2();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("Mutable default value is not allowed"));
    }

    [Fact]
    public void EmptyDictDefault_ReportsError()
    {
        var code = @"
def foo(data: dict[str, int] = {}) -> None:
    pass
";
        var (module, context) = Parse(code);

        var validator = new DefaultParameterValidatorV2();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("Mutable default value is not allowed"));
    }

    [Fact]
    public void TupleDefault_IsValid()
    {
        // Tuples are immutable, so they are allowed as defaults
        var code = @"
def foo(point: tuple[int, int] = (0, 0)) -> None:
    pass
";
        var (module, context) = Parse(code);

        var validator = new DefaultParameterValidatorV2();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void UnaryOperationDefault_IsValid()
    {
        var code = @"
def foo(x: int = -1) -> None:
    pass
";
        var (module, context) = Parse(code);

        var validator = new DefaultParameterValidatorV2();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void BinaryOperationDefault_IsValid()
    {
        var code = @"
def foo(x: int = 1 + 2) -> None:
    pass
";
        var (module, context) = Parse(code);

        var validator = new DefaultParameterValidatorV2();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void FunctionCallDefault_ReportsError()
    {
        var code = @"
def helper() -> int:
    return 42

def foo(x: int = helper()) -> None:
    pass
";
        var (module, context) = Parse(code);

        var validator = new DefaultParameterValidatorV2();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("must be a compile-time constant expression"));
    }

    [Fact]
    public void MethodDefault_InClass()
    {
        var code = @"
class Foo:
    def bar(self, x: int = 42) -> None:
        pass
";
        var (module, context) = Parse(code);

        var validator = new DefaultParameterValidatorV2();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void MethodDefault_MutableInClass_ReportsError()
    {
        var code = @"
class Foo:
    def bar(self, items: list[int] = []) -> None:
        pass
";
        var (module, context) = Parse(code);

        var validator = new DefaultParameterValidatorV2();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("Mutable default value is not allowed"));
    }

    [Fact]
    public void MultipleParameters_MixedDefaults()
    {
        var code = @"
def foo(a: int, b: str = ""default"", c: int = 10) -> None:
    pass
";
        var (module, context) = Parse(code);

        var validator = new DefaultParameterValidatorV2();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void NestedFunction_ValidatesDefaults()
    {
        var code = @"
def outer() -> None:
    def inner(x: list[int] = []) -> None:
        pass
";
        var (module, context) = Parse(code);

        var validator = new DefaultParameterValidatorV2();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("Mutable default value is not allowed"));
    }
}
