using System.Text;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using Xunit.Abstractions;

using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// <c>is None</c> / <c>is not None</c> are a REFERENCE null check for every type, and the <c>==</c> /
/// <c>!=</c> the compiler synthesizes from <c>__eq__</c> / <c>__ne__</c> never dereference a None left
/// operand (#2224, ruling R-EJ "d+b"): <c>is</c> never runs user code; <c>==</c> runs <c>__eq__</c> /
/// <c>operator ==</c>. Before the fix <c>is None</c> lowered to <c>x == null</c>, which bound the
/// user operator: a NullReferenceException for a <c>D | None</c> holding None, and True for a live
/// value whose <c>__eq__</c> admits None.
/// </summary>
/// <remarks>
/// <para>Axes: operand {Sharpy class with a well-behaved <c>__eq__(object)</c>, with a permissive
/// (always-True) <c>__eq__(object)</c>, with no <c>__eq__</c>, with a typed <c>__eq__(other: T)</c>,
/// a struct with a permissive <c>__eq__</c>, a CLR reference whose <c>==</c> treats a "destroyed" object
/// as null (Unity's idiom), <c>System.Version</c>, <c>str</c>, <c>int</c> / <c>int | None</c>, and the
/// Optional controls <c>W?</c> / <c>int?</c>} × subject {statically non-nullable value, <c>T | None</c>
/// holding a value, <c>T | None</c> holding None} × spelling {<c>x is None</c>, <c>x is not None</c>,
/// <c>None is x</c>, <c>None is not x</c>} × position {return value, <c>if</c>, <c>while</c>, ternary,
/// comprehension filter, <c>assert</c>}. Every cell is EXECUTED; its expected value is the reference
/// truth (absent ⇔ None), which is python3's identity answer.</para>
/// <para>The equality family (<see cref="EqualityCells"/>) pins what <c>==</c> / <c>!=</c> do with a
/// None operand on the same operands — python3 values, hand-spelled from <c>python3</c> runs — and the
/// None-test siblings that also lowered to <c>== null</c> / <c>!= null</c>: truthiness of a
/// <c>T | None</c> and the hoisted <c>??</c> / <c>??=</c>.</para>
/// </remarks>
[Collection("HeavyCompilation")]
public class IsNoneReferenceCheckMatrixTests : IntegrationTestBase
{
    public IsNoneReferenceCheckMatrixTests(ITestOutputHelper output) : base(output)
    {
    }

    private const string FakeNullSource = """
        namespace Spyfakenull
        {
            public class Handle
            {
                public Handle(bool destroyed) { Destroyed = destroyed; }

                public bool Destroyed { get; }

                // Unity's UnityEngine.Object idiom: a destroyed object compares equal to null.
                public static bool operator ==(Handle? a, Handle? b)
                {
                    var aNull = a is null || a.Destroyed;
                    var bNull = b is null || b.Destroyed;
                    if (aNull || bNull)
                        return aNull && bNull;
                    return ReferenceEquals(a, b);
                }

                public static bool operator !=(Handle? a, Handle? b) => !(a == b);

                public override bool Equals(object? obj) => obj is Handle h && this == h;

                public override int GetHashCode() => 0;
            }
        }
        """;

    /// <summary>The fake-null specimen assembly, shared with <see cref="ClrReferenceNoneTestMatrixTests"/>.</summary>
    internal static readonly Lazy<string> FakeNullAssembly = new(() => BuildSpecimenAssembly("Spyfakenull", FakeNullSource));

