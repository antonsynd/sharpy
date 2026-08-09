using Sharpy.Compiler.Discovery.Caching;
using Xunit;

using Sharpy.TestInfrastructure;

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

    /// <summary>
    /// #1313: an identity built from a real assembly carries the identity of the compiler that
    /// would build the index, so a rebuilt compiler invalidates every cached index by construction.
    /// </summary>
    [Fact]
    public void FromAssembly_StampsTheCurrentCompilerIdentity()
    {
        var identity = AssemblyIdentity.FromAssembly(SharpyCoreReference.Assembly);

        Assert.NotEmpty(identity.CompilerVersion);
        Assert.Equal(Sharpy.Compiler.Shared.CompilerIdentity.Version, identity.CompilerVersion);
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

    /// <summary>
    /// #1313: the compiler component is APPENDED as a <c>c{version}</c> segment, keeping the
    /// key's leading <c>{name}-</c> prefix and <c>.json.gz</c> suffix intact.
    /// </summary>
    [Fact]
    public void ToCacheKey_AppendsCompilerIdentityComponent()
    {
        var identity = new AssemblyIdentity
        {
            Name = "TestAssembly",
            Version = "1.0.0",
            ContentHash = "abcdef1234567890",
            CompilerVersion = "9.9.9.9-deadbeefcafe1234"
        };

        var cacheKey = identity.ToCacheKey();

        Assert.StartsWith("testassembly-", cacheKey);
        Assert.EndsWith("-c9.9.9.9-deadbeefcafe1234.json.gz", cacheKey);
    }

    /// <summary>
    /// An identity with no compiler stamp (hand-built, e.g. in tests) still produces a
    /// well-formed key with the <c>c0</c> sentinel rather than a dangling separator.
    /// </summary>
    [Fact]
    public void ToCacheKey_WithoutCompilerVersion_UsesTheC0Sentinel()
    {
        var identity = new AssemblyIdentity
        {
            Name = "TestAssembly",
            Version = "1.0.0",
            ContentHash = "abcdef1234567890"
        };

        Assert.EndsWith("-c0.json.gz", identity.ToCacheKey());
    }

    [Fact]
    public void ToCacheKey_DifferingOnlyByCompilerVersion_ProducesDistinctKeys()
    {
        var built = new AssemblyIdentity
        {
            Name = "TestAssembly",
            Version = "1.0.0",
            ContentHash = "abcdef1234567890",
            CompilerVersion = "1.0.0.0-aaaaaaaaaaaa"
        };
        var rebuilt = new AssemblyIdentity
        {
            Name = "TestAssembly",
            Version = "1.0.0",
            ContentHash = "abcdef1234567890",
            CompilerVersion = "1.0.0.0-bbbbbbbbbbbb"
        };

        Assert.NotEqual(built.ToCacheKey(), rebuilt.ToCacheKey());
    }

    /// <summary>
    /// #1313: <c>OverloadIndexCache.CleanupOldCaches</c> finds an assembly's stale cache files
    /// with the glob <c>{name}-*.json.gz</c>. Appending the compiler component must not push the
    /// key out of that glob, or old indices accumulate forever. This test replicates the glob
    /// literally and runs it through the real file system.
    /// </summary>
    [Fact]
    public void ToCacheKey_RemainsMatchedByTheCleanupOldCachesGlob()
    {
        var identity = new AssemblyIdentity
        {
            Name = "TestAssembly",
            Version = "1.0.0",
            ContentHash = "abcdef1234567890",
            CompilerVersion = "9.9.9.9-deadbeefcafe1234"
        };
        var cacheKey = identity.ToCacheKey();

        var directory = Path.Combine(Path.GetTempPath(), $"sharpy-cachekey-glob-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, cacheKey), "not a real index");

            // Verbatim copy of the pattern in OverloadIndexCache.CleanupOldCaches.
            var pattern = $"{identity.Name.ToLowerInvariant()}-*.json.gz";
            var matched = Directory.GetFiles(directory, pattern).Select(Path.GetFileName).ToList();

            Assert.Contains(cacheKey, matched);
        }
        finally
        {
            try
            { Directory.Delete(directory, recursive: true); }
            catch { }
        }
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

        // Same assembly, different compiler: not the same index (#1313). This is what the
        // in-memory fast path in OverloadIndexCache.TryLoad enforces for free.
        var identity4 = new AssemblyIdentity
        {
            Name = "Test",
            Version = "1.0.0",
            ContentHash = "abc123",
            CompilerVersion = "1.0.0.0-aaaaaaaaaaaa"
        };

        // Act & Assert
        Assert.Equal(identity1, identity2);
        Assert.NotEqual(identity1, identity3);
        Assert.NotEqual(identity1, identity4);
        Assert.NotEqual(identity1.GetHashCode(), identity4.GetHashCode());
    }
}
