using Sharpy.Compiler.Discovery.Caching;
using Xunit;

namespace Sharpy.Compiler.Tests.Discovery.Caching;

public class AssemblyIdentityTests
{
    [Fact]
    public void FromAssembly_CreatesValidIdentity()
    {
        // Arrange
        var assembly = SharpyCoreReference.Assembly;

        // Act
        var identity = AssemblyIdentity.FromAssembly(assembly);

        // Assert
        Assert.NotNull(identity);
        Assert.Equal("Sharpy.Core", identity.Name);
        Assert.NotEmpty(identity.Version);
        Assert.NotEmpty(identity.ContentHash);
    }

    [Fact]
    public void ToCacheKey_GeneratesValidKey()
    {
        // Arrange
        var identity = new AssemblyIdentity
        {
            Name = "TestAssembly",
            Version = "1.0.0",
            ContentHash = "abcdef1234567890"
        };

        // Act
        var cacheKey = identity.ToCacheKey();

        // Assert
        Assert.Contains("testassembly", cacheKey);
        Assert.Contains("1.0.0", cacheKey);
        Assert.EndsWith(".json.gz", cacheKey);
    }

    [Fact]
    public void Equals_ComparesCorrectly()
    {
        // Arrange
        var identity1 = new AssemblyIdentity
        {
            Name = "Test",
            Version = "1.0.0",
            ContentHash = "abc123"
        };

        var identity2 = new AssemblyIdentity
        {
            Name = "Test",
            Version = "1.0.0",
            ContentHash = "abc123"
        };

        var identity3 = new AssemblyIdentity
        {
            Name = "Test",
            Version = "1.0.0",
            ContentHash = "different"
        };

        // Act & Assert
        Assert.Equal(identity1, identity2);
        Assert.NotEqual(identity1, identity3);
    }
}
