using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.BlastRadius.Probes;

/// <summary>One analysed source file handed to a probe: the entry file's tree and its semantic facts.</summary>
internal sealed record ProbeInput(Module Ast, SemanticInfo Semantic, SymbolTable Symbols);

/// <summary>
/// One place in a program where a rule applies. A probe reports every such place, not only the
/// ones the rule would refuse, so a report's denominator is counted the same way as its numerator.
/// </summary>
internal sealed record ProbeSite
{
    /// <summary>What the site is, for a reader: <c>Child.__init__</c>.</summary>
    public required string Subject { get; init; }

    /// <summary>1-based line in the analysed source.</summary>
    public required int Line { get; init; }

    /// <summary>One of the probe's <see cref="IRuleProbe.Verdicts"/>.</summary>
    public required string Verdict { get; init; }

    /// <summary>Axis name to value, for every axis the probe declares that applies to this site.</summary>
    public required IReadOnlyDictionary<string, string> Coords { get; init; }

    /// <summary>The evidence in words: which slots, which types, what their values read.</summary>
    public string Detail { get; init; } = "";
}

/// <summary>A verdict a probe can give a site, and what it means.</summary>
internal sealed record VerdictInfo(string Name, string Meaning);

/// <summary>An axis a probe places sites on, and what each value means.</summary>
internal sealed record AxisInfo(string Name, string Meaning, IReadOnlyList<(string Value, string Meaning)> Values);

/// <summary>
/// A proposed language rule written as a pass that only reports. It reads a program that compiles
/// today and says, site by site, what the rule would do to it. It never refuses anything.
/// </summary>
internal interface IRuleProbe
{
    /// <summary>The ruling the probe measures: <c>R-RO</c>.</summary>
    string Rule { get; }

    /// <summary>The rule in one sentence.</summary>
    string Title { get; }

    /// <summary>What one site is: <c>a derived __init__</c>.</summary>
    string SiteNoun { get; }

    /// <summary>Every verdict the probe gives, the ones that cost an edit first.</summary>
    IReadOnlyList<VerdictInfo> Verdicts { get; }

    IReadOnlyList<AxisInfo> Axes { get; }

    /// <summary>What the probe cannot see. Printed in every report.</summary>
    IReadOnlyList<string> Limits { get; }

    IEnumerable<ProbeSite> Probe(ProbeInput input);
}

internal static class ProbeRegistry
{
    public static IReadOnlyList<IRuleProbe> All { get; } = new IRuleProbe[]
    {
        new SuperCallOrderProbe(),
    };

    public static IRuleProbe? Find(string rule)
        => All.FirstOrDefault(p => string.Equals(p.Rule, rule, StringComparison.OrdinalIgnoreCase));
}
