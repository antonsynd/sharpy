using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Tests.Helpers;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

/// <summary>
/// The one in-process analysis behind the seam (#2324, P63b Decision 14): the property host's
/// <c>Compiler().Analyze</c> for one file (no stdlib references — a lane's expected code is measured here)
/// and <see cref="ProjectCompilationHelper"/> for a multi-file project (root namespace <c>Test</c>, the
/// import lanes' route). <see cref="ErrorInjector"/>'s verdicts read it; <see cref="Codes"/> is the
/// census's verdict-free read — the distinct codes per severity, never a decision.
/// </summary>
internal static class PropertyAnalysis
{
    public static (bool Success, List<CompilerDiagnostic> All) AnalyseSingleFile(string source)
    {
        var analysis = new Sharpy.Compiler.Compiler().Analyze(source, "injection.spy");
        return (analysis.Success, analysis.Diagnostics.GetAll().ToList());
    }

    public static (bool Success, List<CompilerDiagnostic> All) AnalyseProject(IReadOnlyList<(string Path, string Content)> files)
    {
        using var helper = new ProjectCompilationHelper();
        helper.WithRootNamespace("Test");
        foreach (var (path, content) in files)
            helper.AddSourceFile(path, content);
        helper.CreateProjectFile();
        var result = helper.Compile();
        return (result.Success, result.Diagnostics.GetAll().ToList());
    }

    /// <summary>What an analysis drew, by severity, for the census: distinct error codes, distinct warning
    /// codes and the warning count, or the exception it threw. Describes itself as one bucket key.</summary>
    public sealed record Drawn(IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings, int WarningCount, string? Threw)
    {
        public string Bucket => Threw is { } t
            ? $"THREW {t}"
            : $"errors [{string.Join(", ", Errors)}] warnings [{string.Join(", ", Warnings)}]"
              + (WarningCount > Warnings.Count ? $" ({WarningCount} warning diagnostics)" : "");
    }

    public static Drawn Codes(string source) => Census(() => AnalyseSingleFile(source));

    public static Drawn ProjectCodes(IReadOnlyList<(string Path, string Content)> files) => Census(() => AnalyseProject(files));

    private static Drawn Census(Func<(bool Success, List<CompilerDiagnostic> All)> analyse)
    {
        try
        {
            var (_, all) = analyse();
            return new Drawn(
                Distinct(all.Where(d => d.IsError)),
                Distinct(all.Where(d => d.IsWarning)),
                all.Count(d => d.IsWarning),
                null);
        }
        catch (Exception e)
        {
            return new Drawn(Array.Empty<string>(), Array.Empty<string>(), 0, $"{e.GetType().Name}: {e.Message}");
        }
    }

    private static List<string> Distinct(IEnumerable<CompilerDiagnostic> diagnostics) =>
        diagnostics.Select(d => d.Code ?? "(no code)").Distinct().OrderBy(c => c, StringComparer.Ordinal).ToList();
}
