using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.TestInfrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The class contract behind the <c>builtins.</c> escape hatch (#1322): for every name the
/// <see cref="Semantic.Registry.BuiltinRegistry"/> registers, the qualified spelling
/// <c>builtins.X(args)</c> and the bare spelling <c>X(args)</c> must mean the SAME thing — the same
/// result type when both are accepted, and the same diagnostic codes when either is refused.
///
/// <para><b>Why a sweep and not cells.</b> SPY0483 warns rather than refuses a shadowing
/// declaration precisely because <c>builtins.X</c> gets the builtin back. That justification is
/// only as good as the weakest name: the escape was repaired for <c>len</c> (the reported name),
/// while <c>builtins.dict()</c> still reported "module has no member 'dict'" where bare gives
/// SPY0227, and <c>builtins.tuple(xs)</c> compiled a call that bare refuses outright (SPY0338) and
/// came back from codegen as CS0411 behind SPY0908. Both are the same defect — the qualified path
/// resolved against the CLR-discovered surface of the <c>Sharpy.Builtins</c> static class, whose
/// method inventory is an implementation detail, rather than against the registry the bare spelling
/// uses. A per-name fix cannot state that; this sweep does.</para>
///
/// <para><b>Representative shape per name.</b> Names in <see cref="CallShapes"/> get a hand-written
/// argument list; every other registered name is swept with an empty one. An arity refusal is a
/// perfectly good cell — what is under test is that both spellings reach the SAME verdict, not that
/// the call succeeds — and the empty shape is what caught the <c>dict</c> half of the defect.</para>
///
/// <para><b>Errors only, deliberately.</b> Warnings are excluded from the comparison because the
/// two programs are not warning-equivalent by construction: only the qualified one carries an
/// <c>import builtins</c>. Errors and result types are the semantics.</para>
/// </summary>
public class BuiltinsQualifiedAgreementTests
{
    private readonly ITestOutputHelper _output;

    public BuiltinsQualifiedAgreementTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// The argument list swept for a given builtin. Written against the prelude in
    /// <see cref="BuildSource"/> (<c>xs</c>, <c>d</c>, <c>text</c>). Names absent here are swept
    /// with no arguments.
    /// </summary>
    private static readonly Dictionary<string, string> CallShapes = new(StringComparer.Ordinal)
    {
        ["abs"] = "-5",
        ["all"] = "xs",
        ["any"] = "xs",
        ["ascii"] = "text",
        ["bin"] = "5",
        ["bool"] = "1",
        ["bytes"] = "text",
        ["chr"] = "65",
        ["dict"] = "d",
        ["divmod"] = "7, 2",
        ["enumerate"] = "xs",
        ["filter"] = "lambda v: v > 1, xs",
        ["float"] = "\"1.5\"",
        ["format"] = "5, \"d\"",
        ["frozendict"] = "d",
        ["frozenset"] = "xs",
        ["hash"] = "text",
        ["hex"] = "255",
        ["id"] = "xs",
        ["input"] = "",
        ["int"] = "\"5\"",
        ["isinstance"] = "xs, list",
        ["iter"] = "xs",
        ["len"] = "xs",
        ["list"] = "xs",
        ["map"] = "lambda v: v + 1, xs",
        ["max"] = "1, 2",
        ["min"] = "1, 2",
        ["oct"] = "8",
        ["ord"] = "\"a\"",
        ["pow"] = "2, 3",
        ["print"] = "text",
        ["range"] = "3",
        ["repr"] = "5",
        ["reversed"] = "xs",
        ["round"] = "1.5",
        ["set"] = "xs",
        ["sorted"] = "xs",
        ["str"] = "5",
        ["sum"] = "xs",
        ["tuple"] = "xs",
        ["type"] = "xs",
        ["zip"] = "xs, xs",
    };

    /// <summary>
    /// Names excluded from the sweep, each for a reason that is about the SPELLING being
    /// unavailable rather than about the two spellings disagreeing.
    /// </summary>
    private static readonly HashSet<string> NotCallableByName = new(StringComparer.Ordinal)
    {
        // Not value-position callables: `None` is the literal, and the view/iterator/interface
        // names are annotation-only spellings with no constructor in either namespace.
        "None", "object",
        "dict_items", "dict_keys", "dict_values", "iterator",
        "IEnumerable", "IEnumerator",
    };

    /// <summary>
    /// The names whose two spellings are KNOWN to disagree, each with the issue that will drain it.
    /// An entry is a debt, not an exemption: the sweep fails on a stale one, so a fix that removes
    /// the disagreement without removing the line is caught here rather than quietly kept.
    /// </summary>
    private static readonly Dictionary<string, string> KnownDisagreements = new(StringComparer.Ordinal)
    {
        // Empty, and that is the target state: every spelling the sweep covers now agrees. The last
        // entry was #1381 (`builtins.isinstance` narrowed nothing, because the recogniser matched an
        // Identifier callee and could not be told what a member-access receiver denoted); it drained
        // when the recogniser gained the receiver predicate. Add an entry only with an issue that
        // will remove it again.
    };

    private sealed class CallCollector : AstVisitor
    {
        public readonly List<FunctionCall> Calls = new();

        public override void VisitExpression(Expression node)
        {
            if (node is FunctionCall call)
                Calls.Add(call);
            DefaultVisit(node);
        }
    }

    private static string BuildSource(string callee, string args) =>
        "import builtins\n"
        + "\n"
        + "def main() -> None:\n"
        + "    xs: list[int] = [1, 2, 3]\n"
        + "    d: dict[str, int] = {\"a\": 1}\n"
        + "    text: str = \"ab\"\n"
        + "    print(len(xs), len(d), len(text))\n"
        // Parenthesized so the swept call sits in EXPRESSION position. `type` is a soft keyword:
        // bare `type(xs)` at statement start parses as a type-alias declaration (SPY0101) while the
        // qualified spelling does not, which is a difference between what the two SPELLINGS parse
        // to rather than what they mean — the parentheses put both past it. No expected type is
        // supplied, so inference is neutral for every name.
        + $"    ({callee}({args}))\n";

    /// <summary>
    /// A compiler configured the way the product entry surfaces configure one: with the
    /// <c>Sharpy.Core</c> reference loaded, which is what makes <c>import builtins</c> resolve.
    /// Without it every qualified cell measures an unresolved-module cascade (SPY0300) and the
    /// sweep compares two kinds of nothing.
    /// </summary>
    private static Compiler NewCompiler() => new(new CompilerOptions
    {
        OutputType = "library",
        References = new[] { SharpyCoreReference.Location }
    });

    private sealed record Outcome(IReadOnlyList<string> ErrorCodes, string? ResultType)
    {
        public string Describe() => ErrorCodes.Count > 0
            ? $"errors [{string.Join(", ", ErrorCodes)}]"
            : $"type '{ResultType ?? "<none>"}'";
    }

    /// <summary>
    /// Analyzes one spelling and reports what it produced: the error codes, or — when there are
    /// none — the result type the checker gave the swept call.
    /// </summary>
    private static Outcome Analyze(string name, bool qualified)
    {
        var callee = qualified ? $"builtins.{name}" : name;
        var source = BuildSource(callee, CallShapes.GetValueOrDefault(name, string.Empty));

        var result = NewCompiler().Analyze(source, "agreement.spy");

        var errorCodes = result.Diagnostics.GetErrors()
            .Select(diagnostic => diagnostic.Code ?? "<no-code>")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToList();

        string? resultType = null;
        if (errorCodes.Count == 0 && result.Module != null && result.SemanticInfo != null)
        {
            var collector = new CallCollector();
            collector.Visit(result.Module);

            // The swept call is the one whose callee is the name under test — written bare or
            // qualified. The prelude's own calls (print/len) are skipped by that test.
            var swept = collector.Calls.LastOrDefault(call => call.Function switch
            {
                Identifier id => !qualified && id.Name == name,
                MemberAccess member => qualified && member.Member == name,
                _ => false
            });

            if (swept != null)
                resultType = result.SemanticInfo.GetExpressionType(swept)?.GetDisplayName();
        }

        return new Outcome(errorCodes, resultType);
    }

    [Fact]
    public void EveryRegistryBuiltin_QualifiedSpellingAgreesWithBare()
    {
        var probe = NewCompiler().Analyze("import builtins\n\n\ndef main() -> None:\n    pass\n", "probe.spy");
        probe.Diagnostics.GetErrors().Should().BeEmpty(
            "the harness must resolve `import builtins`, or every qualified cell measures an "
            + "unresolved-module cascade instead of the contract");
        var registry = probe.SymbolTable!.BuiltinRegistry;

        var names = registry.RegisteredTypes.Keys
            .Concat(registry.RegisteredFunctionNames)
            .Distinct(StringComparer.Ordinal)
            .Where(name => !NotCallableByName.Contains(name))
            // Escaped-only spellings and CLR-shaped names (Exception subclasses arrive from
            // discovery) stay in; a name that is not a legal bare identifier does not.
            .Where(name => name.Length > 0 && (char.IsLetter(name[0]) || name[0] == '_'))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var disagreements = new List<string>();
        var staleExceptions = new List<string>();
        foreach (var name in names)
        {
            var bare = Analyze(name, qualified: false);
            var qualified = Analyze(name, qualified: true);

            var agrees = bare.ErrorCodes.SequenceEqual(qualified.ErrorCodes, StringComparer.Ordinal)
                && string.Equals(bare.ResultType, qualified.ResultType, StringComparison.Ordinal);

            if (KnownDisagreements.TryGetValue(name, out var issue))
            {
                if (agrees)
                {
                    staleExceptions.Add(
                        $"{name}: listed in KnownDisagreements ({issue}) but the two spellings now "
                        + "agree — delete the entry");
                }
                continue;
            }

            if (!agrees)
            {
                disagreements.Add(
                    $"{name}({CallShapes.GetValueOrDefault(name, string.Empty)}): "
                    + $"bare gave {bare.Describe()}, builtins.{name} gave {qualified.Describe()}");
            }
        }

        _output.WriteLine($"Swept {names.Count} registry builtins; {disagreements.Count} disagreements.");
        foreach (var disagreement in disagreements)
            _output.WriteLine("  " + disagreement);

        // A sweep that swept nothing passes for free. Pin the size and the names whose divergence
        // is the reason this test exists, so a registry-enumeration regression fails here rather
        // than silently emptying the class under guard.
        names.Count.Should().BeGreaterThan(40,
            "the sweep enumerates the registry itself and must not quietly collapse to a handful of names");
        names.Should().Contain(new[] { "len", "dict", "tuple", "list", "set", "int", "str" });

        staleExceptions.Should().BeEmpty(
            "an exception that no longer describes a disagreement is drained, not kept");

        disagreements.Should().BeEmpty(
            "builtins.X(...) must mean exactly what bare X(...) means — same result type, or same "
            + "refusal (#1322)");
    }

    /// <summary>
    /// The VALUE-position half of the same contract (#1382). The sweep above is call-position only —
    /// <see cref="BuildSource"/> emits nothing but call shapes — so a builtin type name used as a
    /// VALUE (<c>f = builtins.dict</c>) was never compared against its bare twin, which is exactly
    /// where the two spellings diverged: the qualified one drew SPY0203 "no member" while the bare
    /// one drew the designed SPY0342 constructor-reference refusal.
    /// </summary>
    /// <remarks>
    /// Registry TYPES only, because that is the surface the constructor-reference rule governs. A
    /// primitive-named builtin (<c>int</c>, <c>str</c>) is not a registry type, so
    /// <c>builtins.int</c> never resolves through the registry and falls through to the
    /// <c>Builtins.Int</c> method group — a DIFFERENT #1322 gap, measured as pre-existing at
    /// fb5fc7f43 and listed below rather than silently swept in.
    /// </remarks>
    [Fact]
    public void EveryRegistryType_QualifiedValuePositionAgreesWithBare()
    {
        var probe = NewCompiler().Analyze("import builtins\n\n\ndef main() -> None:\n    pass\n", "probe.spy");
        probe.Diagnostics.GetErrors().Should().BeEmpty();
        var registry = probe.SymbolTable!.BuiltinRegistry;

        var names = registry.RegisteredTypes.Keys
            .Where(name => name.Length > 0 && (char.IsLetter(name[0]) || name[0] == '_'))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var disagreements = new List<string>();
        var stale = new List<string>();
        foreach (var name in names)
        {
            var bare = AnalyzeValuePosition(name, qualified: false);
            var qualified = AnalyzeValuePosition(name, qualified: true);
            var agrees = bare.ErrorCodes.SequenceEqual(qualified.ErrorCodes, StringComparer.Ordinal);

            if (KnownValueDisagreements.TryGetValue(name, out var issue))
            {
                if (agrees)
                    stale.Add($"{name}: listed ({issue}) but the spellings now agree — delete the entry");
                continue;
            }

            if (!agrees)
                disagreements.Add($"{name}: bare gave {bare.Describe()}, builtins.{name} gave {qualified.Describe()}");
        }

        _output.WriteLine($"Swept {names.Count} registry types in value position; {disagreements.Count} disagreements.");
        foreach (var d in disagreements)
            _output.WriteLine("  " + d);

        names.Count.Should().BeGreaterThan(10,
            "a sweep that swept nothing passes for free");
        names.Should().Contain(new[] { "dict", "list", "set" });
        stale.Should().BeEmpty("an entry is a debt, not an exemption");
        disagreements.Should().BeEmpty(
            "a builtin type name used as a VALUE must mean the same qualified as bare (#1382)");
    }

    /// <summary>
    /// Value-position disagreements that are NOT #1382's: each is a name the registry does not hold
    /// as a type, so the qualified spelling never reaches the constructor-reference rule.
    /// </summary>
    private static readonly Dictionary<string, string> KnownValueDisagreements = new(StringComparer.Ordinal)
    {
        // 8 primitive entries DRAINED (#1463): the TryResolveBuiltinsQualifiedType carve-out
        // became position-aware, so callee position still routes through overload resolution
        // while value position resolves the type for the constructor-reference tiers.

        // builtins.None is a CPython-matching refusal (SPY0214): bare None produces SPY0227
        // (VoidType in value position), qualified produces SPY0214 (a literal, not a member).
        // The disagreement is deliberate — not a defect on the qualified side.
        ["None"] = "#1463",
    };

    private static string BuildValueSource(string reference) =>
        "import builtins\n"
        + "\n"
        + "def main() -> None:\n"
        + $"    f = {reference}\n"
        + "    print(1)\n";

    private static Outcome AnalyzeValuePosition(string name, bool qualified)
    {
        var source = BuildValueSource(qualified ? $"builtins.{name}" : name);
        var result = NewCompiler().Analyze(source, "agreement_value.spy");

        var errorCodes = result.Diagnostics.GetErrors()
            .Select(diagnostic => diagnostic.Code ?? "<no-code>")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToList();

        return new Outcome(errorCodes, null);
    }
}
