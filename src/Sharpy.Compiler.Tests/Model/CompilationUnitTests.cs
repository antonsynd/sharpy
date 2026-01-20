using Sharpy.Compiler.Model;
using Xunit;

namespace Sharpy.Compiler.Tests.Model;

public class CompilationUnitTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_ValidInputs_SetsProperties()
    {
        var unit = new CompilationUnit(
            "/path/to/file.spy",
            "mypackage.mymodule",
            "x: int = 42");

        Assert.Equal("/path/to/file.spy", unit.FilePath);
        Assert.Equal("mypackage.mymodule", unit.ModulePath);
        Assert.Equal("x: int = 42", unit.SourceText);
        Assert.NotEmpty(unit.ContentHash);
        Assert.Equal(CompilationPhase.Created, unit.Phase);
    }

    [Fact]
    public void Constructor_NullFilePath_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CompilationUnit(null!, "module", "source"));
    }

    [Fact]
    public void Constructor_NullModulePath_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CompilationUnit("/path/file.spy", null!, "source"));
    }

    [Fact]
    public void Constructor_NullSourceText_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CompilationUnit("/path/file.spy", "module", null!));
    }

    #endregion

    #region ContentHash Tests

    [Fact]
    public void ContentHash_SameContent_SameHash()
    {
        var unit1 = new CompilationUnit("/path/a.spy", "module.a", "x = 1");
        var unit2 = new CompilationUnit("/path/b.spy", "module.b", "x = 1");

        Assert.Equal(unit1.ContentHash, unit2.ContentHash);
    }

    [Fact]
    public void ContentHash_DifferentContent_DifferentHash()
    {
        var unit1 = new CompilationUnit("/path/a.spy", "module.a", "x = 1");
        var unit2 = new CompilationUnit("/path/a.spy", "module.a", "x = 2");

        Assert.NotEqual(unit1.ContentHash, unit2.ContentHash);
    }

    [Fact]
    public void ContentHash_IsDeterministic()
    {
        var content = "def foo() -> int:\n    return 42";
        var unit1 = new CompilationUnit("/path/a.spy", "module.a", content);
        var unit2 = new CompilationUnit("/path/b.spy", "module.b", content);

        Assert.Equal(unit1.ContentHash, unit2.ContentHash);
    }

    #endregion

    #region IsStale Tests

    [Fact]
    public void IsStale_NullCachedHash_ReturnsTrue()
    {
        var unit = new CompilationUnit("/path/a.spy", "module.a", "x = 1");

        Assert.True(unit.IsStale(null));
    }

    [Fact]
    public void IsStale_EmptyCachedHash_ReturnsTrue()
    {
        var unit = new CompilationUnit("/path/a.spy", "module.a", "x = 1");

        Assert.True(unit.IsStale(""));
    }

    [Fact]
    public void IsStale_MatchingHash_ReturnsFalse()
    {
        var unit = new CompilationUnit("/path/a.spy", "module.a", "x = 1");

        Assert.False(unit.IsStale(unit.ContentHash));
    }

    [Fact]
    public void IsStale_DifferentHash_ReturnsTrue()
    {
        var unit = new CompilationUnit("/path/a.spy", "module.a", "x = 1");

        Assert.True(unit.IsStale("different_hash"));
    }

    #endregion

    #region Default State Tests

    [Fact]
    public void DefaultState_CollectionsAreEmpty()
    {
        var unit = new CompilationUnit("/path/a.spy", "module.a", "x = 1");

        Assert.Empty(unit.DeclaredTypes);
        Assert.Empty(unit.DeclaredFunctions);
        Assert.Empty(unit.Imports);
        Assert.Empty(unit.FromImports);
        Assert.Empty(unit.DirectDependencies);
    }

    [Fact]
    public void DefaultState_ArtifactsAreNull()
    {
        var unit = new CompilationUnit("/path/a.spy", "module.a", "x = 1");

        Assert.Null(unit.Tokens);
        Assert.Null(unit.Ast);
        Assert.Null(unit.ModuleScope);
        Assert.Null(unit.GeneratedCSharp);
    }

    [Fact]
    public void DefaultState_HasNoErrors()
    {
        var unit = new CompilationUnit("/path/a.spy", "module.a", "x = 1");

        Assert.False(unit.HasErrors);
    }

    #endregion

    #region Diagnostics Tests

    [Fact]
    public void Diagnostics_AddError_HasErrorsBecomesTrue()
    {
        var unit = new CompilationUnit("/path/a.spy", "module.a", "x = 1");

        unit.Diagnostics.AddError("Test error", 1, 1);

        Assert.True(unit.HasErrors);
    }

    [Fact]
    public void Diagnostics_IsThreadSafe()
    {
        var unit = new CompilationUnit("/path/a.spy", "module.a", "x = 1");

        // Simulate concurrent access
        Parallel.For(0, 100, i =>
        {
            unit.Diagnostics.AddError($"Error {i}", i, 1);
        });

        Assert.Equal(100, unit.Diagnostics.ErrorCount);
    }

    #endregion
}
