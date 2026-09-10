using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
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

    /// <summary>
    /// #1811: when a generic candidate's inference conflicts at an overloaded callee,
    /// the candidate is inapplicable (not an ICE). SPY0354 names the reason.
    /// </summary>
    [Fact]
    public void OverloadedCallee_InferenceConflict_InapplicableNotICE()
    {
        var source = @"
def f[T](xs: list[T], y: T) -> None:
    print(y)

def f[T](x: T) -> None:
    print(x)

def main():
    xs: list[int] = [0]
    f(xs, ""a"")
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        var errors = typeChecker.Diagnostics.GetErrors();
        errors.Should().Contain(e => e.Code == DiagnosticCodes.Semantic.NoMatchingOverload,
            "inference conflict makes the two-arg candidate inapplicable → SPY0354");
        errors.Should().NotContain(e => e.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            "the conflict is decided by Sharpy, not leaked to Roslyn as CS0411");
    }

    /// <summary>
    /// #1811: a keyword argument that drives inference into a generic overload —
    /// inference succeeds from the binding that includes the keyword, the candidate wins.
    /// </summary>
    [Fact]
    public void OverloadedCallee_KeywordDrivenInference_Succeeds()
    {
        var source = @"
def f[T](xs: list[T], x: T) -> T:
    return x

def f(xs: list[int], x: str) -> str:
    return x

def main():
    xs: list[int] = [0]
    v: int = f(xs, x=1)
    print(v + 1)
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty(
            "the generic candidate wins: T=int from both xs and x=1; the non-generic's x: str refuses 1");
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

    #region Wrapper cells: T through ?, | None, !E, list[T]?, (T) -> U (#1797)

    /// <summary>
    /// The wrapper cells (plan-499995 Phase 3): a type parameter reached through a wrapper —
    /// <c>T?</c>, <c>T | None</c>, <c>T!E</c>, <c>list[T]?</c>, <c>(T) -&gt; U</c> — binds from
    /// the argument's PAYLOAD (<c>Some(5)</c> binds <c>T</c> to int; <c>Ok(5)</c> too; a plain
    /// value binds through <c>T | None</c>), and an argument that carries no payload —
    /// <c>None()</c>, a bare <c>None</c> — binds NOTHING and is refused by name (SPY0237, with the
    /// explicit-syntax steer) rather than binding <c>T</c> to its own absence. A written type
    /// argument closes the slot and the payload-less argument is then fine.
    /// </summary>
    [Theory]
    [InlineData("def f[T](x: T?) -> T:\n    return x.unwrap()\n", "f(Some(5))", "int32", "T? × Some")]
    [InlineData("def f[T](x: T?) -> T:\n    return x.unwrap()\n", "f[int](None())", "int32", "T? × None(), closed by the written argument")]
    [InlineData("def f[T](x: T?) -> T:\n    return x.unwrap()\n", "f(None())", null, "T? × None(): nothing to bind")]
    [InlineData("def g[T](x: T | None) -> T:\n    return x\n", "g(5)", "int32", "T | None × plain")]
    [InlineData("def g[T](x: T | None) -> T:\n    return x\n", "g[str](None)", "str", "T | None × None, closed by the written argument")]
    [InlineData("def g[T](x: T | None) -> T:\n    return x\n", "g(None)", null, "T | None × None: nothing to bind")]
    [InlineData("def r[T](x: T!str) -> T:\n    return x.unwrap()\n", "r(Ok(5))", "int32", "T!E × Ok")]
    [InlineData("def r[T](x: T!str) -> T:\n    return x.unwrap()\n", "r[int](Err(\"e\"))", "int32", "T!E × Err, closed by the written argument")]
    [InlineData("def h[T](x: list[T]?) -> T:\n    return x.unwrap()[0]\n", "h(Some([1, 2]))", "int32", "list[T]? × Some(list)")]
    [InlineData("def h[T](x: list[T]?) -> T:\n    return x.unwrap()[0]\n", "h(None())", null, "list[T]? × None(): nothing to bind")]
    [InlineData("def a[T, U](x: T, f: (T) -> U) -> U:\n    return f(x)\n", "a(1, lambda v: str(v))", "str", "(T) -> U × plain + lambda")]
    public void WrapperCell_BindsFromThePayload_OrRefusesByName(string decl, string call, string? expectedResult, string cell)
    {
        var source = decl + "\ndef main():\n    result = " + call + "\n";
        var (module, _, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        var errors = typeChecker.Diagnostics.GetErrors().Select(e => $"{e.Code}:{e.Message}").ToList();
        if (expectedResult == null)
        {
            errors.Should().Contain(e => e.StartsWith(DiagnosticCodes.Semantic.CannotInferGenericType, StringComparison.Ordinal),
                $"{cell} must be refused by name (SPY0237); got [{string.Join(", ", errors)}]");
            errors.Should().Contain(e => e.Contains("explicit syntax", StringComparison.Ordinal),
                $"{cell} must steer to the explicit syntax; got [{string.Join(", ", errors)}]");
            return;
        }

        errors.Should().BeEmpty($"{cell} binds from the payload; got [{string.Join(", ", errors)}]");
        var resultType = GetCallResultType(module, semanticInfo, "result");
        resultType.Should().NotBeNull($"{cell}: the call's type is recorded");
        resultType!.GetDisplayName().Should().Be(expectedResult, $"{cell}: the payload's type is the binding");
    }

    #endregion
}
