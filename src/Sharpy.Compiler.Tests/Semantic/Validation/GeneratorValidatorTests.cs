using Xunit;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

public class GeneratorValidatorTests
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
    public void ValidGeneratorFunction_NoDiagnostics()
    {
        var code = @"
def count_up(n: int) -> int:
    i: int = 0
    while i < n:
        yield i
        i += 1
";
        var (module, context) = Parse(code);
        var validator = new GeneratorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void YieldInNext_EmitsError()
    {
        var code = @"
class BadIterator:
    def __next__(self) -> int:
        yield 1
";
        var (module, context) = Parse(code);
        var validator = new GeneratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("__next__") && e.Message.Contains("cannot contain 'yield'"));
    }

    [Fact]
    public void YieldWithReturnValue_EmitsError()
    {
        var code = @"
def bad_gen() -> int:
    yield 1
    return 42
";
        var (module, context) = Parse(code);
        var validator = new GeneratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("return") && e.Message.Contains("value"));
    }

    [Fact]
    public void GeneratorIterConflict_EmitsError()
    {
        var code = @"
class BadClass:
    def __iter__(self) -> int:
        yield 1

    def __next__(self) -> int:
        return 1
";
        var (module, context) = Parse(code);
        var validator = new GeneratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("generator") && e.Message.Contains("__iter__"));
    }

    [Fact]
    public void BareReturnInExceptHandler_NoError()
    {
        // Bare 'return' (-> yield break) in an except handler is allowed.
        // The validator only inspects YieldStatement nodes, and bare return
        // is a ReturnStatement, so SPY0271 is not triggered.
        var code = @"
def gen() -> int:
    i = 0
    while i < 3:
        try:
            pass
        except Exception:
            return
        yield i
        i += 1
";
        var (module, context) = Parse(code);
        var validator = new GeneratorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void YieldValueInExceptHandler_EmitsError()
    {
        // yield <expr> in an except handler is still forbidden (SPY0271).
        var code = @"
def gen() -> int:
    try:
        pass
    except Exception:
        yield 1
";
        var (module, context) = Parse(code);
        var validator = new GeneratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("inside an 'except' handler"));
    }

    [Fact]
    public void BareReturnInTryBodyWithExcept_NoError()
    {
        // Bare 'return' (-> yield break) in a try body with except handlers
        // is allowed; only yield <expr> is forbidden there.
        var code = @"
def gen() -> int:
    i = 0
    while i < 3:
        try:
            if i == 2:
                return
        except Exception:
            pass
        yield i
        i += 1
";
        var (module, context) = Parse(code);
        var validator = new GeneratorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void YieldValueInTryBodyWithExcept_EmitsError()
    {
        // yield <expr> in a try body with except handlers is still forbidden (SPY0270).
        var code = @"
def gen() -> int:
    try:
        yield 1
    except Exception:
        pass
";
        var (module, context) = Parse(code);
        var validator = new GeneratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("inside a 'try' block that has 'except' handlers"));
    }

    [Fact]
    public void BareReturnInFinally_NoError()
    {
        // The validator only inspects YieldStatement nodes in finally bodies.
        // Bare 'return' is a ReturnStatement, so SPY0272 is not triggered here.
        var code = @"
def gen() -> int:
    i = 0
    while i < 3:
        try:
            pass
        finally:
            return
        yield i
        i += 1
";
        var (module, context) = Parse(code);
        var validator = new GeneratorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }
}
