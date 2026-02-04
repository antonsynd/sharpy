using FluentAssertions;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

[Collection("Sequential")]
public class NameResolutionServiceTests
{
    private readonly NameResolutionService _service;

    public NameResolutionServiceTests()
    {
        _service = new NameResolutionService();
    }

    #region CodeGenInfo Resolution Tests

    [Fact]
    public void ResolveName_WithCodeGenInfo_UsesCodeGenInfoName()
    {
        // Arrange
        var symbol = new VariableSymbol { Name = "my_var", Kind = SymbolKind.Variable };
        var codeGenInfo = new CodeGenInfo
        {
            CSharpName = "myVar",
            OriginalName = "my_var",
            IsModuleLevel = true
        };

        // Act
        var result = _service.ResolveName(symbol, codeGenInfo);

        // Assert
        result.Should().Be("myVar");
    }

    [Fact]
    public void ResolveName_WithCodeGenInfoAndVersion_IncludesVersionSuffix()
    {
        // Arrange
        var symbol = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };
        var codeGenInfo = new CodeGenInfo
        {
            CSharpName = "x",
            OriginalName = "x",
            Version = 2,
            IsModuleLevel = true
        };

        // Act
        var result = _service.ResolveName(symbol, codeGenInfo);

        // Assert
        result.Should().Be("x_2");
    }

    [Fact]
    public void ResolveName_ModuleSymbol_EscapesCSharpKeywords()
    {
        // Arrange
        var symbol = new ModuleSymbol { Name = "base", Kind = SymbolKind.Module, FilePath = "/path/to/base.spy" };
        var codeGenInfo = new CodeGenInfo
        {
            CSharpName = "base",
            OriginalName = "base",
            IsModuleLevel = true
        };

        // Act
        var result = _service.ResolveName(symbol, codeGenInfo);

        // Assert
        result.Should().Be("@base");
    }

    [Fact]
    public void ResolveName_LocalDeclaration_ReturnsNullFromCodeGenInfo()
    {
        // Arrange - local variable (not module level) with new declaration
        var symbol = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };
        var codeGenInfo = new CodeGenInfo
        {
            CSharpName = "x",
            OriginalName = "x",
            IsModuleLevel = false // Local variable
        };

        // Act
        var result = _service.ResolveName(
            symbol,
            codeGenInfo,
            isNewDeclaration: true);

        // Assert - should fall through to NameMangler since it's a local declaration
        result.Should().Be("x");
    }

    [Fact]
    public void ResolveName_ForceModuleLevelFields_OverridesWithPascalCase()
    {
        // Arrange - execution order issue with forceModuleLevelFields
        var symbol = new VariableSymbol { Name = "my_var", Kind = SymbolKind.Variable };
        var codeGenInfo = new CodeGenInfo
        {
            CSharpName = "myVar", // camelCase computed for local
            OriginalName = "my_var",
            IsModuleLevel = false,
            HasExecutionOrderIssues = true
        };

        // Act
        var result = _service.ResolveName(
            symbol,
            codeGenInfo,
            isNewDeclaration: false,
            forceModuleLevelFields: true);

        // Assert - should use PascalCase for static field
        result.Should().Be("MyVar");
    }

    #endregion

    #region Local Variable Versioning Tests

    [Fact]
    public void ResolveLocalName_FirstDeclaration_ReturnsBaseName()
    {
        // Arrange
        var variableVersions = new Dictionary<string, int> { { "x", 0 } };
        var sourceNames = new HashSet<string>();

        // Act
        var result = _service.ResolveLocalName("x", isNewDeclaration: false, variableVersions, sourceNames);

        // Assert
        result.Should().Be("x");
    }

    [Fact]
    public void ResolveLocalName_Redeclaration_ReturnsVersionedName()
    {
        // Arrange
        var variableVersions = new Dictionary<string, int> { { "x", 0 } };
        var sourceNames = new HashSet<string>();

        // Act
        var result = _service.ResolveLocalName("x", isNewDeclaration: true, variableVersions, sourceNames);

        // Assert
        result.Should().Be("x_1");
    }

    [Fact]
    public void ResolveLocalName_Reference_ReturnsCurrentVersion()
    {
        // Arrange
        var variableVersions = new Dictionary<string, int> { { "x", 2 } };
        var sourceNames = new HashSet<string>();

        // Act
        var result = _service.ResolveLocalName("x", isNewDeclaration: false, variableVersions, sourceNames);

        // Assert
        result.Should().Be("x_2");
    }

    [Fact]
    public void ResolveLocalName_SnakeCaseName_ConvertsToCamelCase()
    {
        // Arrange
        var variableVersions = new Dictionary<string, int> { { "myVar", 0 } };
        var sourceNames = new HashSet<string>();

        // Act
        var result = _service.ResolveLocalName("my_var", isNewDeclaration: false, variableVersions, sourceNames);

        // Assert
        result.Should().Be("myVar");
    }

    [Fact]
    public void ResolveLocalName_UnknownVariable_ReturnsNull()
    {
        // Arrange
        var variableVersions = new Dictionary<string, int>();
        var sourceNames = new HashSet<string>();

        // Act
        var result = _service.ResolveLocalName("unknown", isNewDeclaration: false, variableVersions, sourceNames);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Collision Avoidance Tests

    [Fact]
    public void ResolveLocalName_CollisionWithSourceName_SkipsToNextVersion()
    {
        // Arrange
        var variableVersions = new Dictionary<string, int> { { "x", 0 } };
        var sourceNames = new HashSet<string> { "x_1" }; // User declared x_1

        // Act
        var result = _service.ResolveLocalName("x", isNewDeclaration: true, variableVersions, sourceNames);

        // Assert - should skip x_1 and use x_2
        result.Should().Be("x_2");
    }

    [Fact]
    public void ResolveLocalName_MultipleCollisions_SkipsAllCollisions()
    {
        // Arrange
        var variableVersions = new Dictionary<string, int> { { "x", 0 } };
        var sourceNames = new HashSet<string> { "x_1", "x_2", "x_3" };

        // Act
        var result = _service.ResolveLocalName("x", isNewDeclaration: true, variableVersions, sourceNames);

        // Assert - should skip x_1, x_2, x_3 and use x_4
        result.Should().Be("x_4");
    }

    [Fact]
    public void ComputeNextVersion_NoCollision_ReturnsNextVersion()
    {
        // Arrange
        var sourceNames = new HashSet<string>();

        // Act
        var result = _service.ComputeNextVersion("x", currentVersion: 0, sourceNames);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public void ComputeNextVersion_WithCollision_SkipsCollisions()
    {
        // Arrange
        var sourceNames = new HashSet<string> { "x_1", "x_2" };

        // Act
        var result = _service.ComputeNextVersion("x", currentVersion: 0, sourceNames);

        // Assert
        result.Should().Be(3);
    }

    #endregion

    #region NameMangler Fallback Tests

    [Fact]
    public void ResolveName_NoCodeGenInfo_FallsBackToNameMangler()
    {
        // Arrange
        var symbol = new FunctionSymbol { Name = "my_function", Kind = SymbolKind.Function };

        // Act
        var result = _service.ResolveName(symbol, codeGenInfo: null);

        // Assert
        result.Should().Be("MyFunction");
    }

    [Fact]
    public void ResolveName_VariableSymbolNoCodeGen_ReturnsCamelCase()
    {
        // Arrange
        var symbol = new VariableSymbol { Name = "my_variable", Kind = SymbolKind.Variable };

        // Act
        var result = _service.ResolveName(symbol, codeGenInfo: null);

        // Assert
        result.Should().Be("myVariable");
    }

    [Fact]
    public void ResolveName_FunctionSymbolNoCodeGen_ReturnsPascalCase()
    {
        // Arrange
        var symbol = new FunctionSymbol { Name = "get_user_name", Kind = SymbolKind.Function };

        // Act
        var result = _service.ResolveName(symbol, codeGenInfo: null);

        // Assert
        result.Should().Be("GetUserName");
    }

    [Fact]
    public void ResolveName_TypeSymbolNoCodeGen_ReturnsPascalCase()
    {
        // Arrange
        var symbol = new TypeSymbol { Name = "my_class", Kind = SymbolKind.Type, TypeKind = TypeKind.Class };

        // Act
        var result = _service.ResolveName(symbol, codeGenInfo: null);

        // Assert
        result.Should().Be("MyClass");
    }

    [Fact]
    public void ResolveName_ModuleSymbolNoCodeGen_SanitizesAndEscapes()
    {
        // Arrange
        var symbol = new ModuleSymbol { Name = "my.module.path", Kind = SymbolKind.Module, FilePath = "/path/to/module.spy" };

        // Act
        var result = _service.ResolveName(symbol, codeGenInfo: null);

        // Assert
        result.Should().Be("my_module_path");
    }

    #endregion

    #region C# Keyword Escaping Tests

    [Theory]
    [InlineData("class", "@class")]
    [InlineData("if", "@if")]
    [InlineData("for", "@for")]
    [InlineData("while", "@while")]
    [InlineData("return", "@return")]
    [InlineData("base", "@base")]
    [InlineData("namespace", "@namespace")]
    public void EscapeCSharpKeyword_CSharpKeywords_AddsAtPrefix(string keyword, string expected)
    {
        // Act
        var result = NameResolutionService.EscapeCSharpKeyword(keyword);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("myVariable", "myVariable")]
    [InlineData("MyClass", "MyClass")]
    [InlineData("notAKeyword", "notAKeyword")]
    public void EscapeCSharpKeyword_NonKeywords_ReturnsUnchanged(string name, string expected)
    {
        // Act
        var result = NameResolutionService.EscapeCSharpKeyword(name);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region GetBaseName Tests

    [Theory]
    [InlineData("x", "x")]
    [InlineData("my_var", "myVar")]
    [InlineData("some_long_name", "someLongName")]
    public void GetBaseName_ConvertsToCorrectCamelCase(string input, string expected)
    {
        // Act
        var result = _service.GetBaseName(input);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Integration Tests (Full Resolution Flow)

    [Fact]
    public void ResolveName_FullFlow_CodeGenInfoTakesPriority()
    {
        // Arrange - symbol with CodeGenInfo AND in variableVersions
        var symbol = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };
        var codeGenInfo = new CodeGenInfo
        {
            CSharpName = "moduleX",
            OriginalName = "x",
            IsModuleLevel = true
        };
        var variableVersions = new Dictionary<string, int> { { "x", 3 } };
        var sourceNames = new HashSet<string>();

        // Act
        var result = _service.ResolveName(
            symbol,
            codeGenInfo,
            isNewDeclaration: false,
            variableVersions,
            sourceNames);

        // Assert - CodeGenInfo takes priority over local versioning
        result.Should().Be("moduleX");
    }

    [Fact]
    public void ResolveName_LocalDeclarationWithVersioning_WorksCorrectly()
    {
        // Arrange - local variable with existing version
        var symbol = new VariableSymbol { Name = "counter", Kind = SymbolKind.Variable };
        var variableVersions = new Dictionary<string, int> { { "counter", 1 } };
        var sourceNames = new HashSet<string>();

        // Act - redeclare the variable
        var result = _service.ResolveName(
            symbol,
            codeGenInfo: null,
            isNewDeclaration: true,
            variableVersions,
            sourceNames);

        // Assert - should get next version
        result.Should().Be("counter_2");
    }

    [Fact]
    public void ResolveName_ParameterKindSymbol_ReturnsCamelCase()
    {
        // Arrange - VariableSymbol with Parameter kind (parameters are tracked as variables)
        var symbol = new VariableSymbol { Name = "my_param", Kind = SymbolKind.Parameter };

        // Act
        var result = _service.ResolveName(symbol, codeGenInfo: null);

        // Assert
        result.Should().Be("myParam");
    }

    [Fact]
    public void ResolveName_ModuleWithDots_ReplacesDotsAndEscapes()
    {
        // Arrange - module name with dots that's also a keyword component
        var symbol = new ModuleSymbol { Name = "my.module.base", Kind = SymbolKind.Module, FilePath = "/path/to/module.spy" };

        // Act
        var result = _service.ResolveName(symbol, codeGenInfo: null);

        // Assert
        result.Should().Be("my_module_base");
    }

    [Fact]
    public void TryResolveFromCodeGenInfo_NullCodeGenInfo_ReturnsNull()
    {
        // Arrange
        var symbol = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };

        // Act
        var result = _service.TryResolveFromCodeGenInfo(symbol, null, false);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void TryResolveFromCodeGenInfo_LocalRedeclaration_ReturnsNull()
    {
        // Arrange - local variable being redeclared should return null to let local versioning handle it
        var symbol = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };
        var codeGenInfo = new CodeGenInfo
        {
            CSharpName = "x",
            OriginalName = "x",
            IsModuleLevel = false
        };

        // Act
        var result = _service.TryResolveFromCodeGenInfo(symbol, codeGenInfo, isNewDeclaration: true);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void TryResolveFromCodeGenInfo_ModuleLevelRedeclaration_ReturnsName()
    {
        // Arrange - module level variable redeclaration should use CodeGenInfo
        var symbol = new VariableSymbol { Name = "x", Kind = SymbolKind.Variable };
        var codeGenInfo = new CodeGenInfo
        {
            CSharpName = "X",
            OriginalName = "x",
            IsModuleLevel = true,
            Version = 1
        };

        // Act
        var result = _service.TryResolveFromCodeGenInfo(symbol, codeGenInfo, isNewDeclaration: true);

        // Assert - module level uses CodeGenInfo even for redeclarations
        result.Should().Be("X_1");
    }

    #endregion
}
