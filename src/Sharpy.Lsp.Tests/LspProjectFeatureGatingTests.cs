using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Xunit;
using IOPath = System.IO.Path;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// #1109: the LSP <c>.spyproj</c> workspace path (<see cref="LanguageService"/> →
/// <see cref="CompilerApi.AnalyzeProject"/>) must gate experimental features and apply warning
/// configuration identically to the compile path. Before the fix, <c>AnalyzeProject</c>
/// constructed its <c>ProjectCompiler</c> option-less, silently dropping the project's
/// <c>&lt;Features&gt;</c>, <c>&lt;WarningsAsErrors&gt;</c>, and <c>&lt;NoWarn&gt;</c>. These tests
/// drive real project initialization and assert on the diagnostics the LSP would publish.
/// </summary>
public class LspProjectFeatureGatingTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _service;
    private readonly string _tempDir;

    public LspProjectFeatureGatingTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        _service = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);
        _tempDir = IOPath.Combine(IOPath.GetTempPath(), $"sharpy_ls_gate_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    // `defer` is a parser-scoped experimental feature: the statement is always parsed, but its use
    // reports SPY0331 unless the feature is enabled — a clean probe for whether the project's
    // <Features> reaches AnalyzeProject's feature gate.
    private const string DeferMain =
        "def main() -> None:\n    defer print(\"cleanup\")\n    print(\"body\")\n";

    // Emits the SPY0451 unused-variable warning, used to exercise <WarningsAsErrors>/<NoWarn> parity.
    private const string UnusedVarMain =
        "def main() -> None:\n    unused: int = 5\n    print(\"done\")\n";

    [Fact]
    public async Task ProjectWorkspace_FeatureEnabled_NoFeatureGateDiagnostic()
    {
        WriteProject("<Features>defer</Features>", DeferMain);
        await _service.InitializeProjectAsync(_tempDir);

        var diags = await GetMainFileDiagnosticsAsync();
        diags.Should().NotContain(d => d.Code == DiagnosticCodes.Semantic.FeatureNotEnabled,
            "<Features>defer</Features> must flow into AnalyzeProject's feature gate");
    }

    [Fact]
    public async Task ProjectWorkspace_FeatureDisabled_PublishesFeatureGateDiagnostic()
    {
        WriteProject(propertyGroupExtra: "", DeferMain);
        await _service.InitializeProjectAsync(_tempDir);

        var diags = await GetMainFileDiagnosticsAsync();
        diags.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.FeatureNotEnabled,
            "an ungated defer use must still publish SPY0331 in a .spyproj workspace");
    }

    [Fact]
    public async Task ProjectWorkspace_FeatureEnabled_ReachesBuffersOutsideTheProject()
    {
        // A scratch buffer the .spyproj does not list falls back to single-file workspace analysis.
        // Before #1149 that path had no features at all, so the same construct the project compiles
        // was red-squiggled one file over.
        WriteProject("<Features>defer</Features>", DeferMain);
        await _service.InitializeProjectAsync(_tempDir);

        var scratchUri = "file:///scratch/not-in-project.spy";
        _workspace.OpenDocument(scratchUri, DeferMain, 1);
        var analysis = await _service.GetAnalysisAsync(scratchUri);

        analysis.Should().NotBeNull();
        analysis!.Diagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Semantic.FeatureNotEnabled,
            "the workspace .spyproj's <Features> must reach single-file analysis too");
    }

    [Fact]
    public async Task ProjectWorkspace_ConfiguredFeature_ReachesProjectAnalysis()
    {
        // The editor enables a feature the .spyproj does not: workspace configuration widens the
        // project's settings, it never narrows them, so project-file analysis stops gating too.
        WriteProject(propertyGroupExtra: "", DeferMain);
        await _service.InitializeProjectAsync(_tempDir);
        (await GetMainFileDiagnosticsAsync()).Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.FeatureNotEnabled);

        var applied = await _service.ApplyWorkspaceFeaturesAsync(new[] { "defer" });

        applied.Should().BeTrue();
        var diags = await GetMainFileDiagnosticsAsync();
        diags.Should().NotContain(d => d.Code == DiagnosticCodes.Semantic.FeatureNotEnabled,
            "sharpy.features must reach the project path as well as the single-file path");
    }

    [Fact]
    public async Task ProjectWorkspace_WarningsAsErrors_PromotesPublishedSeverity()
    {
        WriteProject("<WarningsAsErrors>true</WarningsAsErrors>", UnusedVarMain);
        await _service.InitializeProjectAsync(_tempDir);

        var diags = _service.ProjectAnalysis!.Diagnostics.GetAll();
        diags.Should().Contain(
            d => d.Code == DiagnosticCodes.Validation.UnusedVariable
                 && d.Severity == CompilerDiagnosticSeverity.Error,
            "<WarningsAsErrors> must promote the unused-variable warning to an error");
    }

    [Fact]
    public async Task ProjectWorkspace_Baseline_WarningStaysWarning()
    {
        WriteProject(propertyGroupExtra: "", UnusedVarMain);
        await _service.InitializeProjectAsync(_tempDir);

        var diags = _service.ProjectAnalysis!.Diagnostics.GetAll();
        diags.Should().Contain(
            d => d.Code == DiagnosticCodes.Validation.UnusedVariable
                 && d.Severity == CompilerDiagnosticSeverity.Warning,
            "without <WarningsAsErrors> the unused-variable diagnostic stays a warning (severity parity control)");
    }

    [Fact]
    public async Task ProjectWorkspace_NoWarn_SuppressesPublishedWarning()
    {
        WriteProject("<NoWarn>SPY0451</NoWarn>", UnusedVarMain);
        await _service.InitializeProjectAsync(_tempDir);

        var diags = _service.ProjectAnalysis!.Diagnostics.GetAll();
        diags.Should().NotContain(d => d.Code == DiagnosticCodes.Validation.UnusedVariable,
            "<NoWarn>SPY0451</NoWarn> must suppress the unused-variable warning");
    }

    private async Task<IReadOnlyList<CompilerDiagnostic>> GetMainFileDiagnosticsAsync()
    {
        var mainPath = IOPath.Combine(_tempDir, "main.spy");
        var uri = new Uri(mainPath).ToString();
        var analysis = await _service.GetAnalysisAsync(uri);
        analysis.Should().NotBeNull("the project file must have a cached analysis result");
        return analysis!.Diagnostics;
    }

    private void WriteProject(string propertyGroupExtra, string mainContent)
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

        File.WriteAllText(IOPath.Combine(_tempDir, "test.spyproj"), projectContent);
        File.WriteAllText(IOPath.Combine(_tempDir, "main.spy"), mainContent);
    }

    public void Dispose()
    {
        _service.Dispose();
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
