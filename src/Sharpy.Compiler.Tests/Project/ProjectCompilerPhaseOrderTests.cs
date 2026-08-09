using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// One phase-boundary invariant of a warm incremental build (#175): symbols restored from the cache
/// are in the symbol table <em>before</em> import resolution runs, so a file that imports a skipped
/// module resolves against the restored symbols.
///
/// <para>The ordering is load-bearing and easy to break silently. <c>RestoreCachedSymbols</c> is
/// called from Phase 2 (<c>InitializeSharedState</c>), while declarations are collected in Phase 3
/// and imports resolved in Phase 4. Move the restore later — or let it register into the wrong
/// scope, which is what #1309 was — and the importing file sees nothing under the imported name.
/// Existing incremental tests import a <em>function</em>; a class is the harder case, because its
/// members have to survive the round-trip too, and it is where an empty-but-present symbol shows
/// up as a member error instead of an unresolved name.</para>
///
/// <para>Deliberately shallow: this pins the ordering, not the fidelity of what is restored.</para>
/// </summary>
public class ProjectCompilerPhaseOrderTests
{
    private readonly ITestOutputHelper _output;

    public ProjectCompilerPhaseOrderTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private const string LibSource = @"class Greeter:
    _name: str

    def __init__(self, name: str):
        self._name = name

    def greet(self) -> str:
        return ""hello "" + self._name
";

    private const string MainUsingGreeter = @"from lib import Greeter


def main():
    g = Greeter(""world"")
    print(g.greet())
";

    /// <summary>
    /// Edit only the importer, then rebuild: <c>lib.spy</c> is skipped and its <c>Greeter</c> comes
    /// back from the symbol cache, and the freshly compiled <c>main.spy</c> binds it. The skip count
    /// is asserted because without it "the build succeeded" would also hold for a build that
    /// recompiled everything and never exercised the restore path at all.
    /// </summary>
    [Fact]
    public void WarmBuild_RestoredClassIsVisibleToImportResolution()
    {
        using var helper = NewProject();

        helper.AssertCompilationSucceeded(helper.Compile());

        helper.UpdateSourceFile("main.spy", MainUsingGreeter.Replace(
            "print(g.greet())", "print(g.greet())\n    print(\"again\")", StringComparison.Ordinal));

        var warm = helper.Compile();

        helper.AssertCompilationSucceeded(warm);
        warm.Metrics!.SkippedFileCount.Should().BeGreaterThan(
            0, "lib.spy is unchanged and must be skipped — if nothing is skipped, nothing was "
            + "restored and this build never tested the restore-before-imports ordering");
    }

    /// <summary>
    /// The control for the test above, and the reason its success is not vacuous: on the same warm
    /// build, calling a method <c>Greeter</c> does not have must still be an error. A restore that
    /// produced a nameless or memberless symbol would let the good program pass <em>and</em> let
    /// this one pass, because there would be nothing left to check the call against.
    /// </summary>
    [Fact]
    public void WarmBuild_RestoredClassStillRejectsAnUnknownMember()
    {
        using var helper = NewProject();

        helper.AssertCompilationSucceeded(helper.Compile());

        helper.UpdateSourceFile("main.spy", MainUsingGreeter.Replace(
            "g.greet()", "g.no_such_method()", StringComparison.Ordinal));

        var warm = helper.Compile();

        warm.Success.Should().BeFalse(
            "the restored Greeter must still be member-checked against; if this compiles, the "
            + "importing file bound a symbol with no members and the passing case above means nothing");
        warm.Diagnostics.GetErrors().Select(d => d.Message).Should().Contain(
            m => m.Contains("no_such_method", StringComparison.Ordinal),
            "the error must be about the missing member, not an unrelated failure");
    }

    private Helpers.ProjectCompilationHelper NewProject()
    {
        var helper = new Helpers.ProjectCompilationHelper(_output)
            .WithRootNamespace("PhaseOrder")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy")
            .WithIncremental();

        helper.AddSourceFile("lib.spy", LibSource);
        helper.AddSourceFile("main.spy", MainUsingGreeter);
        helper.CreateProjectFile();

        return helper;
    }
}
