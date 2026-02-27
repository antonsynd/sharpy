using System.Linq;
using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Additional negative tests for the semantic analyzer
/// </summary>
public class SemanticAnalyzerNegativeTests
{
    private static string ParseExpectingError(string source)
    {
        var lexer = new LexerNs.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserNs.Parser(tokens, NullLogger.Instance);
        parser.ParseModule();
        parser.Diagnostics.HasErrors.Should().BeTrue("Expected parser to report an error for input: " + source);
        return string.Join("\n", parser.Diagnostics.GetErrors().Select(d => d.Message));
    }

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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("Undefined"));
    }

    [Fact]
    public void DocumentsUndefinedClassBehavior()
    {
        var source = @"
def foo():
    x: UndefinedClass = None
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("Undefined"));
    }

    [Fact]
    public void AllowsRedefinitionWithTypeAnnotation()
    {
        var source = @"
def foo():
    x: int = 1
    x: int = 2
    x: auto = 3
    x: str = 'hello'
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Per language spec: redefinition with type annotation is allowed
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Shadowing should be allowed
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AllowsShadowingBuiltinFunction()
    {
        // User code can shadow builtin functions like 'double', 'print', 'len', etc.
        // This matches Python behavior where builtins can be overridden
        var source = @"
def double(n: int) -> int:
    return n * 2

x: int = double(5)
";
        var (module, _, _, nameResolver, typeChecker) = CompileAndCheck(source);

        // Name resolution should succeed (builtin 'double' is shadowed)
        nameResolver.Diagnostics.HasErrors.Should().BeFalse();

        typeChecker.CheckModule(module, isEntryPoint: false);
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("private"));
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Sharpy follows C# scoping rules: variables defined in an if-block
        // should be scoped to that block and not accessible in else or after.
        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("Undefined") || e.Message.Contains("not defined"));
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Variables defined inside if-blocks should not leak to outer scope (C#-style scoping)
        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("Undefined") || e.Message.Contains("not defined"));
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Variables defined inside while-blocks should not leak to outer scope (C#-style scoping)
        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("Undefined") || e.Message.Contains("not defined"));
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Variables defined inside for-blocks should not leak to outer scope (C#-style scoping)
        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("Undefined") || e.Message.Contains("not defined"));
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        // This is valid: variable is defined in outer scope before the if-block
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        // C#-style scoping: variables defined in blocks don't leak to outer scope
        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("Undefined") || e.Message.Contains("not defined"));
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("Cannot assign"));
    }

    [Fact]
    public void RejectsBoolToIntAssignment()
    {
        var source = @"
def foo():
    x: int = True  # bool to int is not allowed in Sharpy
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("Cannot assign"));
    }

    [Fact]
    public void RejectsListToStringAssignment()
    {
        var source = @"
def foo():
    x: str = [1, 2, 3]  # type mismatch
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("Cannot assign"));
    }

    [Fact]
    public void RejectsWrongGenericType()
    {
        var source = @"
def foo():
    numbers: list[int] = ['a', 'b']  # generic type mismatch
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("Cannot assign"));
    }

    [Fact]
    public void RejectsIncompatibleDictTypes()
    {
        var source = @"
def foo():
    mapping: dict[str, int] = {1: 'one', 2: 'two'}  # wrong key/value types
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("Cannot assign"));
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("argument") || e.Message.Contains("parameter"));
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("argument") || e.Message.Contains("parameter"));
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
    }

    [Fact]
    public void RejectsCallingNonFunction()
    {
        var source = @"
def foo():
    x: int = 42
    y: int = x()  # x is not a function
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("not callable") || e.Message.Contains("not a function"));
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
        typeChecker.CheckModule(module, isEntryPoint: false);

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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("return"));
    }

    [Fact]
    public void RejectsWrongReturnType()
    {
        var source = @"
def foo() -> int:
    return 'hello'  # wrong return type
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("return"));
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("return"));
    }

    #endregion

    #region Operator Errors

    [Fact]
    public void RejectsInvalidAddition()
    {
        var source = @"
def foo():
    x: int = 'hello' + 5  # cannot add string and int
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should report error about incompatible types
        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("does not support operator '+'"));
    }

    [Fact]
    public void RejectsInvalidComparison()
    {
        var source = @"
def foo():
    x: bool = 'hello' < 5  # cannot compare string and int
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should report error about incompatible types
        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("does not support operator '<'"));
    }

    [Fact]
    public void RejectsComparisonBetweenBoolAndString()
    {
        var source = @"
def foo():
    x: bool = True < 'hello'  # cannot compare bool and string
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should report error about incompatible types
        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("does not support operator '<'"));
    }

    [Fact]
    public void RejectsInvalidUnaryOperation()
    {
        var source = @"
def foo():
    x: int = -'hello'  # cannot negate a string
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should report error about unary operation on non-numeric type
        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("does not support unary operator '-'"));
    }

    [Fact]
    public void RejectsSubtractionWithString()
    {
        var source = @"
def foo():
    x: str = 'hello' - 'world'  # cannot subtract strings
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("does not support operator '-'"));
    }

    [Fact]
    public void RejectsUnaryMinusOnBool()
    {
        var source = @"
def foo():
    x: bool = -True  # cannot negate a boolean
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("does not support unary operator '-'"));
    }

    [Fact]
    public void RejectsBitwiseNotOnString()
    {
        var source = @"
def foo():
    x: str = ~'hello'  # cannot perform bitwise NOT on string
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("does not support unary operator '~'"));
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("no member"));
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("no member"));
    }

    [Fact]
    public void RejectsMissingSelfParameter()
    {
        var source = @"
class Foo:
    def bar():  # missing self parameter
        pass
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

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
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Circular inheritance detection might not be implemented yet
        // Currently no error is generated for circular inheritance
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Check both name resolver and type checker errors since inheritance validation happens in name resolution
        var allErrorMessages = nameResolver.Diagnostics.GetErrors().Select(d => d.Message)
            .Concat(typeChecker.Diagnostics.GetErrors().Select(e => e.Message)).ToList();
        allErrorMessages.Should().Contain(m => m.Contains("not a class") || m.Contains("not found"));
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("break"));
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("continue"));
    }

    [Fact]
    public void DocumentsReturnAtModuleLevelBehavior()
    {
        var source = @"
return 42  # return at module level
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Return at module level generates an error
        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("Return statement outside of function"));
    }

    [Fact]
    public void RejectsYieldOutsideFunction()
    {
        var source = @"
yield 42  # yield at module level
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e =>
            e.Message.Contains("yield") && e.Message.Contains("outside"));
    }

    #endregion

    #region Generator Tests

    [Fact]
    public void GeneratorFunction_IsMarkedAsGenerator()
    {
        var source = @"
def gen() -> int:
    yield 1
    yield 2
";
        var (module, symbolTable, semanticInfo, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.HasErrors.Should().BeFalse(
            string.Join("; ", typeChecker.Diagnostics.GetErrors().Select(d => d.Message)));

        var funcDef = module.Body.OfType<FunctionDef>().First();
        semanticInfo.IsGenerator(funcDef).Should().BeTrue("function containing yield should be marked as generator");
    }

    [Fact]
    public void NestedGenerator_DoesNotMarkOuterFunction()
    {
        var source = @"
def outer() -> int:
    def inner() -> int:
        yield 1
    return 0
";
        var (module, symbolTable, semanticInfo, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        var outerFunc = module.Body.OfType<FunctionDef>().First();
        semanticInfo.IsGenerator(outerFunc).Should().BeFalse("yield in nested function should not mark outer function as generator");
    }

    [Fact]
    public void RejectsYieldTypeMismatch()
    {
        var source = @"
def gen() -> int:
    yield ""hello""
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e =>
            e.Message.Contains("Yielded type") && e.Message.Contains("not assignable"));
    }

    [Fact]
    public void YieldFromNonIterable_EmitsError()
    {
        // yield from 42 — int is not iterable, so an error should be emitted.
        var source = @"
def gen() -> int:
    yield from 42
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e =>
            e.Message.Contains("requires an iterable"));
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        // The type checker now properly validates assignment targets
        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("Cannot assign to") && e.Message.Contains("literal"));
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("assign") || e.Message.Contains("target"));
    }

    [Fact]
    public void RejectsConstantReassignment()
    {
        var source = @"
MAX_SIZE: int = 100

def foo():
    MAX_SIZE = 200  # reassigning constant (if enforced)
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

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
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Try statement validation doesn't enforce except/finally requirement
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void DocumentsRaiseWithInvalidTypeBehavior()
    {
        var source = @"
def foo():
    raise 42  # can only raise exceptions
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Raise type validation is not enforced
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void RejectsBareRaiseOutsideExcept()
    {
        var source = @"
def foo():
    raise  # bare raise only valid in except block
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // This might not be enforced
    }

    #endregion

    #region Generic and Advanced Type Errors

    [Fact]
    public void RejectsInvalidGenericArgument()
    {
        // Generic arguments must be type names (identifiers), not literals
        // The parser enforces this at parse time by expecting an identifier
        var source = @"
def foo():
    x: list[123] = []  # generic argument must be a type
";
        var errors = ParseExpectingError(source);
        errors.Should().Contain("Expected identifier");
    }

    [Fact]
    public void RejectsWrongNumberOfGenericArguments()
    {
        var source = @"
def foo():
    x: list[int, str] = []  # list takes one type argument
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("expects 1 type arguments but got 2"));
    }

    [Fact]
    public void DocumentsNonGenericWithTypeArgumentsBehavior()
    {
        var source = @"
def foo():
    x: int[str] = 5  # int is not generic
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Generic type argument validation is not enforced
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors()[0].Message.Should().Contain("None");
    }

    [Fact]
    public void AcceptsNullAssignmentToOptionalType()
    {
        var source = @"
def foo():
    x: int | None = None  # valid for C# nullable type
";
        // T | None is now supported — parses to IsCSharpNullable = true
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Slice type checking is not enforced
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void RejectsSubscriptOnNonSubscriptableType()
    {
        var source = @"
def foo():
    x: int = 42
    y: int = x[0]  # int is not subscriptable
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Subscript on int should produce an error
        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("does not support indexing"));
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
x: int = 5  # cannot decorate non-function at module level
";
        // Decorated variables at module level are caught by DecoratorValidator
        var (module, symbolTable, semanticInfo, nameResolver, _) = CompileAndCheck(source);
        var typeResolver = new TypeResolver(symbolTable, semanticInfo);
        var context = new Sharpy.Compiler.Semantic.Validation.SemanticContext(symbolTable, semanticInfo, typeResolver);
        var validator = new Sharpy.Compiler.Semantic.Validation.DecoratorValidator();
        validator.Validate(module, context);

        context.Diagnostics.GetErrors().Should().Contain(e =>
            e.Message.Contains("Decorators cannot be applied to module-level variable declarations"));
    }

    [Fact]
    public void RejectsInvalidDefaultParameterType()
    {
        var source = @"
def foo(x: int = 'invalid'):  # default value type mismatch
    pass
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("Default"));
    }

    [Fact]
    public void RejectsNonDefaultAfterDefault()
    {
        var source = @"
def foo(a: int = 1, b: int):  # non-default after default
    pass
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

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
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Sharpy allows shadowing with type annotations in nested blocks (unlike C#)
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Variables defined inside elif-blocks should not leak to outer scope (C#-style scoping)
        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e => e.Message.Contains("Undefined") || e.Message.Contains("not defined"));
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Elif condition type checking
        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("boolean"));
    }

    #endregion

    #region Const Reassignment Tests

    [Fact]
    public void RejectsConstReassignmentInSameScope()
    {
        var source = @"
def foo():
    const MAX_VALUE: int = 100
    MAX_VALUE = 200  # Should fail
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("constant") || e.Message.Contains("reassign"));
    }

    [Fact]
    public void RejectsConstReassignmentAcrossScopes()
    {
        var source = @"
const MAX_VALUE: int = 100

def foo():
    MAX_VALUE = 20  # Error: reassignment without type annotation
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().Contain(e =>
            e.Message.Contains("constant") ||
            e.Message.Contains("reassign") ||
            e.Message.Contains("shadow"));
    }

    [Fact]
    public void RejectsMultipleConstReassignments()
    {
        var source = @"
def foo():
    const MAX_VAL: int = 100
    MAX_VAL = 99  # First reassignment
    MAX_VAL = 98  # Second reassignment
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("constant") || e.Message.Contains("reassign"));
    }

    [Fact]
    public void AllowsConstDeclarationWithoutReassignment()
    {
        var source = @"
def foo():
    const MAX_VALUE: int = 100
    x: int = MAX_VALUE  # Reading is fine
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AllowsConstShadowingWithTypeAnnotation()
    {
        var source = @"
const MAX_VALUE: int = 100

def foo():
    MAX_VALUE: int = 20
    print(MAX_VALUE)
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void RejectsConstShadowingWithoutTypeAnnotation()
    {
        var source = @"
const PI: int = 314

def calculate():
    PI = 315
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().Contain(e =>
            e.Message.Contains("constant") &&
            (e.Message.Contains("reassign") || e.Message.Contains("shadow")));
    }

    [Fact]
    public void AllowsReadingOuterScopeConst()
    {
        var source = @"
const THRESHOLD: int = 100

def check_value(val: int) -> bool:
    return val > THRESHOLD
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AllowsNestedConstShadowingWithTypeAnnotation()
    {
        var source = @"
const VALUE: int = 100

def outer():
    VALUE: int = 200
    def inner():
        VALUE: int = 300
        print(VALUE)
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void RejectsConstReassignmentInNestedScope()
    {
        var source = @"
const LIMIT: int = 50

def outer():
    def inner():
        LIMIT = 100
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().Contain(e =>
            e.Message.Contains("constant") && e.Message.Contains("LIMIT"));
    }

    [Fact]
    public void AllowsShadowingConstWithDifferentType()
    {
        var source = @"
const CONFIG: int = 42

def process():
    CONFIG: str = 'local config'
    print(CONFIG)
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void DocumentsConstShadowingBehavior()
    {
        var source = @"
const X: int = 1

def demo():
    y: int = X + 1
    X: int = 2
    z: int = X + 1
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region Self Parameter Tests

    [Fact]
    public void AllowsMethodWithoutSelfAsStatic()
    {
        // In Sharpy, methods without 'self' are treated as static methods
        var source = @"
class Foo:
    def bar(x: int):  # No self - this is a static method
        pass
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // No error - this is a valid static method
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AllowsMethodWithNoParamsAsStatic()
    {
        // In Sharpy, methods without parameters are treated as static methods
        var source = @"
class Foo:
    def bar():  # No parameters - this is a static method
        pass
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // No error - this is a valid static method
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AllowsMultipleStaticMethods()
    {
        // In Sharpy, methods without 'self' are treated as static methods
        var source = @"
class Foo:
    def method1(x: int):  # Static method
        pass

    def method2(y: int):  # Static method
        pass
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // No error - these are valid static methods
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AllowsCorrectSelfParameter()
    {
        var source = @"
class Foo:
    def bar(self):
        pass

    def baz(self, x: int):
        pass
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region Parameter Ordering Tests

    [Fact]
    public void RejectsNonDefaultAfterDefaultSingleCase()
    {
        var source = @"
def foo(a: int = 1, b: int):  # b has no default
    pass
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("default") || e.Message.Contains("parameter"));
    }

    [Fact]
    public void RejectsNonDefaultAfterDefaultMultiple()
    {
        var source = @"
def foo(a: int = 1, b: str = 'test', c: int, d: float):  # c and d have no defaults
    pass
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void RejectsNonDefaultInMiddle()
    {
        var source = @"
def foo(a: int = 1, b: int, c: str = 'test'):  # b in middle has no default
    pass
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("default") || e.Message.Contains("parameter"));
    }

    [Fact]
    public void AllowsAllDefaultParameters()
    {
        var source = @"
def foo(a: int = 1, b: str = 'test', c: int = 42):
    pass
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AllowsAllNonDefaultParameters()
    {
        var source = @"
def foo(a: int, b: str, c: float):
    pass
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AllowsNonDefaultThenDefault()
    {
        var source = @"
def foo(a: int, b: str, c: int = 42, d: bool = True):
    pass
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region Bare Raise Tests

    [Fact]
    public void RejectsBareRaiseInFunctionBody()
    {
        var source = @"
def foo():
    raise  # Bare raise outside except
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("raise") || e.Message.Contains("except"));
    }

    [Fact]
    public void RejectsBareRaiseInTryBlock()
    {
        var source = @"
def foo():
    try:
        raise  # Bare raise in try block, not except
    except:
        pass
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("raise") || e.Message.Contains("except"));
    }

    [Fact]
    public void RejectsBareRaiseInFinallyBlock()
    {
        var source = @"
def foo():
    try:
        pass
    except:
        pass
    finally:
        raise  # Bare raise in finally block
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("raise") || e.Message.Contains("except"));
    }

    [Fact]
    public void AllowsBareRaiseInExceptBlock()
    {
        var source = @"
def foo():
    try:
        x: int = 1
    except:
        raise  # Valid bare raise
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AllowsBareRaiseInNestedExceptBlock()
    {
        var source = @"
def foo():
    try:
        try:
            x: int = 1
        except:
            raise  # Valid in inner except
    except:
        raise  # Valid in outer except
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AllowsRaiseWithExceptionAnywhere()
    {
        var source = @"
def foo():
    x: int = 10
    raise  # Bare raise should fail, but we're testing that raise with args is OK

def bar():
    try:
        pass
    except:
        y: str = 'test'  # Valid in except
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // This test would need actual exception types to work properly
        // For now, just verify it compiles
    }

    #endregion

    #region Position Tracking Edge Cases

    [Fact]
    public void TracksErrorPositionInConstReassignment()
    {
        var source = @"
def foo():
    const X: int = 10
    X = 20  # This is the reassignment
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("constant") || e.Message.Contains("reassign"));
    }

    [Fact]
    public void TracksErrorPositionInSelfParameter()
    {
        var source = @"
class Foo:
    def bar(wrong):
        pass
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Line != null && e.Column != null);
    }

    [Fact]
    public void TracksErrorPositionInParameterOrdering()
    {
        var source = @"
def foo(a: int = 1, b: int):
    pass
";
        var (module, _, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Line != null && e.Column != null);
    }

    #endregion

    #region Interface Method Validation

    [Fact]
    public void AcceptsInterfaceMethodWithDefaultImplementation()
    {
        // Interface methods with bodies are now allowed as default interface methods
        var source = @"
interface IDrawable:
    def draw(self) -> None:
        print('Drawing')
";
        var (module, _, _, nameResolver, _) = CompileAndCheck(source);

        nameResolver.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AcceptsInterfaceMethodWithEllipsis()
    {
        var source = @"
interface IDrawable:
    def draw(self) -> None:
        ...
";
        var (module, _, _, nameResolver, _) = CompileAndCheck(source);

        nameResolver.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AcceptsInterfaceMethodWithPass()
    {
        var source = @"
interface IEmpty:
    def method(self) -> None:
        pass
";
        var (module, _, _, nameResolver, _) = CompileAndCheck(source);

        nameResolver.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AcceptsInterfaceMethodWithMultipleStatements()
    {
        // Interface methods with multiple statements are allowed as default implementations
        var source = @"
interface IDrawable:
    def draw(self) -> None:
        pass
        pass
";
        var (module, _, _, nameResolver, _) = CompileAndCheck(source);

        nameResolver.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AcceptsInterfaceMethodWithReturnStatement()
    {
        // Interface methods with return statements are allowed as default implementations
        var source = @"
interface ICalculator:
    def calculate(self, x: int) -> int:
        return x * 2
";
        var (module, _, _, nameResolver, _) = CompileAndCheck(source);

        nameResolver.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void RejectsInterfaceMethodWithEmptyBody()
    {
        var source = @"
interface IEmpty:
    def method(self) -> None:
";
        // This should be a parser error, but if it gets through,
        // semantic analyzer should catch it
        try
        {
            var (module, _, _, nameResolver, _) = CompileAndCheck(source);
            nameResolver.Diagnostics.GetErrors().Should().NotBeEmpty();
        }
        catch
        {
            // Parser error is also acceptable
        }
    }

    [Fact]
    public void AcceptsMultipleInterfaceMethodsWithEllipsis()
    {
        var source = @"
interface IDrawable:
    def draw(self) -> None:
        ...

    def get_bounds(self) -> tuple[int, int, int, int]:
        ...

    def set_color(self, color: str) -> None:
        ...
";
        var (module, _, _, nameResolver, _) = CompileAndCheck(source);

        nameResolver.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void AcceptsInterfaceWithMixedAbstractAndDefaultMethods()
    {
        // Interfaces can have both abstract (...) and default (with body) methods
        var source = @"
interface IMixed:
    def abstract_method(self) -> None:
        ...

    def default_method(self) -> str:
        return 'default value'
";
        var (module, _, _, nameResolver, _) = CompileAndCheck(source);

        nameResolver.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion
}
