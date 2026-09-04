using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The narrow-width arithmetic matrix (#1666): <b>width × operator × store × target kind</b>,
/// generated rather than enumerated, so a defect in any arm of the contract fails a cell instead
/// of slipping between hand-written examples.
///
/// <para><b>Contract under test:</b> the recorded type of an arithmetic expression IS the CLR
/// result type of the expression the emitter produces — and a store of that result is admitted
/// exactly when C# admits it. Two consequences the matrix is built from:</para>
/// <list type="number">
/// <item>Every sub-32-bit width promotes to <c>int32</c> (C# §12.4.7), so a plain store of any
/// arithmetic result back into the narrow type is SPY0220 — the promoted width is named in the
/// message. <c>uint32</c> keeps its width for <c>+ - * &amp; | ^ &lt;&lt; &gt;&gt; ~</c> but not
/// for <c>// % **</c> (lowered to <c>Builtins.FloorDiv</c>/<c>FloorMod</c>/<c>CheckedIntPow</c>
/// calls whose bound overload returns <c>long</c>) nor for unary <c>-</c> (§12.9.3 widens
/// <c>uint</c> to <c>long</c>).</item>
/// <item>Augmented assignment narrows by C#'s own compound-assignment rule (§12.21.4):
/// <c>x op= y</c> is <c>x = (T)(x op y)</c> when <c>y</c> is implicitly convertible to <c>T</c> —
/// a narrow-or-equal width OR an in-range integer constant — <b>or the operator is a shift</b>,
/// whose right operand is a count and never has to fit the target. A wider variable RHS
/// (<c>x8 += i</c>) and an out-of-range constant (<c>x8 += 300</c>) stay SPY0220: those two are
/// the rule's positive controls, and they are the two arms that would silently vanish if the
/// decision were widened to "always narrow".</item>
/// </list>
///
/// <para><b>Target kind is an axis on purpose.</b> The decision has no target-kind input — it is
/// the single augmented-result check in <c>CheckAssignment</c>, recording <c>NarrowTo</c> on the
/// <c>Assignment</c> node that every emitter arm routes through <c>GenerateAugmentedValue</c> —
/// so the identifier / attribute / index / self / nested-attribute cells are what falsifies that
/// claim if any arm ever grows its own copy.</para>
///
/// <para><b>Base classification (measured with <c>sharpyc run</c>, never <c>emit</c>, against the
/// CLI built at 6e2b68812 — the commit this batch's plan was written from):</b>
/// plain narrow store SPY0908/CS0266 · print worked · narrow argument SPY0908/CS1503 ·
/// narrow return SPY0908/CS0266 · augmented narrow-RHS (identifier, attribute, index)
/// SPY0908/CS0266 · augmented constant-RHS SPY0220 · augmented int-variable RHS SPY0220 ·
/// augmented shift by an int variable SPY0908/CS0266 · augmented out-of-range constant SPY0220 ·
/// wrap cells SPY0220 · <c>uint32</c> <c>//</c>, <c>%</c>, <c>**</c>, unary <c>-</c> and their
/// augmented forms SPY0908/CS0266 · <c>-uint64</c> SPY0908/CS0023 ·
/// <c>uint64 ** uint64</c> SPY0908/CS0266.
/// Every cell this matrix newly ACCEPTS was one of those SPY0908s or SPY0220s; every cell it
/// newly REFUSES (the <c>uint32</c>/<c>uint64</c> typing arms) was SPY0908 there, so no program
/// that ran before is refused now.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class NarrowWidthArithmeticMatrixTests : IntegrationTestBase
{
    public NarrowWidthArithmeticMatrixTests(ITestOutputHelper output) : base(output) { }

    // ───────────────────────────── the axes ─────────────────────────────

    private sealed record Width(string Name, int Bits, bool Signed, string OutOfRangeConstant)
    {
        /// <summary>The unchecked wrap the emitted cast performs — the spec's overflow policy.</summary>
        public long Narrow(long value) => (Signed, Bits) switch
        {
            (true, 8) => (sbyte)value,
            (true, 16) => (short)value,
            (false, 8) => (byte)value,
            (false, 16) => (ushort)value,
            (false, 32) => (uint)value,
            _ => value,
        };
    }

    private static readonly Width[] Widths =
    {
        new("int8", 8, true, "300"),
        new("int16", 16, true, "40000"),
        new("uint8", 8, false, "300"),
        new("uint16", 16, false, "70000"),
        new("uint32", 32, false, "5000000000"),
    };

    private sealed record Op(string Symbol, bool Unary)
    {
        public string Id => Unary ? $"u{Symbol}" : Symbol;
    }

    private static readonly Op[] Ops =
    {
        new("+", false), new("-", false), new("*", false),
        new("&", false), new("|", false), new("^", false),
        new("<<", false), new(">>", false),
        new("**", false), new("//", false), new("%", false),
        new("-", true), new("~", true),
    };

    private static Op[] BinaryOps => Ops.Where(o => !o.Unary).ToArray();

    private static readonly string[] TargetKinds = { "identifier", "attribute", "index" };

    /// <summary>The four RHS shapes the §12.21.4 rule sorts differently.</summary>
    private static readonly string[] RhsKinds = { "narrow", "const", "int-var", "out-of-range-const" };

    private const long LeftValue = 7;
    private const long RightValue = 2;

    // ───────────────────────── the two rules, once ─────────────────────────

    /// <summary>The recorded — and emitted — result type of <c>a op b</c> at width <paramref name="w"/>.</summary>
    private static string ResultTypeOf(Width w, Op op)
    {
        if (w.Name != "uint32")
            return "int32";

        if (op.Unary)
            return op.Symbol == "-" ? "int64" : "uint32";

        return op.Symbol is "//" or "%" or "**" ? "int64" : "uint32";
    }

    /// <summary>Whether C# narrows <c>x op= rhs</c> back into <paramref name="w"/> (§12.21.4).</summary>
    private static bool AugmentedIsAccepted(Op op, string rhsKind) => rhsKind switch
    {
        "narrow" => true,
        "const" => true,
        "int-var" => op.Symbol is "<<" or ">>",
        "out-of-range-const" => false,     // shifts are declared N/A, not refused — see NaCells
        _ => throw new ArgumentOutOfRangeException(nameof(rhsKind), rhsKind, null),
    };

    // ───────────────────────── expected values ─────────────────────────

    private static long FloorDiv(long x, long y)
    {
        var q = x / y;
        return (x % y != 0 && (x < 0) != (y < 0)) ? q - 1 : q;
    }

    private static long FloorMod(long x, long y)
    {
        var r = x % y;
        return (r != 0 && (r < 0) != (y < 0)) ? r + y : r;
    }

    private static long IntPow(long x, long y)
    {
        long result = 1;
        for (long i = 0; i < y; i++)
            result *= x;
        return result;
    }

    private static long RawResult(Op op) => op.Unary
        ? (op.Symbol == "-" ? -LeftValue : ~LeftValue)
        : op.Symbol switch
        {
            "+" => LeftValue + RightValue,
            "-" => LeftValue - RightValue,
            "*" => LeftValue * RightValue,
            "&" => LeftValue & RightValue,
            "|" => LeftValue | RightValue,
            "^" => LeftValue ^ RightValue,
            "<<" => LeftValue << (int)RightValue,
            ">>" => LeftValue >> (int)RightValue,
            "**" => IntPow(LeftValue, RightValue),
            "//" => FloorDiv(LeftValue, RightValue),
            "%" => FloorMod(LeftValue, RightValue),
            _ => throw new ArgumentOutOfRangeException(nameof(op), op.Symbol, null),
        };

    /// <summary>What <c>print(a op b)</c> writes: the raw result read at its own result type.</summary>
    private static string ExpressionValue(Width w, Op op)
    {
        var raw = RawResult(op);
        return ResultTypeOf(w, op) == "uint32"
            ? ((uint)raw).ToString(CultureInfo.InvariantCulture)
            : raw.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>What <c>x op= rhs; print(x)</c> writes: the result narrowed by the emitted cast.</summary>
    private static string AugmentedValue(Width w, Op op)
        => w.Narrow(RawResult(op)).ToString(CultureInfo.InvariantCulture);

    // ───────────────────────── cells and programs ─────────────────────────

    private sealed record Cell(
        string Id,
        string Store,
        string Width,
        string Op,
        string TargetKind,
        bool Accepted,
        int Line,
        string Expected,
        string? Code);

    private sealed record NaCell(string Store, string Op, string Reason);

    /// <summary>
    /// The cells the matrix declares inapplicable, with the reason each is not a defect.
    /// </summary>
    private static readonly NaCell[] NaCells =
        (from store in RhsKinds.Select(r => $"augmented/{r}")
         from op in Ops.Where(o => o.Unary)
         select new NaCell(store, op.Id,
             "unary operators have no augmented form — there is no `x =- y` statement"))
        .Concat(
            from op in BinaryOps.Where(o => o.Symbol is "<<" or ">>")
            select new NaCell("augmented/out-of-range-const", op.Id,
                "a shift's RHS is a COUNT, not a value of the target type (§12.21.4's carve-out): "
                + "an out-of-range shift count is admitted and masked, so there is no refusal to assert"))
        .ToArray();

    private sealed record MatrixProgram(string Id, string Source, bool ShouldCompile, IReadOnlyList<Cell> Cells);

    private sealed class SourceBuilder
    {
        private readonly List<string> _lines = new();
        public int Add(string line) { _lines.Add(line); return _lines.Count; }
        public string Text => string.Join("\n", _lines) + "\n";
    }

    private static IReadOnlyList<MatrixProgram> BuildPrograms()
    {
        var programs = new List<MatrixProgram>();
        foreach (var w in Widths)
        {
            programs.AddRange(PlainDeclPrograms(w));
            programs.Add(PrintProgram(w));
            programs.AddRange(ArgumentPrograms(w));
            programs.AddRange(ReturnPrograms(w));
            foreach (var target in TargetKinds)
                foreach (var rhs in RhsKinds)
                    programs.AddRange(AugmentedPrograms(w, target, rhs));
        }

        programs.Add(WrapProgram());
        programs.AddRange(SelfAndNestedAttributePrograms());
        programs.AddRange(UnsignedWideTypingPrograms());
        programs.AddRange(MixedSignednessPrograms());
        programs.AddRange(ConstantOperandPrograms());
        programs.AddRange(AugmentedMixedPrograms());
        programs.AddRange(IntegerPowerPrograms());
        programs.AddRange(MinMaxPrograms());
        return programs;
    }

    // ── store: plain declaration of the narrow type ──
    private static IEnumerable<MatrixProgram> PlainDeclPrograms(Width w)
    {
        foreach (var accepted in new[] { true, false })
        {
            var ops = Ops.Where(o => (ResultTypeOf(w, o) == w.Name) == accepted).ToList();
            if (ops.Count == 0)
                continue;

            var sb = new SourceBuilder();
            sb.Add("def main() -> None:");
            sb.Add($"    a: {w.Name} = {LeftValue}");
            sb.Add($"    b: {w.Name} = {RightValue}");

            var cells = new List<Cell>();
            for (var i = 0; i < ops.Count; i++)
            {
                var op = ops[i];
                var expr = op.Unary ? $"{op.Symbol}a" : $"a {op.Symbol} b";
                var line = sb.Add($"    c{i}: {w.Name} = {expr}");
                if (accepted)
                    sb.Add($"    print(c{i})");

                cells.Add(new Cell(
                    $"plain-decl/{w.Name}/{op.Id}", "plain decl", w.Name, op.Id, "-",
                    accepted, line,
                    accepted
                        ? ExpressionValue(w, op)
                        : $"Cannot assign type '{ResultTypeOf(w, op)}' to variable of type '{w.Name}'",
                    accepted ? null : DiagnosticCodes.Semantic.TypeMismatch));
            }

            yield return new MatrixProgram(
                $"plain-decl/{w.Name}/{(accepted ? "accepted" : "refused")}", sb.Text, accepted, cells);
        }
    }

    // ── store: print (never refused — the promoted result is printable at every width) ──
    private static MatrixProgram PrintProgram(Width w)
    {
        var sb = new SourceBuilder();
        sb.Add("def main() -> None:");
        sb.Add($"    a: {w.Name} = {LeftValue}");
        sb.Add($"    b: {w.Name} = {RightValue}");

        var cells = new List<Cell>();
        foreach (var op in Ops)
        {
            var expr = op.Unary ? $"{op.Symbol}a" : $"a {op.Symbol} b";
            var line = sb.Add($"    print({expr})");
            cells.Add(new Cell(
                $"print/{w.Name}/{op.Id}", "print", w.Name, op.Id, "-",
                true, line, ExpressionValue(w, op), null));
        }

        return new MatrixProgram($"print/{w.Name}", sb.Text, true, cells);
    }

    // ── store: argument bound to a narrow parameter ──
    private static IEnumerable<MatrixProgram> ArgumentPrograms(Width w)
    {
        foreach (var accepted in new[] { true, false })
        {
            var ops = Ops.Where(o => (ResultTypeOf(w, o) == w.Name) == accepted).ToList();
            if (ops.Count == 0)
                continue;

            var sb = new SourceBuilder();
            sb.Add($"def take(v: {w.Name}) -> None:");
            sb.Add("    print(v)");
            sb.Add("");
            sb.Add("def main() -> None:");
            sb.Add($"    a: {w.Name} = {LeftValue}");
            sb.Add($"    b: {w.Name} = {RightValue}");

            var cells = new List<Cell>();
            foreach (var op in ops)
            {
                var expr = op.Unary ? $"{op.Symbol}a" : $"a {op.Symbol} b";
                var line = sb.Add($"    take({expr})");
                cells.Add(new Cell(
                    $"argument/{w.Name}/{op.Id}", "argument", w.Name, op.Id, "-",
                    accepted, line,
                    accepted
                        ? ExpressionValue(w, op)
                        : $"Cannot pass argument of type '{ResultTypeOf(w, op)}' to parameter of type '{w.Name}'",
                    accepted ? null : DiagnosticCodes.Semantic.TypeMismatch));
            }

            yield return new MatrixProgram(
                $"argument/{w.Name}/{(accepted ? "accepted" : "refused")}", sb.Text, accepted, cells);
        }
    }

    // ── store: return from a def annotated with the narrow type ──
    private static IEnumerable<MatrixProgram> ReturnPrograms(Width w)
    {
        foreach (var accepted in new[] { true, false })
        {
            var ops = Ops.Where(o => (ResultTypeOf(w, o) == w.Name) == accepted).ToList();
            if (ops.Count == 0)
                continue;

            var sb = new SourceBuilder();
            var cells = new List<Cell>();
            for (var i = 0; i < ops.Count; i++)
            {
                var op = ops[i];
                var expr = op.Unary ? $"{op.Symbol}a" : $"a {op.Symbol} b";
                sb.Add($"def r{i}() -> {w.Name}:");
                sb.Add($"    a: {w.Name} = {LeftValue}");
                sb.Add($"    b: {w.Name} = {RightValue}");
                var line = sb.Add($"    return {expr}");
                sb.Add("");

                cells.Add(new Cell(
                    $"return/{w.Name}/{op.Id}", "return", w.Name, op.Id, "-",
                    accepted, line,
                    accepted
                        ? ExpressionValue(w, op)
                        : $"Cannot return type '{ResultTypeOf(w, op)}' from function expecting '{w.Name}'",
                    accepted ? null : DiagnosticCodes.Semantic.MissingReturnValue));
            }

            sb.Add("def main() -> None:");
            for (var i = 0; i < ops.Count; i++)
                sb.Add($"    print(r{i}())");

            yield return new MatrixProgram(
                $"return/{w.Name}/{(accepted ? "accepted" : "refused")}", sb.Text, accepted, cells);
        }
    }

    // ── store: augmented assignment, crossed with target kind and RHS shape ──
    private static IEnumerable<MatrixProgram> AugmentedPrograms(Width w, string targetKind, string rhsKind)
    {
        var applicable = BinaryOps
            .Where(o => !(rhsKind == "out-of-range-const" && o.Symbol is "<<" or ">>"))
            .ToList();

        foreach (var accepted in new[] { true, false })
        {
            var ops = applicable.Where(o => AugmentedIsAccepted(o, rhsKind) == accepted).ToList();
            if (ops.Count == 0)
                continue;

            var rhs = rhsKind switch
            {
                "narrow" => "y",
                "const" => RightValue.ToString(CultureInfo.InvariantCulture),
                "int-var" => "i",
                _ => w.OutOfRangeConstant,
            };

            var sb = new SourceBuilder();
            if (targetKind == "attribute")
            {
                sb.Add("class Box:");
                for (var i = 0; i < ops.Count; i++)
                    sb.Add($"    n{i}: {w.Name} = {LeftValue}");
                sb.Add("");
            }

            sb.Add("def main() -> None:");
            sb.Add($"    y: {w.Name} = {RightValue}");
            sb.Add($"    i: int = {RightValue}");
            sb.Add($"    seed: {w.Name} = {LeftValue}");
            sb.Add("    print(y == y and i == i and seed == seed)");

            switch (targetKind)
            {
                case "attribute":
                    sb.Add("    box: Box = Box()");
                    break;
                case "index":
                    sb.Add($"    xs: list[{w.Name}] = []");
                    for (var i = 0; i < ops.Count; i++)
                        sb.Add("    xs.append(seed)");
                    break;
                default:
                    for (var i = 0; i < ops.Count; i++)
                        sb.Add($"    x{i}: {w.Name} = {LeftValue}");
                    break;
            }

            var cells = new List<Cell>();
            for (var i = 0; i < ops.Count; i++)
            {
                var op = ops[i];
                var target = targetKind switch
                {
                    "attribute" => $"box.n{i}",
                    "index" => $"xs[{i}]",
                    _ => $"x{i}",
                };

                var line = sb.Add($"    {target} {op.Symbol}= {rhs}");
                if (accepted)
                    sb.Add($"    print({target})");

                cells.Add(new Cell(
                    $"augmented/{rhsKind}/{w.Name}/{targetKind}/{op.Id}",
                    $"augmented/{rhsKind}", w.Name, op.Id, targetKind,
                    accepted, line,
                    accepted
                        ? AugmentedValue(w, op)
                        : $"of augmented assignment is not assignable to target type '{w.Name}'",
                    accepted ? null : DiagnosticCodes.Semantic.TypeMismatch));
            }

            yield return new MatrixProgram(
                $"augmented/{rhsKind}/{w.Name}/{targetKind}/{(accepted ? "accepted" : "refused")}",
                sb.Text, accepted, cells);
        }
    }

    // ── the narrowing cast's wrap policy: the values only the cast can produce ──
    private static MatrixProgram WrapProgram()
    {
        var sb = new SourceBuilder();
        sb.Add("def main() -> None:");

        var cells = new List<Cell>();
        void Wrap(string width, string init, string op, string rhs, string expected)
        {
            var name = $"w{cells.Count}";
            sb.Add($"    {name}: {width} = {init}");
            var line = sb.Add($"    {name} {op}= {rhs}");
            sb.Add($"    print({name})");
            cells.Add(new Cell($"wrap/{width}/{init}{op}={rhs}", "augmented/wrap", width, op, "identifier",
                true, line, expected, null));
        }

        // Each expected value is C#'s own unchecked narrowing of the promoted result — the policy
        // `arithmetic_operators.md` states, and the reason the cast may not be `checked`.
        Wrap("int8", "127", "+", "2", "-127");
        Wrap("int8", "-128", "-", "1", "127");
        Wrap("int16", "32767", "+", "1", "-32768");
        Wrap("uint8", "0", "-", "1", "255");
        Wrap("uint16", "0", "-", "1", "65535");
        Wrap("uint32", "0", "-", "1", "4294967295");
        Wrap("int8", "5", "<<", "300", "0");   // C# masks the count: 300 & 31 == 12, (sbyte)20480 == 0

        return new MatrixProgram("wrap", sb.Text, true, cells);
    }

    // ── the two attribute shapes the identifier axis cannot reach ──
    private static IEnumerable<MatrixProgram> SelfAndNestedAttributePrograms()
    {
        var sb = new SourceBuilder();
        sb.Add("class Inner:");
        sb.Add("    n: int8 = 7");
        sb.Add("");
        sb.Add("class Outer:");
        sb.Add("    inner: Inner = Inner()");
        sb.Add("");
        sb.Add("class Counter:");
        sb.Add("    n: int8 = 7");
        sb.Add("    m: int8 = 7");
        sb.Add("");
        sb.Add("    def bump(self, y: int8) -> None:");
        var selfNarrow = sb.Add("        self.n += y");
        sb.Add("");
        sb.Add("    def halve(self) -> None:");
        var selfConst = sb.Add("        self.m //= 2");
        sb.Add("");
        sb.Add("def main() -> None:");
        sb.Add("    y: int8 = 2");
        sb.Add("    c: Counter = Counter()");
        sb.Add("    c.bump(y)");
        sb.Add("    c.halve()");
        sb.Add("    print(c.n)");
        sb.Add("    print(c.m)");
        sb.Add("    o: Outer = Outer()");
        var nestedNarrow = sb.Add("    o.inner.n += y");
        sb.Add("    print(o.inner.n)");
        var nestedConst = sb.Add("    o.inner.n %= 4");
        sb.Add("    print(o.inner.n)");

        yield return new MatrixProgram("nested-targets", sb.Text, true, new[]
        {
            new Cell("augmented/narrow/int8/self/+", "augmented/narrow", "int8", "+", "self",
                true, selfNarrow, "9", null),
            new Cell("augmented/const/int8/self/floordiv", "augmented/const", "int8", "//", "self",
                true, selfConst, "3", null),
            new Cell("augmented/narrow/int8/nested-attribute/+", "augmented/narrow", "int8", "+",
                "nested-attribute", true, nestedNarrow, "9", null),
            new Cell("augmented/const/int8/nested-attribute/%", "augmented/const", "int8", "%",
                "nested-attribute", true, nestedConst, "1", null),
        });
    }

    // ── uint64: the width whose `//`, `%` and `~` keep their type and whose `-` has no operator ──

    /// <summary>
    /// The <c>uint64</c> expressions that keep their own width, declared as data so the matrix's
    /// totality count is computed over the axis rather than carrying a magic constant.
    /// <c>Builtins.FloorDiv</c>/<c>FloorMod</c> DO have a <c>(ulong, ulong)</c> overload (#1662)
    /// and <c>CheckedIntPow</c> now does too (#1700), so unlike <c>uint32</c> these do not widen;
    /// <c>~</c> is predefined on <c>ulong</c>.
    /// </summary>
    private static readonly (string Expression, string Expected)[] UnsignedWideKeeps =
    {
        ("a + b", "9"),
        ("a // b", "3"),
        ("a % b", "1"),
        ("~a", "18446744073709551608"),
        ("a ** b", "49"),
    };

    /// <summary>
    /// Unary <c>-</c> on <c>ulong</c> matches NO predefined C# operator (§12.9.3 / CS0023): a named
    /// refusal, not an emitter ICE.
    /// </summary>
    private static readonly (string Expression, string Message)[] UnsignedWideRefusals =
    {
        ("-a", "Type 'uint64' does not support unary operator '-'"),
    };

    private static IEnumerable<MatrixProgram> UnsignedWideTypingPrograms()
    {
        var sb = new SourceBuilder();
        sb.Add("def main() -> None:");
        sb.Add("    a: uint64 = 7");
        sb.Add("    b: uint64 = 2");
        var keep = new List<Cell>();
        for (var i = 0; i < UnsignedWideKeeps.Length; i++)
        {
            var (expr, expected) = UnsignedWideKeeps[i];
            sb.Add($"    k{i}: uint64 = {expr}");
            var line = sb.Add($"    print(k{i})");
            keep.Add(new Cell($"plain-decl/uint64/{expr}", "plain decl", "uint64", expr, "-",
                true, line, expected, null));
        }

        yield return new MatrixProgram("uint64/accepted", sb.Text, true, keep);

        foreach (var (expr, message) in UnsignedWideRefusals)
        {
            var neg = new SourceBuilder();
            neg.Add("def main() -> None:");
            neg.Add("    a: uint64 = 3");
            var negLine = neg.Add($"    print({expr})");
            yield return new MatrixProgram($"uint64/refused/{expr}", neg.Text, false, new[]
            {
                new Cell($"unary/uint64/{expr}", "unary", "uint64", "u-", "-", false, negLine,
                    message, DiagnosticCodes.Semantic.InvalidUnaryOperation),
            });
        }
    }

    // ─────────── mixed-signedness pairs, the constant axis and `**` (#1699, #1700) ───────────

    /// <summary>
    /// An ordered operand pair and the type C# ECMA-334 §12.4.7 promotes it to — <c>null</c> when
    /// C# has no predefined operator for the pair (CS0034 / CS0019), which Sharpy must refuse by
    /// name instead of handing it to Roslyn. Written out per pair, never derived from the rule the
    /// implementation uses.
    /// </summary>
    private sealed record MixedPair(string Left, string Right, string? Promoted)
    {
        public string Id => $"{Left}*{Right}";
    }

    private static readonly MixedPair[] MixedPairs =
    {
        new("uint32", "int16",  "int64"),
        new("uint32", "int32",  "int64"),
        new("uint32", "uint16", "uint32"),
        new("uint64", "int32",  null),
        new("uint64", "int64",  null),
        new("uint64", "uint32", "uint64"),
        new("int64",  "uint64", null),
    };

    /// <summary>
    /// The operator families that share the promotion table, each with the printed form its
    /// accepted cell uses. Shifts are NOT here on purpose — a shift count is not promoted with the
    /// left operand (§12.4.7's carve-out) — and appear as the exempt control in
    /// <see cref="ShiftAndUnaryControlsAreUnaffected"/>. The comparison forms print two values so
    /// the cell's line is never the bare <c>True</c> the value reader filters out as prelude.
    /// </summary>
    private sealed record MixedFamily(string Id, string Operator, string Expression, string Value, bool Stores);

    private static readonly MixedFamily[] MixedFamilies =
    {
        new("arith", "+", "a + b", "9", true),
        new("cmp", ">", "a > b, a < b", "True False", false),
        new("eq", "==", "a == b, a != b", "False True", false),
        new("bitwise", "&", "a & b", "2", true),
    };

    /// <summary>
    /// {arith, cmp, eq, bitwise} x mixed pair. A pair C# promotes runs and stores into the promoted
    /// type; a pair it cannot is SPY0222 naming both types in EVERY family — the comparison and
    /// equality arms included, which returned <c>bool</c> unconditionally and sent
    /// <c>uint64 &gt; int32</c> to Roslyn as a native <c>&gt;</c> (SPY0908 / CS0034).
    /// </summary>
    private static IEnumerable<MatrixProgram> MixedSignednessPrograms()
    {
        foreach (var pair in MixedPairs)
        {
            var sb = new SourceBuilder();
            sb.Add("def main() -> None:");
            sb.Add($"    a: {pair.Left} = {LeftValue}");
            sb.Add($"    b: {pair.Right} = {RightValue}");

            var cells = new List<Cell>();
            var stored = 0;
            foreach (var family in MixedFamilies)
            {
                int line;
                if (pair.Promoted == null)
                {
                    line = sb.Add($"    print(a {family.Operator} b)");
                    cells.Add(new Cell(
                        $"mixed/{family.Id}/{pair.Id}", $"mixed/{family.Id}", pair.Id, family.Id, "-",
                        false, line,
                        $"Type '{pair.Left}' does not support operator '{family.Operator}' "
                        + $"with operand of type '{pair.Right}'",
                        DiagnosticCodes.Semantic.InvalidBinaryOperation));
                    continue;
                }

                if (family.Stores)
                {
                    sb.Add($"    c{stored}: {pair.Promoted} = {family.Expression}");
                    line = sb.Add($"    print(c{stored})");
                    stored++;
                }
                else
                {
                    line = sb.Add($"    print({family.Expression})");
                }

                cells.Add(new Cell(
                    $"mixed/{family.Id}/{pair.Id}", $"mixed/{family.Id}", pair.Id, family.Id, "-",
                    true, line, family.Value, null));
            }

            yield return new MatrixProgram(
                $"mixed/{pair.Id}/{(pair.Promoted == null ? "refused" : "accepted")}",
                sb.Text, pair.Promoted != null, cells);
        }

        // The store column: the recorded type is APPLIED, so a store back into the left operand's
        // type is refused BY NAME when promotion widened it (`c: uint32 = a + b` for uint32*int16
        // was SPY0908 / CS0266) while the promoted-type store above runs.
        foreach (var pair in MixedStoreColumn)
        {
            var sb = new SourceBuilder();
            sb.Add("def main() -> None:");
            sb.Add($"    a: {pair.Left} = {LeftValue}");
            sb.Add($"    b: {pair.Right} = {RightValue}");
            var line = sb.Add($"    c: {pair.Left} = a + b");

            yield return new MatrixProgram($"mixed/store/{pair.Id}", sb.Text, false, new[]
            {
                new Cell($"mixed/store/{pair.Id}", "mixed/store", pair.Id, "+", "-",
                    false, line,
                    $"Cannot assign type '{pair.Promoted}' to variable of type '{pair.Left}'",
                    DiagnosticCodes.Semantic.TypeMismatch),
            });
        }

        // decimal x float64 is the one NON-integer pair promotion refuses, and the comparison arm
        // admitted it (SPY0908 / CS0019) for exactly the reason the integer pairs slipped through.
        foreach (var (leftDecl, rightDecl, pairId, family, expr, message) in NonIntegerRefusals)
        {
            var dec = new SourceBuilder();
            dec.Add("def main() -> None:");
            dec.Add(leftDecl);
            dec.Add(rightDecl);
            var decLine = dec.Add($"    print({expr})");
            yield return new MatrixProgram($"mixed/{pairId}/refused", dec.Text, false, new[]
            {
                new Cell($"mixed/{family}/{pairId}", $"mixed/{family}", pairId, family, "-",
                    false, decLine, message, DiagnosticCodes.Semantic.InvalidBinaryOperation),
            });
        }
    }

    /// <summary>
    /// The non-integer pairs the same promotion answer refuses. <c>decimal</c> never mixes with a
    /// float kind (CPython raises TypeError for <c>Decimal(7) + 1.5</c>), and the comparison arm
    /// must refuse it for the same reason arithmetic does.
    /// </summary>
    private static readonly (string LeftDecl, string RightDecl, string PairId, string Family,
        string Expression, string Message)[] NonIntegerRefusals =
    {
        ("    d: decimal = 1", "    f: float = 2.0", "decimal*float64", "cmp", "d < f",
            "Type 'decimal' does not support operator '<' with operand of type 'float64'"),
    };

    /// <summary>The pairs whose promoted type is not the left operand's, so a left-typed store refuses.</summary>
    private static MixedPair[] MixedStoreColumn
        => MixedPairs.Where(p => p.Promoted != null && p.Promoted != p.Left).ToArray();

    /// <summary>
    /// A constant operand converts to the OTHER operand's type before promotion (§10.2.11 then
    /// §12.4.7), so <c>u32 + 1</c> is <c>uint32</c> and not <c>int64</c> — <c>c: uint32 = a + 1</c>
    /// was a live false SPY0220. <c>ResultType</c> is <c>null</c> for the one constant that cannot
    /// convert: a negative literal has no <c>ulong</c> form, so <c>uint64 + (-1)</c> is refused
    /// exactly like a signed variable operand (it was SPY0908 / CS0034).
    /// </summary>
    private sealed record ConstantOperandCase(
        string Target, string Kind, string Constant, string? ResultType, string Expected)
    {
        public string Id => $"constant/{Kind}/{Target}";
    }

    private static readonly ConstantOperandCase[] ConstantOperandCases =
    {
        new("uint32", "literal",      "1",          "uint32", "8"),
        new("uint32", "const-ref",    "K",          "uint32", "10"),
        new("uint32", "folded",       "(1 << 2)",   "uint32", "11"),
        new("uint32", "suffixed",     "1u",         "uint32", "8"),
        new("uint32", "negative",     "(-1)",       "int64",  "6"),
        new("uint32", "long-literal", "4294967296", "int64",  "4294967303"),
        new("uint64", "literal",      "1",          "uint64", "8"),
        new("uint64", "const-ref",    "K",          "uint64", "10"),
        new("uint64", "folded",       "(1 << 2)",   "uint64", "11"),
        new("uint64", "suffixed",     "1u",         "uint64", "8"),
        new("uint64", "long-literal", "4294967296", "uint64", "4294967303"),
        new("uint64", "negative",     "(-1)",       null,
            "does not support operator '+' with operand of type 'int32'"),
    };

    private static IEnumerable<MatrixProgram> ConstantOperandPrograms()
    {
        foreach (var c in ConstantOperandCases)
        {
            var sb = new SourceBuilder();
            if (c.Kind == "const-ref")
            {
                sb.Add("const K: int = 3");
                sb.Add("");
            }
            sb.Add("def main() -> None:");
            sb.Add($"    a: {c.Target} = {LeftValue}");

            if (c.ResultType == null)
            {
                var refusedLine = sb.Add($"    print(a + {c.Constant})");
                yield return new MatrixProgram(c.Id, sb.Text, false, new[]
                {
                    new Cell(c.Id, $"constant/{c.Kind}", c.Target, "+", "-", false, refusedLine,
                        c.Expected, DiagnosticCodes.Semantic.InvalidBinaryOperation),
                });
                continue;
            }

            sb.Add($"    c: {c.ResultType} = a + {c.Constant}");
            var line = sb.Add("    print(c)");
            yield return new MatrixProgram(c.Id, sb.Text, true, new[]
            {
                new Cell(c.Id, $"constant/{c.Kind}", c.Target, "+", "-", true, line, c.Expected, null),
            });
        }
    }

    /// <summary>
    /// The augmented column of the mixed axis: the constant pre-step and the promotion answer must
    /// reach <c>x op= y</c> too, or the two sites disagree about the same pair.
    /// </summary>
    private sealed record AugmentedMixedCase(
        string Id, string Target, string RhsDeclaration, string Operator, string Rhs, bool Accepted,
        string Expected, string? Code);

    private static readonly AugmentedMixedCase[] AugmentedMixedCases =
    {
        new("augmented-mixed/uint32/literal", "uint32", "", "+", "1", true, "8", null),
        new("augmented-mixed/uint64/negative", "uint64", "", "+", "(-1)", false,
            "does not support operator '+=' with operand of type 'int32'",
            DiagnosticCodes.Semantic.InvalidBinaryOperation),
        new("augmented-mixed/uint32/int16-var", "uint32", "    r: int16 = 1", "+", "r", false,
            "Result type 'int64' of augmented assignment is not assignable to target type 'uint32'",
            DiagnosticCodes.Semantic.TypeMismatch),
        new("augmented-mixed/int64/uint32-var", "int64", "    r: uint32 = 1", "+", "r", true, "8", null),
        // `**=` goes through the SAME ClassifyIntegerPower the binary site uses (#1700): a uint64
        // target keeps its width for a constant exponent and for a signed variable one.
        new("augmented-mixed/uint64/power-constant", "uint64", "", "**", "2", true, "49", null),
        new("augmented-mixed/uint64/power-int32-var", "uint64", "    r: int32 = 2", "**", "r", true, "49", null),
    };

    private static IEnumerable<MatrixProgram> AugmentedMixedPrograms()
    {
        foreach (var c in AugmentedMixedCases)
        {
            var sb = new SourceBuilder();
            sb.Add("def main() -> None:");
            sb.Add($"    x: {c.Target} = {LeftValue}");
            if (c.RhsDeclaration.Length > 0)
                sb.Add(c.RhsDeclaration);
            var line = sb.Add($"    x {c.Operator}= {c.Rhs}");
            if (c.Accepted)
                sb.Add("    print(x)");

            yield return new MatrixProgram(c.Id, sb.Text, c.Accepted, new[]
            {
                new Cell(c.Id, "augmented/mixed", c.Target, c.Operator, "identifier",
                    c.Accepted, line, c.Expected, c.Code),
            });
        }
    }

    /// <summary>
    /// Integer <c>**</c> binds a <c>CheckedIntPow</c> overload and the recorded type IS that
    /// overload's return type (#1700). Each cell STORES into the type it claims, so a cell that
    /// recorded the wrong width fails on the store rather than only on the value.
    /// </summary>
    private sealed record PowerCase(
        string Id, string Declarations, string Expression, string ResultType, string Expected);

    private static readonly PowerCase[] PowerCases =
    {
        new("power/int32*uint64", "    a: int32 = 3\n    n: uint64 = 20", "a ** n", "int64", "3486784401"),
        new("power/uint64*int32", "    a: uint64 = 3\n    n: int32 = 4", "a ** n", "uint64", "81"),
        new("power/uint64*uint64", "    a: uint64 = 3\n    n: uint64 = 4", "a ** n", "uint64", "81"),
        new("power/uint32*uint32", "    a: uint32 = 3\n    n: uint32 = 4", "a ** n", "int64", "81"),
        new("power/int32*int32", "    a: int32 = 3\n    n: int32 = 4", "a ** n", "int32", "81"),
        new("power/uint64*constant", "    a: uint64 = 3", "a ** 2", "uint64", "9"),
        new("power/fold-uint64", "", "4UL ** 2", "uint64", "16"),
        new("power/fold-uint64-wide", "", "2UL ** 63", "uint64", "9223372036854775808"),
        new("power/fold-int32", "", "2 ** 30", "int32", "1073741824"),
        new("power/fold-int64", "", "2 ** 31", "int64", "2147483648"),
    };

    /// <summary>The `**` cells that must be REFUSED, each by name.</summary>
    private sealed record PowerRefusedCase(
        string Id, string Declarations, string Statement, string Expected, string Code);

    private static readonly PowerRefusedCase[] PowerRefusedCases =
    {
        new("power/store/int32*uint64", "    a: int32 = 3\n    n: uint64 = 20", "    z: int32 = a ** n",
            "Cannot assign type 'int64' to variable of type 'int32'",
            DiagnosticCodes.Semantic.TypeMismatch),
        new("power/store/fold-overflow", "", "    print(2UL ** 64)",
            "Result of integer exponentiation does not fit a 64-bit integer",
            DiagnosticCodes.Semantic.IntegerPowerOverflow),
    };

    private static IEnumerable<MatrixProgram> IntegerPowerPrograms()
    {
        foreach (var c in PowerCases)
        {
            var sb = new SourceBuilder();
            sb.Add("def main() -> None:");
            foreach (var d in c.Declarations.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                sb.Add(d);
            sb.Add($"    p: {c.ResultType} = {c.Expression}");
            var line = sb.Add("    print(p)");

            yield return new MatrixProgram(c.Id, sb.Text, true, new[]
            {
                new Cell(c.Id, "power", c.Id, "**", "-", true, line, c.Expected, null),
            });
        }

        foreach (var c in PowerRefusedCases)
        {
            var sb = new SourceBuilder();
            sb.Add("def main() -> None:");
            foreach (var d in c.Declarations.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                sb.Add(d);
            var line = sb.Add(c.Statement);

            yield return new MatrixProgram(c.Id, sb.Text, false, new[]
            {
                new Cell(c.Id, "power/store", c.Id, "**", "-", false, line, c.Expected, c.Code),
            });
        }
    }

    /// <summary>
    /// <c>min</c>/<c>max</c> is the third consumer of the promotion seam, and the value form's
    /// recorded type must be the type the emitted <c>Builtins.Min&lt;T&gt;</c> returns — which is
    /// why a promoted cell records its type argument (C# cannot infer one <c>T</c> from
    /// <c>(ulong, int)</c>). <c>StoreType</c> is the store each cell uses, so a wrong recorded type
    /// fails on the store rather than only on the printed value (#1699, #1014).
    /// </summary>
    private sealed record MinMaxCase(
        string Id, string Declarations, string Expression, string? StoreType,
        string Expected, string? Code);

    private static readonly MinMaxCase[] MinMaxCases =
    {
        new("minmax/uint64/constant-min", "    a: uint64 = 5", "min(a, 1)", "uint64", "1", null),
        new("minmax/uint64/constant-max", "    a: uint64 = 5", "max(a, 1)", "uint64", "5", null),
        new("minmax/uint64/three-arg", "    a: uint64 = 5", "min(a, 1, 2)", "uint64", "1", null),
        new("minmax/uint32/constant", "    a: uint32 = 5", "min(a, 1)", "uint32", "1", null),
        new("minmax/int8/constant", "    a: int8 = 5", "min(a, 1)", "int8", "1", null),
        new("minmax/uint32*int16", "    a: uint32 = 5\n    b: int16 = 3", "min(a, b)", "int64", "3", null),
        new("minmax/uint64*uint32", "    a: uint64 = 5\n    b: uint32 = 3", "max(a, b)", "uint64", "5", null),
        // The control: a mixed numeric pair whose promotion is a THIRD type still works, and works
        // for the reason it always did (int -> double is a standard implicit conversion).
        new("minmax/int*float", "", "min(2, 3.0)", "float", "2.0", null),
        // The refusals the constant rule must NOT widen away: a negative constant has no ulong
        // form, and a signed VARIABLE never converts.
        new("minmax/uint64/negative", "    a: uint64 = 5", "min(a, -1)", null,
            "Cannot determine common numeric type for 'min' with argument types 'uint64', 'int32'",
            DiagnosticCodes.Semantic.TypeMismatch),
        new("minmax/uint64*int32-var", "    a: uint64 = 5\n    b: int32 = 1", "min(a, b)", null,
            "Cannot determine common numeric type for 'min' with argument types 'uint64', 'int32'",
            DiagnosticCodes.Semantic.TypeMismatch),
    };

    private static IEnumerable<MatrixProgram> MinMaxPrograms()
    {
        foreach (var c in MinMaxCases)
        {
            var sb = new SourceBuilder();
            sb.Add("def main() -> None:");
            foreach (var d in c.Declarations.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                sb.Add(d);

            int line;
            if (c.StoreType == null)
            {
                line = sb.Add($"    print({c.Expression})");
            }
            else
            {
                sb.Add($"    p: {c.StoreType} = {c.Expression}");
                line = sb.Add("    print(p)");
            }

            yield return new MatrixProgram(c.Id, sb.Text, c.StoreType != null, new[]
            {
                new Cell(c.Id, "minmax", c.Id, "min/max", "-",
                    c.StoreType != null, line, c.Expected, c.Code),
            });
        }
    }

    // ───────────────────────────── the tests ─────────────────────────────

    public static IEnumerable<object[]> Programs()
        => BuildPrograms().Select(p => new object[] { p.Id });

    [Theory]
    [MemberData(nameof(Programs))]
    public void MatrixProgramBehavesAsTheRulePredicts(string programId)
    {
        var program = BuildPrograms().Single(p => p.Id == programId);
        var result = CompileAndExecute(program.Source);

        if (program.ShouldCompile)
        {
            result.Success.Should().BeTrue(
                $"{programId} is admitted by the rule but failed: "
                + string.Join("; ", result.CompilationErrors) + "\n" + program.Source);

            var printed = result.StandardOutput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && line != "True")
                .ToList();

            printed.Should().HaveCount(program.Cells.Count,
                $"{programId} prints one line per cell\n{program.Source}");

            for (var i = 0; i < program.Cells.Count; i++)
            {
                printed[i].Should().Be(program.Cells[i].Expected,
                    $"cell {program.Cells[i].Id} ({program.Cells[i].Store}) value\n{program.Source}");
            }

            return;
        }

        result.Success.Should().BeFalse($"{programId} must be refused\n{program.Source}");

        foreach (var cell in program.Cells)
        {
            result.RawDiagnostics.Should().Contain(
                d => d.Code == cell.Code
                    && d.Line == cell.Line
                    && d.Message.Contains(cell.Expected, StringComparison.Ordinal),
                $"cell {cell.Id} must be refused at line {cell.Line} with {cell.Code} naming "
                + $"'{cell.Expected}'; got "
                + string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}@{d.Line}:{d.Message}"))
                + "\n" + program.Source);
        }

        // One diagnostic per cell — a cell that silently became accepted would leave the
        // per-cell assertion above satisfied by a neighbour's message.
        result.RawDiagnostics
            .Count(d => program.Cells.Any(c => c.Code == d.Code && c.Line == d.Line))
            .Should().Be(program.Cells.Count, $"{programId} reports exactly one diagnostic per cell");
    }

    /// <summary>
    /// The matrix is total over its axes: every (store, width, operator, target kind) is either a
    /// live cell in some program or a declared N/A with a reason. Without this a generator bug
    /// that dropped a whole arm would leave every remaining cell green.
    /// </summary>
    [Fact]
    public void MatrixIsTotalOverItsAxes()
    {
        var programs = BuildPrograms();
        var live = programs.SelectMany(p => p.Cells).ToList();

        live.Select(c => c.Id).Should().OnlyHaveUniqueItems("each cell appears once");

        // Non-augmented stores: width × operator, no target kind.
        foreach (var store in new[] { "plain decl", "print", "argument", "return" })
            foreach (var w in Widths)
                foreach (var op in Ops)
                {
                    live.Should().Contain(
                        c => c.Store == store && c.Width == w.Name && c.Op == op.Id,
                        $"{store} × {w.Name} × {op.Id} is a cell of the matrix");
                }

        // Augmented stores: width × binary operator × target kind × RHS shape, minus the N/A set.
        foreach (var rhs in RhsKinds)
            foreach (var w in Widths)
                foreach (var target in TargetKinds)
                    foreach (var op in Ops)
                    {
                        var store = $"augmented/{rhs}";
                        if (NaCells.Any(n => n.Store == store && n.Op == op.Id))
                            continue;

                        live.Should().Contain(
                            c => c.Store == store && c.Width == w.Name
                                && c.Op == op.Id && c.TargetKind == target,
                            $"{store} × {w.Name} × {op.Id} × {target} is a cell of the matrix");
                    }

        NaCells.Should().OnlyContain(n => n.Reason.Length > 20, "every N/A cell states why");

        // The mixed-signedness axes (#1699): pair × family, plus the store column, the constant
        // column, the augmented column and the `**` column. Each declared entry must be a live
        // cell — a generator that dropped one would otherwise leave the rest green.
        foreach (var pair in MixedPairs)
            foreach (var family in MixedFamilies)
                live.Should().Contain(c => c.Store == $"mixed/{family.Id}" && c.Width == pair.Id,
                    $"mixed/{family.Id} × {pair.Id} is a cell of the matrix");

        foreach (var pair in MixedStoreColumn)
            live.Should().Contain(c => c.Id == $"mixed/store/{pair.Id}",
                $"the {pair.Id} store column is a cell of the matrix");

        foreach (var (_, _, pairId, family, _, _) in NonIntegerRefusals)
            live.Should().Contain(c => c.Store == $"mixed/{family}" && c.Width == pairId,
                $"mixed/{family} × {pairId} is a cell of the matrix");

        foreach (var c in ConstantOperandCases)
            live.Should().Contain(l => l.Id == c.Id, $"{c.Id} is a cell of the matrix");
        foreach (var c in AugmentedMixedCases)
            live.Should().Contain(l => l.Id == c.Id, $"{c.Id} is a cell of the matrix");
        foreach (var c in PowerCases)
            live.Should().Contain(l => l.Id == c.Id, $"{c.Id} is a cell of the matrix");
        foreach (var c in PowerRefusedCases)
            live.Should().Contain(l => l.Id == c.Id, $"{c.Id} is a cell of the matrix");
        foreach (var c in MinMaxCases)
            live.Should().Contain(l => l.Id == c.Id, $"{c.Id} is a cell of the matrix");

        var liveAugmented = live.Count(c => RhsKinds.Any(r => c.Store == $"augmented/{r}")
            && TargetKinds.Contains(c.TargetKind));
        var expectedAugmented = RhsKinds.Length * Widths.Length * TargetKinds.Length * BinaryOps.Length
            - (Widths.Length * TargetKinds.Length * 2);   // the two shift × out-of-range-const columns

        liveAugmented.Should().Be(expectedAugmented);
        live.Count(c => c.Store == "augmented/mixed").Should().Be(AugmentedMixedCases.Length,
            "the augmented column of the mixed axis is exactly its declared cases");

        // Every non-augmented cell is accounted for by a declared axis — no magic constant, so a
        // generator that stopped emitting an arm fails here instead of silently shrinking.
        var expectedNonAugmented =
            (4 * Widths.Length * Ops.Length)                       // plain decl / print / argument / return
            + UnsignedWideKeeps.Length + UnsignedWideRefusals.Length
            + (MixedPairs.Length * MixedFamilies.Length) + MixedStoreColumn.Length
            + NonIntegerRefusals.Length
            + ConstantOperandCases.Length
            + PowerCases.Length + PowerRefusedCases.Length
            + MinMaxCases.Length;

        live.Count(c => !c.Store.StartsWith("augmented/", StringComparison.Ordinal))
            .Should().Be(expectedNonAugmented);
    }

    /// <summary>
    /// The exempt controls, so the promotion change cannot be credited with refusing things that
    /// have their own rule: a shift promotes its LEFT operand alone (§12.4.7's carve-out), unary
    /// operators are not binary promotion, and <c>x == None</c> keeps the `is None` steer that runs
    /// before the numeric arm — the three places a mixed-signedness refusal could hide.
    /// </summary>
    [Fact]
    public void ShiftAndUnaryControlsAreUnaffected()
    {
        var result = CompileAndExecute(@"
def main() -> None:
    a: uint64 = 5
    n: int32 = 1
    print(a << n)
    b: uint32 = 5
    m: int16 = 1
    print(b << m)
    print(~a)
    c: int64 = 5
    print(-c)
");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Should().Equal("10", "10", "18446744073709551610", "-5");

        var noneControl = CompileAndExecute(@"
def main() -> None:
    y: int | None = 1
    print(y == None)
");
        noneControl.Success.Should().BeFalse("`x == None` is refused with the `is None` steer");
        noneControl.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.InvalidBinaryOperation
                && d.Message.Contains("Did you mean 'is None'?", StringComparison.Ordinal),
            "the None shortcut runs before the numeric arm; got "
            + string.Join(" | ", noneControl.RawDiagnostics.Select(d => $"{d.Code}:{d.Message}")));
    }

    /// <summary>
    /// The rule's positive controls, spelled as their own test so that a change which widens the
    /// augmented decision to "always narrow" cannot pass by editing a generated expectation:
    /// a wider VARIABLE RHS and an out-of-range CONSTANT RHS stay SPY0220 at every target kind.
    /// </summary>
    [Theory]
    [InlineData("identifier", "x")]
    [InlineData("attribute", "box.n")]
    [InlineData("index", "xs[0]")]
    public void WiderVariableAndOutOfRangeConstantStayRefused(string targetKind, string target)
    {
        var prelude = targetKind switch
        {
            "attribute" => "class Box:\n    n: int8 = 7\n\ndef main() -> None:\n    box: Box = Box()\n",
            "index" => "def main() -> None:\n    seed: int8 = 7\n    xs: list[int8] = []\n    xs.append(seed)\n",
            _ => "def main() -> None:\n    x: int8 = 7\n",
        };

        var wider = CompileAndExecute(prelude + "    i: int = 2\n" + $"    {target} += i\n");
        wider.Success.Should().BeFalse($"a wider variable RHS is refused for a {targetKind} target");
        wider.RawDiagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.Semantic.TypeMismatch
            && d.Message.Contains("of augmented assignment is not assignable to target type 'int8'",
                StringComparison.Ordinal));

        var outOfRange = CompileAndExecute(prelude + $"    {target} += 300\n");
        outOfRange.Success.Should().BeFalse($"an out-of-range constant RHS is refused for a {targetKind} target");
        outOfRange.RawDiagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.Semantic.TypeMismatch
            && d.Message.Contains("of augmented assignment is not assignable to target type 'int8'",
                StringComparison.Ordinal));
    }

    /// <summary>
    /// The store positions an in-range integer constant must reach (#1698): declaration, plain
    /// identifier store, attribute store, index store, dict-value store, <c>return</c> and
    /// argument — ECMA-334 §10.2.11 asks for an implicit conversion at each of them, and the C#
    /// each lowers to performs it. Before this the helper was consulted at three sites only, so
    /// <c>x8 = 7</c> and <c>self.n8 = 7</c> were SPY0220 while <c>x8: int8 = 7</c> was fine.
    /// <para>The last two lines are the ones that make the acceptance honest rather than merely
    /// quiet: the rebound variable keeps its DECLARED width, so a later read of it still types as
    /// <c>int8</c>.</para>
    /// </summary>
    [Fact]
    public void InRangeIntegerConstantConvertsAtEveryStorePosition()
    {
        var result = CompileAndExecute(@"
class Box:
    n: int8 = 0

def take(v: int8) -> None:
    print(v)

def make() -> int8:
    return 120

def main() -> None:
    x: int8 = 7
    x = 120
    print(x)
    b: Box = Box()
    b.n = 120
    print(b.n)
    seed: int8 = 1
    xs: list[int8] = [seed]
    xs[0] = 120
    print(xs[0])
    d: dict[str, int8] = {}
    d[""k""] = 120
    print(d[""k""])
    take(120)
    print(make())
    u: uint8 = 0
    u = 255
    print(u)
    echoed: list[int8] = [x]
    print(echoed[0])
");

        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Should().Equal("120", "120", "120", "120", "120", "120", "255", "120");
    }

    /// <summary>
    /// The positive control for the store positions above: a constant OUT of the destination's
    /// range is refused at every one of them, so the acceptance is a range decision and not a
    /// blanket "an integer literal fits anywhere".
    /// </summary>
    [Theory]
    [InlineData("    x: int8 = 7\n    x = 300\n", "SPY0220")]
    [InlineData("    b: Box = Box()\n    b.n = 300\n", "SPY0220")]
    [InlineData("    seed: int8 = 1\n    xs: list[int8] = [seed]\n    xs[0] = 300\n", "SPY0220")]
    [InlineData("    d: dict[str, int8] = {}\n    d[\"k\"] = 300\n", "SPY0220")]
    [InlineData("    u: uint8 = 0\n    u = -1\n", "SPY0220")]
    public void OutOfRangeIntegerConstantIsRefusedAtEveryStorePosition(string body, string code)
    {
        var result = CompileAndExecute("class Box:\n    n: int8 = 0\n\ndef main() -> None:\n" + body);

        result.Success.Should().BeFalse($"an out-of-range constant is refused: {body}");
        result.RawDiagnostics.Should().Contain(d => d.Code == code,
            $"expected {code}; got " + string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}:{d.Message}")));
    }

    /// <summary>
    /// The <c>return</c> arm of the same rule, both directions.
    /// </summary>
    [Fact]
    public void OutOfRangeIntegerConstantIsRefusedInReturnPosition()
    {
        var result = CompileAndExecute(@"
def make() -> int8:
    return 300

def main() -> None:
    print(make())
");
        result.Success.Should().BeFalse("300 does not fit int8");
        result.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.MissingReturnValue);
    }

    /// <summary>
    /// Ordinary <c>int</c> arithmetic is untouched by the floor and by the narrowing rule — the
    /// control that shows the matrix measures narrow widths and not "arithmetic in general".
    /// </summary>
    [Fact]
    public void IntArithmeticIsUnaffected()
    {
        var result = CompileAndExecute(@"
def main() -> None:
    a: int = 7
    b: int = 2
    c: int = a + b
    print(c)
    print(a // b)
    a += b
    a += 1
    print(a)
    n: int64 = 7
    n //= 2
    print(n)
");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim()).Should().Equal("9", "3", "10", "3");
    }
}
