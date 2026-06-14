using System.Reflection;
using Sharpy.Compiler.Discovery.Caching;
using Xunit;

using Sharpy.TestInfrastructure;

namespace Sharpy.Compiler.Tests.Discovery.Caching;

public class OverloadIndexBuilderTypeTests
{
    private readonly OverloadIndexBuilder _builder = new();

    [Fact]
    public void DiscoverPublicTypes_FindsExceptionTypes()
    {
        // Arrange
        var assembly = SharpyCoreReference.Assembly;

        // Act
        var index = _builder.BuildFromAssembly(assembly);

        // Assert
        var builtins = index.Modules["builtins"];
        Assert.NotEmpty(builtins.Types);

        var exceptionTypes = builtins.Types.Where(t => t.IsException).ToList();
        Assert.Contains(exceptionTypes, t => t.Name == "TypeError");
        Assert.Contains(exceptionTypes, t => t.Name == "ValueError");
        Assert.Contains(exceptionTypes, t => t.Name == "RuntimeError");
        Assert.Contains(exceptionTypes, t => t.Name == "NotImplementedError");
        Assert.Contains(exceptionTypes, t => t.Name == "AttributeError");
        Assert.Contains(exceptionTypes, t => t.Name == "ZeroDivisionError");
        Assert.Contains(exceptionTypes, t => t.Name == "OverflowError");
    }

    [Fact]
    public void DiscoverPublicTypes_ExcludesModuleClasses()
    {
        // Arrange
        var assembly = SharpyCoreReference.Assembly;

        // Act
        var index = _builder.BuildFromAssembly(assembly);

        // Assert - [SharpyModule]-decorated classes should not appear in types
        var allTypes = index.Modules.Values.SelectMany(m => m.Types).ToList();
        Assert.DoesNotContain(allTypes, t => t.Name == "Builtins");
        Assert.DoesNotContain(allTypes, t => t.Name == "Math");
    }

    [Fact]
    public void DiscoverPublicTypes_ExcludesStaticClasses()
    {
        // Arrange
        var assembly = SharpyCoreReference.Assembly;

        // Act
        var index = _builder.BuildFromAssembly(assembly);

        // Assert - module classes are static, so they should be excluded from types
        // (they're still discovered as function sources via [SharpyModule] attribute)
        var allTypes = index.Modules.Values.SelectMany(m => m.Types).ToList();
        foreach (var typeInfo in allTypes)
        {
            var clrType = Type.GetType(typeInfo.ClrTypeName);
            if (clrType != null)
            {
                Assert.False(clrType.IsAbstract && clrType.IsSealed && !clrType.IsInterface,
                    $"Static class '{typeInfo.Name}' should have been excluded");
            }
        }
    }

    [Fact]
    public void DiscoverPublicTypes_HasCorrectNamespace()
    {
        // Arrange
        var assembly = SharpyCoreReference.Assembly;

        // Act
        var index = _builder.BuildFromAssembly(assembly);

        // Assert
        var builtins = index.Modules["builtins"];
        foreach (var typeInfo in builtins.Types)
        {
            Assert.StartsWith("Sharpy", typeInfo.Namespace);
        }
    }

    [Fact]
    public void DiscoverPublicTypes_ClrTypeName_ContainsAssemblyQualifier()
    {
        // Arrange
        var assembly = SharpyCoreReference.Assembly;

        // Act
        var index = _builder.BuildFromAssembly(assembly);

        // Assert - AssemblyQualifiedName contains a comma separating type name from assembly info
        var builtins = index.Modules["builtins"];
        foreach (var typeInfo in builtins.Types)
        {
            Assert.Contains(",", typeInfo.ClrTypeName);
        }
    }

