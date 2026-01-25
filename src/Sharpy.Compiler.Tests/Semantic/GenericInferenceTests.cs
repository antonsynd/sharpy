using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for generic type argument inference.
/// These tests verify that the compiler can infer type arguments from function arguments.
/// </summary>
public class GenericInferenceTests
{
    private (Module, SymbolTable, SemanticInfo, TypeChecker) CompileAndCheck(string source)
    {
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new global::Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();

        // Name resolution first
        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        // Type checking
        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance);

        return (module, symbolTable, semanticInfo, typeChecker);
    }

    /// <summary>
    /// Helper to get the expression type of a function call within main()
    /// </summary>
    private SemanticType? GetCallResultType(Module module, SemanticInfo semanticInfo, string varName)
    {
        // Find the main function
        var mainFunc = module.Body.OfType<FunctionDef>()
            .FirstOrDefault(f => f.Name == "main");
        if (mainFunc == null)
            return null;

        // Find the variable declaration in main
        var varDecl = mainFunc.Body.OfType<VariableDeclaration>()
            .FirstOrDefault(v => v.Name == varName);
        if (varDecl?.InitialValue == null)
            return null;

        return semanticInfo.GetExpressionType(varDecl.InitialValue);
    }

    #region Basic Single Type Parameter Inference

    [Fact]
    public void InferTypeFromSingleArgument_Int()
    {
        // Given a generic function called with an int argument
        var source = @"
def identity[T](value: T) -> T:
    return value

def main():
    result = identity(42)  # Should infer T=int
";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Then there should be no errors
        typeChecker.Errors.Should().BeEmpty("inference should deduce T=int from argument 42");

        // And the result should be typed as int
        var resultType = GetCallResultType(module, semanticInfo, "result");
        resultType.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferTypeFromSingleArgument_Str()
    {
        // Given a generic function called with a string argument
        var source = @"
def identity[T](value: T) -> T:
    return value

def main():
    result = identity(""hello"")  # Should infer T=str
";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Then there should be no errors
        typeChecker.Errors.Should().BeEmpty("inference should deduce T=str from argument \"hello\"");

        // And the result should be typed as str
        var resultType = GetCallResultType(module, semanticInfo, "result");
        resultType.Should().Be(SemanticType.Str);
    }

    [Fact]
    public void InferTypeFromMultipleArguments_AllSame()
    {
        // Given a generic function with multiple parameters of the same type parameter
        var source = @"
def pair[T](a: T, b: T) -> tuple[T, T]:
    return (a, b)

def main():
    result = pair(1, 2)  # Should infer T=int from both arguments
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Then there should be no errors
        typeChecker.Errors.Should().BeEmpty("inference should deduce T=int from both arguments");
    }

    #endregion

    #region Generic Container Type Inference

    [Fact]
    public void InferTypeFromGenericContainer_List()
    {
        // Given a function that takes list[T] and returns T
        var source = @"
def first[T](items: list[T]) -> T:
    return items[0]

def main():
    numbers: list[int] = [1, 2, 3]
    result = first(numbers)  # Should infer T=int from list[int]
";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Then there should be no errors
        typeChecker.Errors.Should().BeEmpty("inference should extract T=int from list[int]");

        // And the result should be typed as int
        var resultType = GetCallResultType(module, semanticInfo, "result");
        resultType.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferTypeFromGenericContainer_Dict()
    {
        // Given a function that takes dict[K, V] and returns tuple of key-value
        var source = @"
def get_first_pair[K, V](d: dict[K, V]) -> tuple[K, V]:
    for k, v in d.items():
        return (k, v)
    raise Exception(""empty dict"")

def main():
    data: dict[str, int] = {""a"": 1}
    result = get_first_pair(data)  # Should infer K=str, V=int
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Then there should be no errors
        typeChecker.Errors.Should().BeEmpty("inference should extract K=str, V=int from dict[str, int]");
    }

    #endregion

    #region Multiple Type Parameter Inference

    [Fact]
    public void InferMultipleTypeParameters()
    {
        // Given a function with multiple type parameters
        var source = @"
def convert[T, U](value: T, converter: (T) -> U) -> U:
    return converter(value)

def str_to_int(s: str) -> int:
    return int(s)

def main():
    result = convert(""42"", str_to_int)  # Should infer T=str, U=int
";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Then there should be no errors
        typeChecker.Errors.Should().BeEmpty("inference should deduce T=str from arg 1, U=int from function return type");

        // And the result should be typed as int
        var resultType = GetCallResultType(module, semanticInfo, "result");
        resultType.Should().Be(SemanticType.Int);
    }

    #endregion

    #region Inference Failure Cases

    [Fact]
    public void InferenceFailsWithNoArguments()
    {
        // Given a generic function with no parameters that provide type info
        var source = @"
def create_empty[T]() -> list[T]:
    return []

def main():
    result = create_empty()  # Cannot infer T - should error
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Then there should be an error about missing type arguments
        typeChecker.Errors.Should().NotBeEmpty();
        var errorMessage = typeChecker.Errors[0].Message;
        // The error could mention: cannot be inferred, explicit, type argument, required
        (errorMessage.Contains("infer") ||
         errorMessage.Contains("explicit") ||
         errorMessage.Contains("type argument") ||
         errorMessage.Contains("required")).Should().BeTrue(
            $"Error message should mention inference failure, but was: {errorMessage}");
    }

    [Fact]
    public void InferenceFailsWithConflictingTypes()
    {
        // Given arguments that would require different types for the same type parameter
        var source = @"
def pair[T](a: T, b: T) -> tuple[T, T]:
    return (a, b)

def main():
    result = pair(1, ""hello"")  # T cannot be both int and str
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Then there should be an error about conflicting types
        typeChecker.Errors.Should().NotBeEmpty();
        var errorMessage = typeChecker.Errors[0].Message;
        // The error could mention: conflict, mismatch, Cannot assign, different types
        (errorMessage.Contains("conflict") ||
         errorMessage.Contains("mismatch") ||
         errorMessage.Contains("Cannot assign") ||
         errorMessage.Contains("different")).Should().BeTrue(
            $"Error message should mention type conflict, but was: {errorMessage}");
    }

    [Fact]
    public void InferenceWithObjectArgument()
    {
        // Given arguments where the type is object
        var source = @"
def identity[T](value: T) -> T:
    return value

def main():
    x: object = 42
    result = identity(x)  # T would be object, not int
";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // This should compile (inferring T=object)
        typeChecker.Errors.Should().BeEmpty();
    }

    #endregion

    #region Inference with Constraints

    [Fact]
    public void InferenceWithConstraints_Satisfied()
    {
        // Given a function with constrained type parameter and satisfying argument
        var source = @"
interface IComparable[T]:
    def __lt__(self, other: T) -> bool: ...

def find_max[T: IComparable[T]](a: T, b: T) -> T:
    if a < b:
        return b
    return a

def main():
    # int supports comparison
    result = find_max(1, 2)  # Should infer T=int, constraint satisfied
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Then there should be no errors (int supports comparison)
        typeChecker.Errors.Should().BeEmpty("int satisfies IComparable constraint");
    }

    #endregion

    #region Explicit Type Arguments Still Work

    [Fact]
    public void ExplicitTypeArguments_StillWork()
    {
        // Given explicit type arguments (the existing functionality)
        var source = @"
def identity[T](value: T) -> T:
    return value

def main():
    result = identity[int](42)  # Explicit type argument
";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Then there should be no errors
        typeChecker.Errors.Should().BeEmpty();

        // And the result should be typed as int
        var resultType = GetCallResultType(module, semanticInfo, "result");
        resultType.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void ExplicitTypeArguments_OverrideInference()
    {
        // Given explicit type arguments that differ from what would be inferred
        var source = @"
def identity[T](value: T) -> T:
    return value

def main():
    # Explicitly specify int even though we pass a literal that could be inferred
    result = identity[int](42)
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Then there should be no errors
        typeChecker.Errors.Should().BeEmpty();
    }

    #endregion
}
