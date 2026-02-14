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
}
