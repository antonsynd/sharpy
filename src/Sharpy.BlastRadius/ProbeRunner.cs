using Sharpy.BlastRadius.Probes;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Shared;

namespace Sharpy.BlastRadius;

/// <summary>Analyses corpus items with the compiler's own front end and runs one probe over each.</summary>
internal sealed class ProbeRunner
{
    private readonly CompilerApi _api = new(logger: null, DefaultReferences.Resolve());

    // Every experimental feature on: a fixture that needs one is still a program that runs today,
    // and the tool then needs no copy of the `.features` sidecar rule. ForLsp is the factory that
    // takes a feature set and asks for analysis only.
    private readonly CompilerOptions _options = CompilerOptionsFactory.ForLsp(
        features: FeatureFlags.None.Enable(FeatureFlags.KnownFeatures.Keys));

    private readonly IRuleProbe _probe;
    private readonly HashSet<string> _verdicts;
    private readonly Dictionary<string, HashSet<string>> _axes;

    public ProbeRunner(IRuleProbe probe)
    {
        _probe = probe;
        _verdicts = probe.Verdicts.Select(v => v.Name).ToHashSet(StringComparer.Ordinal);
        _axes = probe.Axes.ToDictionary(
            a => a.Name, a => a.Values.Select(v => v.Value).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    public ItemOutcome Run(CorpusItem item)
    {
        if (item.SkipReason != null)
            return new ItemOutcome(item, item.SkipReason, Array.Empty<ProbeSite>());

        SemanticResult result;
        try
        {
            result = item.Path != null
                ? _api.Analyze(item.Source, item.Path, _options)
                : _api.Analyze(item.Source, _options);

            // A specification block is often a few declarations followed by loose statements, which
            // a module cannot hold (SPY0340). The statements go into a main() below, and every
            // declaration keeps its line.
            if (!result.Success && item.Path == null && HoistLooseStatements(item.Source, result) is { } hoisted)
                result = _api.Analyze(hoisted, _options);
        }
        catch (Exception e)
        {
            return new ItemOutcome(item, $"analysis threw {e.GetType().Name}", Array.Empty<ProbeSite>());
        }

        if (!result.Success || result.Ast == null || result.SemanticInfo == null || result.SymbolTable == null)
        {
            return new ItemOutcome(item, ItemOutcome.DoesNotCompile, Array.Empty<ProbeSite>(),
                ErrorCode: result.Diagnostics.FirstOrDefault(d => d.IsError)?.Code ?? "no code");
        }

        var sites = _probe.Probe(new ProbeInput(result.Ast, result.SemanticInfo, result.SymbolTable))
            .Where(s => s.Line > item.PreludeLines)
            .Select(s => Declared(s) with { Line = s.Line - item.PreludeLines + item.LineOffset })
            .OrderBy(s => s.Line).ThenBy(s => s.Subject, StringComparer.Ordinal)
            .ToList();
        return new ItemOutcome(item, null, sites);
    }

    /// <summary>
    /// A report prints the verdicts and axis values a probe declares, each with its meaning. A site
    /// outside them would be counted and never explained, so it is a defect of the probe.
    /// </summary>
    private ProbeSite Declared(ProbeSite site)
    {
        if (!_verdicts.Contains(site.Verdict))
            throw new InvalidOperationException($"{_probe.Rule} gave the verdict '{site.Verdict}', which it does not declare.");
        foreach (var (axis, value) in site.Coords)
        {
            if (!_axes.TryGetValue(axis, out var values) || !values.Contains(value))
                throw new InvalidOperationException($"{_probe.Rule} gave '{axis}: {value}', which it does not declare.");
        }

        return site;
    }

    /// <summary>
    /// The source with each top-level statement that cannot stand at module level blanked out where
    /// it stood and repeated, indented, in a <c>main()</c> appended at the end. Those are the
    /// statements the compiler called executable (SPY0340) and, with them, the top-level variables,
    /// so a name the example binds stays a local of the statements that use it. Null when SPY0340
    /// is not the only error.
    /// </summary>
    private string? HoistLooseStatements(string source, SemanticResult failed)
    {
        var errors = failed.Diagnostics.Where(d => d.IsError).ToList();
        if (errors.Count == 0 || errors.Any(d => d.Code != DiagnosticCodes.Semantic.ModuleLevelExecutableStatement))
            return null;
        var flagged = errors.Where(d => d.Line != null).Select(d => d.Line!.Value).ToList();
        if (failed.Ast is not { } module)
            return null;

        var lines = source.Split('\n');
        var main = new List<string>();
        for (var i = 0; i < module.Body.Length; i++)
        {
            // A statement runs to the line before the next one. Its own LineEnd is not that line
            // for every statement: it stops at the first line of a multi-line string.
            var first = module.Body[i].LineStart;
            var last = i + 1 < module.Body.Length ? module.Body[i + 1].LineStart - 1 : lines.Length;
            var loose = flagged.Any(line => first <= line && line <= last)
                || module.Body[i].UnwrapDecorated() is VariableDeclaration { IsConst: false } or Assignment;
            for (var line = first; loose && line <= last; line++)
            {
                main.Add(lines[line - 1].Length == 0 ? "" : "    " + lines[line - 1]);
                lines[line - 1] = "";
            }
        }

        return main.Count == 0
            ? null
            : string.Join("\n", lines).TrimEnd('\n') + "\n\ndef main() -> None:\n" + string.Join("\n", main) + "\n";
    }
}
