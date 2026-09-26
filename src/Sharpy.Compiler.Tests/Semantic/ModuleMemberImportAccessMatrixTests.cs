using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// A module member's importability is the access level its declaration was classified with
/// (<c>AccessLevelConventions.FromName</c>, the escape included — #2033), and the from-import and the
/// qualified reference read that one level. The from-import used a spelling test,
/// <c>!name.StartsWith("__")</c>, so it refused the public escaped <c>`__f`</c> and the dunder
/// <c>__h__</c> that <c>lib.`__f`()</c> / <c>lib.__h__()</c> reached; and the qualified reference had
/// no privacy check at all, so the private <c>lib.__g()</c> reached C# as CS0122 behind SPY0908 while
/// <c>from lib import __g</c> was SPY0283.
/// </summary>
/// <remarks>
/// Cells: member {<c>`__f`</c> escaped function, <c>`__C`</c> escaped class, <c>__h__</c> dunder,
/// <c>_p</c> protected (module-internal), <c>__g</c> private} × reference {from-import, aliased
/// from-import, qualified}. Every member but <c>__g</c> runs in every reference; <c>__g</c> is exactly
/// one SPY0283 in every reference, never SPY0908.
/// </remarks>
public class ModuleMemberImportAccessMatrixTests : IntegrationTestBase
{
    public ModuleMemberImportAccessMatrixTests(ITestOutputHelper output) : base(output) { }

    private const string Lib =
        "def `__f`() -> int:\n    return 1\n\n"
        + "def __g() -> int:\n    return 2\n\n"
        + "def __h__() -> int:\n    return 3\n\n"
        + "def _p() -> int:\n    return 4\n\n"
        + "class `__C`:\n    def m(self) -> int:\n        return 5\n";

    /// <summary>(declared spelling, call suffix, expected output) — the call suffix turns a reference
    /// to the member into an int.</summary>
    private static readonly Dictionary<string, (string Spelling, string Call, string Output)> Members = new()
    {
        ["escaped_function"] = ("`__f`", "()", "1"),
        ["escaped_class"] = ("`__C`", "().m()", "5"),
        ["dunder_function"] = ("__h__", "()", "3"),
        ["protected_function"] = ("_p", "()", "4"),
        ["private_function"] = ("__g", "()", "2"),
    };

    private static readonly string[] References = ["from_import", "aliased_from_import", "qualified"];

    private static string MainModule(string member, string reference)
    {
        var (spelling, call, _) = Members[member];
        return reference switch
        {
            "from_import" => $"from lib import {spelling}\n\ndef main() -> None:\n    print({spelling}{call})\n",
            "aliased_from_import" => $"from lib import {spelling} as alias\n\ndef main() -> None:\n    print(alias{call})\n",
            "qualified" => $"import lib\n\ndef main() -> None:\n    print(lib.{spelling}{call})\n",
            _ => throw new ArgumentOutOfRangeException(nameof(reference)),
        };
    }

    private ExecutionResult Run(string member, string reference)
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"sharpy_import_access_{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);
        try
        {
            File.WriteAllText(Path.Combine(projectDir, "lib.spy"), Lib);
            File.WriteAllText(Path.Combine(projectDir, "main.spy"), MainModule(member, reference));
            return CompileAndExecuteProject(projectDir, "main.spy");
        }
        finally
        {
            try
            { Directory.Delete(projectDir, recursive: true); }
            catch (IOException) { }
        }
    }

    // The escaped class under an alias is not a cell: an aliased escaped TYPE keeps the declaration's
    // escape on the alias (SPY0200 on its bare use, the same at d6a7aef05 for any escaped type), a
    // sibling reported with this change — its references are spelled in codegen.
    public static IEnumerable<object[]> PublicCells()
        => Members.Keys.Where(m => m != "private_function")
            .SelectMany(m => References.Select(r => new object[] { m, r }))
            .Where(c => !((string)c[0] == "escaped_class" && (string)c[1] == "aliased_from_import"));

    public static IEnumerable<object[]> PrivateCells()
        => References.Select(r => new object[] { "private_function", r });

    [Theory]
    [MemberData(nameof(PublicCells))]
    public void NonPrivateMember_IsReachable_InEveryReference(string member, string reference)
    {
        var result = Run(member, reference);

        result.Success.Should().BeTrue(
            $"[{member} × {reference}] {string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message).Concat(result.CompilationErrors))}");
        result.StandardOutput.Trim().Should().Be(Members[member].Output);
    }

    [Theory]
    [MemberData(nameof(PrivateCells))]
    public void PrivateMember_IsSpy0283_InEveryReference(string member, string reference)
    {
        var result = Run(member, reference);

        result.Success.Should().BeFalse($"[{member} × {reference}] must be refused");
        var errors = result.RawDiagnostics.Where(d => d.Severity == CompilerDiagnosticSeverity.Error).ToList();
        errors.Select(e => e.Code).Should().NotContain(
            DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError, $"[{member} × {reference}] never SPY0908");
        errors.Should().ContainSingle(
            $"[{member} × {reference}] {string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message))}");
        errors[0].Code.Should().Be(DiagnosticCodes.Semantic.AccessViolation);
        errors[0].Message.Should().Contain("private symbol '__g'");
    }

    /// <summary>
    /// Positive control for the qualified refusal: a module VARIABLE is emitted public whatever its
    /// spelling, so <c>lib.__w</c> ran before this change and still runs — the refusal is the private
    /// FUNCTION's alone.
    /// </summary>
    [Fact]
    public void QualifiedPrivateModuleVariable_StillRuns()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"sharpy_import_access_{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);
        try
        {
            File.WriteAllText(Path.Combine(projectDir, "lib.spy"), "__w: int = 8\n");
            File.WriteAllText(Path.Combine(projectDir, "main.spy"), "import lib\n\ndef main() -> None:\n    print(lib.__w)\n");
            var result = CompileAndExecuteProject(projectDir, "main.spy");

            result.Success.Should().BeTrue(string.Join(" | ", result.CompilationErrors));
            result.StandardOutput.Trim().Should().Be("8");
        }
        finally
        {
            try
            { Directory.Delete(projectDir, recursive: true); }
            catch (IOException) { }
        }
    }

    [Fact]
    public void Matrix_IsTotal()
    {
        PublicCells().Should().HaveCount(11);
        PrivateCells().Should().HaveCount(3);
    }
}
