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
/// imported, module-qualified, aliased (aliased MODULE), aliased-name (aliased from-import, #1863),
/// backtick-escaped}. Every cell executes or names the
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

    /// <summary>The six spellings a type name can be written in.</summary>
    private static readonly string[] Spellings = { "bare", "imported", "module-qualified", "aliased", "aliased-name", "backtick" };

    private const int KindCount = 3;
    private const int SpellingCount = 6;

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
", "sized\nreversible\n", "", "global::Sharpy.ISized" },   // bare `ISized` now qualifies — #1831 (R-AQ)

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

        // aliased-NAME: `from sharpy import ISized as IS`. The alias binds the SAME registry symbol
        // the un-aliased spelling binds (#1863 sibling, b17); `Countdown` (which synthesizes ISized
        // via __len__) is seen as an `IS`, and the emitted name is still global::Sharpy.ISized. The
        // alias "IS" is not a builtin name, so this import is warning-free (no SPY0484).
        new object[] { "SharpyOnly", "aliased-name", @"from sharpy import ISized as IS
" + Countdown + @"
def size(s: IS) -> str:
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

        // aliased-NAME: `from System.Collections.Generic import List as L` — the collision resolves to
        // the .NET type through the alias, and the emitted construction is the qualified BCL type.
        new object[] { "Collision", "aliased-name", @"from System.Collections.Generic import List as L

def main() -> None:
    xs: L[int] = L[int]()
    xs.add(3)
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
        // BARE: the fallback finds System.Text.StringBuilder with no import; the emitted annotation is
        // now fully qualified (previously a short name -> CS0246 behind SPY0908, the #1830 Skip this
        // row replaces). The emit assertion is the discriminating fact — a short name binds only when a
        // prelude `using` happens to cover it.
        new object[] { "ClrOnly", "bare", @"def take(sb: StringBuilder) -> str:
    return sb.to_string()

def main() -> None:
    print(""ok"")
", "ok\n", "", "global::System.Text.StringBuilder" },

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

        // aliased-NAME: `from system.text import StringBuilder as SB` — the CLR-only type reaches
        // Sharpy through the alias and the using-directive spells the reflected type fully qualified
        // (`using SB = global::System.Text.StringBuilder;`), not the mangled alias (#1863).
        new object[] { "ClrOnly", "aliased-name", @"from system.text import StringBuilder as SB

def main() -> None:
    sb: SB = SB()
    sb.append(""an"")
    print(sb.to_string())
", "an\n", "", "global::System.Text.StringBuilder" },

        new object[] { "ClrOnly", "backtick", @"def take(sb: `StringBuilder`) -> str:
    return ""x""

def main() -> None:
    print(""ok"")
", "", DiagnosticCodes.Semantic.UndefinedType, "" },
    };

    [Fact]
    public void Cells_CoverEveryKindAndSpelling_Once()
    {
        // Axis sizes are anchored to literals, and the rosters are asserted against them, so a
        // silently shrunk roster cannot make the coverage claim pass vacuously.
        Kinds.Length.Should().Be(KindCount);
        Spellings.Length.Should().Be(SpellingCount);

        var cells = Cells.Select(c => ((string)c[0], (string)c[1])).ToList();

        // Every kind x spelling is a row: the #1830 Skip that carried `ClrOnly x bare` is drained.
        cells.Should().HaveCount(KindCount * SpellingCount);
        cells.Should().OnlyHaveUniqueItems();

        foreach (var kind in Kinds)
        {
            foreach (var spelling in Spellings)
            {
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
