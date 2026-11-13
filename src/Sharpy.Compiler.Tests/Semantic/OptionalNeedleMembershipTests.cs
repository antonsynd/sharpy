using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The checker half of plan-4e2867 (D1–D3, #2635, #2647): what <c>TypeChecker.ClassifyMembership</c>
/// accepts, refuses and RECORDS for an <c>in</c>/<c>not in</c> needle. Analysis only — the emitted
/// form and the run-time answer belong to <c>OptionalMembershipDifferentialTests</c> and the
/// fixtures. Every accepted row asserts the recorded <see cref="OperatorLoweringKind"/>, so an
/// "accepted, nothing recorded" row (<c>Native</c>) is a claim, not an absence.
/// </summary>
public class OptionalNeedleMembershipTests
{
    private const string Prelude =
        "from email import EmailMessage\n"
        + "from ipaddress import IPv4Address, IPv4Network\n"
        + "from System.Drawing import Point, PointF, Rectangle, RectangleF\n"
        + "\n"
        + "enum Tone:\n    A = \"a\"\n    B = \"b\"\n"
        + "\n"
        + "class IBag:\n    def __contains__(self, v: int) -> bool:\n        return v == 1\n"
        + "\n";

    private sealed record Analysis(Module Module, SemanticInfo Info, IReadOnlyList<string> Errors);

    private static Analysis Analyze(string declarations, string expression)
    {
        var body = string.Concat(declarations
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => $"    {line}\n"));
        var source = Prelude + "def main() -> None:\n" + body + $"    b: bool = {expression}\n    print(b)\n";
        var options = new CompilerOptions
        {
            // System.Drawing.Primitives is not among the default references here (`sharpyc run`
            // compiles against the whole shared framework); the RectangleF rows import from it.
            References = new[]
            {
                Sharpy.TestInfrastructure.SharpyCoreReference.Location, SharpyStdlibReference.Location,
                typeof(System.Drawing.PointF).Assembly.Location,
            },
            OutputType = "exe",
            WarningsAsErrors = false,
        };
        var result = new Compiler(options, NullLogger.Instance).Analyze(source, "optional_needle_membership.spy");
        Assert.True(result.Module != null && result.SemanticInfo != null, "analysis produced no module:\n" + source);
        var errors = result.Diagnostics.GetErrors().Select(d => $"{d.Code}: {d.Message}").ToList();
        return new Analysis(result.Module!, result.SemanticInfo!, errors);
    }

    private static IEnumerable<Node> Descendants(Node node) =>
        node.GetChildNodes().SelectMany(child => Descendants(child).Prepend(child));

    private static OperatorLoweringKind RecordedKind(Analysis analysis)
    {
        var membership = Descendants(analysis.Module).OfType<BinaryOp>()
            .Single(b => b.Operator is BinaryOperator.In or BinaryOperator.NotIn);
        return analysis.Info.GetOperatorLoweringForIr(membership)?.Kind ?? OperatorLoweringKind.Native;
    }

    // declarations (`;`-separated statements of main), the membership expression, the recorded kind.
    public static TheoryData<string, string, OperatorLoweringKind> Accepted => new()
    {
        // D1: a value-type `T | None` lifted into a `T` slot answers False for None.
        { "x: int | None = None", "x in [1, 2]", OperatorLoweringKind.MembershipNoneIsFalse },
        { "x: int | None = None", "x not in [1, 2]", OperatorLoweringKind.MembershipNoneIsFalse },
        { "errno: int | None = None", "errno in (111, 98)", OperatorLoweringKind.MembershipNoneIsFalse },
        { "x: int | None = None", "x in IBag()", OperatorLoweringKind.MembershipNoneIsFalse },
        { "x: int | None = None", "x in {1: \"a\"}", OperatorLoweringKind.MembershipNoneIsFalse },
        { "x: int | None = None", "x in {1, 2}", OperatorLoweringKind.MembershipNoneIsFalse },
        { "x: int | None = None", "x in range(3)", OperatorLoweringKind.MembershipNoneIsFalse },
        { "x: float | None = None", "x in [1.5]", OperatorLoweringKind.MembershipNoneIsFalse },
        { "x: bool | None = None", "x in [True]", OperatorLoweringKind.MembershipNoneIsFalse },
        { "x: bytes | None = None; ys: list[bytes] = [bytes([1])]", "x in ys", OperatorLoweringKind.MembershipNoneIsFalse },

        // D1: a lifted or plain REFERENCE needle records nothing; the receiver's Contains(null) answers.
        { "x: str | None = None", "x in [\"a\"]", OperatorLoweringKind.Native },
        { "t: Tone | None = None", "t in [\"a\"]", OperatorLoweringKind.Native },
        { "a: IPv4Address | None = None", "a in IPv4Network(\"10.0.0.0/8\")", OperatorLoweringKind.Native },
        { "s: str | None = None", "s in EmailMessage()", OperatorLoweringKind.Native },
        { "t: Tone | None = None", "t in EmailMessage()", OperatorLoweringKind.Native },

        // The plain path comes BEFORE the lift (M4): an optional element type can hold the None.
        { "xs: list[int | None] = [None, 1]; x: int | None = None", "x in xs", OperatorLoweringKind.Native },
        { "xs: list[int | None] = [None, 1]", "None in xs", OperatorLoweringKind.Native },
        { "x: int = 1", "x in [1, 2]", OperatorLoweringKind.Native },
        { "", "\"a\" in \"abc\"", OperatorLoweringKind.Native },

        // D2: a subsequence container raises on None, for a value or a reference needle.
        { "x: str | None = None", "x in \"abc\"", OperatorLoweringKind.MembershipNoneRaises },
        { "x: str | None = None", "x not in \"abc\"", OperatorLoweringKind.MembershipNoneRaises },
        { "t: Tone | None = None", "t in \"abc\"", OperatorLoweringKind.MembershipNoneRaises },
        { "x: int | None = None", "x in bytes([1])", OperatorLoweringKind.MembershipNoneRaises },
        { "x: bytes | None = None", "x in bytes([1])", OperatorLoweringKind.MembershipNoneRaises },

        // D3: every public single-argument Contains overload is a slot (ruling C: int AND bytes).
        { "", "1 in bytes([1, 2])", OperatorLoweringKind.Native },
        { "", "bytes([1]) in bytes([1, 2])", OperatorLoweringKind.Native },
        { "", "IPv4Address(\"10.0.0.1\") in IPv4Network(\"10.0.0.0/8\")", OperatorLoweringKind.Native },
        { "", "\"subject\" in EmailMessage()", OperatorLoweringKind.Native },

        // D3 (#2647): a CLR-backed container takes the needles its written `y.Contains(x)` takes.
        // RectangleF declares Contains(PointF) and Contains(RectangleF); C# reaches both from the
        // integer types by op_Implicit, and the CLR call seam's verdict says so here.
        { "rf: RectangleF = RectangleF(0.0f, 0.0f, 10.0f, 10.0f)", "Point(1, 1) in rf", OperatorLoweringKind.Native },
        { "rf: RectangleF = RectangleF(0.0f, 0.0f, 10.0f, 10.0f)", "Point(1, 1) not in rf", OperatorLoweringKind.Native },
        { "rf: RectangleF = RectangleF(0.0f, 0.0f, 10.0f, 10.0f)", "Rectangle(1, 1, 2, 2) in rf", OperatorLoweringKind.Native },
        { "rf: RectangleF = RectangleF(0.0f, 0.0f, 10.0f, 10.0f)", "PointF(1.0f, 1.0f) in rf", OperatorLoweringKind.Native },
        { "r: Rectangle = Rectangle(0, 0, 10, 10)", "Point(1, 1) in r", OperatorLoweringKind.Native },
        { "r: Rectangle = Rectangle(0, 0, 10, 10)", "Rectangle(1, 1, 2, 2) in r", OperatorLoweringKind.Native },
    };

    [Theory]
    [MemberData(nameof(Accepted))]
    public void Needle_IsAccepted_AndRecordsItsKind(string declarations, string expression, OperatorLoweringKind kind)
    {
        var analysis = Analyze(declarations, expression);

        Assert.True(analysis.Errors.Count == 0,
            $"`{expression}` ({declarations}) is refused: " + string.Join("; ", analysis.Errors));
        Assert.Equal(kind, RecordedKind(analysis));
    }

    // declarations, the membership expression.
    public static TheoryData<string, string> Refused => new()
    {
        // Ruling A: the literal None into a non-optional slot.
        { "", "None in [1, 2]" },
        { "", "None in \"abc\"" },
        { "", "None in bytes([1])" },
        { "", "None in EmailMessage()" },
        { "", "None in IPv4Network(\"10.0.0.0/8\")" },

        // D3: a needle no Contains overload takes (SPY0908 before #2647).
        { "n: int64 = 1", "n in bytes([1])" },
        { "", "\"a\" in bytes([1])" },
        { "", "\"x\" in IPv4Network(\"10.0.0.0/8\")" },
        { "", "1 in EmailMessage()" },

        // D3: the CLR call seam's verdict is asked only where it is a fact about the needle's own
        // CLR type. No op_Implicit reaches PointF or RectangleF from these, and a needle with no
        // CLR type at all (IBag) is not waved through on "PointF converts from something".
        { "rf: RectangleF = RectangleF(0.0f, 0.0f, 10.0f, 10.0f)", "\"x\" in rf" },
        { "rf: RectangleF = RectangleF(0.0f, 0.0f, 10.0f, 10.0f)", "1 in rf" },
        { "rf: RectangleF = RectangleF(0.0f, 0.0f, 10.0f, 10.0f)", "IBag() in rf" },
        { "rf: RectangleF = RectangleF(0.0f, 0.0f, 10.0f, 10.0f)", "None in rf" },
        { "r: Rectangle = Rectangle(0, 0, 10, 10); pf: PointF = PointF(1.0f, 1.0f)", "pf in r" },

        // Ruling B: bool, float and decimal needles into range/bytes stay refused.
        { "", "True in range(3)" },
        { "", "1.5 in range(3)" },
        { "d: decimal = decimal(1)", "d in range(3)" },
        { "", "True in bytes([1])" },
        { "", "1.5 in bytes([1])" },
        { "d: decimal = decimal(1)", "d in bytes([1])" },

        // The lift changes nothing about the underlying type: `T | None` is refused where `T` is.
        { "x: str | None = None", "x in [1, 2]" },
        { "x: int64 | None = None", "x in bytes([1])" },
        { "x: str | None = None", "x in IPv4Network(\"10.0.0.0/8\")" },
        { "x: bool | None = None", "x in range(3)" },
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public void Needle_IsRefused_WithInvalidBinaryOperation_AndRecordsNothing(string declarations, string expression)
    {
        var analysis = Analyze(declarations, expression);

        var error = Assert.Single(analysis.Errors);
        Assert.StartsWith(DiagnosticCodes.Semantic.InvalidBinaryOperation + ": ", error);
        Assert.Contains("does not support operator 'in'", error);
        Assert.Equal(OperatorLoweringKind.Native, RecordedKind(analysis));
    }

    [Fact]
    public void ChainLink_CarriesTheSameKindAsTheBinaryForm()
    {
        var analysis = Analyze("x: int | None = None; xs: list[int] = [1, 2]", "x in xs in [[1, 2]]");

        Assert.True(analysis.Errors.Count == 0, string.Join("; ", analysis.Errors));
        var chain = Descendants(analysis.Module).OfType<ComparisonChain>().Single();
        var lowering = analysis.Info.GetComparisonChainLowering(chain);
        Assert.NotNull(lowering);
        Assert.Equal(OperatorLoweringKind.MembershipNoneIsFalse, lowering!.Links[0].Kind);
        Assert.Equal(OperatorLoweringKind.Native, lowering.Links[1].Kind);
    }

    /// <summary>M8: no single slot is pushed onto the needle's store for a CLR-backed container, so a
    /// conditional needle of EITHER slot type is not refused against the other slot (whichever
    /// overload reflection lists first, one of the two rows is the falsifying one).</summary>
    [Theory]
    [InlineData("c: bool = True; p: bytes = bytes([1]); q: bytes = bytes([2])", "(p if c else q) in bytes([1, 2])")]
    [InlineData("c: bool = True; m: int = 1; n: int = 2", "(m if c else n) in bytes([1, 2])")]
    public void ClrBackedContainer_PushesNoSlotOntoTheNeedleStore(string declarations, string expression)
    {
        var analysis = Analyze(declarations, expression);

        Assert.True(analysis.Errors.Count == 0, string.Join("; ", analysis.Errors));
        Assert.Equal(OperatorLoweringKind.Native, RecordedKind(analysis));
    }
}
