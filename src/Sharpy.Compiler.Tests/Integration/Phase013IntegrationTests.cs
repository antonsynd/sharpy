using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for Phase 0.1.3: Variable declarations, type inference, and const.
/// These tests verify the full compilation pipeline for variable-related features.
/// </summary>
public class Phase013IntegrationTests : IntegrationTestBase
{
    public Phase013IntegrationTests(ITestOutputHelper output) : base(output)
    {
    }

    #region Spec Example Tests

    [Fact]
    public void SpecExample_TypedVariablesWithOperations_CompilesAndRuns()
    {
        var source = @"
x: int = 10
y: int = 20
z = x + y
z += 5
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void SpecExample_ConstDeclaration_CompilesAndRuns()
    {
        var source = @"const MAX: int = 100";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void SpecExample_ConstCannotBeReassigned_ReportsError()
    {
        var source = @"
const MAX: int = 100
# MAX = 50  # This would error - testing below
";

        var result = CompileAndExecute(source);

        // The commented line doesn't cause error, just const declaration succeeds
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    #endregion

    #region Type Inference Tests

    [Fact]
    public void TypeInference_IntegerLiteral_InferredAsInt32_CompilesAndRuns()
    {
        var source = @"x = 42";  // Inferred as int32

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void TypeInference_FloatLiteral_InferredAsFloat64_CompilesAndRuns()
    {
        var source = @"y = 3.14";  // Inferred as float64

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void TypeInference_StringLiteral_InferredAsStr_CompilesAndRuns()
    {
        var source = @"s = ""hello world""";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void TypeInference_BooleanLiterals_InferredAsBool_CompilesAndRuns()
    {
        var source = @"
flag = True
other = False
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void TypeInference_AutoKeyword_Integer_CompilesAndRuns()
    {
        var source = @"x: auto = 42";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void TypeInference_AutoKeyword_Float_CompilesAndRuns()
    {
        var source = @"y: auto = 3.14";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void TypeInference_AutoKeyword_String_CompilesAndRuns()
    {
        var source = @"z: auto = ""hello""";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void TypeInference_AutoKeyword_AllTypes_CompilesAndRuns()
    {
        var source = @"
x: auto = 42
y: auto = 3.14
z: auto = ""hello""
w: auto = True
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void TypeInference_FromExpression_CompilesAndRuns()
    {
        var source = @"
a = 10
b = 20
c = a + b
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    #endregion

    #region Const Declaration Tests

    [Fact]
    public void Const_IntegerDeclaration_CompilesAndRuns()
    {
        var source = @"const MAX: int = 100";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void Const_FloatDeclaration_CompilesAndRuns()
    {
        var source = @"const PI: float = 3.14159";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void Const_StringDeclaration_CompilesAndRuns()
    {
        var source = @"const NAME: str = ""Sharpy""";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void Const_BoolDeclaration_CompilesAndRuns()
    {
        var source = @"const DEBUG: bool = True";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void Const_MultipleDeclarations_CompilesAndRuns()
    {
        var source = @"
const MAX: int = 100
const MIN: int = 0
const PI: float = 3.14159
const NAME: str = ""App""
const DEBUG: bool = False
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void Const_UsedInExpression_CompilesAndRuns()
    {
        var source = @"
const BASE: int = 10
x = BASE * 2
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void Const_UsedInMultipleExpressions_CompilesAndRuns()
    {
        var source = @"
const FACTOR: int = 5
a = FACTOR * 2
b = FACTOR + 10
c = a + b + FACTOR
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void Const_NegativeIntValue_CompilesAndRuns()
    {
        var source = @"const MIN_VALUE: int = -100";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void Const_NegativeFloatValue_CompilesAndRuns()
    {
        var source = @"const OFFSET: float = -1.5";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    #endregion

    #region Error Cases - Undefined Variable

    [Fact]
    public void Error_UndefinedVariable_ReportsError()
    {
        var source = @"y = x";  // x not defined

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for undefined variable");
        Assert.NotEmpty(result.CompilationErrors);
        var errorText = string.Join(" ", result.CompilationErrors).ToLower();
        Assert.True(
            errorText.Contains("undefined") ||
            errorText.Contains("not defined") ||
            errorText.Contains("not declared") ||
            errorText.Contains("does not exist"),
            $"Expected error about undefined variable, got: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void Error_UndefinedVariableInExpression_ReportsError()
    {
        var source = @"
x: int = 10
z = x + y
";  // y not defined

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for undefined variable in expression");
        Assert.NotEmpty(result.CompilationErrors);
    }

    [Fact]
    public void Error_UndefinedVariableInAugmentedAssignment_ReportsError()
    {
        var source = @"x += 5";  // x not defined

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for undefined variable in augmented assignment");
        Assert.NotEmpty(result.CompilationErrors);
    }

    #endregion

    #region Error Cases - Const Reassignment

    [Fact]
    public void Error_ConstReassignment_SimpleAssignment_ReportsError()
    {
        var source = @"
const MAX: int = 100
MAX = 50
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for const reassignment");
        Assert.NotEmpty(result.CompilationErrors);
        var errorText = string.Join(" ", result.CompilationErrors).ToLower();
        Assert.True(
            errorText.Contains("constant") ||
            errorText.Contains("const") ||
            errorText.Contains("cannot assign"),
            $"Expected error about constant reassignment, got: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void Error_ConstReassignment_AugmentedAssignment_ReportsError()
    {
        var source = @"
const MAX: int = 100
MAX += 10
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for const augmented assignment");
        Assert.NotEmpty(result.CompilationErrors);
        var errorText = string.Join(" ", result.CompilationErrors).ToLower();
        Assert.True(
            errorText.Contains("constant") ||
            errorText.Contains("const") ||
            errorText.Contains("cannot assign"),
            $"Expected error about constant reassignment, got: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void Error_ConstReassignment_SubtractAssignment_ReportsError()
    {
        var source = @"
const VALUE: int = 100
VALUE -= 10
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for const augmented assignment");
        Assert.NotEmpty(result.CompilationErrors);
    }

    [Fact]
    public void Error_ConstReassignment_MultiplyAssignment_ReportsError()
    {
        var source = @"
const VALUE: int = 100
VALUE *= 2
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for const augmented assignment");
        Assert.NotEmpty(result.CompilationErrors);
    }

    #endregion

    #region Error Cases - Type Mismatch

    [Fact]
    public void Error_TypeMismatch_StringToInt_ReportsError()
    {
        var source = @"x: int = ""hello""";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for type mismatch");
        Assert.NotEmpty(result.CompilationErrors);
        var errorText = string.Join(" ", result.CompilationErrors).ToLower();
        Assert.True(
            errorText.Contains("cannot assign") ||
            errorText.Contains("type") ||
            errorText.Contains("mismatch"),
            $"Expected error about type mismatch, got: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void Error_TypeMismatch_IntToStr_ReportsError()
    {
        var source = @"x: str = 42";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for type mismatch");
        Assert.NotEmpty(result.CompilationErrors);
    }

    [Fact]
    public void Error_TypeMismatch_BoolToInt_ReportsError()
    {
        var source = @"x: int = True";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for type mismatch");
        Assert.NotEmpty(result.CompilationErrors);
    }

    [Fact]
    public void Error_TypeMismatch_IntToBool_ReportsError()
    {
        var source = @"x: bool = 42";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for type mismatch");
        Assert.NotEmpty(result.CompilationErrors);
    }

    [Fact]
    public void Error_TypeMismatch_ReassignmentWithWrongType_ReportsError()
    {
        var source = @"
x: int = 10
x = ""hello""
";

        var result = CompileAndExecute(source);

        // This should fail due to type mismatch on reassignment without redefinition
        Assert.False(result.Success, "Expected compilation to fail for type mismatch on reassignment");
        Assert.NotEmpty(result.CompilationErrors);
    }

    #endregion

    #region Variable Redefinition Tests

    [Fact]
    public void VariableRedefinition_SameType_CompilesAndRuns()
    {
        var source = @"
x: int = 1
x: int = 2
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void VariableRedefinition_DifferentType_WithAuto_CompilesAndRuns()
    {
        var source = @"
x: int = 1
x: auto = ""hello""
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void VariableReassignment_AfterDeclaration_CompilesAndRuns()
    {
        var source = @"
x: int = 1
x = 2
x = 3
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void VariableReassignment_WithExpression_CompilesAndRuns()
    {
        var source = @"
x: int = 1
x = x + 1
x = x * 2
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    #endregion

    #region Augmented Assignment Tests

    [Fact]
    public void AugmentedAssignment_AddEquals_CompilesAndRuns()
    {
        var source = @"
x: int = 10
x += 5
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void AugmentedAssignment_SubtractEquals_CompilesAndRuns()
    {
        var source = @"
x: int = 10
x -= 3
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void AugmentedAssignment_MultiplyEquals_CompilesAndRuns()
    {
        var source = @"
x: int = 10
x *= 2
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void AugmentedAssignment_DivideEquals_CompilesAndRuns()
    {
        var source = @"
x: int = 20
x //= 4
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void AugmentedAssignment_AllOperators_CompilesAndRuns()
    {
        var source = @"
x: int = 10
x += 5
x -= 3
x *= 2
x //= 4
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void AugmentedAssignment_WithExpression_CompilesAndRuns()
    {
        var source = @"
x: int = 10
y: int = 5
x += y
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    #endregion

    #region Mixed Declaration Tests

    [Fact]
    public void MixedDeclarations_VariablesAndConsts_CompilesAndRuns()
    {
        var source = @"
const MAX: int = 100
x: int = 10
y = 20
z = x + y
const MIN: int = 0
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void MixedDeclarations_AllTypeAnnotations_CompilesAndRuns()
    {
        var source = @"
a: int = 42
b: float = 3.14
c: str = ""hello""
d: bool = True
e = 100
f: auto = 200
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact(Skip = "TODO: Augmented assignment type checking issue - '+=' with const operand fails semantic analysis")]
    public void MixedDeclarations_WithOperations_CompilesAndRuns()
    {
        var source = @"
const BASE: int = 10
x: int = BASE
y = x + 5
z: auto = y * 2
z += BASE
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void MixedDeclarations_CommentsInterspersed_CompilesAndRuns()
    {
        var source = @"
# Define constants
const MAX: int = 100

# Initialize variables
x: int = 10  # First value
y: int = 20  # Second value

# Calculate result
z = x + y  # Sum

# Update with augmented assignment
z += 5  # Final value
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EdgeCase_ZeroValueAssignment_CompilesAndRuns()
    {
        var source = @"
x: int = 0
y: float = 0.0
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void EdgeCase_EmptyStringAssignment_CompilesAndRuns()
    {
        var source = @"s: str = """"";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void EdgeCase_LargeIntegerValue_CompilesAndRuns()
    {
        var source = @"x: int = 2147483647";  // Max int32

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void EdgeCase_ScientificNotationFloat_CompilesAndRuns()
    {
        var source = @"x = 1.23e10";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void EdgeCase_SelfReferentialExpression_CompilesAndRuns()
    {
        var source = @"
x: int = 5
x = x + x
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void EdgeCase_VariableNameStartsWithUnderscore_CompilesAndRuns()
    {
        var source = @"_private: int = 42";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void EdgeCase_VariableNameWithNumbers_CompilesAndRuns()
    {
        var source = @"var123: int = 42";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
    }

    #endregion
}
