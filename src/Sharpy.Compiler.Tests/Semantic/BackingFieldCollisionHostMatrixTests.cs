using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Shared;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// SPY0407 Rule 3b on every host (#2409, R-KB): a property form that synthesizes the named backing
/// field <c>_Level</c> — the mixed auto/function-style form, the observer form, the lone auto set half
/// — is refused at a declared <c>_level</c> that already occupies it, whether the declaration is a
/// class field, a struct field or a MODULE variable. The module class holds both, so the module host
/// was CS0102 behind SPY0908 while the class and struct hosts drew SPY0407 (#1307, #1456, #2395).
/// </summary>
/// <remarks>
/// Closed axes: host {class, struct, module} × form {mixed, observer, lone set half} × declared
/// variable {none, <c>_level</c>, escaped <c>`_level`</c>} = 27 cells. <c>_level</c> is exactly one
/// SPY0407 at the declaration and never SPY0908; <c>none</c> and the escaped spelling (emitted verbatim
/// as <c>_level</c>, which <c>_Level</c> does not collide with) run — the positive controls showing
/// each form compiles on each host and that the check keys on the EMITTED name. Measured @ 2c42408b9
/// before the fix: class and struct × <c>_level</c> SPY0407 at 3:5; module × {mixed, lone set} ×
/// <c>_level</c> SPY0908 CS0102 <c>'_Level'</c>.
/// <para>module × observer × {none, escaped} is SPY0909 at HEAD — a module observer's body is not
/// type-checked (#2369, P30). Those two cells assert today's SPY0909 so they fail when #2369 is fixed
/// and are flipped to <c>run</c> then (drain on fix); module × observer × <c>_level</c> is refused
/// before the emitter runs, so it is a live cell of this contract.</para>
/// <para>Mutation (recorded in the commit body): drop the module-level call of
/// <c>ValidateBackingFieldCollision</c> → the module × {mixed, observer, lone set} × <c>_level</c>
/// cells go red.</para>
/// </remarks>
public class BackingFieldCollisionHostMatrixTests : IntegrationTestBase
{
    public BackingFieldCollisionHostMatrixTests(ITestOutputHelper output) : base(output) { }

    public static readonly string[] Hosts = { "class", "struct", "module" };
    public static readonly string[] Forms = { "mixed", "observer", "lone_set" };
    public static readonly string[] Variables = { "none", "plain", "escaped" };

    /// <summary>Cells pinned at today's SPY0909 by #2369 (a module observer's body is unchecked).</summary>
    private static bool IsKnownRed2369(string host, string form, string variable) =>
        host == "module" && form == "observer" && variable != "plain";

    private static string CellSource(string host, string form, string variable)
    {
        bool module = host == "module";
        string selfOnly = module ? "" : "self";
        string declared = variable switch
        {
            "none" => "",
            "plain" => "_level: int = 3\n",
            "escaped" => "`_level`: int = 3\n",
            _ => throw new ArgumentOutOfRangeException(nameof(variable), variable, null),
        };
        string members = form switch
        {
            "mixed" => $"property level: int = 1\nproperty get level({selfOnly}) -> int:\n    return 7\n",
            "observer" => "property level: int = 1\n    after_set(old_value):\n        print(old_value)\n",
            "lone_set" => "property set level: int\n",
            _ => throw new ArgumentOutOfRangeException(nameof(form), form, null),
        };
        if (module)
            return declared + members + "\ndef main() -> None:\n    level = 5\n    print(\"ok\")\n";

        static string Indent(string s) => string.Join("\n", s.Split('\n').Select(l => l.Length == 0 ? l : "    " + l));
        return $"{host} Host:\n" + Indent("tag: int = 0\n" + declared + members)
            + "\ndef main() -> None:\n    h = Host()\n    h.level = 5\n    print(\"ok\")\n";
    }

    public static IEnumerable<object[]> Cells() =>
        from host in Hosts
        from form in Forms
        from variable in Variables
        select new object[] { host, form, variable };

    [Theory]
    [MemberData(nameof(Cells))]
    public void SynthesizedBackingField_IsRefusedAtTheDeclaredVariable_OnEveryHost(string host, string form, string variable)
    {
        var cell = $"{host} × {form} × {variable}";
        var result = CompileAndExecute(CellSource(host, form, variable), features: FeatureFlags.None.Enable("property_observers"));
        var all = string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code} {d.Line}:{d.Column} {d.Message}"));
        var errors = result.RawDiagnostics.Where(d => d.Severity == CompilerDiagnosticSeverity.Error).ToList();

        errors.Select(d => d.Code).Should().NotContain(DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError, $"[{cell}] never SPY0908");

        if (variable == "plain")
        {
            result.Success.Should().BeFalse($"[{cell}] the declared '_level' occupies the synthesized '_Level'");
            errors.Should().ContainSingle($"[{cell}] exactly one diagnostic (#2075): {all}");
            var error = errors[0];
            error.Code.Should().Be(DiagnosticCodes.Validation.MixedAutoAndFunctionStyleProperty, $"[{cell}] {all}");
            // The declaration: line 1 col 1 at module level, line 3 col 5 under `tag` in a type body.
            (error.Line, error.Column).Should().Be(host == "module" ? (1, 1) : (3, 5), $"[{cell}] {all}");
            error.Message.Should().Contain(host == "module" ? "at module level" : "in 'Host'", $"[{cell}]");
            error.Message.Should().Contain("'_Level'", $"[{cell}]");
        }
        else if (IsKnownRed2369(host, form, variable))
        {
            // Drain on fix: when #2369 types a module observer's body this cell runs — flip it to the
            // `else` arm and delete IsKnownRed2369.
            errors.Select(d => d.Code).Should().Equal(new[] { DiagnosticCodes.Infrastructure.InternalCompilerError },
                $"[{cell}] pinned at #2369's SPY0909; if this fails, #2369 is fixed — move the cell to the run arm: {all}");
        }
        else
        {
            result.Success.Should().BeTrue($"[{cell}] {all}");
            result.StandardOutput.Trim().Split('\n').Last().Should().Be("ok", $"[{cell}]");
        }
    }
}
