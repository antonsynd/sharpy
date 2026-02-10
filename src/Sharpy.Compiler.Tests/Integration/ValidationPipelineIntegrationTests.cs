using Xunit;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for the validation pipeline.
/// Verifies that the complete pipeline works correctly with real code.
/// </summary>
public class ValidationPipelineIntegrationTests
{
    private (Module module, TypeChecker typeChecker) CompileAndCheck(string code)
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

        return (module, typeChecker);
    }

    #region Default Pipeline Behavior

    [Fact]
    public void DefaultPipeline_ValidCode_NoErrors()
    {
        var code = @"
class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int) -> None:
        self.x = x
        self.y = y

    def __add__(self, other: Point) -> Point:
        return Point(self.x + other.x, self.y + other.y)

def main() -> int:
    p1 = Point(1, 2)
    p2 = Point(3, 4)
    p3 = p1 + p2
    return p3.x
";
        var (_, typeChecker) = CompileAndCheck(code);

        Assert.Empty(typeChecker.Diagnostics.GetErrors());
    }

    [Fact]
    public void DefaultPipeline_SignatureError_ReportsError()
    {
        var code = @"
class BadOperator:
    def __add__(self) -> int:
        return 0
";
        var (_, typeChecker) = CompileAndCheck(code);

        Assert.NotEmpty(typeChecker.Diagnostics.GetErrors());
        Assert.Contains(typeChecker.Diagnostics.GetErrors(), e => e.Message.Contains("must have exactly 2 parameters"));
    }

    [Fact]
    public void DefaultPipeline_ControlFlowError_ReportsError()
    {
        var code = @"
def foo() -> int:
    x = 5
";
        var (_, typeChecker) = CompileAndCheck(code);

        Assert.NotEmpty(typeChecker.Diagnostics.GetErrors());
        Assert.Contains(typeChecker.Diagnostics.GetErrors(), e => e.Message.Contains("must return"));
    }

    [Fact]
    public void DefaultPipeline_BreakOutsideLoop_ReportsError()
    {
        var code = @"
def foo() -> None:
    break
";
        var (_, typeChecker) = CompileAndCheck(code);

        Assert.NotEmpty(typeChecker.Diagnostics.GetErrors());
        Assert.Contains(typeChecker.Diagnostics.GetErrors(), e => e.Message.Contains("'break' statement outside loop"));
    }

    #endregion

    #region Multiple Error Detection

    [Fact]
    public void DefaultPipeline_MultipleValidatorErrors_AllReported()
    {
        var code = @"
class BadClass:
    def __add__(self) -> int:
        x = 5
        break
";
        var (_, typeChecker) = CompileAndCheck(code);

        var errors = typeChecker.Diagnostics.GetErrors();
        // Signature error
        Assert.Contains(errors, e => e.Message.Contains("must have exactly 2 parameters"));
        // Control flow error (break outside loop)
        Assert.Contains(errors, e => e.Message.Contains("'break' statement outside loop"));
    }

    [Fact]
    public void DefaultPipeline_ProtocolAndControlFlowErrors_AllReported()
    {
        var code = @"
class Container:
    def __len__(self, extra: int) -> int:
        x = 5

    def __contains__(self) -> bool:
        return True
";
        var (_, typeChecker) = CompileAndCheck(code);

        var errors = typeChecker.Diagnostics.GetErrors();
        // Protocol signature errors
        Assert.Contains(errors, e => e.Message.Contains("__len__") && e.Message.Contains("1 parameter"));
        Assert.Contains(errors, e => e.Message.Contains("__contains__") && e.Message.Contains("2 parameters"));
        // Control flow error (__len__ missing return)
        Assert.Contains(errors, e => e.Message.Contains("must return"));
    }

    #endregion

    #region Error Location Preservation

    [Fact]
    public void DefaultPipeline_ErrorsHaveCorrectLineNumbers()
    {
        var code = @"
def foo() -> int:
    x = 5
";
        var (_, typeChecker) = CompileAndCheck(code);

        var error = typeChecker.Diagnostics.GetErrors().FirstOrDefault(e => e.Message.Contains("must return"));
        Assert.NotNull(error);
        Assert.True(error.Line.HasValue, "Error should have line number");
        Assert.True(error.Line > 0, "Line number should be positive");
    }

    [Fact]
    public void SignatureError_HasCorrectLineNumber()
    {
        var code = @"
class Bad:
    def __neg__(self, extra: int) -> int:
        return 0
";
        var (_, typeChecker) = CompileAndCheck(code);

        var error = typeChecker.Diagnostics.GetErrors().FirstOrDefault(e => e.Message.Contains("must have exactly 1 parameter"));
        Assert.NotNull(error);
        Assert.True(error.Line.HasValue, "Error should have line number");
        Assert.Equal(3, error.Line.Value);  // __neg__ is on line 3
    }

    #endregion

    #region Complex Code Patterns

    [Fact]
    public void DefaultPipeline_ClassWithMultipleInvalidOperators_AllReported()
    {
        var code = @"
class BadOperators:
    def __add__(self) -> int:
        return 0

    def __neg__(self, extra: int) -> int:
        return 0
";
        var (_, typeChecker) = CompileAndCheck(code);

        // Should have errors for both operators
        Assert.Contains(typeChecker.Diagnostics.GetErrors(), e => e.Message.Contains("__add__") && e.Message.Contains("2 parameters"));
        Assert.Contains(typeChecker.Diagnostics.GetErrors(), e => e.Message.Contains("__neg__") && e.Message.Contains("1 parameter"));
    }

    [Fact]
    public void DefaultPipeline_StructWithOperators_ValidatesSignatures()
    {
        var code = @"
struct Vector:
    x: int
    y: int

    def __add__(self, other: Vector) -> Vector:
        return Vector(self.x + other.x, self.y + other.y)

    def __neg__(self) -> Vector:
        return Vector(-self.x, -self.y)
";
        var (_, typeChecker) = CompileAndCheck(code);

        Assert.Empty(typeChecker.Diagnostics.GetErrors());
    }

    [Fact]
    public void DefaultPipeline_StructWithBadOperator_ReportsError()
    {
        var code = @"
struct BadVector:
    x: int

    def __add__(self) -> BadVector:
        return BadVector(0)
";
        var (_, typeChecker) = CompileAndCheck(code);

        Assert.Contains(typeChecker.Diagnostics.GetErrors(), e => e.Message.Contains("must have exactly 2 parameters"));
    }

    #endregion

    #region Valid Code Patterns

    [Fact]
    public void DefaultPipeline_AllProtocols_Valid()
    {
        var code = @"
class Container:
    def __len__(self) -> int:
        return 0

    def __iter__(self) -> object:
        return self

    def __contains__(self, item: int) -> bool:
        return False

    def __getitem__(self, index: int) -> int:
        return 0

    def __setitem__(self, index: int, value: int) -> None:
        pass

    @override
    def __str__(self) -> str:
        return """"

    def __repr__(self) -> str:
        return """"

    def __eq__(self, other: object) -> bool:
        return False

    @override
    def __hash__(self) -> int:
        return 0

    def __bool__(self) -> bool:
        return True
";
        var (_, typeChecker) = CompileAndCheck(code);

        Assert.Empty(typeChecker.Diagnostics.GetErrors());
    }

    [Fact]
    public void DefaultPipeline_AllOperators_Valid()
    {
        var code = @"
class Number:
    def __add__(self, other: Number) -> Number:
        return self

    def __sub__(self, other: Number) -> Number:
        return self

    def __mul__(self, other: Number) -> Number:
        return self

    def __div__(self, other: Number) -> Number:
        return self

    @override
    def __eq__(self, other: object) -> bool:
        return True

    @override
    def __hash__(self) -> int:
        return 0

    def __lt__(self, other: Number) -> bool:
        return False

    def __neg__(self) -> Number:
        return self
";
        var (_, typeChecker) = CompileAndCheck(code);

        Assert.Empty(typeChecker.Diagnostics.GetErrors());
    }

    #endregion

    #region Error Deduplication

    [Fact]
    public void DefaultPipeline_NoDuplicateErrors()
    {
        var code = @"
def foo() -> int:
    x = 5
";
        var (_, typeChecker) = CompileAndCheck(code);

        // Should have exactly one "must return" error, not duplicates from both
        // legacy validator and pipeline validator
        var returnErrors = typeChecker.Diagnostics.GetErrors()
            .Where(e => e.Message.Contains("must return"))
            .ToList();

        Assert.Single(returnErrors);
    }

    [Fact]
    public void DefaultPipeline_NoDuplicateBreakErrors()
    {
        var code = @"
def foo() -> None:
    break
";
        var (_, typeChecker) = CompileAndCheck(code);

        // Should have exactly one "break statement outside loop" error
        var breakErrors = typeChecker.Diagnostics.GetErrors()
            .Where(e => e.Message.Contains("break"))
            .ToList();

        Assert.Single(breakErrors);
    }

    [Fact]
    public void DefaultPipeline_NoDuplicateOperatorErrors()
    {
        var code = @"
def foo() -> None:
    x: int = 'hello' + 5
";
        var (_, typeChecker) = CompileAndCheck(code);

        // Should have type error(s) but no duplicates for same operator error
        var operatorErrors = typeChecker.Diagnostics.GetErrors()
            .Where(e => e.Message.Contains("does not support operator"))
            .GroupBy(e => (e.Line, e.Message))
            .ToList();

        // Each error should appear only once (grouping by line+message should have count 1)
        foreach (var group in operatorErrors)
        {
            Assert.Single(group);
        }
    }

    #endregion

    #region Error Ordering

    [Fact]
    public void DefaultPipeline_ReportsMultipleErrorTypes()
    {
        // Code with multiple error types to verify all are reported
        var code = @"
class BadOperator:
    def __add__(self) -> int:
        break
        return 0
";
        var (_, typeChecker) = CompileAndCheck(code);

        // Should have at least 2 errors: signature and control flow
        Assert.True(typeChecker.Diagnostics.GetErrors().Count >= 2);

        // Verify both error types are present
        Assert.Contains(typeChecker.Diagnostics.GetErrors(), e => e.Message.Contains("parameter"));
        Assert.Contains(typeChecker.Diagnostics.GetErrors(), e => e.Message.Contains("break"));
    }

    #endregion

    #region TypeInferenceService Integration

    [Fact]
    public void DefaultPipeline_InfersBinaryOpTypes()
    {
        var code = @"
def foo() -> int:
    x: int = 5 + 3
    y: int = x * 2
    return y
";
        var (_, typeChecker) = CompileAndCheck(code);

        Assert.Empty(typeChecker.Diagnostics.GetErrors());
    }

    [Fact]
    public void DefaultPipeline_InfersUnaryOpTypes()
    {
        var code = @"
def foo() -> int:
    x: int = 5
    y: int = -x
    return y
";
        var (_, typeChecker) = CompileAndCheck(code);

        Assert.Empty(typeChecker.Diagnostics.GetErrors());
    }

    [Fact]
    public void DefaultPipeline_InfersIterableElementTypes()
    {
        var code = @"
def foo() -> None:
    items: list[int] = [1, 2, 3]
    for item in items:
        x: int = item
";
        var (_, typeChecker) = CompileAndCheck(code);

        Assert.Empty(typeChecker.Diagnostics.GetErrors());
    }

    [Fact]
    public void DefaultPipeline_InfersIndexAccessTypes()
    {
        var code = @"
def foo() -> int:
    items: list[int] = [1, 2, 3]
    return items[0]
";
        var (_, typeChecker) = CompileAndCheck(code);

        Assert.Empty(typeChecker.Diagnostics.GetErrors());
    }

    [Fact]
    public void DefaultPipeline_InfersLenReturnType()
    {
        var code = @"
def foo() -> int:
    items: list[int] = [1, 2, 3]
    return len(items)
";
        var (_, typeChecker) = CompileAndCheck(code);

        Assert.Empty(typeChecker.Diagnostics.GetErrors());
    }

    #endregion
}
