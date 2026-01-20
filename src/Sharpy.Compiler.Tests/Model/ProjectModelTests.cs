using Sharpy.Compiler.Model;
using Xunit;

namespace Sharpy.Compiler.Tests.Model;

public class ProjectModelTests
{
    private static ProjectConfig CreateTestConfig()
    {
        return new ProjectConfig
        {
            RootNamespace = "TestProject",
            ProjectDirectory = "/test/project",
            SourceFiles = new List<string>()
        };
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ValidConfig_SetsProperties()
    {
        var config = CreateTestConfig();

        var model = new ProjectModel(config);

        Assert.Equal(config, model.Config);
        Assert.Empty(model.Units);
        Assert.Null(model.GlobalSymbols);
        Assert.Null(model.DependencyGraph);
    }

    [Fact]
    public void Constructor_NullConfig_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ProjectModel(null!));
    }

    #endregion

    #region Unit Management Tests

    [Fact]
    public void AddUnit_ValidUnit_AddsToUnits()
    {
        var model = new ProjectModel(CreateTestConfig());
        var unit = new CompilationUnit("/test/a.spy", "test.a", "x = 1");

        model.AddUnit(unit);

        Assert.Equal(1, model.UnitCount);
        Assert.Same(unit, model.GetUnit("/test/a.spy"));
    }

    [Fact]
    public void AddUnit_DuplicatePath_Throws()
    {
        var model = new ProjectModel(CreateTestConfig());
        var unit1 = new CompilationUnit("/test/a.spy", "test.a", "x = 1");
        var unit2 = new CompilationUnit("/test/a.spy", "test.a", "x = 2");

        model.AddUnit(unit1);

        Assert.Throws<ArgumentException>(() => model.AddUnit(unit2));
    }

    [Fact]
    public void CreateUnit_CreatesAndAdds()
    {
        var model = new ProjectModel(CreateTestConfig());

        var unit = model.CreateUnit("/test/a.spy", "test.a", "x = 1");

        Assert.Equal(1, model.UnitCount);
        Assert.Same(unit, model.GetUnit("/test/a.spy"));
    }

    [Fact]
    public void GetUnit_PathNormalization_FindsUnit()
    {
        var model = new ProjectModel(CreateTestConfig());
        model.CreateUnit("/test/src/module.spy", "test.module", "x = 1");

        // Should find with forward slashes
        var unit = model.GetUnit("/test/src/module.spy");
        Assert.NotNull(unit);

        // Should find with different path separator
        var unit2 = model.GetUnit("\\test\\src\\module.spy");
        Assert.NotNull(unit2);
    }

    [Fact]
    public void GetUnit_NotFound_ReturnsNull()
    {
        var model = new ProjectModel(CreateTestConfig());

        Assert.Null(model.GetUnit("/nonexistent.spy"));
    }

    #endregion

    #region Diagnostics Tests

    [Fact]
    public void HasErrors_NoErrors_ReturnsFalse()
    {
        var model = new ProjectModel(CreateTestConfig());
        model.CreateUnit("/test/a.spy", "test.a", "x = 1");

        Assert.False(model.HasErrors);
    }

    [Fact]
    public void HasErrors_GlobalError_ReturnsTrue()
    {
        var model = new ProjectModel(CreateTestConfig());
        model.GlobalDiagnostics.AddError("Project error");

        Assert.True(model.HasErrors);
    }

    [Fact]
    public void HasErrors_UnitError_ReturnsTrue()
    {
        var model = new ProjectModel(CreateTestConfig());
        var unit = model.CreateUnit("/test/a.spy", "test.a", "x = 1");
        unit.Diagnostics.AddError("File error", 1, 1);

        Assert.True(model.HasErrors);
    }

    [Fact]
    public void TotalErrorCount_AggregatesAllErrors()
    {
        var model = new ProjectModel(CreateTestConfig());
        model.GlobalDiagnostics.AddError("Error 1");
        model.GlobalDiagnostics.AddError("Error 2");

        var unit1 = model.CreateUnit("/test/a.spy", "test.a", "x = 1");
        unit1.Diagnostics.AddError("Error 3", 1, 1);

        var unit2 = model.CreateUnit("/test/b.spy", "test.b", "y = 2");
        unit2.Diagnostics.AddError("Error 4", 1, 1);
        unit2.Diagnostics.AddError("Error 5", 2, 1);

        Assert.Equal(5, model.TotalErrorCount);
    }

    [Fact]
    public void GetAllDiagnostics_CombinesAll()
    {
        var model = new ProjectModel(CreateTestConfig());
        model.GlobalDiagnostics.AddError("Global error");
        model.GlobalDiagnostics.AddWarning("Global warning");

        var unit = model.CreateUnit("/test/a.spy", "test.a", "x = 1");
        unit.Diagnostics.AddError("File error", 1, 1);

        var allDiagnostics = model.GetAllDiagnostics();

        Assert.Equal(3, allDiagnostics.Count);
    }

    #endregion

    #region Build Order Tests

    [Fact]
    public void GetBuildOrder_NoDependencyGraph_ReturnsNull()
    {
        var model = new ProjectModel(CreateTestConfig());
        model.CreateUnit("/test/a.spy", "test.a", "x = 1");

        Assert.Null(model.GetBuildOrder());
    }

    [Fact]
    public void GetUnitsInBuildOrder_NoDependencyGraph_ReturnsAllUnits()
    {
        var model = new ProjectModel(CreateTestConfig());
        model.CreateUnit("/test/a.spy", "test.a", "x = 1");
        model.CreateUnit("/test/b.spy", "test.b", "y = 2");

        var units = model.GetUnitsInBuildOrder().ToList();

        Assert.Equal(2, units.Count);
    }

    #endregion

    #region Incremental Compilation Tests

    [Fact]
    public void IsFileStale_UnitExists_DelegatesToUnit()
    {
        var model = new ProjectModel(CreateTestConfig());
        var unit = model.CreateUnit("/test/a.spy", "test.a", "x = 1");

        Assert.False(model.IsFileStale("/test/a.spy", unit.ContentHash));
        Assert.True(model.IsFileStale("/test/a.spy", "different_hash"));
    }

    [Fact]
    public void IsFileStale_UnitNotFound_ReturnsTrue()
    {
        var model = new ProjectModel(CreateTestConfig());

        Assert.True(model.IsFileStale("/nonexistent.spy", "any_hash"));
    }

    #endregion
}
