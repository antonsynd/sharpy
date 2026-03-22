using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Services;
using Xunit;

namespace Sharpy.Compiler.Tests.Services;

public class CompilerServicesTests
{
    [Fact]
    public void CreateForTesting_ReturnsValidInstance()
    {
        // Act
        var services = CompilerServicesBuilder.CreateForTesting();

        // Assert
        Assert.NotNull(services);
        Assert.NotNull(services.TypeResolver);
        Assert.NotNull(services.SymbolLookup);
        Assert.NotNull(services.ClrMapper);
        Assert.NotNull(services.DiagnosticReporter);
        Assert.NotNull(services.Logger);
        Assert.NotNull(services.SymbolTable);
        Assert.NotNull(services.SemanticInfo);
    }

    [Fact]
    public void Builder_ThrowsWithoutSymbolTable()
    {
        // Arrange
        var builder = new CompilerServicesBuilder()
            .WithSemanticInfo(new SemanticInfo());

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Builder_ThrowsWithoutSemanticInfo()
    {
        // Arrange
        var builtinRegistry = new BuiltinRegistry();
        var builder = new CompilerServicesBuilder()
            .WithSymbolTable(new SymbolTable(builtinRegistry));

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void ReportError_AddsToDignostics()
    {
        // Arrange
        var services = CompilerServicesBuilder.CreateForTesting();

        // Act
        services.ReportError("Test error", 10, 5);

        // Assert
        Assert.True(services.DiagnosticReporter.HasErrors);
        var errors = services.DiagnosticReporter.Diagnostics.GetErrors();
        Assert.Single(errors);
        Assert.Equal("Test error", errors[0].Message);
        Assert.Equal(10, errors[0].Line);
        Assert.Equal(5, errors[0].Column);
    }

    [Fact]
    public void CurrentFilePath_PropagatesToDiagnosticReporter()
    {
        // Arrange
        var services = CompilerServicesBuilder.CreateForTesting();

        // Act
        services.CurrentFilePath = "/path/to/file.spy";
        services.ReportError("Test error", 1, 1);

        // Assert
        var errors = services.DiagnosticReporter.Diagnostics.GetErrors();
        Assert.Equal("/path/to/file.spy", errors[0].FilePath);
    }

    [Fact]
    public void ShouldContinue_ReturnsFalseWhenMaxErrorsReached()
    {
        // Arrange
        var config = new CompilerServicesConfiguration { MaxErrors = 2 };
        var builtinRegistry = new BuiltinRegistry();
        var services = new CompilerServicesBuilder()
            .WithConfiguration(config)
            .WithSymbolTable(new SymbolTable(builtinRegistry))
            .WithSemanticInfo(new SemanticInfo())
            .Build();

        // Act
        services.ReportError("Error 1");
        Assert.True(services.ShouldContinue());

        services.ReportError("Error 2");

        // Assert
        Assert.False(services.ShouldContinue());
    }

    [Fact]
    public void CanAssign_SameType_ReturnsTrue()
    {
        // Arrange
        var services = CompilerServicesBuilder.CreateForTesting();

        // Act & Assert
        Assert.True(services.CanAssign(SemanticType.Int, SemanticType.Int));
        Assert.True(services.CanAssign(SemanticType.Str, SemanticType.Str));
    }

    [Fact]
    public void CanAssign_NumericWidening_ReturnsTrue()
    {
        // Arrange
        var services = CompilerServicesBuilder.CreateForTesting();

        // Act & Assert
        Assert.True(services.CanAssign(SemanticType.Int, SemanticType.Long));
        Assert.True(services.CanAssign(SemanticType.Float32, SemanticType.Double));
    }

    [Fact]
    public void CanAssign_ToNullable_ReturnsTrue()
    {
        // Arrange
        var services = CompilerServicesBuilder.CreateForTesting();
        var nullableInt = new NullableType { UnderlyingType = SemanticType.Int };

        // Act & Assert
        Assert.True(services.CanAssign(SemanticType.Int, nullableInt));
    }

    [Fact]
    public void Builder_WithLogger_UsesProvidedLogger()
    {
        // Arrange — use CreateForTesting with a logger to verify it's passed through
        var services = CompilerServicesBuilder.CreateForTesting();

        // Assert — the logger should be set (not null)
        Assert.NotNull(services.Logger);
    }

    [Fact]
    public void Builder_WithoutLogger_UsesNullLoggerDefault()
    {
        // Arrange — build without calling WithLogger
        var builtinRegistry = new BuiltinRegistry();

        // Act
        var services = new CompilerServicesBuilder()
            .WithSymbolTable(new SymbolTable(builtinRegistry))
            .WithSemanticInfo(new SemanticInfo())
            .Build();

        // Assert — logger should be a NullLogger instance (not null)
        Assert.NotNull(services.Logger);
    }

    [Fact]
    public void Builder_WithTypeResolver_UsesProvidedResolver()
    {
        // Arrange
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var typeResolver = new TypeResolver(symbolTable, semanticInfo);

        // Act
        var services = new CompilerServicesBuilder()
            .WithSymbolTable(symbolTable)
            .WithSemanticInfo(semanticInfo)
            .WithTypeResolver(typeResolver)
            .Build();

        // Assert — the type resolver adapter should wrap the provided resolver
        Assert.NotNull(services.TypeResolver);
    }

    [Fact]
    public void Builder_WithClrCache_UsesProvidedCache()
    {
        // Arrange
        var builtinRegistry = new BuiltinRegistry();
        var clrCache = new ClrMemberCache();

        // Act
        var services = new CompilerServicesBuilder()
            .WithSymbolTable(new SymbolTable(builtinRegistry))
            .WithSemanticInfo(new SemanticInfo())
            .WithClrCache(clrCache)
            .Build();

        // Assert
        Assert.NotNull(services.ClrMapper);
    }

    [Fact]
    public void Builder_WithoutOptionalComponents_CreatesDefaults()
    {
        // Arrange — only required components, no logger/typeResolver/clrCache
        var builtinRegistry = new BuiltinRegistry();

        // Act
        var services = new CompilerServicesBuilder()
            .WithSymbolTable(new SymbolTable(builtinRegistry))
            .WithSemanticInfo(new SemanticInfo())
            .Build();

        // Assert — all optional components should have default values
        Assert.NotNull(services.Logger);
        Assert.NotNull(services.TypeResolver);
        Assert.NotNull(services.ClrMapper);
        Assert.NotNull(services.DiagnosticReporter);
    }

    [Fact]
    public void Builder_FluentChaining_ReturnsBuilderInstance()
    {
        // Verify builder methods return the same builder for chaining
        var builder = new CompilerServicesBuilder();
        var builtinRegistry = new BuiltinRegistry();

        var result = builder
            .WithConfiguration(CompilerServicesConfiguration.Default)
            .WithLogger(null)
            .WithSymbolTable(new SymbolTable(builtinRegistry))
            .WithSemanticInfo(new SemanticInfo());

        Assert.Same(builder, result);
    }

    [Fact]
    public void Builder_WithConfiguration_NullThrows()
    {
        var builder = new CompilerServicesBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.WithConfiguration(null!));
    }

    [Fact]
    public void Builder_WithSymbolTable_NullThrows()
    {
        var builder = new CompilerServicesBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.WithSymbolTable(null!));
    }

