using System.Linq;
using Xunit;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

public class DecoratorValidatorTests
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

        var context = new SemanticContext(symbolTable, semanticInfo, typeResolver);
        return (module, context);
    }

    [Fact]
    public void Staticmethod_OnMethod_ReportsError()
    {
        var code = @"
class Foo:
    @staticmethod
    def bar(self):
        pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Single(errors);
        Assert.Contains("@staticmethod", errors[0].Message);
        Assert.Equal(DiagnosticCodes.Semantic.InvalidDecoratorUsage, errors[0].Code);
    }

    [Fact]
    public void Classmethod_OnMethod_ReportsError()
    {
        var code = @"
class Foo:
    @classmethod
    def bar(cls):
        pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Single(errors);
        Assert.Contains("@classmethod", errors[0].Message);
        Assert.Equal(DiagnosticCodes.Semantic.InvalidDecoratorUsage, errors[0].Code);
    }

    [Fact]
    public void CustomDecorator_NoError()
    {
        var code = @"
class Foo:
    @override
    def bar(self):
        pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void Staticmethod_OnTopLevelFunction_ReportsError()
    {
        var code = @"
@staticmethod
def foo():
    pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Single(errors);
        Assert.Contains("@staticmethod", errors[0].Message);
    }

    [Fact]
    public void MultipleUnsupportedDecorators_ReportsMultipleErrors()
    {
        var code = @"
class Foo:
    @staticmethod
    def bar():
        pass

    @classmethod
    def baz(cls):
        pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, e => e.Message.Contains("@staticmethod"));
        Assert.Contains(errors, e => e.Message.Contains("@classmethod"));
    }

    #region Decorator argument validation

    [Fact]
    public void Virtual_WithArgs_ReportsError()
    {
        var code = @"
class Foo:
    @virtual(""arg"")
    def bar(self):
        pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Single(errors);
        Assert.Contains("does not accept arguments", errors[0].Message);
        Assert.Contains("@virtual", errors[0].Message);
        Assert.Equal(DiagnosticCodes.Semantic.InvalidDecoratorUsage, errors[0].Code);
    }

    [Fact]
    public void Static_WithArgs_ReportsError()
    {
        var code = @"
class Foo:
    @static(True)
    def bar():
        pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Single(errors);
        Assert.Contains("does not accept arguments", errors[0].Message);
        Assert.Contains("@static", errors[0].Message);
        Assert.Equal(DiagnosticCodes.Semantic.InvalidDecoratorUsage, errors[0].Code);
    }

    [Fact]
    public void Custom_WithArithmeticExpression_ReportsNonConstError()
    {
        var code = @"
@[custom(1 + 2)]
def foo():
    pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Single(errors);
        Assert.Contains("compile-time constant", errors[0].Message);
        Assert.Equal(DiagnosticCodes.Validation.NonConstantDecoratorArgument, errors[0].Code);
    }

    [Fact]
    public void Custom_WithFunctionCall_ReportsNonConstError()
    {
        var code = @"
@[custom(some_func())]
def foo():
    pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Single(errors);
        Assert.Contains("compile-time constant", errors[0].Message);
        Assert.Equal(DiagnosticCodes.Validation.NonConstantDecoratorArgument, errors[0].Code);
    }

    [Fact]
    public void Custom_WithStringLiteral_NoError()
    {
        var code = @"
@[custom(""literal"")]
def foo():
    pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void Custom_WithKeywordArgs_NoError()
    {
        var code = @"
@[attr(name=""value"")]
def foo():
    pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void Custom_WithTypeCall_NoError()
    {
        var code = @"
@[custom(type(int))]
def foo():
    pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void Custom_WithEnumMemberAccess_NoError()
    {
        var code = @"
@[custom(StringComparison.ordinal)]
def foo():
    pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void Custom_WithNonConstKeywordArg_ReportsError()
    {
        var code = @"
@[attr(name=1 + 2)]
def foo():
    pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Single(errors);
        Assert.Contains("compile-time constant", errors[0].Message);
        Assert.Equal(DiagnosticCodes.Validation.NonConstantDecoratorArgument, errors[0].Code);
    }

    [Fact]
    public void Custom_WithBoolAndIntLiterals_NoError()
    {
        var code = @"
@[custom(True, 42)]
def foo():
    pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void Custom_WithNone_NoError()
    {
        var code = @"
@[custom(None)]
def foo():
    pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void Custom_WithVariableReference_ReportsNonConstError()
    {
        var code = @"
@[custom(some_var)]
def foo():
    pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Single(errors);
        Assert.Contains("compile-time constant", errors[0].Message);
        Assert.Equal(DiagnosticCodes.Validation.NonConstantDecoratorArgument, errors[0].Code);
    }

    [Fact]
    public void Custom_WithNegativeInt_NoError()
    {
        var code = @"
@[custom(-42)]
def foo():
    pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void Custom_WithNegativeFloat_NoError()
    {
        var code = @"
@[custom(-3.14)]
def foo():
    pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    #endregion

    #region @test decorator

    [Fact]
    public void Test_OnFunction_NoError()
    {
        var code = @"
@test
def test_something():
    pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        Assert.Empty(context.Diagnostics.GetWarnings());
    }

    [Fact]
    public void Test_OnClass_ReportsInvalidTarget()
    {
        var code = @"
@test
class MyClass:
    pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Single(errors);
        Assert.Contains("@test", errors[0].Message);
        Assert.Contains("class", errors[0].Message);
        Assert.Equal(DiagnosticCodes.Validation.TestDecoratorInvalidTarget, errors[0].Code);
    }

    [Fact]
    public void Test_WithDescription_NoError()
    {
        var code = @"
@test(""tests something specific"")
def test_something():
    pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        Assert.Empty(context.Diagnostics.GetWarnings());
    }

    [Fact]
    public void Test_WithNonStringArg_ReportsInvalidArgument()
    {
        var code = @"
@test(42)
def test_something():
    pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        var warnings = context.Diagnostics.GetWarnings();
        Assert.Single(warnings);
        Assert.Contains("string literal", warnings[0].Message);
        Assert.Equal(DiagnosticCodes.Validation.TestDecoratorInvalidArgument, warnings[0].Code);
    }

    [Fact]
    public void Test_WithMultipleArgs_ReportsInvalidArgument()
    {
        var code = @"
@test(""a"", ""b"")
def test_something():
    pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        var warnings = context.Diagnostics.GetWarnings();
        Assert.Single(warnings);
        Assert.Contains("at most one", warnings[0].Message);
        Assert.Equal(DiagnosticCodes.Validation.TestDecoratorInvalidArgument, warnings[0].Code);
    }

    [Fact]
    public void Test_WithStatic_ReportsInvalidCombination()
    {
        var code = @"
class Foo:
    @test
    @static
    def test_something():
        pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        // SPY0449 should be reported; other unrelated errors may also be present.
        Assert.Contains(errors, e => e.Code == DiagnosticCodes.Validation.TestDecoratorInvalidCombination);
        var combination = errors.First(e => e.Code == DiagnosticCodes.Validation.TestDecoratorInvalidCombination);
        Assert.Contains("@test", combination.Message);
        Assert.Contains("@static", combination.Message);
    }

    [Fact]
    public void Test_WithAbstract_ReportsInvalidCombination()
    {
        var code = @"
class Foo:
    @test
    @abstract
    def test_something(self):
        ...
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Contains(errors, e => e.Code == DiagnosticCodes.Validation.TestDecoratorInvalidCombination);
        var combination = errors.First(e => e.Code == DiagnosticCodes.Validation.TestDecoratorInvalidCombination);
        Assert.Contains("@test", combination.Message);
        Assert.Contains("@abstract", combination.Message);
    }

    [Fact]
    public void Test_OnDunderInit_ReportsInvalidTarget()
    {
        var code = @"
class Foo:
    @test
    def __init__(self):
        pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Contains(errors, e => e.Code == DiagnosticCodes.Validation.TestDecoratorInvalidTarget);
        var target = errors.First(e => e.Code == DiagnosticCodes.Validation.TestDecoratorInvalidTarget);
        Assert.Contains("@test", target.Message);
        Assert.Contains("dunder", target.Message);
    }

    #endregion

    #region @test.parametrize decorator

    [Fact]
    public void TestParametrize_OnFunctionWithMatchingArity_NoError()
    {
        var code = @"
@test.parametrize([(1, 2, 3), (4, 5, 9)])
def test_add(a: int, b: int, expected: int):
    pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        Assert.Empty(context.Diagnostics.GetWarnings());
    }

    [Fact]
    public void TestParametrize_SingleParamFlatList_NoError()
    {
        var code = @"
@test.parametrize([1, 2, 3])
def test_one(x: int):
    pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        Assert.Empty(context.Diagnostics.GetWarnings());
    }

    [Fact]
    public void TestParametrize_ArityMismatch_ReportsWarning()
    {
        var code = @"
@test.parametrize([(1, 2), (3,)])
def test_pair(a: int, b: int):
    pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        var warnings = context.Diagnostics.GetWarnings();
        Assert.Single(warnings);
        Assert.Equal(DiagnosticCodes.Validation.TestDecoratorInvalidArgument, warnings[0].Code);
        Assert.Contains("expected 2", warnings[0].Message);
    }

    [Fact]
    public void TestParametrize_NonListArgument_ReportsWarning()
    {
        var code = @"
@test.parametrize(42)
def test_x(a: int):
    pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        var warnings = context.Diagnostics.GetWarnings();
        Assert.NotEmpty(warnings);
        Assert.Contains(warnings, w => w.Code == DiagnosticCodes.Validation.TestDecoratorInvalidArgument);
    }

    [Fact]
    public void TestParametrize_NoArguments_ReportsWarning()
    {
        var code = @"
@test.parametrize
def test_x(a: int):
    pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        var warnings = context.Diagnostics.GetWarnings();
        Assert.NotEmpty(warnings);
        Assert.Contains(warnings, w => w.Code == DiagnosticCodes.Validation.TestDecoratorInvalidArgument);
    }

    [Fact]
    public void TestParametrize_CombinedWithPlainTest_ReportsError()
    {
        var code = @"
@test
@test.parametrize([(1,), (2,)])
def test_x(a: int):
    pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        var errors = context.Diagnostics.GetErrors();
        Assert.Contains(errors, e => e.Code == DiagnosticCodes.Validation.TestDecoratorInvalidCombination
            && e.Message.Contains("@test.parametrize"));
    }

    [Fact]
    public void TestParametrize_OnMethodWithSelf_ExcludesSelfFromArity()
    {
        var code = @"
class Foo:
    @test.parametrize([(1, 2), (3, 4)])
    def test_method(self, a: int, b: int):
        pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        Assert.Empty(context.Diagnostics.GetWarnings());
    }

    [Fact]
    public void TestParametrize_OnClass_ReportsInvalidTarget()
    {
        var code = @"
@test.parametrize([(1,)])
class Foo:
    pass
";
        var (module, context) = Parse(code);

        var validator = new DecoratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Contains(errors, e => e.Code == DiagnosticCodes.Validation.TestDecoratorInvalidTarget
            && e.Message.Contains("@test.parametrize"));
    }

    #endregion
}
