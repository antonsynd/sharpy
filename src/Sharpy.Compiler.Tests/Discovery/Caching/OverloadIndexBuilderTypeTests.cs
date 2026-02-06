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

        // Assert - Exports is static, so it should be excluded from types
        // (it's still discovered as a function source via the separate export discovery path)
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
    public void DiscoverPublicTypes_ExcludesStr()
    {
        // Arrange
        var assembly = SharpyCoreReference.Assembly;

        // Act
        var index = _builder.BuildFromAssembly(assembly);

        // Assert
        var allTypes = index.Modules.Values.SelectMany(m => m.Types).ToList();
        Assert.DoesNotContain(allTypes, t => t.Name == "Str");
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
        foreach (var exType in exceptionTypes)
        {
            Assert.Equal("Exception", exType.BaseTypeName);
        }
    }
}
