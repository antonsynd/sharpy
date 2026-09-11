using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The standing matrix for a member's OWN type parameters (#1836): <b>a member's type parameter
/// list is either emitted on the member, or refused by name — never accepted and dropped</b>.
///
/// <para>Axes: member kind (8) × type-parameter source (2) = 16 cells. The class-level column is
/// the positive control for the whole matrix: every member kind carries a type parameter declared
/// on the TYPE, so a red in the method-level column is about the member's own list and not about
/// generics being broken on that member kind.</para>
///
/// <para>What the matrix found: <c>__init__</c> lowers to a C# constructor and every operator
/// dunder lowers to a C# <c>operator</c>, and C# makes neither generic (§15.11, §15.10). Both
/// accepted <c>def __init__[V](self, v: V)</c> / <c>def __add__[V](self, o: V)</c> and emitted the
/// list nowhere, so the generated C# named an unknown type <c>V</c>: CS0246 behind SPY0908, an
/// internal-error report for a program the user wrote. Every other member kind — module function,
/// instance method, static method, <c>__call__</c> — emitted it correctly, which is what makes
/// "emitted on the member, whatever kind of member it is" the violated contract rather than a
/// missing feature. The cure is a refusal by name (SPY0705) that steers to the class-level
/// spelling, i.e. to this matrix's other column.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class MemberTypeParameterEmissionMatrixTests : IntegrationTestBase
{
    private const int MemberKindCount = 8;
    private const int SourceCount = 2;
    private const int CellCount = MemberKindCount * SourceCount;

    /// <summary>
    /// A module function has no enclosing type, so it has no class-level column. Rostered, not
    /// silently absent.
    /// </summary>
    private const int InapplicableCellCount = 1;

    private const string Ice = DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError;
    private const string NotCallable = DiagnosticCodes.Semantic.NotCallable;
    private const string NotEmittable = DiagnosticCodes.ValidationOverflow.MemberTypeParametersNotEmittable;

    public MemberTypeParameterEmissionMatrixTests(ITestOutputHelper output) : base(output) { }

    public enum MemberKind
    {
        ModuleFunction,
        InstanceMethod,
        StaticMethod,
        CallDunder,
        Init,
        BinaryOperator,
        ComparisonOperator,
        UnaryOperator,
    }

    public enum Source
    {
        /// <summary>The type parameter is declared on the MEMBER: <c>def m[V](...)</c>.</summary>
        MethodLevel,

        /// <summary>The type parameter is declared on the TYPE: <c>class C[V]</c>.</summary>
        ClassLevel,
    }

    /// <summary>
    /// Whether a member of this kind can carry its OWN type parameter list in the generated C#.
    /// A constructor and an operator cannot: the C# grammar has no type-parameter-list on either.
    /// </summary>
    private static bool CarriesItsOwnTypeParameters(MemberKind kind) => kind switch
    {
        MemberKind.Init => false,
        MemberKind.BinaryOperator => false,
        MemberKind.ComparisonOperator => false,
        MemberKind.UnaryOperator => false,
        _ => true,
    };

    private static bool IsApplicable(MemberKind kind, Source source)
        => !(kind == MemberKind.ModuleFunction && source == Source.ClassLevel);

    /// <summary>
    /// The rostered known reds: cells whose program is correct and whose failure belongs to an OPEN
    /// issue outside this matrix's subject. Each names the code it fails with, so the cell stays a
    /// measurement rather than an exemption — a different failure at the same cell is still red.
    /// </summary>
    private static bool IsKnownRed(
        MemberKind kind, Source source, out string code, out string issue)
    {
        // #1859: the callable check does not find `__call__` through a CONSTRUCTED generic
        // receiver. `c(1)` on a `C[int]` is SPY0230 "not callable" while the non-generic twin runs.
        // Nothing to do with a member's own type parameters — this cell declares none — but it is
        // the class-level control for CallDunder, so it is rostered, not deleted.
        if (kind == MemberKind.CallDunder && source == Source.ClassLevel)
        {
            code = NotCallable;
            issue = "#1859";
            return true;
        }

        code = string.Empty;
        issue = string.Empty;
        return false;
    }

    private static string Program(MemberKind kind, Source source)
    {
        var m = source == Source.MethodLevel;
        var cls = m ? "class C:" : "class C[V]:";
        var tp = m ? "[V]" : "";

        return kind switch
        {
            MemberKind.ModuleFunction =>
                "def f[V](v: V) -> None:\n    print(\"ok\")\n\n"
                + "def main() -> None:\n    f(1)\n",

            MemberKind.InstanceMethod =>
                $"{cls}\n    def __init__(self) -> None:\n        pass\n\n"
                + $"    def m{tp}(self, v: V) -> None:\n        print(\"ok\")\n\n"
                + "def main() -> None:\n"
                + (m ? "    c: C = C()\n" : "    c: C[int] = C[int]()\n")
                + "    c.m(1)\n",

            MemberKind.StaticMethod =>
                $"{cls}\n    def __init__(self) -> None:\n        pass\n\n"
                + $"    def s{tp}(v: V) -> None:\n        print(\"ok\")\n\n"
                + "def main() -> None:\n"
                + (m ? "    C.s(1)\n" : "    C[int].s(1)\n"),

            MemberKind.CallDunder =>
                $"{cls}\n    def __init__(self) -> None:\n        pass\n\n"
                + $"    def __call__{tp}(self, v: V) -> None:\n        print(\"ok\")\n\n"
                + "def main() -> None:\n"
                + (m ? "    c: C = C()\n" : "    c: C[int] = C[int]()\n")
                + "    c(1)\n",

            MemberKind.Init =>
                $"{cls}\n    def __init__{tp}(self, v: V) -> None:\n        print(\"ok\")\n\n"
                + "def main() -> None:\n"
                + (m ? "    c: C = C(1)\n" : "    c: C[int] = C[int](1)\n"),

            MemberKind.BinaryOperator =>
                $"{cls}\n    def __init__(self) -> None:\n        pass\n\n"
                + $"    def __add__{tp}(self, other: V) -> int:\n        return 1\n\n"
                + "def main() -> None:\n"
                + (m ? "    c: C = C()\n" : "    c: C[int] = C[int]()\n")
                + "    print(c + 2)\n",

            MemberKind.ComparisonOperator =>
                $"{cls}\n    def __init__(self) -> None:\n        pass\n\n"
                + $"    def __eq__{tp}(self, other: V) -> bool:\n        return True\n\n"
                + "def main() -> None:\n"
                + (m ? "    c: C = C()\n" : "    c: C[int] = C[int]()\n")
                + "    print(c == 2)\n",

            MemberKind.UnaryOperator =>
                $"{cls}\n    def __init__(self) -> None:\n        pass\n\n"
                + $"    def __neg__{tp}(self) -> V:\n        raise ValueError(\"x\")\n\n"
                + "def main() -> None:\n"
                + (m ? "    c: C = C()\n" : "    c: C[int] = C[int]()\n")
                + "    print(\"built\")\n",

            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    public static TheoryData<MemberKind, Source> Cells()
    {
        var data = new TheoryData<MemberKind, Source>();
        foreach (MemberKind k in Enum.GetValues<MemberKind>())
        {
            foreach (Source s in Enum.GetValues<Source>())
            {
                if (IsApplicable(k, s))
                    data.Add(k, s);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void TypeParameters_AreEmitted_OrRefusedByName_NeverDropped(MemberKind kind, Source source)
    {
        var program = Program(kind, source);
        var result = CompileAndExecute(program);
        var seen = string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message))
            + " || " + string.Join(" | ", result.CompilationErrors);

        // The whole-matrix invariant: whatever the answer is, it is never an internal error.
        result.RawDiagnostics.Should().NotContain(d => d.Code == Ice,
            $"{kind}/{source} must be emitted or refused by name, never leaked to Roslyn: {seen}"
            + $"\n--- source ---\n{program}");

        if (IsKnownRed(kind, source, out var knownRedCode, out var knownRedIssue))
        {
            // A rostered known red still asserts WHAT it fails with, so the day it is fixed this
            // cell goes red for the right reason and has to be re-read, and so a DIFFERENT failure
            // here is never absorbed by the roster.
            result.RawDiagnostics.Should().Contain(d => d.Code == knownRedCode,
                $"{kind}/{source} is a rostered known red citing {knownRedIssue}; when that is "
                + $"fixed this cell must go green, not stay red for another reason: {seen}"
                + $"\n--- source ---\n{program}");
            return;
        }

        if (source == Source.ClassLevel || CarriesItsOwnTypeParameters(kind))
        {
            result.Success.Should().BeTrue(
                $"{kind}/{source} carries its type parameter where C# can hold it: {seen}"
                + $"\n--- source ---\n{program}");
        }
        else
        {
            result.RawDiagnostics.Should().Contain(d => d.Code == NotEmittable,
                $"{kind}/MethodLevel lowers to a C# construct that cannot be generic, so the "
                + $"declaration is refused by name (SPY0705): {seen}\n--- source ---\n{program}");
        }
    }

    /// <summary>
    /// The axes are anchored to LITERALS and the one inapplicable cell is justified. A count taken
    /// from the same enums the generator walks would agree with a generator that had lost an axis.
    /// </summary>
    [Fact]
    public void Axes_AreAnchored_AndTheInapplicableCellIsJustified()
    {
        Enum.GetValues<MemberKind>().Length.Should().Be(MemberKindCount);
        Enum.GetValues<Source>().Length.Should().Be(SourceCount);
        CellCount.Should().Be(16, "8 member kinds x 2 type-parameter sources");
        Cells().Count.Should().Be(15, "16 cells less the 1 module function x class level cell");
        InapplicableCellCount.Should().Be(1);

        IsApplicable(MemberKind.ModuleFunction, Source.ClassLevel).Should().BeFalse(
            "a module function has no enclosing type to declare a type parameter on");

        // The known-red roster is exactly one cell, and it is NOT in the column under test: a
        // roster entry in the method-level column would be an exemption for this matrix's subject.
        var knownReds = Cells().Cast<object[]>()
            .Select(row => ((MemberKind)row[0], (Source)row[1]))
            .Where(c => IsKnownRed(c.Item1, c.Item2, out _, out _))
            .ToList();
        knownReds.Should().BeEquivalentTo(new[] { (MemberKind.CallDunder, Source.ClassLevel) });
        knownReds.Should().OnlyContain(c => c.Item2 == Source.ClassLevel,
            "a known red in the MethodLevel column would exempt the behaviour under test");

        // The refusal column is exactly the C# constructs with no type-parameter-list.
        Enum.GetValues<MemberKind>().Where(k => !CarriesItsOwnTypeParameters(k))
            .Should().BeEquivalentTo(new[]
            {
                MemberKind.Init, MemberKind.BinaryOperator,
                MemberKind.ComparisonOperator, MemberKind.UnaryOperator,
            });
    }
}
