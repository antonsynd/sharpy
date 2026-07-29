using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Xunit;
using IOPath = System.IO.Path;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// #1149: gated syntax the CLI accepts must be accepted in-editor under the same configuration.
/// <see cref="SharpyWorkspace"/> used to hardcode its analysis options with no features, so a
/// <c>defer</c> statement a project enabled via <c>&lt;Features&gt;</c> still red-squiggled SPY0331
/// in the editor. These are the workspace-path counterpart of
/// <see cref="LspProjectFeatureGatingTests"/> (which covers the <c>.spyproj</c> project path,
/// #1109): the single-file analysis every buffer outside a project — and every buffer before
/// indexing finishes — runs through.
/// </summary>
public class LspWorkspaceFeatureGatingTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;
    private readonly string _tempDir;

    public LspWorkspaceFeatureGatingTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        _tempDir = IOPath.Combine(IOPath.GetTempPath(), $"sharpy_ws_gate_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    // `defer` is parser-scoped and experimental: always parsed, but its use reports SPY0331 unless
    // the feature is enabled — a clean probe for whether features reach the workspace's feature gate.
    private const string DeferSource =
        "def main() -> None:\n    defer print(\"cleanup\")\n    print(\"body\")\n";

    private const string Uri = "file:///workspace-gating/main.spy";

    [Fact]
    public async Task NoFeatureSource_GatedSyntaxReportsFeatureGateDiagnostic()
    {
        _workspace.OpenDocument(Uri, DeferSource, 1);

        var diagnostics = await AnalyzeAsync();

        diagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.FeatureNotEnabled,
            "an ungated defer use must report SPY0331 in the editor exactly as it does in the CLI");
    }

    [Fact]
    public async Task WorkspaceConfiguredFeature_GatedSyntaxIsAccepted()
    {
        _workspace.SetConfiguredFeatures(new[] { "defer" });
        _workspace.OpenDocument(Uri, DeferSource, 1);

        var diagnostics = await AnalyzeAsync();

        diagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Semantic.FeatureNotEnabled,
            "sharpy.features is the editor's counterpart of --enable-feature");
    }

    [Fact]
    public async Task ProjectFeature_GatedSyntaxIsAcceptedInSingleFileAnalysis()
    {
        _workspace.SetProjectConfig(LoadProject("<Features>defer</Features>"));
        _workspace.OpenDocument(Uri, DeferSource, 1);

        var diagnostics = await AnalyzeAsync();

        diagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Semantic.FeatureNotEnabled,
            "the workspace .spyproj's <Features> must reach single-file analysis too");
    }

    [Fact]
    public async Task ProjectWithoutFeature_StillReportsFeatureGateDiagnostic()
    {
        _workspace.SetProjectConfig(LoadProject(propertyGroupExtra: ""));
        _workspace.OpenDocument(Uri, DeferSource, 1);

        var diagnostics = await AnalyzeAsync();

        diagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.FeatureNotEnabled,
            "a .spyproj that does not enable defer must gate it, matching the CLI (negative control)");
    }

    [Fact]
    public async Task ConfiguredFeatureChange_InvalidatesCachedAnalysis()
    {
        _workspace.OpenDocument(Uri, DeferSource, 1);
        var before = await AnalyzeAsync();
        before.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.FeatureNotEnabled);

        var changed = _workspace.SetConfiguredFeatures(new[] { "defer" });

        changed.Should().BeTrue("enabling a feature changes the resolved analysis options");
        var after = await AnalyzeAsync();
        after.Should().NotContain(d => d.Code == DiagnosticCodes.Semantic.FeatureNotEnabled,
            "a didChangeConfiguration must not leave the pre-change diagnostics cached");
    }

    [Fact]
    public void SameConfiguration_ReportsNoChange()
    {
        _workspace.SetConfiguredFeatures(new[] { "defer" }).Should().BeTrue();

        _workspace.SetConfiguredFeatures(new[] { "defer" }).Should().BeFalse(
            "re-applying identical configuration must not throw away every cached analysis");
    }

    [Fact]
    public void UnknownConfiguredFeature_IsDroppedNotEnabled()
    {
        var changed = _workspace.SetConfiguredFeatures(new[] { "not_a_real_feature" });

        changed.Should().BeFalse("an unknown name enables nothing");
        _workspace.Options.Features.EnabledFeatures.Should().BeEmpty();
    }

    [Fact]
    public void UnknownConfiguredFeature_DoesNotDiscardKnownNames()
    {
        _workspace.SetConfiguredFeatures(new[] { "not_a_real_feature", "defer" });

        _workspace.Options.Features.IsEnabled("defer").Should().BeTrue(
            "one bad name must not silently disable the features that are spelled correctly");
        _workspace.Options.Features.IsEnabled("not_a_real_feature").Should().BeFalse();
    }

    [Fact]
    public void ProjectAndConfiguration_FeaturesAreUnioned()
    {
        _workspace.SetProjectConfig(LoadProject("<Features>defer</Features>"));
        _workspace.SetConfiguredFeatures(new[] { "matmul" });

        _workspace.Options.Features.IsEnabled("defer").Should().BeTrue(
            "the editor must not be able to disable a feature the project enabled");
        _workspace.Options.Features.IsEnabled("matmul").Should().BeTrue();
    }

    [Fact]
    public void ProjectWarningConfiguration_ReachesWorkspaceOptions()
    {
        _workspace.SetProjectConfig(LoadProject(
            "<WarningsAsErrors>true</WarningsAsErrors>\n        <NoWarn>SPY0451</NoWarn>"));

        _workspace.Options.WarningsAsErrors.Should().BeTrue();
        _workspace.Options.SuppressedWarnings.Should().Contain("SPY0451");
        _workspace.Options.OutputType.Should().Be("library",
            "in-editor analysis never requires an entry point");
    }

    private async Task<IReadOnlyList<CompilerDiagnostic>> AnalyzeAsync()
    {
        var analysis = await _workspace.GetAnalysisAsync(Uri, CancellationToken.None);
        analysis.Should().NotBeNull();
        return analysis!.Diagnostics;
    }

    private ProjectConfig LoadProject(string propertyGroupExtra)
    {
        var extra = string.IsNullOrEmpty(propertyGroupExtra)
            ? string.Empty
            : "        " + propertyGroupExtra + "\n";
        var projectContent =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<Project>\n" +
            "    <PropertyGroup>\n" +
            "        <RootNamespace>Test</RootNamespace>\n" +
            "        <OutputType>exe</OutputType>\n" +
            extra +
            "    </PropertyGroup>\n" +
            "    <ItemGroup>\n" +
            "        <SpyFile Include=\"main.spy\" />\n" +
            "    </ItemGroup>\n" +
            "</Project>\n";

        var projectPath = IOPath.Combine(_tempDir, "test.spyproj");
        File.WriteAllText(projectPath, projectContent);
        File.WriteAllText(IOPath.Combine(_tempDir, "main.spy"), DeferSource);
        return ProjectFileParser.Load(projectPath);
    }

    public void Dispose()
    {
        _workspace.Dispose();
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}