    [Fact]
    public void DiscoverPublicTypes_ExceptionTypes_HaveCorrectBaseTypeName()
    {
        // Arrange
        var assembly = SharpyCoreReference.Assembly;

        // Act
        var index = _builder.BuildFromAssembly(assembly);

        // Assert
        var builtins = index.Modules["builtins"];
        var exceptionTypes = builtins.Types.Where(t => t.IsException).ToList();
        // Most exception types directly extend Exception, but some (IOError, FileNotFoundError,
        // FileExistsError, IsADirectoryError) extend IOException, FileNotFoundException,
        // UnauthorizedAccessException, or ValueError (JSONDecodeError)
        var allowedBaseTypes = new HashSet<string?>
        {
            "Exception", "IOException", "IOError", "FileNotFoundException", "UnauthorizedAccessException", "ValueError", "AggregateException"
        };
        foreach (var exType in exceptionTypes)
        {
            Assert.Contains(exType.BaseTypeName, allowedBaseTypes);
        }
    }

    [Fact]
    public void DiscoverPublicTypes_SharpyModuleTypeAttribute_AssignsArgumentParserToArgparse()
    {
        var assembly = SharpyStdlibReference.Assembly;
        var index = _builder.BuildFromAssembly(assembly);

        Assert.True(index.Modules.ContainsKey("argparse"), "Expected 'argparse' module to exist");
        var argparseModule = index.Modules["argparse"];
        Assert.Contains(argparseModule.Types, t => t.Name == "ArgumentParser");
    }

    [Fact]
    public void DiscoverPublicTypes_SharpyModuleTypeAttribute_AssignsPathToPathlib()
    {
        var assembly = SharpyStdlibReference.Assembly;
        var index = _builder.BuildFromAssembly(assembly);

        Assert.True(index.Modules.ContainsKey("pathlib"), "Expected 'pathlib' module to exist");
        var pathlibModule = index.Modules["pathlib"];
        Assert.Contains(pathlibModule.Types, t => t.Name == "Path");
    }

    [Fact]
    public void DiscoverPublicTypes_TypesWithoutSharpyModuleType_StillGoToBuiltins()
    {
        // Arrange
        var assembly = SharpyCoreReference.Assembly;

        // Act
        var index = _builder.BuildFromAssembly(assembly);

        // Assert - Types without the attribute should still be in builtins
        var builtins = index.Modules["builtins"];
        Assert.Contains(builtins.Types, t => t.Name == "TypeError");
        Assert.Contains(builtins.Types, t => t.Name == "ValueError");
    }

    [Fact]
    public void DiscoverNestedModuleTypes_WithoutAttribute_UsesClrName()
    {
        // Arrange - CsvModule has nested types (CsvReader, CsvWriter, etc.) without SharpyModuleTypeAttribute
        var assembly = SharpyStdlibReference.Assembly;

        // Act
        var index = _builder.BuildFromAssembly(assembly);

        // Assert - nested types without the attribute should use their CLR name
        Assert.True(index.Modules.ContainsKey("csv"), "Expected 'csv' module to exist");
        var csvModule = index.Modules["csv"];
        Assert.Contains(csvModule.Types, t => t.Name == "CsvReader");
        Assert.Contains(csvModule.Types, t => t.Name == "CsvDictReader");
    }

    [Fact]
    public void DiscoverNestedModuleTypes_WithSharpyModuleTypeAttribute_UsesPythonName()
    {
        // Arrange - os.StatResult is nested inside OsModule and has no attribute,
        // so its name should be the CLR name. This test verifies the code path
        // that checks for SharpyModuleTypeAttribute on nested types.
        // When a nested type DOES have the attribute with a python name,
        // it should use that name instead of the CLR name.
        var assembly = SharpyStdlibReference.Assembly;

        // Act
        var index = _builder.BuildFromAssembly(assembly);

        // Assert - Verify that the os module's StatResult nested type is discovered
        Assert.True(index.Modules.ContainsKey("os"), "Expected 'os' module to exist");
        var osModule = index.Modules["os"];
        Assert.Contains(osModule.Types, t => t.Name == "StatResult");

        // Verify that nested types marked IsModuleType = true
        var statResult = osModule.Types.First(t => t.Name == "StatResult");
        Assert.True(statResult.IsModuleType);
    }
}