    /// <summary>Compiles an in-test C# specimen into its own assembly (nullable context enabled) and
    /// returns its path; a specimen that does not build is a test-setup error.</summary>
    internal static string BuildSpecimenAssembly(string name, string source)
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location) && File.Exists(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .ToList<MetadataReference>();
        var compilation = CSharpCompilation.Create(
            name,
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        var dir = Path.Combine(Path.GetTempPath(), $"sharpy_{name.ToLowerInvariant()}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{name}.dll");
        var result = compilation.Emit(path);
        if (!result.Success)
        {
            throw new InvalidOperationException($"the {name} specimen assembly must build: "
                + string.Join("; ", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        }
        return path;
    }

    protected override IEnumerable<string> GetAdditionalReferenceAssemblyPaths()
        => base.GetAdditionalReferenceAssemblyPaths().Append(FakeNullAssembly.Value);

    public enum Operand
    {
        WellBehavedEq, PermissiveEq, NoEq, TypedEq, PermissiveStruct,
        ClrFakeNull, ClrVersion, Str, Int, OptionalClass, OptionalInt,
    }

    public enum Position { Return, If, While, Ternary, ComprehensionFilter, Assert }

    private const string Prelude = """
        from system import Version
        from spyfakenull import Handle

        class W:
            v: int
            def __init__(self, v: int):
                self.v = v
            def __eq__(self, other: object) -> bool:
                return isinstance(other, W) and self.v == other.v
            def __hash__(self) -> int:
                return self.v

        class P:
            v: int
            def __init__(self, v: int):
                self.v = v
            def __eq__(self, other: object) -> bool:
                return True
            def __hash__(self) -> int:
                return 1

        class N:
            v: int
            def __init__(self, v: int):
                self.v = v

        class T:
            v: int
            def __init__(self, v: int):
                self.v = v
            def __eq__(self, other: T) -> bool:
                return self.v == other.v

        struct S:
            v: int
            def __init__(self, v: int):
                self.v = v
            def __eq__(self, other: object) -> bool:
                return True
            def __hash__(self) -> int:
                return 1


        """;

    /// <summary>One subject: a declaration line binding <c>x</c>, and whether <c>x</c> holds None.</summary>
    private sealed record Subject(string Name, string Declaration, bool Absent);

    /// <summary>
    /// The subjects for one operand. A statically non-nullable value is "present" by construction —
    /// including the destroyed <c>Handle</c>, which <c>is None</c> must NOT report as None (R-EJ: the
    /// reference check; <c>== None</c> is the spelling that asks its overload, #2238).
    /// </summary>
    private static Subject[] Subjects(Operand operand) => operand switch
    {
        Operand.WellBehavedEq => ClassSubjects("W"),
        Operand.PermissiveEq => ClassSubjects("P"),
        Operand.NoEq => ClassSubjects("N"),
        Operand.TypedEq => ClassSubjects("T"),
        Operand.PermissiveStruct => new[] { new Subject("value", "x: S = S(2)", false) },
        Operand.ClrFakeNull => new[]
        {
            new Subject("destroyed", "x: Handle = Handle(True)", false),
            new Subject("live", "x: Handle = Handle(False)", false),
            new Subject("nullable-destroyed", "x: Handle | None = Handle(True)", false),
            new Subject("nullable-none", "x: Handle | None = None", true),
        },
        Operand.ClrVersion => new[]
        {
            new Subject("value", "x: Version = Version(1, 2)", false),
            new Subject("nullable-value", "x: Version | None = Version(1, 2)", false),
            new Subject("nullable-none", "x: Version | None = None", true),
        },
        Operand.Str => new[]
        {
            new Subject("value", "x: str = \"abc\"", false),
            new Subject("nullable-value", "x: str | None = \"\"", false),
            new Subject("nullable-none", "x: str | None = None", true),
        },
        Operand.Int => new[]
        {
            new Subject("value", "x: int = 0", false),
            new Subject("nullable-value", "x: int | None = 0", false),
            new Subject("nullable-none", "x: int | None = None", true),
        },
        Operand.OptionalClass => new[]
        {
            new Subject("some", "x: W? = Some(W(2))", false),
            new Subject("none", "x: W? = None()", true),
        },
        Operand.OptionalInt => new[]
        {
            new Subject("some", "x: int? = Some(0)", false),
            new Subject("none", "x: int? = None()", true),
        },
        _ => throw new ArgumentOutOfRangeException(nameof(operand)),
    };

    private static Subject[] ClassSubjects(string cls) => new[]
    {
        new Subject("value", $"x: {cls} = {cls}(2)", false),
        new Subject("nullable-value", $"x: {cls} | None = {cls}(2)", false),
        new Subject("nullable-none", $"x: {cls} | None = None", true),
    };

    private static readonly (string Spelling, bool Negated)[] Spellings =
    {
        ("x is None", false), ("x is not None", true), ("None is x", false), ("None is not x", true),
    };

    private static string Body(Position position, string test) => position switch
    {
        Position.Return => $"    return {test}\n",
        Position.If => $"    if {test}:\n        return True\n    return False\n",
        Position.While => $"    n = 0\n    while {test} and n < 1:\n        n += 1\n    return n == 1\n",
        Position.Ternary => $"    return True if {test} else False\n",
        Position.ComprehensionFilter => $"    return len([1 for _i in range(1) if {test}]) == 1\n",
        Position.Assert => $"    try:\n        assert {test}\n        return True\n    except AssertionError:\n        return False\n",
        _ => throw new ArgumentOutOfRangeException(nameof(position)),
    };

    /// <summary>Builds one program per operand: one function per cell, each call isolated so a throw
    /// names its cell. Returns the program and the expected stdout.</summary>
    private static (string Program, string Expected) IsNoneProgram(Operand operand)
    {
        var defs = new StringBuilder(Prelude);
        var calls = new StringBuilder();
        var expected = new StringBuilder();
        var i = 0;
        foreach (var subject in Subjects(operand))
            foreach (var (spelling, negated) in Spellings)
                foreach (var position in Enum.GetValues<Position>())
                {
                    var fn = $"cell_{i++}";
                    defs.Append($"def {fn}() -> bool:\n    {subject.Declaration}\n{Body(position, spelling)}\n");
                    var label = $"{subject.Name}|{spelling}|{position}";
                    calls.Append($"    run(\"{label}\", {fn})\n");
                    var truth = subject.Absent != negated;
                    expected.Append($"{label} {(truth ? "True" : "False")}\n");
                }

        defs.Append("def run(label: str, f: () -> bool):\n    try:\n        print(label, f())\n"
            + "    except Exception:\n        print(label, \"THROW\")\n\n");
        defs.Append("def main():\n").Append(calls);
        return (defs.ToString(), expected.ToString());
    }

    public static TheoryData<Operand> Operands()
    {
        var data = new TheoryData<Operand>();
        foreach (var operand in Enum.GetValues<Operand>())
            data.Add(operand);
        return data;
    }

    [Fact]
    public void Matrix_CoversEveryCell()
    {
        // Anchored to literals: 11 operands; subjects 3+3+3+3+1+4+3+3+3+2+2 = 30; × 4 spellings ×
        // 6 positions = 720 executed cells.
        Enum.GetValues<Operand>().Length.Should().Be(11);
        Enum.GetValues<Operand>().Sum(o => Subjects(o).Length).Should().Be(30);
        Enum.GetValues<Operand>().Sum(o => IsNoneProgram(o).Expected.Split('\n',
            StringSplitOptions.RemoveEmptyEntries).Length).Should().Be(720);
    }

    [Theory]
    [MemberData(nameof(Operands))]
    public void IsNone_IsAReferenceCheck_InEveryPosition(Operand operand)
    {
        var (program, expected) = IsNoneProgram(operand);
        var result = CompileAndExecute(program);

        result.Success.Should().BeTrue(
            $"{string.Join("; ", result.CompilationErrors)} stderr: {result.StandardError}");
        result.StandardOutput.Replace("\r\n", "\n").Should().Be(expected);
    }

    /// <summary>
    /// The lowering is a form that cannot bind a user operator: for a Sharpy class with an
    /// <c>__eq__</c>, no None test prints <c>== null</c> / <c>!= null</c>, and the null PATTERN is there
    /// (positive control: the absence assertion alone would pass on a program that tests nothing).
    /// </summary>
    [Fact]
    public void IsNone_OnAClassWithEq_EmitsTheNullPattern_NeverEqualsNull()
    {
        var result = CompileAndExecute(Prelude + """
            def probe(x: P | None, y: P) -> None:
                print(x is None, x is not None, None is x, None is not y)

            def main():
                probe(None, P(1))
            """);

        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("True False True True");
        result.GeneratedCSharp.Should().Contain("x is null").And.Contain("x is not null")
            .And.Contain("y is not null");
        result.GeneratedCSharp.Should().NotContain("x == null").And.NotContain("x != null")
            .And.NotContain("y != null");
    }

    /// <summary>
    /// <c>==</c> / <c>!=</c> with a None operand on the same operands — python3 values (measured with
    /// the equivalent python program). <c>==</c> runs <c>__eq__</c> (a permissive <c>__eq__</c> says a
    /// live value equals None, as in python3); a None LEFT operand is never dereferenced, and python3's
    /// reflected <c>__eq__</c> is asked when the parameter admits None. <c>== None</c> runs the CLR
    /// overload (a destroyed handle reads as None) — on every operand form, #2238.
    /// </summary>
    [Theory]
    [InlineData("W", "z == None", true)]
    [InlineData("W", "z != None", false)]
    [InlineData("W", "None == z", true)]
    [InlineData("W", "None != z", false)]
    [InlineData("W", "z == e", false)]
    [InlineData("W", "z != e", true)]
    [InlineData("W", "e == z", false)]
    [InlineData("W", "z == z2", true)]
    [InlineData("W", "z != z2", false)]
    [InlineData("W", "e == None", false)]
    [InlineData("W", "None == e", false)]
    [InlineData("P", "z == None", true)]
    [InlineData("P", "None == z", true)]
    [InlineData("P", "z != None", false)]
    [InlineData("P", "z == e", true)]
    [InlineData("P", "z != e", false)]
    [InlineData("P", "z == z2", true)]
    [InlineData("P", "e == None", true)]
    [InlineData("P", "None == e", true)]
    [InlineData("P", "e != None", false)]
    [InlineData("N", "z == None", true)]
    [InlineData("N", "z == e", false)]
    [InlineData("N", "e == None", false)]
    [InlineData("T", "z == None", true)]
    [InlineData("T", "z == z2", true)]
    [InlineData("T", "z != z2", false)]
    [InlineData("Handle", "e == None", true)]
    [InlineData("Handle", "e != None", false)]
    [InlineData("Handle", "None == e", true)]
    [InlineData("Handle", "z == None", true)]
    [InlineData("Version", "e == None", false)]
    [InlineData("Version", "z == None", true)]
    public void Equality_WithANoneOperand_RunsTheOperator_AndNeverThrows(string cls, string test, bool truth)
    {
        // e holds a value (a destroyed one for Handle), z and z2 hold None — all typed `cls | None`.
        var value = cls switch { "Handle" => "Handle(True)", "Version" => "Version(1, 2)", _ => $"{cls}(2)" };
        var result = CompileAndExecute(Prelude + $"""
            def probe() -> bool:
                e: {cls} | None = {value}
                z: {cls} | None = None
                z2: {cls} | None = None
                return {test}

            def main():
                print(probe())
            """);

        result.Success.Should().BeTrue(
            $"{string.Join("; ", result.CompilationErrors)} stderr: {result.StandardError}");
        result.StandardOutput.Trim().Should().Be(truth ? "True" : "False");
    }

    /// <summary>The #2221 refusal is lifted for CLR types (#2238, ruling R-ES): <c>== None</c> on a
    /// NON-nullable CLR reference runs its operator, so the destroyed handle answers <c>== None</c> → True
    /// while <c>is None</c>, the reference check, answers False.</summary>
    [Theory]
    [InlineData("x == None", "True")]
    [InlineData("x != None", "False")]
    [InlineData("None == x", "True")]
    [InlineData("x is None", "False")]
    [InlineData("x is not None", "True")]
    public void EqualsNone_OnANonNullableClrReference_RunsTheOperator(string test, string truth)
    {
        var result = CompileAndExecute(Prelude + $"""
            def main():
                x: Handle = Handle(True)
                print({test})
            """);

        result.Success.Should().BeTrue(
            $"{string.Join("; ", result.CompilationErrors)} stderr: {result.StandardError}");
        result.StandardOutput.Trim().Should().Be(truth);
    }

    /// <summary>
    /// The other None tests that lowered to <c>== null</c> / <c>!= null</c> on a <c>T | None</c>:
    /// truthiness (<c>if x:</c>) and the hoisted <c>??</c> / <c>??=</c> (a right operand that hoists —
    /// a comprehension — takes the manufactured-sink path). Absence is decided without user code: a
    /// permissive <c>__eq__</c> must not make a live value falsy / absent, and a None value must not throw.
    /// </summary>
    [Fact]
    public void TruthinessAndCoalesce_OnAClassWithEq_DecideAbsenceWithoutUserCode()
    {
        var result = CompileAndExecute(Prelude + """
            def pick(cls_v: int) -> P | None:
                if cls_v > 0:
                    return P(cls_v)
                return None

            def pickw(v: int) -> W | None:
                if v > 0:
                    return W(v)
                return None

            def main():
                t = pick(3)
                f = pick(0)
                print("truthy" if t else "falsy", "truthy" if f else "falsy")
                fw = pickw(0)
                print("truthy" if fw else "falsy")
                a = f ?? [P(7) for i in range(1)][0]
                b = t ?? [P(8) for i in range(1)][0]
                print(a.v, b.v)
                c = pick(0)
                c ??= [P(5) for i in range(1)][0]
                d = pick(4)
                d ??= [P(6) for i in range(1)][0]
                print(c.v, d.v)
                g = pickw(0)
                print((g ?? [W(1) for i in range(1)][0]).v)
            """);

        result.Success.Should().BeTrue(
            $"{string.Join("; ", result.CompilationErrors)} stderr: {result.StandardError}");
        result.StandardOutput.Replace("\r\n", "\n").Should().Be("truthy falsy\nfalsy\n7 3\n5 4\n1\n");
        // Positive control that the hoisted arms ran (the comprehension right operand hoists).
        result.GeneratedCSharp.Should().Contain("__coalesceLeft_");
    }

    /// <summary>Narrowing after <c>is not None</c> still narrows in every statement position.</summary>
    [Fact]
    public void IsNotNone_StillNarrows()
    {
        var result = CompileAndExecute(Prelude + """
            def pick(v: int) -> W | None:
                if v > 0:
                    return W(v)
                return None

            def main():
                e = pick(3)
                if e is not None:
                    print(e.v + 1)
                cur = pick(5)
                while cur is not None:
                    print(cur.v)
                    cur = None
                g = pick(7)
                assert g is not None
                print(g.v)
                print(e.v if e is not None else -1)
            """);

        result.Success.Should().BeTrue(
            $"{string.Join("; ", result.CompilationErrors)} stderr: {result.StandardError}");
        result.StandardOutput.Replace("\r\n", "\n").Should().Be("4\n5\n7\n3\n");
    }
}
