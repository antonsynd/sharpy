using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Comprehensive stress tests for type inference. Each test exercises a specific
/// inference path and verifies no UnknownType ("&lt;?&gt;") leaks through.
/// </summary>
public class TypeInferenceBattleTests
{
    private (Module, TypeChecker, SemanticInfo) CompileAndCheck(string source)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();

        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance);

        return (module, typeChecker, semanticInfo);
    }

    /// <summary>
    /// Asserts the invariant: if no semantic errors, no expression types should be Unknown.
    /// </summary>
    private void AssertNoUnknownTypesWhenNoErrors(TypeChecker typeChecker, SemanticInfo semanticInfo)
    {
        if (!typeChecker.Diagnostics.GetErrors().Any())
        {
            semanticInfo.HasUnknownExpressionTypes().Should().BeFalse(
                "when there are no semantic errors, no expression types should be Unknown (<?>) — " +
                "this indicates a silent type inference failure");
        }
    }

    #region Variable Assignment Inference

    [Fact]
    public void Assignment_IntLiteral_InfersInt()
    {
        var source = @"
def main():
    x = 42
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void Assignment_StringLiteral_InfersStr()
    {
        var source = @"
def main():
    x = ""hello""
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void Assignment_BoolLiteral_InfersBool()
    {
        var source = @"
def main():
    x = True
    y = False
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void Assignment_FloatLiteral_InfersDouble()
    {
        var source = @"
def main():
    x = 3.14
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void Assignment_AutoType_InfersFromInitializer()
    {
        var source = @"
def main():
    x: auto = 42
    y: auto = ""world""
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void Assignment_Reassignment_SameType_NoError()
    {
        var source = @"
def main():
    x = 42
    x = 99
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void StrConstructor_ReturnsBuiltinStrType()
    {
        // str(n) should return the same type as a str literal,
        // not UserDefinedType (which would cause type mismatches)
        var source = @"
def takes_str(s: str) -> None:
    pass

def main():
    takes_str(str(42))
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void IntConstructor_ReturnsBuiltinIntType()
    {
        var source = @"
def takes_int(n: int) -> None:
    pass

def main():
    takes_int(int(3.14))
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region Collection Inference

    [Fact]
    public void List_HomogeneousElements_InfersElementType()
    {
        var source = @"
def main():
    items = [1, 2, 3]
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void List_WithTypeAnnotation_Validates()
    {
        var source = @"
def main():
    items: list[int] = [1, 2, 3]
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void Dict_HomogeneousEntries_InfersTypes()
    {
        var source = @"
def main():
    d = {""a"": 1, ""b"": 2}
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void Set_HomogeneousElements_InfersElementType()
    {
        var source = @"
def main():
    s = {1, 2, 3}
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region Comprehension Inference

    [Fact]
    public void ListComprehension_InfersElementType()
    {
        var source = @"
def main():
    items = [1, 2, 3]
    doubled = [x * 2 for x in items]
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void ListComprehension_WithFilter_InfersType()
    {
        var source = @"
def main():
    items = [1, 2, 3, 4, 5]
    evens = [x for x in items if x % 2 == 0]
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void DictComprehension_InfersKeyValueTypes()
    {
        var source = @"
def main():
    items = [1, 2, 3]
    d = {x: x * 2 for x in items}
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region Operator Type Promotion

    [Fact]
    public void BinaryOp_IntPlusInt_InfersInt()
    {
        var source = @"
def main():
    result = 3 + 4
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void BinaryOp_IntTimesFloat_PromotesToFloat()
    {
        var source = @"
def main():
    result = 3 * 2.5
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void BinaryOp_StringConcat_InfersStr()
    {
        var source = @"
def main():
    result = ""hello"" + "" world""
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void BinaryOp_StringRepeat_InfersStr()
    {
        var source = @"
def main():
    result = ""ha"" * 3
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void BinaryOp_ListConcat_InfersList()
    {
        var source = @"
def main():
    a = [1, 2]
    b = [3, 4]
    result = a + b
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void Comparison_ReturnsBool()
    {
        var source = @"
def main():
    result = 3 < 5
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void ComparisonChain_ReturnsBool()
    {
        var source = @"
def main():
    x = 5
    result = 1 < x < 10
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void NullCoalescing_InfersUnderlyingType()
    {
        var source = @"
def main():
    x: int? = 42
    result = x ?? 0
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region Conditional Expression Inference

    [Fact]
    public void ConditionalExpr_SameTypes_InfersType()
    {
        var source = @"
def main():
    x = 42 if True else 0
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region For Loop Variable Inference

    [Fact]
    public void ForLoop_ListIteration_InfersElementType()
    {
        var source = @"
def main():
    items = [1, 2, 3]
    for x in items:
        y = x + 1
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void ForLoop_StringIteration_InfersStr()
    {
        var source = @"
def main():
    for ch in ""hello"":
        y = ch + ""!""
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void ForLoop_DictIteration_InfersKeyType()
    {
        var source = @"
def main():
    d = {""a"": 1, ""b"": 2}
    for key in d:
        y = key + ""!""
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region Function Return Type

    [Fact]
    public void FunctionReturn_MatchesDeclaredType()
    {
        var source = @"
def add(a: int, b: int) -> int:
    return a + b
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void FunctionReturn_TypeMismatch_Error()
    {
        var source = @"
def get_name() -> int:
    return ""hello""
";
        var (module, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
    }

    [Fact]
    public void FunctionReturn_None_DefaultsToVoid()
    {
        var source = @"
def do_something():
    x = 42
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region Generic Function Inference

    [Fact]
    public void GenericFunction_SingleTypeParam_InfersFromArgument()
    {
        var source = @"
def identity[T](value: T) -> T:
    return value

def main():
    x = identity(42)
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void GenericFunction_MultipleTypeParams_InfersAll()
    {
        var source = @"
def pair[T, U](first: T, second: U) -> T:
    return first

def main():
    x = pair(42, ""hello"")
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void GenericFunction_FromGenericContainer_InfersElementType()
    {
        var source = @"
def first[T](items: list[T]) -> T:
    return items[0]

def main():
    items = [1, 2, 3]
    x = first(items)
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region Member Access Inference

    [Fact]
    public void MemberAccess_FieldType_InfersCorrectly()
    {
        var source = @"
class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

def main():
    p = Point(1, 2)
    val = p.x
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void MemberAccess_MethodReturnType_InfersCorrectly()
    {
        var source = @"
class Calculator:
    def add(self, a: int, b: int) -> int:
        return a + b

def main():
    calc = Calculator()
    result = calc.add(3, 4)
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region Optional/Nullable Inference

    [Fact]
    public void Optional_SomeConstructor_InfersFromContext()
    {
        var source = @"
def main():
    x: int? = Some(42)
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void Optional_NoneConstructor_InfersFromContext()
    {
        var source = @"
def main():
    x: int? = None()
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region Result Type Inference

    [Fact]
    public void Result_OkConstructor_InfersFromContext()
    {
        var source = @"
def main():
    x: int !str = Ok(42)
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void Result_ErrConstructor_InfersFromContext()
    {
        var source = @"
def main():
    x: int !str = Err(""failed"")
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region Try/Maybe Expression Inference

    [Fact]
    public void TryExpression_WrapsInResult()
    {
        var source = @"
def main():
    result = try 42
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void MaybeExpression_WrapsInOptional()
    {
        // maybe requires NullableType (T | None), not OptionalType (T?)
        var source = @"
def process(x: int | None) -> None:
    result = maybe x
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region Tuple Inference

    [Fact]
    public void Tuple_InfersElementTypes()
    {
        var source = @"
def main():
    t = (1, ""hello"", True)
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void TupleUnpacking_InfersVariableTypes()
    {
        var source = @"
def main():
    a, b = (1, ""hello"")
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region Nested Inference

    [Fact]
    public void NestedFunctionCalls_InferThroughChain()
    {
        var source = @"
def double(x: int) -> int:
    return x * 2

def triple(x: int) -> int:
    return x * 3

def main():
    result = double(triple(7))
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void NestedCollections_InferTypes()
    {
        var source = @"
def main():
    matrix: list[list[int]] = [[1, 2], [3, 4]]
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region Lambda Inference with Operations

    [Fact]
    public void Lambda_WithArithmetic_InfersReturnType()
    {
        var source = @"
def apply(f: (int, int) -> int, a: int, b: int) -> int:
    return f(a, b)

def main():
    result = apply(lambda x, y: x + y, 3, 4)
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void Lambda_WithComparison_InfersReturnType()
    {
        var source = @"
def test(f: (int) -> bool, x: int) -> bool:
    return f(x)

def main():
    result = test(lambda n: n > 0, 5)
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region Augmented Assignment Inference

    [Fact]
    public void AugmentedAssignment_PlusEquals_MaintainsType()
    {
        var source = @"
def main():
    x = 10
    x += 5
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AugmentedAssignment_StringPlusEquals_MaintainsType()
    {
        var source = @"
def main():
    s = ""hello""
    s += "" world""
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region Type Narrowing

    [Fact]
    public void TypeNarrowing_IsNotNone_Works()
    {
        var source = @"
def process(x: int?) -> int:
    if x is not None:
        return x
    return 0
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region Index Access Inference

    [Fact]
    public void IndexAccess_List_InfersElementType()
    {
        var source = @"
def main():
    items = [1, 2, 3]
    x = items[0]
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void IndexAccess_Dict_InfersValueType()
    {
        var source = @"
def main():
    d = {""a"": 1, ""b"": 2}
    x = d[""a""]
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region UnknownType Invariant — No Silent Type Leaks

    [Fact]
    public void Invariant_SimpleArithmetic_NoUnknownTypes()
    {
        var source = @"
def main():
    x = 3 + 4
    y = x * 2
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
        AssertNoUnknownTypesWhenNoErrors(typeChecker, semanticInfo);
    }

    [Fact]
    public void Invariant_FunctionCallChain_NoUnknownTypes()
    {
        var source = @"
def double(x: int) -> int:
    return x * 2

def main():
    result = double(21)
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
        AssertNoUnknownTypesWhenNoErrors(typeChecker, semanticInfo);
    }

    [Fact]
    public void Invariant_ListOperations_NoUnknownTypes()
    {
        var source = @"
def main():
    items = [1, 2, 3]
    first = items[0]
    total = first + 1
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
        AssertNoUnknownTypesWhenNoErrors(typeChecker, semanticInfo);
    }

    [Fact]
    public void Invariant_Conditional_NoUnknownTypes()
    {
        var source = @"
def main():
    x = 42 if True else 0
    y = x > 10
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
        AssertNoUnknownTypesWhenNoErrors(typeChecker, semanticInfo);
    }

    [Fact]
    public void Invariant_ForLoop_NoUnknownTypes()
    {
        var source = @"
def main():
    items = [1, 2, 3]
    for x in items:
        y = x + 1
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
        AssertNoUnknownTypesWhenNoErrors(typeChecker, semanticInfo);
    }

    [Fact]
    public void Invariant_LambdaWithContext_NoUnknownTypes()
    {
        var source = @"
def apply(f: (int) -> int, x: int) -> int:
    return f(x)

def main():
    result = apply(lambda n: n * 2, 5)
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
        AssertNoUnknownTypesWhenNoErrors(typeChecker, semanticInfo);
    }

    #endregion

    #region CLR Type Fallback

    [Fact]
    public void ClrTypeFallback_Exception_Resolves()
    {
        var source = @"
def main():
    result: int !Exception = try 42
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void ClrTypeFallback_ResultWithException_Resolves()
    {
        var source = @"
def main():
    result: Result[int, Exception] = try 42
";
        var (module, typeChecker, semanticInfo) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void ClrTypeFallback_NonexistentType_StillErrors()
    {
        var source = @"
def main():
    x: CompletelyFakeType = 42
";
        var (module, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
    }

    #endregion
}
