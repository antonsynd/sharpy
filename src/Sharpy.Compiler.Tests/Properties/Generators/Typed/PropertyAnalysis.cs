using Sharpy.Compiler;
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

    /// <summary>A run-to-run fingerprint of analysing <paramref name="source"/>: the success flag and every
    /// diagnostic (code, position, message) in a stable order — for the determinism lanes, which compare two
    /// runs and judge nothing (#2324: the oracle scan bans reading a verdict or a code in a property file).</summary>
    public static string Fingerprint(string source)
    {
        var (success, all) = AnalyseSingleFile(source);
        return $"success={success}; " + string.Join(" | ", all
            .Select(d => $"{d.Code}@{d.Line}:{d.Column}:{d.Message}")
            .OrderBy(d => d, StringComparer.Ordinal));
    }

    /// <summary><see cref="Fingerprint"/> for a full compile (front end and C# emit): the success flag, every
    /// diagnostic, and the generated C#.</summary>
    public static string CompileFingerprint(string source)
    {
        var result = new Sharpy.Compiler.Compiler().Compile(source, "test.spy");
        return $"success={result.Success}; " + string.Join(" | ", result.Diagnostics.GetAll()
                .Select(d => $"{d.Code}@{d.Line}:{d.Column}:{d.Message}")
                .OrderBy(d => d, StringComparer.Ordinal))
            + "\n" + result.GeneratedCSharpCode;
    }

    /// <summary>A full compile's success flag and the sorted multiset of its diagnostic codes — positions and
    /// messages excluded (the overload-order lane compares declaration orders whose line numbers differ).</summary>
    public static string CodeSignature(string source)
    {
        var result = new Sharpy.Compiler.Compiler().Compile(source, "test.spy");
        var codes = result.Diagnostics.GetAll().Select(d => d.Code).OrderBy(c => c, StringComparer.Ordinal);
        return $"success={result.Success};codes=[{string.Join(",", codes)}]";
    }

    /// <summary>The analysis of a program the caller's filter admitted, for a lane that reads its semantic
    /// facts; THROWS (with the codes) when it does not analyse clean — a throw fails the sample.</summary>
    public static CompilationResult AnalyseClean(string source, string label)
    {
        var analysis = new Sharpy.Compiler.Compiler().Analyze(source, "injection.spy");
        var errors = analysis.Diagnostics.GetAll().Where(d => d.IsError).ToList();
        if (!analysis.Success || errors.Count > 0)
            throw new InvalidOperationException($"{label}: a program the filter admitted no longer analyses clean: "
                + string.Join("; ", errors.Select(d => $"{d.Code}: {d.Message}")) + $"\n{source}");
        return analysis;
    }

    /// <summary>The compilation (front end and C# emit) of a program, and in <paramref name="refusal"/> why it
    /// did not compile clean (its first error and the source), or null — for a lane that records a refusal as a
    /// finding and reads the generated C# otherwise.</summary>
    public static CompilationResult CompileJudged(string source, out string? refusal)
    {
        var result = new Sharpy.Compiler.Compiler().Compile(source, "cs_clean_test.spy");
        var first = result.Diagnostics.GetAll().FirstOrDefault(d => d.IsError);
        refusal = result.Success && first is null
            ? null
            : $"{(first is null ? "(no error diagnostic)" : $"{first.Code}: {first.Message}")}\n{source}";
        return result;
    }

    /// <summary>The compilation (front end and C# emit) of a program; THROWS (with the codes) when it does not
    /// compile clean.</summary>
    public static CompilationResult CompileClean(string source, string label)
    {
        var result = new Sharpy.Compiler.Compiler().Compile(source, "injection.spy");
        var errors = result.Diagnostics.GetAll().Where(d => d.IsError).ToList();
        if (!result.Success || errors.Count > 0)
            throw new InvalidOperationException($"{label}: does not compile: "
                + string.Join("; ", errors.Select(d => $"{d.Code}: {d.Message}")) + $"\n{source}");
        return result;
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
