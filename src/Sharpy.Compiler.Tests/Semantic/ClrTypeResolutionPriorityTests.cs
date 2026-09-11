using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Standing matrix for the CLR type-name fallback's resolution priority (#1625, #1765).
///
/// <para><b>Contract.</b> A written type name resolves through one ordered search:
/// <c>BuiltinRegistry.TryFindClrType</c> walks its namespace list, the Sharpy runtime namespace
/// LAST. Two rules fall out, and the matrix exists because P4 shipped each of them by breaking the
/// other: a short name that ALSO names a non-Sharpy .NET type answers with the .NET type
/// (<c>List</c> is <c>System.Collections.Generic.List</c> — #1625), and a name that exists ONLY
/// under <c>Sharpy</c> still resolves (<c>ISized</c>, <c>IReverseEnumerable[T]</c> — an explicitly
/// written name is not a collision, and dropping the namespace made
/// <c>take(r: IReverseEnumerable[int])</c> SPY0202). The "never answer with a Sharpy type for a
/// name the registry owns" half is enforced by the registry's own builtin table and
/// <c>CamelCaseAliases</c>, not by excluding the namespace.</para>
///
/// <para><b>Cell matrix.</b> kind {Sharpy-only, collision short name, CLR-only} x spelling {bare,
/// imported, module-qualified, aliased, backtick-escaped}. Every cell executes or names the
/// diagnostic it expects; the emitted C# is asserted where the discriminating fact is the NAME the
/// emitter chose (a Sharpy answer and a .NET answer both compile, so stdout alone cannot separate
/// them). The axis sizes are anchored to literals below, not counted off the same collection the
/// rows come from.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class ClrTypeResolutionPriorityTests : IntegrationTestBase
{
    public ClrTypeResolutionPriorityTests(ITestOutputHelper output) : base(output) { }

    /// <summary>The three resolution kinds the contract distinguishes.</summary>
    private static readonly string[] Kinds = { "SharpyOnly", "Collision", "ClrOnly" };

    /// <summary>The five spellings a type name can be written in.</summary>
    private static readonly string[] Spellings = { "bare", "imported", "module-qualified", "aliased", "backtick" };

    private const int KindCount = 3;
    private const int SpellingCount = 5;

    /// <summary>
    /// A Sharpy class whose <c>__len__</c>/<c>__reversed__</c> make it satisfy the synthesized
    /// interfaces, so a Sharpy-only interface annotation has something to accept.
    /// </summary>
    private const string Countdown = @"class Countdown:
    n: int

    def __init__(self, n: int) -> None:
        self.n = n

    def __len__(self) -> int:
        return self.n

    def __reversed__(self) -> int:
        yield self.n
";

    /// <summary>
    /// (kind, spelling, source, expected stdout, expected diagnostic code, substring the emitted C#
    /// must contain). The empty string is the "no expectation on this axis" sentinel — a cell
    /// carries either an expected stdout or an expected code, never neither.
    /// </summary>
    public static IEnumerable<object[]> Cells => new[]
    {
        // ---- Sharpy-only: the name exists nowhere but the Sharpy runtime namespace -------------
        new object[] { "SharpyOnly", "bare", Countdown + @"
def size(s: ISized) -> str:
    return ""sized""

def take(r: IReverseEnumerable[int]) -> str:
    return ""reversible""

def main() -> None:
    c = Countdown(5)
    print(size(c))
    print(take(c))
", "sized\nreversible\n", "", "" },   // emitted name is the BARE `ISized` here — #1831

        new object[] { "SharpyOnly", "imported", @"from sharpy import ISized
" + Countdown + @"
def size(s: ISized) -> str:
    return ""sized""

def main() -> None:
    print(size(Countdown(5)))
", "sized\n", "", "global::Sharpy.ISized" },

        new object[] { "SharpyOnly", "module-qualified", @"import sharpy
" + Countdown + @"
def size(s: sharpy.ISized) -> str:
    return ""sized""

def main() -> None:
    print(size(Countdown(5)))
", "sized\n", "", "global::Sharpy.ISized" },

        new object[] { "SharpyOnly", "aliased", @"import sharpy as sh
" + Countdown + @"
def size(s: sh.ISized) -> str:
    return ""sized""

def main() -> None:
    print(size(Countdown(5)))
", "sized\n", "", "global::Sharpy.ISized" },

        // The escape names a user type declared WITH the escape and nothing else (#1325), so the
        // builtin/CLR claim on the spelling does not apply and there is no such type.
        new object[] { "SharpyOnly", "backtick", Countdown + @"
def size(s: `ISized`) -> str:
    return ""sized""

def main() -> None:
    print(size(Countdown(5)))
", "", DiagnosticCodes.Semantic.UndefinedType, "" },

        // ---- Collision: `List` names both Sharpy.List`1 and SCG.List`1 ------------------------
        // The .NET type wins. stdout cannot tell the two apart here, so the emitted parameter type
        // is the assertion — and `count` is a CLR-only member, so it would not even resolve on
        // Sharpy's list.
        new object[] { "Collision", "bare", @"def take(xs: List[int]) -> int:
    return xs.count

def main() -> None:
    print(""ok"")
", "ok\n", "", "Take(global::System.Collections.Generic.List<int> xs)" },

        new object[] { "Collision", "imported", @"from System.Collections.Generic import List

def main() -> None:
    xs: List[int] = List[int]()
    xs.add(7)
    print(xs.count)
", "1\n", "", "new global::System.Collections.Generic.List<int>()" },

        new object[] { "Collision", "module-qualified", @"import system.collections.generic

def take(xs: system.collections.generic.List[int]) -> int:
    return xs.count

def main() -> None:
    print(""ok"")
", "ok\n", "", "Take(global::System.Collections.Generic.List<int> xs)" },

        new object[] { "Collision", "aliased", @"import system.collections.generic as scg

def main() -> None:
    xs: scg.List[int] = scg.List[int]()
    xs.add(2)
    print(xs.count)
", "1\n", "", "new global::System.Collections.Generic.List<int>()" },

        // Same escape rule, and the arm that carried the defect: the CamelCase-alias redirect
        // (List -> list) had no escape gate, so `` `List`[int] `` silently resolved to the builtin
        // and emitted `Sharpy.List<int>` with no user declaration anywhere.
        new object[] { "Collision", "backtick", @"def take(xs: `List`[int]) -> int:
    return 0

def main() -> None:
    print(""ok"")
", "", DiagnosticCodes.Semantic.UndefinedType, "" },

        // ---- CLR-only: the name exists only in a .NET namespace -------------------------------
        new object[] { "ClrOnly", "imported", @"from system.text import StringBuilder

def main() -> None:
    sb: StringBuilder = StringBuilder()
    sb.append(""hi"")
    print(sb.to_string())
", "hi\n", "", "" },

        new object[] { "ClrOnly", "module-qualified", @"import system.text

def main() -> None:
    sb: system.text.StringBuilder = system.text.StringBuilder()
    sb.append(""mq"")
    print(sb.to_string())
", "mq\n", "", "" },

        new object[] { "ClrOnly", "aliased", @"import system.text as st

def main() -> None:
    sb: st.StringBuilder = st.StringBuilder()
    sb.append(""al"")
    print(sb.to_string())
", "al\n", "", "" },

        new object[] { "ClrOnly", "backtick", @"def take(sb: `StringBuilder`) -> str:
    return ""x""

def main() -> None:
    print(""ok"")
", "", DiagnosticCodes.Semantic.UndefinedType, "" },
    };

    /// <summary>
    /// The one cell the matrix cannot assert yet: a BARE CLR-only annotation resolves in semantic
    /// analysis (the fallback finds System.Text.StringBuilder with no import at all) but the
    /// emitter writes the short name, which binds only when a `using` happens to cover it —
    /// CS0246 behind SPY0908 for System.Text. Non-generic CLR-fallback types are the half of
    /// #1765's qualification contract still missing; #1830 owns it and deletes this Skip.
    /// </summary>
    [Fact(Skip = "#1830: a bare CLR-only annotation emits an unqualified name (SPY0908 CS0246)")]
    public void ClrOnly_Bare_ResolvesAndEmitsAQualifiedName()
    {
        var result = CompileAndExecute(@"
def take(sb: StringBuilder) -> str:
    return sb.to_string()

def main() -> None:
    print(""ok"")
");

        result.Success.Should().BeTrue(string.Join(" | ", result.CompilationErrors));
        result.GeneratedCSharp.Should().Contain("global::System.Text.StringBuilder");
    }

    [Fact]
    public void Cells_CoverEveryKindAndSpelling_Once()
    {
        // Axis sizes are anchored to literals, and the rosters are asserted against them, so a
        // silently shrunk roster cannot make the coverage claim pass vacuously.
        Kinds.Length.Should().Be(KindCount);
        Spellings.Length.Should().Be(SpellingCount);

        var cells = Cells.Select(c => ((string)c[0], (string)c[1])).ToList();

        // One cell is carried by the Skip above rather than by a row.
        cells.Should().HaveCount(KindCount * SpellingCount - 1);
        cells.Should().OnlyHaveUniqueItems();

        foreach (var kind in Kinds)
        {
            foreach (var spelling in Spellings)
            {
                if (kind == "ClrOnly" && spelling == "bare")
                    continue; // see ClrOnly_Bare_ResolvesAndEmitsAQualifiedName (#1830)

                cells.Should().Contain((kind, spelling), $"{kind} x {spelling} must be a cell");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void ResolutionPriority_IsTheSameForEverySpelling(
        string kind, string spelling, string code, string expectedStdout, string expectedCode, string emitContains)
    {
        var label = $"{kind} x {spelling}";
        var result = CompileAndExecute(code);

        if (expectedCode.Length > 0)
        {
            result.Success.Should().BeFalse($"{label}: expected {expectedCode}");
            result.RawDiagnostics.Should().Contain(d => d.Code == expectedCode,
                $"{label}: diagnostics were {string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message))}");
            return;
        }

        result.Success.Should().BeTrue(
            $"{label}: {string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message))}");
        result.StandardOutput.Replace("\r\n", "\n").Should().Be(expectedStdout, label);

        if (emitContains.Length > 0)
            result.GeneratedCSharp.Should().Contain(emitContains, label);
    }
}
