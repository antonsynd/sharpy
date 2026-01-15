using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Semantic;

public class DefaultParameterValidatorTests
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

    #region Valid Default Parameters

    [Fact]
    public void AllowsIntegerDefault()
    {
        var source = @"
def foo(x: int = 42):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsFloatDefault()
    {
        var source = @"
def foo(x: float = 3.14):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsStringDefault()
    {
        var source = @"
def foo(name: str = ""default""):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsBooleanDefault()
    {
        var source = @"
def foo(flag: bool = True):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsNoneDefaultForNullableType()
    {
        var source = @"
def foo(x: int? = None):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsNoneDefaultForNullableStringType()
    {
        var source = @"
def bar(x: str? = None):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsNegativeIntegerDefault()
    {
        var source = @"
def foo(x: int = -1):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsTupleDefault()
    {
        var source = @"
def foo(point: tuple[int, int] = (0, 0)):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsMultipleDefaultParameters()
    {
        var source = @"
def foo(x: int = 1, y: int = 2, z: int = 3):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsMixedRequiredAndDefaultParameters()
    {
        var source = @"
def foo(required: str, optional: int = 42):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    #endregion

    #region Mutable Defaults (Not Allowed)

    [Fact]
    public void RejectsEmptyListDefault()
    {
        var source = @"
def foo(items: list[int] = []):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors.Should().Contain(e => e.Message.Contains("Mutable default"));
    }

    [Fact]
    public void RejectsListWithElementsDefault()
    {
        var source = @"
def foo(items: list[int] = [1, 2, 3]):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors.Should().Contain(e => e.Message.Contains("Mutable default"));
    }

    [Fact]
    public void RejectsEmptyDictDefault()
    {
        var source = @"
def foo(data: dict[str, int] = {}):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors.Should().Contain(e => e.Message.Contains("Mutable default"));
    }

    [Fact]
    public void RejectsDictWithEntriesDefault()
    {
        var source = @"
def foo(data: dict[str, int] = {""a"": 1}):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors.Should().Contain(e => e.Message.Contains("Mutable default"));
    }

    [Fact]
    public void RejectsSetLiteralDefault()
    {
        var source = @"
def foo(items: set[int] = {1, 2, 3}):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors.Should().Contain(e => e.Message.Contains("Mutable default"));
    }

    [Fact]
    public void RejectsSetConstructorDefault()
    {
        var source = @"
def foo(items: set[int] = set()):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors.Should().Contain(e => e.Message.Contains("Mutable default"));
    }

    [Fact]
    public void RejectsListConstructorDefault()
    {
        var source = @"
def foo(items: list[int] = list()):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors.Should().Contain(e => e.Message.Contains("Mutable default"));
    }

    [Fact]
    public void RejectsDictConstructorDefault()
    {
        var source = @"
def foo(data: dict[str, int] = dict()):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors.Should().Contain(e => e.Message.Contains("Mutable default"));
    }

    #endregion

    #region None Default for Non-Nullable Types (Not Allowed)

    [Fact]
    public void RejectsNoneDefaultForNonNullableInt()
    {
        var source = @"
def foo(x: int = None):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors.Should().Contain(e => e.Message.Contains("None") && e.Message.Contains("non-nullable"));
    }

    [Fact]
    public void RejectsNoneDefaultForNonNullableString()
    {
        var source = @"
def foo(name: str = None):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors.Should().Contain(e => e.Message.Contains("None") && e.Message.Contains("non-nullable"));
    }

    [Fact]
    public void RejectsNoneDefaultForNonNullableBool()
    {
        var source = @"
def foo(flag: bool = None):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors.Should().Contain(e => e.Message.Contains("None") && e.Message.Contains("non-nullable"));
    }

    [Fact]
    public void RejectsNoneDefaultForNonNullableFloat()
    {
        var source = @"
def foo(value: float = None):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors.Should().Contain(e => e.Message.Contains("None") && e.Message.Contains("non-nullable"));
    }

    #endregion

    #region Enum Default Parameters

    [Fact]
    public void AllowsEnumMemberDefault()
    {
        var source = @"
enum Color:
    RED = 0
    GREEN = 1
    BLUE = 2

def paint(color: Color = Color.RED):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsEnumMemberDefaultInMethod()
    {
        var source = @"
enum Mode:
    NORMAL = 0
    DEBUG = 1
    VERBOSE = 2

class Logger:
    def log(self, mode: Mode = Mode.NORMAL):
        pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsMultipleEnumDefaults()
    {
        var source = @"
enum Level:
    LOW = 0
    MEDIUM = 1
    HIGH = 2

enum Status:
    ACTIVE = 0
    INACTIVE = 1

def configure(level: Level = Level.MEDIUM, status: Status = Status.ACTIVE):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    #endregion

    #region Const Reference Default Parameters

    [Fact]
    public void AllowsConstReferenceDefault()
    {
        var source = @"
const DEFAULT_TIMEOUT: float = 30.0

def connect(timeout: float = DEFAULT_TIMEOUT):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsIntConstReferenceDefault()
    {
        var source = @"
const MAX_RETRIES: int = 3

def fetch(retries: int = MAX_RETRIES):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsStringConstReferenceDefault()
    {
        var source = @"
const DEFAULT_NAME: str = ""Anonymous""

def greet(name: str = DEFAULT_NAME):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsBoolConstReferenceDefault()
    {
        var source = @"
const DEBUG_MODE: bool = False

def run(debug: bool = DEBUG_MODE):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsMultipleConstDefaults()
    {
        var source = @"
const DEFAULT_HOST: str = ""localhost""
const DEFAULT_PORT: int = 8080
const DEFAULT_TIMEOUT: float = 30.0

def connect(host: str = DEFAULT_HOST, port: int = DEFAULT_PORT, timeout: float = DEFAULT_TIMEOUT):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    #endregion

    #region Non-Constant Defaults (Not Allowed)

    [Fact]
    public void RejectsVariableReferenceDefault()
    {
        var source = @"
DEFAULT_VALUE: int = 42
def foo(x: int = DEFAULT_VALUE):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors.Should().Contain(e => e.Message.Contains("compile-time constant"));
    }

    [Fact]
    public void RejectsFunctionCallDefault()
    {
        var source = @"
def get_default() -> int:
    return 42

def foo(x: int = get_default()):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors.Should().Contain(e => e.Message.Contains("compile-time constant"));
    }

    #endregion

    #region Class Method Default Parameters

    [Fact]
    public void AllowsValidDefaultInMethod()
    {
        var source = @"
class MyClass:
    def method(self, x: int = 10):
        pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void RejectsMutableDefaultInMethod()
    {
        var source = @"
class MyClass:
    def method(self, items: list[int] = []):
        pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors.Should().Contain(e => e.Message.Contains("Mutable default"));
    }

    [Fact]
    public void RejectsNoneDefaultForNonNullableInMethod()
    {
        var source = @"
class MyClass:
    def method(self, x: int = None):
        pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors.Should().Contain(e => e.Message.Contains("None") && e.Message.Contains("non-nullable"));
    }

    [Fact]
    public void AllowsNoneDefaultForNullableInMethod()
    {
        var source = @"
class MyClass:
    def method(self, x: int? = None):
        pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void AllowsParenthesizedConstant()
    {
        var source = @"
def foo(x: int = (42)):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsBinaryOperationOnConstants()
    {
        var source = @"
def foo(x: int = 1 + 2):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsUnaryOperationOnConstant()
    {
        var source = @"
def foo(x: bool = not False):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void AllowsNestedTupleConstant()
    {
        var source = @"
def foo(point: tuple[tuple[int, int], tuple[int, int]] = ((0, 0), (1, 1))):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void RejectsParenthesizedMutableDefault()
    {
        var source = @"
def foo(items: list[int] = ([])):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors.Should().Contain(e => e.Message.Contains("Mutable default"));
    }

    [Fact]
    public void MultipleErrorsForMultipleBadDefaults()
    {
        var source = @"
def foo(a: list[int] = [], b: int = None, c: dict[str, int] = {}):
    pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        // Should have at least 3 errors from DefaultParameterValidator:
        // mutable list, None for non-nullable, mutable dict
        // Plus TypeChecker also adds type mismatch errors
        typeChecker.Errors.Should().Contain(e => e.Message.Contains("Mutable default") && e.Message.Contains("'a'"));
        typeChecker.Errors.Should().Contain(e => e.Message.Contains("None") && e.Message.Contains("non-nullable") && e.Message.Contains("'b'"));
        typeChecker.Errors.Should().Contain(e => e.Message.Contains("Mutable default") && e.Message.Contains("'c'"));
    }

    #endregion
}
