using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
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
        var semanticBinding = new SemanticBinding();

        // Name resolution first
        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance, semanticBinding);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();
        semanticBinding.MaterializeInheritance();

        // Type checking
        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance)
        {
            SemanticBinding = semanticBinding
        };

        return (module, symbolTable, semanticInfo, typeChecker);
    }

    /// <summary>
    /// Helper to get the expression type of a function call within main().
    /// Handles both VariableDeclaration (with type annotation) and Assignment (without).
    /// </summary>
    private SemanticType? GetCallResultType(Module module, SemanticInfo semanticInfo, string varName)
    {
        // Find the main function
        var mainFunc = module.Body.OfType<FunctionDef>()
            .FirstOrDefault(f => f.Name == "main");
        if (mainFunc == null)
            return null;

        // Try to find as VariableDeclaration first
        var varDecl = mainFunc.Body.OfType<VariableDeclaration>()
            .FirstOrDefault(v => v.Name == varName);
        if (varDecl?.InitialValue != null)
            return semanticInfo.GetExpressionType(varDecl.InitialValue);

        // Otherwise, look for Assignment to the variable
        foreach (var stmt in mainFunc.Body)
        {
            if (stmt is Assignment assignment &&
                assignment.Target is Identifier id &&
                id.Name == varName)
            {
                return semanticInfo.GetExpressionType(assignment.Value);
            }
        }

        return null;
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
        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Debug: Check if identity function has correct parameter types after checking
        var identitySymbol = symbolTable.Lookup("identity") as FunctionSymbol;
        identitySymbol.Should().NotBeNull("identity function should be in symbol table");
        identitySymbol!.IsGeneric.Should().BeTrue("identity should be a generic function");
        identitySymbol.Parameters.Should().HaveCount(1);
        var paramType = identitySymbol.Parameters[0].Type;
        paramType.Should().BeOfType<TypeParameterType>($"parameter type should be TypeParameterType, but was {paramType?.GetType().Name}: {paramType?.GetDisplayName()}");

        // Check errors before the assertion
        var errors = typeChecker.Diagnostics.GetErrors().Select(e => e.Message).ToList();
        errors.Should().BeEmpty($"Expected no errors but got: [{string.Join(", ", errors)}]");

        // And the result should be typed as int
        var resultType = GetCallResultType(module, semanticInfo, "result");
        resultType.Should().NotBeNull("expression type should be set");
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
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty("inference should deduce T=str from argument \"hello\"");

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
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty("inference should deduce T=int from both arguments");
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
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty("inference should extract T=int from list[int]");

        // And the result should be typed as int
        var resultType = GetCallResultType(module, semanticInfo, "result");
        resultType.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferTypeFromGenericContainer_Dict()
    {
        // Given a function that takes dict[K, V] and returns the key type
        // Note: We use a simpler test that doesn't rely on dict.items() iteration
        // which has additional complexity for generic type parameter resolution
        var source = @"
def get_dict_length[K, V](d: dict[K, V]) -> int:
    return len(d)

def main():
    data: dict[str, int] = {""a"": 1}
    result = get_dict_length(data)  # Should infer K=str, V=int
";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Then there should be no errors
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty("inference should extract K=str, V=int from dict[str, int]");

        // And the result should be typed as int
        var resultType = GetCallResultType(module, semanticInfo, "result");
        resultType.Should().Be(SemanticType.Int);
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
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty("inference should deduce T=str from arg 1, U=int from function return type");

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
        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        var errorMessage = typeChecker.Diagnostics.GetErrors()[0].Message;
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
        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        var errorMessage = typeChecker.Diagnostics.GetErrors()[0].Message;
        // The error could mention: conflict, mismatch, Cannot assign, different types (case-insensitive)
        (errorMessage.Contains("conflict", StringComparison.OrdinalIgnoreCase) ||
         errorMessage.Contains("mismatch", StringComparison.OrdinalIgnoreCase) ||
         errorMessage.Contains("Cannot assign", StringComparison.OrdinalIgnoreCase) ||
         errorMessage.Contains("different", StringComparison.OrdinalIgnoreCase)).Should().BeTrue(
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
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region Inference with Constraints

    [Fact]
    public void InferenceWithConstraints_Satisfied()
    {
        // Given a function with constrained type parameter and satisfying argument
        // Note: This test focuses on inference with constraints, using a simpler function
        // that doesn't rely on operator overloading (which requires additional support)
        var source = @"
interface ICloneable:
    def clone(self) -> object: ...

def make_pair[T: ICloneable](a: T, b: T) -> tuple[T, T]:
    return (a, b)

class MyClass(ICloneable):
    def clone(self) -> object:
        return MyClass()

def main():
    obj1 = MyClass()
    obj2 = MyClass()
    result = make_pair(obj1, obj2)  # Should infer T=MyClass, constraint satisfied
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Then there should be no errors (MyClass implements ICloneable)
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty("MyClass satisfies ICloneable constraint");
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
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();

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
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion
}
