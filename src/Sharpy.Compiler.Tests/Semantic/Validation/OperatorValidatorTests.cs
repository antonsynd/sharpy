using System.Linq;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

public class OperatorValidatorTests
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

    // =====================
    // Binary Operator Tests
    // =====================

    [Fact]
    public void BinaryOp_IntAddition_NoError()
    {
        var code = @"
def foo() -> int:
    return 1 + 2
";
        var (module, context) = Parse(code);

        var validator = new OperatorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void BinaryOp_FloatMultiplication_NoError()
    {
        var code = @"
def foo() -> float:
    return 1.5 * 2.0
";
        var (module, context) = Parse(code);

        var validator = new OperatorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void BinaryOp_StringConcatenation_NoError()
    {
        var code = @"
def foo() -> str:
    return ""hello"" + "" world""
";
        var (module, context) = Parse(code);

        var validator = new OperatorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void BinaryOp_ListConcatenation_NoError()
    {
        var code = @"
def foo() -> list[int]:
    a: list[int] = [1, 2]
    b: list[int] = [3, 4]
    return a + b
";
        var (module, context) = Parse(code);

        var validator = new OperatorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void BinaryOp_BitwiseOperators_NoError()
    {
        var code = @"
def foo() -> int:
    return (1 & 2) | (3 ^ 4)
";
        var (module, context) = Parse(code);

        var validator = new OperatorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void BinaryOp_ComparisonOperators_NoError()
    {
        var code = @"
def foo() -> bool:
    return 1 < 2 and 2 <= 3 and 4 > 3 and 5 >= 4
";
        var (module, context) = Parse(code);

        var validator = new OperatorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void BinaryOp_SetOperators_NoError()
    {
        var code = @"
def foo() -> set[int]:
    a: set[int] = {1, 2}
    b: set[int] = {2, 3}
    union: set[int] = a | b
    intersect: set[int] = a & b
    diff: set[int] = a - b
    return union ^ intersect
";
        var (module, context) = Parse(code);

        var validator = new OperatorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    // =====================
    // Null Coalescing Tests
    // =====================

    [Fact]
    public void NullCoalesce_NullableType_NoError()
    {
        var code = @"
def foo(x: int?) -> int:
    return x ?? 0
";
        var (module, context) = Parse(code);

        var validator = new OperatorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void NullCoalesce_NonNullableType_ReportsError()
    {
        var code = @"
def foo(x: int) -> int:
    return x ?? 0
";
        var (module, context) = Parse(code);

        var validator = new OperatorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("null coalescing") && e.Message.Contains("must be nullable"));
    }

    // =====================
    // Unary Operator Tests
    // =====================

    [Fact]
    public void UnaryOp_Negation_NoError()
    {
        var code = @"
def foo() -> int:
    return -5
";
        var (module, context) = Parse(code);

        var validator = new OperatorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void UnaryOp_Plus_NoError()
    {
        var code = @"
def foo() -> int:
    return +5
";
        var (module, context) = Parse(code);

        var validator = new OperatorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void UnaryOp_BitwiseNot_NoError()
    {
        var code = @"
def foo() -> int:
    return ~5
";
        var (module, context) = Parse(code);

        var validator = new OperatorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void UnaryOp_Not_AlwaysValid()
    {
        var code = @"
def foo() -> bool:
    return not True
";
        var (module, context) = Parse(code);

        var validator = new OperatorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    // ==============================
    // Augmented Assignment Tests
    // ==============================

    [Fact]
    public void AugmentedAssign_IntPlusAssign_NoError()
    {
        var code = @"
def foo() -> int:
    x: int = 5
    x += 3
    return x
";
        var (module, context) = Parse(code);

        var validator = new OperatorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void AugmentedAssign_ListExtend_NoError()
    {
        var code = @"
def foo() -> list[int]:
    items: list[int] = [1, 2]
    items += [3, 4]
    return items
";
        var (module, context) = Parse(code);

        var validator = new OperatorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void AugmentedAssign_SetUpdate_NoError()
    {
        var code = @"
def foo() -> set[int]:
    s: set[int] = {1, 2}
    s |= {3, 4}
    return s
";
        var (module, context) = Parse(code);

        var validator = new OperatorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    // ======================================
    // Logical Operators (Always Valid) Tests
    // ======================================

    [Fact]
    public void LogicalOp_AndOr_NoError()
    {
        var code = @"
def foo() -> bool:
    return True and False or True
";
        var (module, context) = Parse(code);

        var validator = new OperatorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void IdentityOp_IsIsNot_NoError()
    {
        var code = @"
def foo() -> bool:
    x: int? = None
    return x is None and x is not None
";
        var (module, context) = Parse(code);

        var validator = new OperatorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    // ======================================
    // Diagnostic Deduplication Tests
    // ======================================

    [Fact]
    public void UnsupportedOperator_ReportedExactlyOnce()
    {
        // This test verifies the deduplication between TypeChecker (SPY0222)
        // and OperatorValidator (SPY0402). When a custom class doesn't support
        // an operator, both phases detect it. The HasErrorAtPosition check in
        // OperatorValidator should prevent duplicate reporting.
        var code = @"
class Foo:
    pass

def bar() -> int:
    a: Foo = Foo()
    b: Foo = Foo()
    return a + b
";
        var (module, context) = Parse(code);

        var validator = new OperatorValidator();
        validator.Validate(module, context);

        var errors = context.Diagnostics.GetErrors();
        var operatorErrors = errors.Where(e =>
            e.Message.Contains("does not support operator") ||
            e.Message.Contains("Unsupported operand")).ToList();

        // Should have exactly one error, not two (deduplication works)
        Assert.Single(operatorErrors);
    }
}
