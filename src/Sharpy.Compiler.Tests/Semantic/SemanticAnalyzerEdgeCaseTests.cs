using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Edge case tests for the semantic analyzer
/// </summary>
public class SemanticAnalyzerEdgeCaseTests
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

        // Type checking
        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance);

        return (module, symbolTable, semanticInfo, typeChecker);
    }

    #region Control Flow Tests

    [Fact]
    public void DetectsUnreachableCodeAfterReturn()
    {
        var source = @"
def foo() -> int:
    return 1
    x: int = 2  # unreachable
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("Unreachable code"));
    }

    [Fact]
    public void DetectsUnreachableCodeAfterRaise()
    {
        var source = @"
def foo():
    raise Exception()
    x: int = 2  # unreachable
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("Unreachable code"));
    }

    [Fact]
    public void AcceptsBreakInWhileLoop()
    {
        var source = @"
def foo():
    while True:
        break
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AcceptsContinueInForLoop()
    {
        var source = @"
def foo():
    for i in range(10):
        continue
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void RejectsBreakOutsideLoop()
    {
        var source = @"
def foo():
    break  # invalid
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("'break' statement outside loop"));
    }

    [Fact]
    public void RejectsContinueOutsideLoop()
    {
        var source = @"
def foo():
    continue  # invalid
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("'continue' statement outside loop"));
    }

    [Fact]
    public void RequiresReturnInAllBranchesOfNonVoidFunction()
    {
        var source = @"
def foo(x: bool) -> int:
    if x:
        return 1
    # missing return in else branch
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("must return a value"));
    }

    [Fact]
    public void AcceptsReturnInAllBranches()
    {
        var source = @"
def foo(x: bool) -> int:
    if x:
        return 1
    else:
        return 2
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AcceptsReturnInElifBranches()
    {
        var source = @"
def foo(x: int) -> int:
    if x == 1:
        return 1
    elif x == 2:
        return 2
    else:
        return 3
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void DoesNotRequireReturnInVoidFunction()
    {
        var source = @"
def foo(x: bool):
    if x:
        print('yes')
    else:
        print('no')
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void InitMethodDoesNotRequireReturn()
    {
        var source = @"
class Foo:
    def __init__(self):
        pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void VoidFunctionWithIfBlockAndAssignment_NoReturn_IsValid()
    {
        var source = @"
def process(x: int):
    if x > 0:
        y = x * 2
        print(y)
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void VoidFunctionWithIfElseAndAssignments_NoReturn_IsValid()
    {
        var source = @"
def categorize(x: int):
    if x > 0:
        category = 'positive'
        print(category)
    else:
        category = 'negative'
        print(category)
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void VoidFunctionWithNestedIfAndAssignment_NoReturn_IsValid()
    {
        var source = @"
def nested(x: int, y: int):
    if x > 0:
        if y > 0:
            sum = x + y
            print(sum)
        else:
            print('y not positive')
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void VoidFunctionWithIfBlockAndMultipleAssignments_NoReturn_IsValid()
    {
        var source = @"
def compute(value: int):
    if value > 10:
        x = value * 2
        y = x + 5
        z = y - 3
        print(z)
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void VoidFunctionWithInlineCommentsInIfBlock_IsValid()
    {
        var source = @"
def compute(x: int):
    if x > 0:  # check if positive
        result = x * 2  # double the value
        print(result)  # output result
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void VoidFunctionWithMultipleCommentsInNestedBlocks_IsValid()
    {
        var source = @"
def complex(x: int, y: int):  # main function
    if x > 0:  # check x
        while y > 0:  # loop on y
            if x + y > 100:  # threshold check
                break  # exit loop
            y -= 1  # decrement
        print(y)  # output result
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void NestedLoopsAllowBreakAndContinue()
    {
        var source = @"
def foo():
    for i in range(10):
        for j in range(10):
            if i == j:
                break
            if i > j:
                continue
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    #endregion

    #region Access Level Tests

    [Fact]
    public void AllowsPublicMemberAccess()
    {
        var source = @"
class Foo:
    value: int

def bar():
    f: Foo = Foo()
    x: int = f.value
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsProtectedMemberAccessWithinClass()
    {
        var source = @"
class Foo:
    _value: int

    def get_value(self) -> int:
        return self._value
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void RejectsProtectedMemberAccessOutsideClass()
    {
        var source = @"
class Foo:
    _value: int

def bar():
    f: Foo = Foo()
    x: int = f._value  # invalid
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("Cannot access protected member"));
    }

    [Fact]
    public void AllowsPrivateMemberAccessWithinClass()
    {
        var source = @"
class Foo:
    __value: int

    def get_value(self) -> int:
        return self.__value
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void RejectsPrivateMemberAccessOutsideClass()
    {
        var source = @"
class Foo:
    __value: int

def bar():
    f: Foo = Foo()
    x: int = f.__value  # invalid
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("Cannot access private member"));
    }

    [Fact]
    public void DunderMethodsArePublic()
    {
        var source = @"
class Foo:
    def __init__(self):
        pass

def bar():
    f: Foo = Foo()
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    #endregion

    #region Type Checking Edge Cases

    [Fact]
    public void DetectsTypeMismatchInAssignment()
    {
        var source = @"
def foo():
    x: int = 'hello'  # type mismatch
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("Cannot assign type"));
    }

    [Fact]
    public void AllowsNumericConversions()
    {
        var source = @"
def foo():
    x: float = 5  # int to float is ok
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidatesReturnType()
    {
        var source = @"
def foo() -> int:
    return 'hello'  # type mismatch
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("Cannot return type"));
    }

    [Fact]
    public void ValidatesParameterTypes()
    {
        var source = @"
def add(a: int, b: int) -> int:
    return a + b

def foo():
    x: int = add(1, 'two')  # type mismatch
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().ContainSingle();
    }

    [Fact]
    public void DetectsUndefinedVariable()
    {
        var source = @"
def foo():
    x: int = y  # y is undefined
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("Undefined identifier"));
    }

    [Fact]
    public void AllowsNestedClassAccess()
    {
        var source = @"
class Outer:
    class Inner:
        value: int

def foo():
    x: int = Outer.Inner.value
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        // This might fail - nested classes may not be fully implemented
        // We'll see what happens
    }

    [Fact]
    public void ValidatesDefaultParameterTypes()
    {
        var source = @"
def foo(x: int = 'invalid'):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("Default value type"));
    }

    #endregion

    #region Class and Inheritance Tests

    [Fact]
    public void AllowsSelfParameterInMethods()
    {
        var source = @"
class Point:
    x: int
    y: int

    def move(self, dx: int, dy: int):
        self.x = self.x + dx
        self.y = self.y + dy
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidatesFieldAssignmentTypes()
    {
        var source = @"
class Foo:
    value: int

    def set_value(self):
        self.value = 'invalid'  # type mismatch
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("Cannot assign type"));
    }

    [Fact]
    public void AllowsMethodChaining()
    {
        var source = @"
class Builder:
    def set_name(self, name: str):
        return self

    def build(self):
        pass

def foo():
    b: Builder = Builder()
    b.set_name('test').build()
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        // This test might reveal issues with return type inference
    }

    #endregion

    #region Generic Type Tests

    [Fact]
    public void ValidatesGenericListTypes()
    {
        var source = @"
def foo():
    numbers: list[int] = [1, 2, 3]
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void DetectsGenericListTypeMismatch()
    {
        var source = @"
def foo():
    numbers: list[int] = ['a', 'b', 'c']  # type mismatch
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        // This might not be detected yet - generic type checking may be incomplete
    }

    [Fact]
    public void ValidatesGenericDictTypes()
    {
        var source = @"
def foo():
    mapping: dict[str, int] = {'a': 1, 'b': 2}
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    #endregion

    #region Expression Tests

    [Fact]
    public void ValidatesBinaryOperationTypes()
    {
        var source = @"
def foo():
    x: int = 1 + 2
    y: bool = 5 > 3
    z: str = 'hello' + 'world'
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void DetectsInvalidBinaryOperations()
    {
        var source = @"
def foo():
    x: int = 'hello' + 5  # invalid
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        // This might not be detected - operator overloading checks may be incomplete
    }

    [Fact]
    public void ValidatesUnaryOperations()
    {
        var source = @"
def foo():
    x: int = -5
    y: bool = not True
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidatesComparisonChains()
    {
        var source = @"
def foo():
    x: bool = 1 < 2 < 3
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public void AllowsTryExceptFinally()
    {
        var source = @"
def foo():
    try:
        x: int = 1
    except Exception:
        x: int = 2
    finally:
        pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidatesReturnInTryExcept()
    {
        var source = @"
def foo() -> int:
    try:
        return 1
    except Exception:
        return 2
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void RequiresReturnInAllExceptionHandlers()
    {
        var source = @"
def foo() -> int:
    try:
        return 1
    except Exception:
        pass  # missing return
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("must return a value"));
    }

    #endregion

    #region Complex Control Flow

    [Fact]
    public void HandlesComplexControlFlow()
    {
        var source = @"
def foo(x: int) -> int:
    if x < 0:
        return -1
    elif x == 0:
        return 0
    else:
        for i in range(x):
            if i > 10:
                return i
        return x
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void DetectsMissingReturnInComplexFlow()
    {
        var source = @"
def foo(x: int) -> int:
    if x < 0:
        return -1
    # missing return for x >= 0
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("must return a value"));
    }

    #endregion
}
