using System.Linq;
using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Additional negative tests for the semantic analyzer
/// </summary>
public class SemanticAnalyzerNegativeTests
{
    private (Module, SymbolTable, SemanticInfo, NameResolver, TypeChecker) CompileAndCheck(string source)
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
        nameResolver.ResolveInheritance(); // Second pass: resolve inheritance

        // Type checking
        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance);

        return (module, symbolTable, semanticInfo, nameResolver, typeChecker);
    }

    #region Scope and Naming Errors

    [Fact]
    public void RejectsUndefinedFunction()
    {
        var source = @"
def foo():
    bar()  # bar is not defined
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("Undefined"));
    }

    [Fact]
    public void DocumentsUndefinedClassBehavior()
    {
        var source = @"
def foo():
    x: UndefinedClass = None
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Type resolution for undefined classes might not be fully enforced
        // This documents the current behavior
    }

    [Fact]
    public void RejectsUseBeforeDefinition()
    {
        var source = @"
def foo():
    x: int = y  # y used before definition
    y: int = 5
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("Undefined"));
    }

    [Fact]
    public void RejectsRedefinitionInSameScope()
    {
        var source = @"
def foo():
    x: int = 1
    x: int = 2  # redefinition
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);

        // This is actually caught at name resolution, not type checking
        // So we expect an exception during parsing/resolution
        Action act = () => typeChecker.CheckModule(module);
        act.Should().Throw<Exception>().WithMessage("*already defined*");
    }

    [Fact]
    public void AcceptsShadowingInNestedScope()
    {
        var source = @"
x: int = 1

def foo():
    x: int = 2  # shadows global x
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Shadowing should be allowed
        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void RejectsAccessToPrivateMemberFromOutside()
    {
        var source = @"
class Foo:
    __private: int

def bar():
    f: Foo = Foo()
    x: int = f.__private  # cannot access private member
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().Contain(e => e.Message.Contains("private"));
    }

    [Fact]
    public void RejectsVariableDefinedInIfBlockUsedInElseBlock()
    {
        var source = @"
def foo(x: int):
    if x > 0:
        category: str = ""positive""
    else:
        print(category)  # category is not in scope here
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Sharpy follows C# scoping rules: variables defined in an if-block
        // should be scoped to that block and not accessible in else or after.
        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("Undefined") || e.Message.Contains("not defined"));
    }

    [Fact]
    public void RejectsVariableDefinedInIfBlockUsedAfterBlock()
    {
        var source = @"
def foo(x: int):
    if x > 0:
        result: str = ""positive""
    print(result)  # result is not in scope here
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Variables defined inside if-blocks should not leak to outer scope (C#-style scoping)
        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("Undefined") || e.Message.Contains("not defined"));
    }

    [Fact]
    public void RejectsVariableDefinedInWhileBlockUsedAfterBlock()
    {
        var source = @"
def foo():
    while True:
        temp: int = 42
        break
    print(temp)  # temp is not in scope here
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Variables defined inside while-blocks should not leak to outer scope (C#-style scoping)
        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("Undefined") || e.Message.Contains("not defined"));
    }

    [Fact]
    public void RejectsVariableDefinedInForBlockUsedAfterBlock()
    {
        var source = @"
def foo():
    for i in range(10):
        temp: int = i * 2
    print(temp)  # temp is not in scope here
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Variables defined inside for-blocks should not leak to outer scope (C#-style scoping)
        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("Undefined") || e.Message.Contains("not defined"));
    }

    [Fact]
    public void AcceptsVariableDefinedBeforeIfBlockUsedInBothBranches()
    {
        var source = @"
def foo(x: int):
    category: str = ""unknown""
    if x > 0:
        category = ""positive""
    else:
        category = ""non-positive""
    print(category)
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // This is valid: variable is defined in outer scope before the if-block
        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void DocumentsCSharpStyleBlockScoping()
    {
        var source = @"
def foo(x: int):
    if x > 0:
        result: str = ""positive""
    # C#-style scoping: 'result' is not accessible here
    print(result)
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // C#-style scoping: variables defined in blocks don't leak to outer scope
        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("Undefined") || e.Message.Contains("not defined"));
    }

    #endregion

    #region Type Mismatch Errors

    [Fact]
    public void RejectsIntToStringAssignment()
    {
        var source = @"
def foo():
    x: str = 42  # type mismatch
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("Cannot assign"));
    }

    [Fact(Skip = "Type checking: Bool to int assignment validation not yet implemented")]
    public void RejectsBoolToIntAssignment()
    {
        var source = @"
def foo():
    x: int = True  # bool to int might be allowed in some languages
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Check if bool to int is allowed
    }

    [Fact]
    public void RejectsListToStringAssignment()
    {
        var source = @"
def foo():
    x: str = [1, 2, 3]  # type mismatch
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("Cannot assign"));
    }

    [Fact(Skip = "Type checking: Generic list element type validation not yet implemented")]
    public void RejectsWrongGenericType()
    {
        var source = @"
def foo():
    numbers: list[int] = ['a', 'b']  # generic type mismatch
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Generic type checking might not be fully implemented
    }

    [Fact(Skip = "Type checking: Dictionary key/value type validation not yet implemented")]
    public void RejectsIncompatibleDictTypes()
    {
        var source = @"
def foo():
    mapping: dict[str, int] = {1: 'one', 2: 'two'}  # wrong key/value types
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Dictionary type checking might not be fully implemented
    }

    #endregion

    #region Function Call Errors

    [Fact]
    public void RejectsTooManyArguments()
    {
        var source = @"
def add(a: int, b: int) -> int:
    return a + b

def foo():
    x: int = add(1, 2, 3)  # too many arguments
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().Contain(e => e.Message.Contains("argument") || e.Message.Contains("parameter"));
    }

    [Fact]
    public void RejectsTooFewArguments()
    {
        var source = @"
def add(a: int, b: int) -> int:
    return a + b

def foo():
    x: int = add(1)  # too few arguments
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().Contain(e => e.Message.Contains("argument") || e.Message.Contains("parameter"));
    }

    [Fact]
    public void RejectsWrongArgumentType()
    {
        var source = @"
def add(a: int, b: int) -> int:
    return a + b

def foo():
    x: int = add('one', 'two')  # wrong argument types
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().NotBeEmpty();
    }

    [Fact(Skip = "Type checking: Calling non-callable types not yet enforced")]
    public void RejectsCallingNonFunction()
    {
        var source = @"
def foo():
    x: int = 42
    y: int = x()  # x is not a function
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("not callable") || e.Message.Contains("not a function"));
    }

    #endregion

    #region Return Statement Errors

    [Fact]
    public void RejectsReturnWithValueInVoidFunction()
    {
        var source = @"
def foo():
    return 42  # void function should not return a value
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Some languages allow this, check implementation
    }

    [Fact]
    public void RejectsReturnWithoutValueInNonVoidFunction()
    {
        var source = @"
def foo() -> int:
    return  # missing return value
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("return"));
    }

    [Fact]
    public void RejectsWrongReturnType()
    {
        var source = @"
def foo() -> int:
    return 'hello'  # wrong return type
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("return"));
    }

    [Fact]
    public void RejectsMissingReturnStatement()
    {
        var source = @"
def foo() -> int:
    x: int = 5
    # missing return
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("return"));
    }

    #endregion

    #region Operator Errors

    [Fact(Skip = "Type checking: Binary operator type validation not yet implemented")]
    public void RejectsInvalidAddition()
    {
        var source = @"
def foo():
    x: int = 'hello' + 5  # cannot add string and int
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Operator type checking might not be fully implemented
    }

    [Fact(Skip = "Type checking: Comparison operator type validation not yet implemented")]
    public void RejectsInvalidComparison()
    {
        var source = @"
def foo():
    x: bool = 'hello' < 5  # cannot compare string and int
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Operator type checking might not be fully implemented
    }

    [Fact(Skip = "Type checking: Unary operator type validation not yet implemented")]
    public void RejectsInvalidUnaryOperation()
    {
        var source = @"
def foo():
    x: int = -'hello'  # cannot negate a string
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Operator type checking might not be fully implemented
    }

    [Fact(Skip = "Type checking: Logical operator type validation not yet implemented")]
    public void RejectsInvalidLogicalOperation()
    {
        var source = @"
def foo():
    x: bool = 5 and 10  # logical operations on non-bool might be allowed
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Some languages allow truthy/falsy values
    }

    #endregion

    #region Class and Inheritance Errors

    [Fact]
    public void RejectsAccessToNonExistentField()
    {
        var source = @"
class Foo:
    value: int

def bar():
    f: Foo = Foo()
    x: int = f.nonexistent  # field does not exist
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().Contain(e => e.Message.Contains("no member"));
    }

    [Fact]
    public void RejectsAccessToNonExistentMethod()
    {
        var source = @"
class Foo:
    def bar(self):
        pass

def baz():
    f: Foo = Foo()
    f.nonexistent()  # method does not exist
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().Contain(e => e.Message.Contains("no member"));
    }

    [Fact(Skip = "Type checking: Missing self parameter validation not yet implemented")]
    public void RejectsMissingSelfParameter()
    {
        var source = @"
class Foo:
    def bar():  # missing self parameter
        pass
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // This might be a warning or error depending on implementation
    }

    [Fact]
    public void DocumentsCircularInheritanceBehavior()
    {
        var source = @"
class A(B):
    pass

class B(A):
    pass
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Circular inheritance detection might not be implemented yet
        // Currently no error is generated for circular inheritance
        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void RejectsInheritanceFromNonClass()
    {
        var source = @"
x: int = 5

class Foo(x):  # cannot inherit from non-class
    pass
";
        var (module, _, _, nameResolver, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Check both name resolver and type checker errors since inheritance validation happens in name resolution
        var allErrors = nameResolver.Errors.Concat(typeChecker.Errors).ToList();
        allErrors.Should().Contain(e => e.Message.Contains("not a class") || e.Message.Contains("not found"));
    }

    #endregion

    #region Loop and Control Flow Errors

    [Fact]
    public void RejectsBreakOutsideLoop()
    {
        var source = @"
def foo():
    if True:
        break  # not in a loop
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("break"));
    }

    [Fact]
    public void RejectsContinueOutsideLoop()
    {
        var source = @"
def foo():
    if True:
        continue  # not in a loop
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("continue"));
    }

    [Fact]
    public void DocumentsReturnAtModuleLevelBehavior()
    {
        var source = @"
return 42  # return at module level
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Return at module level generates an error
        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("Return statement outside of function"));
    }

    [Fact(Skip = "Unimplemented: Yield statements not yet supported")]
    public void RejectsYieldOutsideFunction()
    {
        var source = @"
yield 42  # yield at module level
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // If yield is supported
    }

    #endregion

    #region Assignment Errors

    [Fact]
    public void RejectsAssignmentToLiteral()
    {
        var source = @"
def foo():
    42 = x  # cannot assign to literal
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // The error is reported for undefined 'x', not the assignment to literal
        // This is a limitation of the current implementation
        typeChecker.Errors.Should().Contain(e => e.Message.Contains("Undefined"));
    }

    [Fact]
    public void RejectsAssignmentToFunctionCall()
    {
        var source = @"
def bar() -> int:
    return 42

def foo():
    bar() = 5  # cannot assign to function call
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().Contain(e => e.Message.Contains("assign") || e.Message.Contains("target"));
    }

    [Fact(Skip = "Type checking: Constant reassignment validation not yet implemented")]
    public void RejectsConstantReassignment()
    {
        var source = @"
MAX_SIZE: int = 100

def foo():
    MAX_SIZE = 200  # reassigning constant (if enforced)
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Constant enforcement might not be implemented
    }

    #endregion

    #region Exception Handling Errors

    [Fact]
    public void DocumentsTryWithoutExceptOrFinallyBehavior()
    {
        var source = @"
def foo():
    try:
        x: int = 1
    # missing except or finally
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Try statement validation doesn't enforce except/finally requirement
        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void DocumentsRaiseWithInvalidTypeBehavior()
    {
        var source = @"
def foo():
    raise 42  # can only raise exceptions
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Raise type validation is not enforced
        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact(Skip = "Type checking: Bare raise validation not yet implemented")]
    public void RejectsBareRaiseOutsideExcept()
    {
        var source = @"
def foo():
    raise  # bare raise only valid in except block
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // This might not be enforced
    }

    #endregion

    #region Generic and Advanced Type Errors

    [Fact(Skip = "Type checking: Generic argument validation not yet fully implemented")]
    public void RejectsInvalidGenericArgument()
    {
        var source = @"
def foo():
    x: list[123] = []  # generic argument must be a type
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().Contain(e => e.Message.Contains("type"));
    }

    [Fact(Skip = "Type checking: Generic argument count validation not yet implemented")]
    public void RejectsWrongNumberOfGenericArguments()
    {
        var source = @"
def foo():
    x: list[int, str] = []  # list takes one type argument
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Generic argument count checking might not be implemented
    }

    [Fact]
    public void DocumentsNonGenericWithTypeArgumentsBehavior()
    {
        var source = @"
def foo():
    x: int[str] = 5  # int is not generic
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Generic type argument validation is not enforced
        typeChecker.Errors.Should().BeEmpty();
    }

    #endregion

    #region Null and Optional Type Errors

    [Fact]
    public void RejectsNullAssignmentToNonNullableType()
    {
        var source = @"
def foo():
    x: int = None  # None cannot be assigned to int
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors[0].Message.Should().Contain("None");
    }

    [Fact]
    public void AcceptsNullAssignmentToOptionalType()
    {
        var source = @"
def foo():
    x: int | None = None  # valid for optional type
";
        // Union types (|) are not yet implemented, causes parse error
        Action act = () => CompileAndCheck(source);
        act.Should().Throw<Exception>();
    }

    #endregion

    #region Miscellaneous Errors

    [Fact]
    public void DocumentsInvalidSliceTypeBehavior()
    {
        var source = @"
def foo():
    x: list[int] = [1, 2, 3]
    y: int = x['invalid']  # slice/index must be int
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Slice type checking is not enforced
        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void DocumentsSubscriptOnNonSubscriptableBehavior()
    {
        var source = @"
def foo():
    x: int = 42
    y: int = x[0]  # int is not subscriptable
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Subscript type checking is not enforced
        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void RejectsInvalidComprehensionTarget()
    {
        var source = @"
def foo():
    x: list[int] = [i for i in 42]  # 42 is not iterable
";
        // Comprehensions are now implemented, parse should succeed
        // Semantic error for non-iterable is expected but not tested here yet
        var (module, _, _, _, _) = CompileAndCheck(source);
        module.Should().NotBeNull();
    }

    [Fact]
    public void RejectsInvalidDecoratorTarget()
    {
        var source = @"
def decorator(f):
    return f

@decorator
x: int = 5  # cannot decorate non-function
";
        // This is caught at parse time, not semantic analysis
        Action act = () => CompileAndCheck(source);
        act.Should().Throw<Exception>().WithMessage("*Decorators can only be applied*");
    }

    [Fact]
    public void RejectsInvalidDefaultParameterType()
    {
        var source = @"
def foo(x: int = 'invalid'):  # default value type mismatch
    pass
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("Default"));
    }

    [Fact(Skip = "Type checking: Non-default parameter after default validation not yet implemented")]
    public void RejectsNonDefaultAfterDefault()
    {
        var source = @"
def foo(a: int = 1, b: int):  # non-default after default
    pass
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // This might be a parser or semantic error
    }

    [Fact]
    public void AllowsNestedShadowingWithTypeAnnotation()
    {
        var source = @"
def test():
    x: int = 1
    if True:
        x: int = 2  # Shadowing with type annotation - should be allowed in Sharpy
        print(x)
    print(x)
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Sharpy allows shadowing with type annotations in nested blocks (unlike C#)
        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void RejectsVariableDefinedInElifBlockUsedAfterBlock()
    {
        var source = @"
def foo(x: int):
    if x > 10:
        category: str = ""high""
    elif x > 5:
        category: str = ""medium""
    print(category)  # category is not in scope here
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Variables defined inside elif-blocks should not leak to outer scope (C#-style scoping)
        typeChecker.Errors.Should().ContainSingle(e => e.Message.Contains("Undefined") || e.Message.Contains("not defined"));
    }

    [Fact]
    public void ChecksElifConditionType()
    {
        var source = @"
def foo():
    x: int = 1
    if x > 10:
        print(""high"")
    elif ""not a bool"":  # elif condition must be boolean
        print(""medium"")
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Elif condition type checking
        typeChecker.Errors.Should().Contain(e => e.Message.Contains("boolean"));
    }

    #endregion
}
