using Xunit;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

public class ControlFlowValidatorTests
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        var context = new SemanticContext(symbolTable, semanticInfo, typeResolver);
        return (module, context);
    }

    #region Missing Return

    [Fact]
    public void Function_WithoutReturn_ReportsError()
    {
        var code = @"
def foo() -> int:
    x = 5
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("must return a value"));
    }

    [Fact]
    public void Function_WithReturn_NoError()
    {
        var code = @"
def foo() -> int:
    return 5
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void IfWithoutElse_MissingReturn_ReportsError()
    {
        var code = @"
def foo(x: int) -> int:
    if x > 0:
        return 1
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("must return a value"));
    }

    [Fact]
    public void IfElseAllBranchesReturn_NoError()
    {
        var code = @"
def foo(x: int) -> int:
    if x > 0:
        return 1
    else:
        return -1
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void IfElifElse_AllBranchesReturn_NoError()
    {
        var code = @"
def foo(x: int) -> int:
    if x > 0:
        return 1
    elif x < 0:
        return -1
    else:
        return 0
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void IfElifNoElse_MissingReturn_ReportsError()
    {
        var code = @"
def foo(x: int) -> int:
    if x > 0:
        return 1
    elif x < 0:
        return -1
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("must return a value"));
    }

    [Fact]
    public void TryCatchAllBranchesReturn_NoError()
    {
        var code = @"
def foo() -> int:
    try:
        return 1
    except Exception:
        return 2
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void FinallyReturn_CoversAllPaths()
    {
        var code = @"
def foo() -> int:
    try:
        x = 1
    except Exception:
        y = 2
    finally:
        return 3
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void VoidFunction_NoReturnNeeded()
    {
        var code = @"
def foo() -> None:
    x = 5
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    #endregion

    #region Break/Continue

    [Fact]
    public void BreakOutsideLoop_ReportsError()
    {
        var code = @"
def foo() -> None:
    break
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("'break' statement outside loop"));
    }

    [Fact]
    public void BreakInsideLoop_NoError()
    {
        var code = @"
def foo() -> None:
    while True:
        break
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void ContinueOutsideLoop_ReportsError()
    {
        var code = @"
def foo() -> None:
    continue
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("'continue' statement outside loop"));
    }

    [Fact]
    public void ContinueInsideLoop_NoError()
    {
        var code = @"
def foo() -> None:
    for i in range(10):
        continue
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void NestedLoops_BreakValidation()
    {
        var code = @"
def foo() -> None:
    for i in range(10):
        for j in range(10):
            break
        break
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    #endregion

    #region Unreachable Code

    [Fact]
    public void UnreachableCode_AfterReturn_ReportsWarning()
    {
        var code = @"
def foo() -> int:
    return 5
    x = 10
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetWarnings(),
            w => w.Message.Contains("Unreachable code"));
    }

    [Fact]
    public void UnreachableCode_AfterRaise_ReportsWarning()
    {
        var code = @"
def foo() -> None:
    raise ValueError()
    x = 10
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetWarnings(),
            w => w.Message.Contains("Unreachable code"));
    }

    [Fact]
    public void UnreachableCode_AfterBreak_ReportsWarning()
    {
        var code = @"
def foo() -> None:
    while True:
        break
        x = 10
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetWarnings(),
            w => w.Message.Contains("Unreachable code"));
    }

    [Fact]
    public void UnreachableCode_AfterContinue_ReportsWarning()
    {
        var code = @"
def foo() -> None:
    for i in range(10):
        continue
        x = 10
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetWarnings(),
            w => w.Message.Contains("Unreachable code"));
    }

    #endregion

    #region Abstract Methods

    [Fact]
    public void AbstractMethod_SkipsValidation()
    {
        var code = @"
@abstract
class Base:
    @abstract
    def foo(self) -> int:
        ...
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    #endregion

    #region Class/Struct Methods

    [Fact]
    public void ClassMethod_ValidatesControlFlow()
    {
        var code = @"
class Foo:
    def bar(self) -> int:
        pass
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("must return a value"));
    }

    [Fact]
    public void StructMethod_ValidatesControlFlow()
    {
        var code = @"
struct Point:
    x: int
    y: int

    def magnitude(self) -> int:
        pass
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("must return a value"));
    }

    #endregion
}
