using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

// This namespace nests inside Sharpy, where an unqualified "List" resolves to Sharpy.List<T>
// (the Pythonic wrapper) rather than the BCL list. Alias the BCL types the scan uses.
using StringList = System.Collections.Generic.List<string>;
using SourceFileList = System.Collections.Generic.IReadOnlyList<string>;

namespace Sharpy.Core.Tests;

/// <summary>
/// Conformance guard: Python-style float formatting has exactly ONE implementation,
/// <c>Builtins.FormatFloat</c> in <c>src/Sharpy.Core/Str.cs</c> (#1204).
///
/// <para>
/// This is the #1145 parallel-site pattern applied to formatting. Before #1204 there were three
/// implementations: the two <c>Builtins.FormatFloat</c> overloads, kept aligned by a doc comment
/// reading <i>"NOTE: Keep in sync"</i>, and a private <c>PrettyPrinter.FormatFloat</c> in the
/// stdlib. The comment was not a mechanism and the copies drifted: the Core pair ended with
/// <c>Replace('E','e')</c> to give CPython's lowercase exponent and the pprint copy never
/// lowercased, so <c>pprint(1e20)</c> printed <c>1E+20</c> where <c>print(1e20)</c> printed
/// <c>1e+20</c> — a live divergence nobody had filed. This test is the mechanism that comment
/// was pretending to be.
/// </para>
///
/// <para>
/// <b>What this is not.</b> It is a source-text tripwire, not a proof. It cannot see a formatter
/// written with <c>$"{x}"</c> or <c>string.Format</c>, and it does not type-check receivers — a
/// <c>ToString("R")</c> on a non-float would be flagged too. It is calibrated to catch the shape
/// the defect actually took (and did take): a hand-rolled renderer that reaches for a general
/// numeric format string and/or appends Python's trailing <c>.0</c>.
/// </para>
///
/// <para>
/// Scope is the runtime — <c>src/Sharpy.Core</c> and <c>src/Sharpy.Stdlib</c>. The compiler is
/// deliberately excluded: <c>RoslynEmitter.Expressions.cs</c>'s <c>DoubleLiteralToken</c> formats a
/// folded double into <b>C# source text</b> for a literal token, which is a different job from
/// rendering a value for a user to read.
/// </para>
/// </summary>
public class FloatFormattingAuthorityTests
{
    /// <summary>The one file allowed to implement Python float formatting.</summary>
    private const string AuthorityFile = "Str.cs";

    /// <summary>
    /// A file exempted from one rule, with the reason it is a different job. Every exemption is
    /// checked for staleness: if the file stops matching, the entry must be deleted rather than
    /// left to over-cover future code (drain-on-fix). An exemption that defers a known divergence
    /// rather than describing a permanently different job cites its issue, so nothing is parked
    /// here anonymously.
    /// </summary>
    private sealed record Exemption(string File, string Reason);

    /// <summary>
    /// General-purpose float format strings. <c>"R"</c> and <c>"G"</c> are both
    /// shortest-round-trip on modern .NET, so either one is a hand-rolled renderer's first move.
    /// </summary>
    private static readonly Regex GeneralFloatFormat = new(@"ToString\(\s*""[RG]""", RegexOptions.Compiled);

    /// <summary>Python's trailing <c>.0</c> for whole values — the other half of the same shape.</summary>
    private static readonly Regex PythonDotZero = new(@"""\.0""", RegexOptions.Compiled);

    /// <summary>A declaration of something named <c>FormatFloat</c>.</summary>
    private static readonly Regex FormatFloatDeclaration = new(@"\bstring\s+FormatFloat\s*\(", RegexOptions.Compiled);

    private static readonly Exemption[] GeneralFloatFormatExemptions =
    {
        new(AuthorityFile, "the authority itself"),
        new("JsonSerializer.cs", "#1229 — JSON wire format: its own number grammar, and NaN/Infinity are an error rather than a spelling"),
        new("YamlRoundtrip.cs", "#1229 — YAML wire format: NaN/Infinity are spelled .nan/.inf/-.inf"),
    };

