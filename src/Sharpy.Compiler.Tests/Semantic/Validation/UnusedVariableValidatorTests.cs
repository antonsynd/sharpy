using Xunit;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

public class UnusedVariableValidatorTests
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

        var context = new SemanticContext(symbolTable, semanticInfo, typeResolver);
        return (module, context);
    }

    private List<CompilerDiagnostic> GetUnusedVarWarnings(SemanticContext context)
    {
        return context.Diagnostics.GetWarnings()
            .Where(w => w.Code == DiagnosticCodes.Validation.UnusedVariable)
            .ToList();
    }

    [Fact]
    public void UnusedLocal_ReportsWarning()
    {
        var code = @"
def foo():
    x: int = 42
    print(""hello"")
";
        var (module, context) = Parse(code);
        var validator = new UnusedVariableValidator();
        validator.Validate(module, context);

        var warnings = GetUnusedVarWarnings(context);
        Assert.Single(warnings);
        Assert.Contains("'x'", warnings[0].Message);
    }

    [Fact]
    public void UsedLocal_NoWarning()
    {
        var code = @"
def foo():
    x: int = 42
    print(x)
";
        var (module, context) = Parse(code);
        var validator = new UnusedVariableValidator();
        validator.Validate(module, context);

        Assert.Empty(GetUnusedVarWarnings(context));
    }

    [Fact]
    public void UnderscorePrefix_NoWarning()
    {
        var code = @"
def foo():
    _unused: int = 42
    print(""hello"")
";
        var (module, context) = Parse(code);
        var validator = new UnusedVariableValidator();
        validator.Validate(module, context);

        Assert.Empty(GetUnusedVarWarnings(context));
    }

    [Fact]
    public void Parameter_NoWarning()
    {
        var code = @"
def foo(x: int):
    print(""hello"")
";
        var (module, context) = Parse(code);
        var validator = new UnusedVariableValidator();
        validator.Validate(module, context);

        Assert.Empty(GetUnusedVarWarnings(context));
    }

    [Fact]
    public void LoopVariable_NoWarning()
    {
        var code = @"
def foo():
    for i in range(10):
        print(""hello"")
";
        var (module, context) = Parse(code);
        var validator = new UnusedVariableValidator();
        validator.Validate(module, context);

        Assert.Empty(GetUnusedVarWarnings(context));
    }

    [Fact]
    public void AugmentedAssignment_CountsAsRead()
    {
        var code = @"
def foo():
    x: int = 0
    x += 1
";
        var (module, context) = Parse(code);
        var validator = new UnusedVariableValidator();
        validator.Validate(module, context);

        // x += 1 reads x, so no unused warning for x
        Assert.Empty(GetUnusedVarWarnings(context));
    }

    [Fact]
    public void TupleUnpacking_UnusedTarget_ReportsWarning()
    {
        // Pre-declare variables then unpack into them
        var code = @"
def foo():
    a: int = 0
    b: int = 0
    a, b = 1, 2
    print(a)
";
        var (module, context) = Parse(code);
        var validator = new UnusedVariableValidator();
        validator.Validate(module, context);

        var warnings = GetUnusedVarWarnings(context);
        // b is assigned in tuple unpacking but never read after
        Assert.Single(warnings);
        Assert.Contains("'b'", warnings[0].Message);
    }

    [Fact]
    public void ExceptVariable_UnusedReportsWarning()
    {
        var code = @"
def foo():
    try:
        print(""trying"")
    except Exception as e:
        print(""caught"")
";
        var (module, context) = Parse(code);
        var validator = new UnusedVariableValidator();
        validator.Validate(module, context);

        var warnings = GetUnusedVarWarnings(context);
        Assert.Single(warnings);
        Assert.Contains("'e'", warnings[0].Message);
    }

    [Fact]
    public void ExceptVariable_Used_NoWarning()
    {
        var code = @"
def foo():
    try:
        print(""trying"")
    except Exception as e:
        print(e)
";
        var (module, context) = Parse(code);
        var validator = new UnusedVariableValidator();
        validator.Validate(module, context);

        Assert.Empty(GetUnusedVarWarnings(context));
    }

    [Fact]
    public void MultipleUnused_ReportsAll()
    {
        var code = @"
def foo():
    a: int = 1
    b: str = ""hello""
    c: float = 3.14
    print(""nothing used"")
";
        var (module, context) = Parse(code);
        var validator = new UnusedVariableValidator();
        validator.Validate(module, context);

        var warnings = GetUnusedVarWarnings(context);
        Assert.Equal(3, warnings.Count);
    }

    [Fact]
    public void ClassMethod_ValidatesBody()
    {
        var code = @"
class Foo:
    def bar(self):
        x: int = 42
        print(""hello"")
";
        var (module, context) = Parse(code);
        var validator = new UnusedVariableValidator();
        validator.Validate(module, context);

        var warnings = GetUnusedVarWarnings(context);
        Assert.Single(warnings);
        Assert.Contains("'x'", warnings[0].Message);
    }

    [Fact]
    public void StructMethod_ValidatesBody()
    {
        var code = @"
struct Point:
    x: int
    y: int

    def magnitude(self) -> float:
        unused: int = 0
        return 0.0
";
        var (module, context) = Parse(code);
        var validator = new UnusedVariableValidator();
        validator.Validate(module, context);

        var warnings = GetUnusedVarWarnings(context);
        Assert.Single(warnings);
        Assert.Contains("'unused'", warnings[0].Message);
    }

    [Fact]
    public void AbstractMethod_SkipsValidation()
    {
        var code = @"
class Base:
    @abstract
    def foo(self) -> int:
        ...
";
        var (module, context) = Parse(code);
        var validator = new UnusedVariableValidator();
        validator.Validate(module, context);

        Assert.Empty(GetUnusedVarWarnings(context));
    }

    [Fact]
    public void StubBody_SkipsValidation()
    {
        var code = @"
def foo() -> int:
    ...
";
        var (module, context) = Parse(code);
        var validator = new UnusedVariableValidator();
        validator.Validate(module, context);

        Assert.Empty(GetUnusedVarWarnings(context));
    }

    [Fact]
    public void VariableUsedInReturn_NoWarning()
    {
        var code = @"
def foo() -> int:
    x: int = 42
    return x
";
        var (module, context) = Parse(code);
        var validator = new UnusedVariableValidator();
        validator.Validate(module, context);

        Assert.Empty(GetUnusedVarWarnings(context));
    }

    [Fact]
    public void VariableUsedInCondition_NoWarning()
    {
        var code = @"
def foo():
    x: int = 42
    if x > 0:
        print(""positive"")
";
        var (module, context) = Parse(code);
        var validator = new UnusedVariableValidator();
        validator.Validate(module, context);

        Assert.Empty(GetUnusedVarWarnings(context));
    }

    [Fact]
    public void VariableUsedInFunctionCall_NoWarning()
    {
        var code = @"
def foo():
    items: list[int] = [1, 2, 3]
    print(len(items))
";
        var (module, context) = Parse(code);
        var validator = new UnusedVariableValidator();
        validator.Validate(module, context);

        Assert.Empty(GetUnusedVarWarnings(context));
    }

    [Fact]
    public void AssignmentTarget_MemberAccess_NoDefinition()
    {
        var code = @"
def foo(obj):
    obj.x = 42
";
        var (module, context) = Parse(code);
        var validator = new UnusedVariableValidator();
        validator.Validate(module, context);

        // obj.x = 42 reads obj (member access), doesn't define a new variable
        Assert.Empty(GetUnusedVarWarnings(context));
    }
}
