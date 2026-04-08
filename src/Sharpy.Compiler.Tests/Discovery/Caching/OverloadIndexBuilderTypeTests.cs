using System.Reflection;
using Sharpy.Compiler.Discovery.Caching;
using Xunit;

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
    public void DiscoverPublicTypes_IncludesStr()
    {
        // Arrange - Str is now a public readonly struct wrapping System.String
        var assembly = SharpyCoreReference.Assembly;

        // Act
        var index = _builder.BuildFromAssembly(assembly);

        // Assert
        var allTypes = index.Modules.Values.SelectMany(m => m.Types).ToList();
        Assert.Contains(allTypes, t => t.Name == "Str");
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
            "Exception", "IOException", "IOError", "FileNotFoundException", "UnauthorizedAccessException", "ValueError"
        };
        foreach (var exType in exceptionTypes)
        {
            Assert.Contains(exType.BaseTypeName, allowedBaseTypes);
        }
    }

    [Fact]
    public void DiscoverPublicTypes_SharpyModuleTypeAttribute_AssignsArgumentParserToArgparse()
    {
        // Arrange
        var assembly = SharpyCoreReference.Assembly;

        // Act
        var index = _builder.BuildFromAssembly(assembly);

        // Assert - ArgumentParser should be in "argparse" module, not "builtins"
        Assert.True(index.Modules.ContainsKey("argparse"), "Expected 'argparse' module to exist");
        var argparseModule = index.Modules["argparse"];
        Assert.Contains(argparseModule.Types, t => t.Name == "ArgumentParser");

        // It should NOT be in builtins
        var builtins = index.Modules["builtins"];
        Assert.DoesNotContain(builtins.Types, t => t.Name == "ArgumentParser");
    }

    [Fact]
    public void DiscoverPublicTypes_SharpyModuleTypeAttribute_AssignsPathToPathlib()
    {
        // Arrange
        var assembly = SharpyCoreReference.Assembly;

        // Act
        var index = _builder.BuildFromAssembly(assembly);

        // Assert - Path should be in "pathlib" module, not "builtins"
        Assert.True(index.Modules.ContainsKey("pathlib"), "Expected 'pathlib' module to exist");
        var pathlibModule = index.Modules["pathlib"];
        Assert.Contains(pathlibModule.Types, t => t.Name == "Path");

        // It should NOT be in builtins
        var builtins = index.Modules["builtins"];
        Assert.DoesNotContain(builtins.Types, t => t.Name == "Path");
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
}