    private static readonly Exemption[] PythonDotZeroExemptions =
    {
        new(AuthorityFile, "the authority itself"),
        new("JsonSerializer.cs", "#1229 — keeps a whole value reloading as a float, not an int"),
        new("YamlRoundtrip.cs", "#1229 — keeps a whole value reloading as a float, not an int"),
    };

    private static readonly Exemption[] FormatFloatDeclarationExemptions =
    {
        new(AuthorityFile, "the authority itself"),
        new("StringExtensions.Format.cs", "precision-directed formatting for f\"{x:.6f}\" — a different CPython rule, deliberately not merged"),
    };

    [Fact]
    public void FloatFormatting_HasExactlyOneImplementation()
    {
        var files = RuntimeSourceFiles();
        files.Should().NotBeEmpty("the guard is vacuous if it finds no runtime sources to scan");

        var violations = new StringList();
        violations.AddRange(FindViolations(files, GeneralFloatFormat, GeneralFloatFormatExemptions,
            "formats a float with a general-purpose format string"));
        violations.AddRange(FindViolations(files, PythonDotZero, PythonDotZeroExemptions,
            "appends Python's trailing \".0\""));
        violations.AddRange(FindViolations(files, FormatFloatDeclaration, FormatFloatDeclarationExemptions,
            "declares its own FormatFloat"));

        violations.Should().BeEmpty(
            "Python float formatting has one authority: Sharpy.Builtins.FormatFloat in Sharpy.Core/Str.cs. "
            + "Call it (Builtins.FormatFloat(float) for a single — widening to double changes the "
            + "shortest-round-trip digits) instead of writing a second renderer. If the new site is genuinely "
            + "a different job, such as a wire format, add it to the exemption list above WITH its reason.\n"
            + "Violations:\n" + string.Join("\n", violations));
    }

    /// <summary>
    /// An exemption that no longer matches is an exemption that has silently widened to cover code
    /// nobody reviewed. Delete it when the site it names is fixed.
    /// </summary>
    [Fact]
    public void FloatFormatting_ExemptionsAreAllStillLive()
    {
        var files = RuntimeSourceFiles();
        var stale = new StringList();

        CollectStale(files, GeneralFloatFormat, GeneralFloatFormatExemptions, "general float format", stale);
        CollectStale(files, PythonDotZero, PythonDotZeroExemptions, "trailing \".0\"", stale);
        CollectStale(files, FormatFloatDeclaration, FormatFloatDeclarationExemptions, "FormatFloat declaration", stale);

        stale.Should().BeEmpty(
            "every float-formatting exemption must still describe real code — delete the ones that do not.\n"
            + string.Join("\n", stale));
    }

    private static SourceFileList RuntimeSourceFiles()
    {
        var root = FindRepositoryRoot();

        return new[] { "Sharpy.Core", "Sharpy.Stdlib" }
            .Select(project => Path.Combine(root, "src", project))
            .Where(Directory.Exists)
            .SelectMany(dir => Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();
    }

    private static StringList FindViolations(
        SourceFileList files, Regex pattern, Exemption[] exemptions, string what)
    {
        var violations = new StringList();

        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            if (exemptions.Any(e => string.Equals(e.File, name, StringComparison.Ordinal)))
                continue;

            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var code = StripLineComment(lines[i]);
                if (pattern.IsMatch(code))
                    violations.Add($"{name}:{i + 1} — {what}: {lines[i].Trim()}");
            }
        }

        return violations;
    }

    private static void CollectStale(
        SourceFileList files, Regex pattern, Exemption[] exemptions, string rule, StringList stale)
    {
        foreach (var exemption in exemptions)
        {
            var matched = files
                .Where(f => string.Equals(Path.GetFileName(f), exemption.File, StringComparison.Ordinal))
                .SelectMany(File.ReadAllLines)
                .Any(line => pattern.IsMatch(StripLineComment(line)));

            if (!matched)
                stale.Add($"stale [{rule}] exemption: {exemption.File} ({exemption.Reason}) no longer matches");
        }
    }

    private static string StripLineComment(string line)
    {
        var idx = line.IndexOf("//", StringComparison.Ordinal);
        return idx >= 0 ? line.Substring(0, idx) : line;
    }

    private static string FindRepositoryRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            if (File.Exists(Path.Combine(current, "src", "Sharpy.Core", AuthorityFile)))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }
}
