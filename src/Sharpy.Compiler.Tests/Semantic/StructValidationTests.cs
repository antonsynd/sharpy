using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for struct-specific semantic validation rules
/// </summary>
public class StructValidationTests
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
        nameResolver.ResolveInheritance(); // Second pass: resolve inheritance

        // Type checking
        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance);

        return (module, symbolTable, semanticInfo, typeChecker);
    }

    #region Constructor Field Initialization Tests

    [Fact]
    public void StructConstructor_MissingOneFieldInitialization_ReportsError()
    {
        var source = @"
struct Point:
    x: int
    y: int
    z: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y
        # ERROR: Field 'z' is not initialized
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().ContainSingle(e =>
            e.Message.Contains("Struct 'Point' constructor must initialize all fields") &&
            e.Message.Contains("'z'"));
    }

    [Fact]
    public void StructConstructor_MissingMultipleFieldInitializations_ReportsError()
    {
        var source = @"
struct Point:
    x: int
    y: int
    z: int
    w: int

    def __init__(self, x: int):
        self.x = x
        # ERROR: Fields 'y', 'z', 'w' are not initialized
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().ContainSingle(e =>
            e.Message.Contains("Struct 'Point' constructor must initialize all fields"));

        var error = typeChecker.Errors.First(e => e.Message.Contains("Struct 'Point' constructor must initialize all fields"));
        error.Message.Should().Contain("'y'");
        error.Message.Should().Contain("'z'");
        error.Message.Should().Contain("'w'");
    }

    [Fact]
    public void StructConstructor_AllFieldsInitialized_NoError()
    {
        var source = @"
struct Point:
    x: int
    y: int
    z: int

    def __init__(self, x: int, y: int, z: int):
        self.x = x
        self.y = y
        self.z = z
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().NotContain(e =>
            e.Message.Contains("constructor must initialize all fields"));
    }

    [Fact]
    public void StructConstructor_FieldsInitializedInDifferentOrder_NoError()
    {
        var source = @"
struct Point:
    x: int
    y: int
    z: int

    def __init__(self, a: int, b: int, c: int):
        self.z = c
        self.x = a
        self.y = b
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().NotContain(e =>
            e.Message.Contains("constructor must initialize all fields"));
    }

    [Fact]
    public void StructConstructor_FieldsInitializedToExpressions_NoError()
    {
        var source = @"
struct Vector2:
    x: float
    y: float
    length: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y
        self.length = (x * x + y * y) ** 0.5
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().NotContain(e =>
            e.Message.Contains("constructor must initialize all fields"));
    }

    [Fact]
    public void StructConstructor_FieldInitializedInConditional_ReportsError()
    {
        var source = @"
struct Point:
    x: int
    y: int

    def __init__(self, x: int, cond: bool):
        self.x = x
        if cond:
            self.y = 10
        # ERROR: Field 'y' might not be initialized (only initialized conditionally)
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().ContainSingle(e =>
            e.Message.Contains("Struct 'Point' constructor must initialize all fields") &&
            e.Message.Contains("'y'"));
    }

    [Fact]
    public void StructConstructor_FieldInitializedInLoop_ReportsError()
    {
        var source = @"
struct Point:
    x: int
    y: int

    def __init__(self, x: int):
        self.x = x
        while True:
            self.y = 10
            break
        # ERROR: Field 'y' might not be initialized (only initialized in loop)
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().ContainSingle(e =>
            e.Message.Contains("Struct 'Point' constructor must initialize all fields") &&
            e.Message.Contains("'y'"));
    }

    [Fact]
    public void StructConstructor_FieldInitializedInTryBlock_ReportsError()
    {
        var source = @"
struct Point:
    x: int
    y: int

    def __init__(self, x: int):
        self.x = x
        try:
            self.y = 10
        except Exception:
            pass
        # ERROR: Field 'y' might not be initialized (only initialized in try block)
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().ContainSingle(e =>
            e.Message.Contains("Struct 'Point' constructor must initialize all fields") &&
            e.Message.Contains("'y'"));
    }

    [Fact]
    public void StructWithNoConstructor_NoError()
    {
        var source = @"
struct Point:
    x: int
    y: int
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().NotContain(e =>
            e.Message.Contains("constructor must initialize all fields"));
    }

    [Fact]
    public void StructWithNoFields_NoError()
    {
        var source = @"
struct Empty:
    def __init__(self):
        pass
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().NotContain(e =>
            e.Message.Contains("constructor must initialize all fields"));
    }

    [Fact]
    public void StructConstructor_MultipleConstructors_BothMustInitializeAllFields()
    {
        var source = @"
struct Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    def __init__(self, value: int):
        self.x = value
        # ERROR: Field 'y' is not initialized in second constructor
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().ContainSingle(e =>
            e.Message.Contains("Struct 'Point' constructor must initialize all fields") &&
            e.Message.Contains("'y'"));
    }

    [Fact]
    public void StructConstructor_FieldInitializedWithDefaultValue_NoError()
    {
        var source = @"
struct Point:
    x: int
    y: int

    def __init__(self):
        self.x = 0
        self.y = 0
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().NotContain(e =>
            e.Message.Contains("constructor must initialize all fields"));
    }

    [Fact]
    public void StructWithMethods_FieldsStillRequireInitialization()
    {
        var source = @"
struct Vector2:
    x: float
    y: float

    def __init__(self, x: float):
        self.x = x
        # ERROR: Field 'y' is not initialized

    def length(self) -> float:
        return (self.x ** 2 + self.y ** 2) ** 0.5
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().ContainSingle(e =>
            e.Message.Contains("Struct 'Vector2' constructor must initialize all fields") &&
            e.Message.Contains("'y'"));
    }

    [Fact]
    public void StructConstructor_FieldInitializedWithComplexExpression_NoError()
    {
        var source = @"
struct Container:
    value: int
    doubled: int

    def __init__(self, val: int):
        self.value = val
        self.doubled = val * 2
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().NotContain(e =>
            e.Message.Contains("constructor must initialize all fields"));
    }

    [Fact]
    public void StructConstructor_OnlyFieldsCountedNotOtherMembers()
    {
        var source = @"
struct Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y
        # Setting a non-field attribute is fine
        self.z = 100
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().NotContain(e =>
            e.Message.Contains("constructor must initialize all fields"));
    }

    #endregion

    #region Struct Field Type Resolution Tests

    [Fact]
    public void Struct_FieldTypes_AreResolved()
    {
        var source = @"
struct Point:
    x: int
    y: int

    def get_x(self) -> int:
        return self.x
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        var pointType = symbolTable.LookupType("Point");
        pointType.Should().NotBeNull();
        pointType!.Fields.Should().HaveCount(2);
        pointType.Fields[0].Name.Should().Be("x");
        pointType.Fields[0].Type.Should().Be(SemanticType.Int);
        pointType.Fields[1].Name.Should().Be("y");
        pointType.Fields[1].Type.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void Struct_FieldTypesWithComplexTypes_AreResolved()
    {
        var source = @"
struct Container:
    items: list[int]
    name: str

    def __init__(self, items: list[int], name: str):
        self.items = items
        self.name = name
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        var containerType = symbolTable.LookupType("Container");
        containerType.Should().NotBeNull();
        containerType!.Fields.Should().HaveCount(2);
        containerType.Fields[0].Name.Should().Be("items");
        containerType.Fields[0].Type.Should().BeOfType<GenericType>();
        containerType.Fields[1].Name.Should().Be("name");
        containerType.Fields[1].Type.Should().Be(SemanticType.Str);
    }

    #endregion

    #region Struct Method Access Tests

    [Fact]
    public void Struct_MethodCanAccessFields()
    {
        var source = @"
struct Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    def distance_from_origin(self) -> float:
        return (self.x ** 2 + self.y ** 2) ** 0.5
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Struct_MethodCanModifyFields()
    {
        var source = @"
struct Counter:
    count: int

    def __init__(self):
        self.count = 0

    def increment(self) -> None:
        self.count = self.count + 1
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    #endregion
}