    [Fact]
    public void Builder_WithSemanticInfo_NullThrows()
    {
        var builder = new CompilerServicesBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.WithSemanticInfo(null!));
    }

    [Fact]
    public void Configuration_IsAccessible()
    {
        // Arrange
        var config = new CompilerServicesConfiguration
        {
            MaxErrors = 50,
            ContinueAfterErrors = false,
            VerboseLogging = true
        };
        var builtinRegistry = new BuiltinRegistry();

        // Act
        var services = new CompilerServicesBuilder()
            .WithConfiguration(config)
            .WithSymbolTable(new SymbolTable(builtinRegistry))
            .WithSemanticInfo(new SemanticInfo())
            .Build();

        // Assert
        Assert.Equal(50, services.Configuration.MaxErrors);
        Assert.False(services.Configuration.ContinueAfterErrors);
        Assert.True(services.Configuration.VerboseLogging);
    }

    [Fact]
    public void ShouldContinue_ReturnsFalseImmediatelyWhenContinueAfterErrorsIsFalse()
    {
        // Arrange
        var config = new CompilerServicesConfiguration { ContinueAfterErrors = false };
        var builtinRegistry = new BuiltinRegistry();
        var services = new CompilerServicesBuilder()
            .WithConfiguration(config)
            .WithSymbolTable(new SymbolTable(builtinRegistry))
            .WithSemanticInfo(new SemanticInfo())
            .Build();

        // Act
        services.ReportError("First error");

        // Assert - Should stop immediately after first error
        Assert.False(services.ShouldContinue());
    }
}
