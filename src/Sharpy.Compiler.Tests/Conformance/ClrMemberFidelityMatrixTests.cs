using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// CLR member fidelity matrix (#1640): every member access on a CLR-origin receiver has the
/// REFLECTED semantic type, in both spellings, in callee and value position — so a correct
/// destination compiles, a mistyped one is SPY0220, and a bogus member is SPY0203. Never Unknown,
/// which is assignable to anything and turns a user's type error into SPY0908 (the compiler
/// reporting its own bug).
///
/// <para>
/// Axes: receiver × member kind × spelling × destination.
/// </para>
/// <list type="bullet">
/// <item><b>receiver</b> — unmapped generic (<c>Stack[int]</c>, <c>Queue[int]</c>), nested generic,
/// honest-identity CLR generic (<c>List[int]</c>, <c>Dictionary[str,int]</c>), non-generic class
/// (<c>StringBuilder</c>, <c>Random</c>), struct (<c>DateTime</c>, <c>TimeSpan</c>), a user class
/// INHERITING a CLR type (<c>IntList(List[int])</c>), and a STATIC type receiver
/// (<c>Environment</c>, <c>DateTime</c>).</item>
/// <item><b>member kind</b> — single-overload method, overload group, property, static field.</item>
/// <item><b>spelling</b> — Pythonic (reverse-mangled) and verbatim CLR-cased.</item>
/// <item><b>destination</b> — correct (compiles), wrong (SPY0220), bogus member (SPY0203).</item>
/// </list>
///
/// <para>
/// The expectation is a diagnostic CODE, not a message substring: the class contract is about which
/// channel answers, and a substring assertion passes vacuously when the answer moves channels.
/// </para>
/// </summary>
public class ClrMemberFidelityMatrixTests
{
    private readonly ITestOutputHelper _output;
    private readonly CompilerApi _api = new();

    public ClrMemberFidelityMatrixTests(ITestOutputHelper output) => _output = output;

    /// <summary>What the compiler must answer for a cell.</summary>
    private enum Expect
    {
        /// <summary>The destination accepts the member's reflected type: no diagnostic.</summary>
        Compiles,

        /// <summary>The destination rejects it: SPY0220, from the checker — never SPY0908.</summary>
        TypeMismatch,

        /// <summary>The member does not exist: SPY0203, from the checker — never SPY0908.</summary>
        AbsentMember,

        /// <summary>
        /// SPY0220 whose offending type is spelled nullable (<c>'str | None' to</c> since #1714
        /// gave NullableType its own display spelling): the member's declared nullable reached
        /// the recorded type (#1705).
        /// </summary>
        TypeMismatchNullable,

        /// <summary>SPY0220 whose offending type is NOT spelled nullable — the non-nullable twin (#1705).</summary>
        TypeMismatchNonNullable,

        /// <summary>A <c>None</c> store into a member declared non-nullable: SPY0229 from the checker (#1705).</summary>
        NoneRefused
    }

    /// <param name="MustName">
    /// A substring some error message must contain — the TYPE NAME the diagnostic has to spell. The
    /// code alone cannot distinguish "typed correctly and rejected" from "typed `object` and
    /// rejected": both are SPY0220. Every enum/char cell below carries it, because `object` was
    /// exactly the wrong answer the fix replaced (#1705).
    /// </param>
    private sealed record Cell(string Label, string Source, Expect Expect, string? MustName = null);

    /// <summary>A cell of the nominal matrix that cannot be measured, and why.</summary>
    private sealed record NotApplicable(string Label, string Reason);

    [Fact]
    [Trait("Category", "Conformance")]
    public void ClrMemberFidelityMatrix_AllCellsPass()
    {
        var failures = new List<string>();
        var cells = GenerateCells().ToList();

        Assert.Equal(cells.Count, cells.Select(c => c.Label).Distinct().Count());

        foreach (var cell in cells)
        {
            CompileResult result;
            try
            {
                result = _api.Compile(cell.Source, new CompilerOptions { OutputType = "library" });
            }
            catch (Exception ex)
            {
                failures.Add($"{cell.Label}: crashed — {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            var errors = result.Diagnostics
                .Where(d => d.Severity == CompilerDiagnosticSeverity.Error)
                .ToList();

            // NOTE (#1837): this arm cannot fire as the harness is configured. `_api.Compile` emits an
            // assembly only when an OutputAssemblyPath is given, so the generated C# never reaches
            // Roslyn and SPY0908 is never raised here — `Expect.Compiles` means "no SEMANTIC
            // diagnostic", not "the generated C# compiles". Measured: a mutation that emits
            // `char.ToString()` into a reflected char slot leaves all cells green while the
            // executing fixture for the same program fails with CS1503. Any cell whose failure mode
            // is a C#-level mismatch therefore needs an executing FBIT fixture beside it; the
            // enum/char/bare-T rows have clr_enum_member_is_the_enum_1705,
            // clr_char_member_is_str_1705 and clr_bare_type_parameter_member_not_nullable_1705.
            if (errors.Any(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError))
            {
                failures.Add($"{cell.Label}: SPY0908 — the member resolved to Unknown instead of its reflected type");
                continue;
            }

            switch (cell.Expect)
            {
                case Expect.Compiles when errors.Count > 0:
                    failures.Add($"{cell.Label}: expected to compile but drew {errors[0].Code}: {errors[0].Message}");
                    break;

                case Expect.TypeMismatch when !errors.Any(d => d.Code == DiagnosticCodes.Semantic.TypeMismatch):
                    failures.Add($"{cell.Label}: expected SPY0220, got {Describe(errors)}");
                    break;

                case Expect.AbsentMember when !errors.Any(d => d.Code == DiagnosticCodes.Semantic.UndefinedMember):
                    failures.Add($"{cell.Label}: expected SPY0203, got {Describe(errors)}");
                    break;

                case Expect.TypeMismatchNullable when !errors.Any(d =>
                        d.Code == DiagnosticCodes.Semantic.TypeMismatch && d.Message.Contains(NullableSpelling)):
                    failures.Add($"{cell.Label}: expected SPY0220 naming a nullable type, got {Describe(errors)}");
                    break;

                case Expect.TypeMismatchNonNullable when !errors.Any(d =>
                        d.Code == DiagnosticCodes.Semantic.TypeMismatch && !d.Message.Contains(NullableSpelling)):
                    failures.Add($"{cell.Label}: expected SPY0220 naming a non-nullable type, got {Describe(errors)}");
                    break;

                case Expect.NoneRefused when !errors.Any(d => d.Code == DiagnosticCodes.Semantic.NullabilityViolation):
                    failures.Add($"{cell.Label}: expected SPY0229, got {Describe(errors)}");
                    break;
            }

            if (cell.MustName != null && !errors.Any(d => d.Message.Contains(cell.MustName, StringComparison.Ordinal)))
            {
                failures.Add($"{cell.Label}: no diagnostic names {cell.MustName} — got {Describe(errors)}");
            }
        }

        _output.WriteLine($"Fidelity cells: {cells.Count}  Failures: {failures.Count}");
        foreach (var na in NotApplicableCells())
            _output.WriteLine($"  N/A {na.Label}: {na.Reason}");
        foreach (var f in failures)
            _output.WriteLine($"  {f}");

        Assert.True(failures.Count == 0,
            $"CLR member fidelity (#1640): {failures.Count} of {cells.Count} cells failed.\n" +
            string.Join("\n", failures.Select(f => "  " + f)));
    }

    /// <summary>
    /// How SPY0220 spells a nullable source type: <c>Cannot assign type 'str | None' to …</c>.
    /// </summary>
    private const string NullableSpelling = "| None' to";

    private static string Describe(IReadOnlyList<CompilerDiagnostic> errors)
        => errors.Count == 0
            ? "no error at all (the member is still Unknown, which is assignable to anything)"
            : $"{errors[0].Code}: {errors[0].Message}";

    /// <summary>
    /// Cells of the nominal matrix that cannot be measured today, each with the reason and the issue
    /// that owns it. Declared rather than silently omitted: an unlisted gap is indistinguishable from
    /// an axis nobody thought of.
    /// </summary>
    private static IEnumerable<NotApplicable> NotApplicableCells()
    {
        yield return new NotApplicable("nrt.static-method-parameter-{nullable,nonnullable}-none",
            "a `None` argument is not checked against the parameter's nullability at any CLR call "
            + "seam — `Environment.get_environment_variable(None)` (declared `string`) compiles today, "
            + "so a nullable-parameter cell (`Debug.fail(None)`, declared `string?`) and its "
            + "non-nullable twin both pass vacuously. The parameter arm of #1705 needs the refusal "
            + "before it can be measured; MapClrParameterType already carries the declared state.");

        yield return new NotApplicable("nrt.static-property-nullable-store (IPrincipal)",
            "`Thread.current_principal` is declared `IPrincipal?`, and an interface the bridge cannot "
            + "map makes the resolver Inconclusive (permissive) — the pair measured nothing; the "
            + "static-store cells use `CultureInfo.default_thread_current_culture` (`CultureInfo?`) instead.");

        yield return new NotApplicable("Stack[int].field-any",
            "System.Collections.Generic.Stack<T> declares no public field, so the "
            + "receiver × field cells of the unmapped-generic row have no member to name.");

        yield return new NotApplicable("StringBuilder.static-member-any",
            "System.Text.StringBuilder declares no public static member, so the "
            + "static-receiver cells of the non-generic-class row have no member to name.");

        yield return new NotApplicable("nrt.closed-generic-annotated-T-return",
            "`List[str].find(...)` is typed `str`, not `str | None`, although `List<T>.Find` returns "
            + "`T?`: the suppression that keeps a BARE `T` non-nullable also swallows the annotated "
            + "case, and NullabilityInfoContext reports the same state for both on a generic "
            + "definition (measured in ClrNullabilityConsumerTotalityTests). #1828.");

        yield return new NotApplicable("nrt.extension-annotated-T-return",
            "`xs.first_or_default()` on `list[str]` is typed `str` for the same reason — "
            + "`Enumerable.FirstOrDefault<TSource>` returns `TSource?`. The extension route now "
            + "applies declared nullability (BuiltinRegistry.WrapIfDeclaredNullable, "
            + "GenericReferenceResolver's staged symbol), so a `string?` extension return DOES "
            + "carry it; only the type-parameter shape is blind. #1828.");

        yield return new NotApplicable("nrt.nested-type-argument",
            "`ProcessStartInfo.environment` (`IDictionary<string, string?>`) is `dict[str, str]`: "
            + "ClrDeclaredNullability reads the TOP-LEVEL state only, by design — `List<string?>` "
            + "stays `list[str]`. Not a bypass; the rule is stated in the type's remarks.");

        yield return new NotApplicable("char.parameter-computed-str",
            "a COMPUTED `str` in a reflected `char` slot is refused by name at the call seam "
            + "(#1402: only a one-character literal converts), which is a call-route cell owned by "
            + "the argument-binding matrix, not by member fidelity. The member-derived direction — a "
            + "char-origin value handed back to a char slot — IS measured below.");

        yield return new NotApplicable("route.warm-incremental",
            "the warm route needs a .spyproj compiled `--clean` then `--incremental`, which this "
            + "harness (CompilerApi single-source) cannot express; the cold/warm pair for CLR member "
            + "types is measured by ClrMemberTypeWarmColdTests.");

        yield return new NotApplicable("list[int].count-clr",
            "`count` on a SHARPY list is Sharpy's own count(value) method, not CLR's Count property — "
            + "the Sharpy surface owns the spelling and resolves it before reflection is asked. The "
            + "CLR-identity spelling is covered by the Dictionary[str,int] and List[int] rows, which "
            + "the bridge does not collapse onto a Sharpy builtin.");
    }

    private static IEnumerable<Cell> GenerateCells()
    {
        // ── Unmapped generic: Stack[int] — method (single overload) and property ──

        yield return new Cell("Stack[int].peek()-correct-pythonic",
            Src("Stack", "s = Stack[int]()\n    s.push(1)\n    n: int = s.peek()"), Expect.Compiles);

        yield return new Cell("Stack[int].peek()-wrong-pythonic",
            Src("Stack", "s = Stack[int]()\n    s.push(1)\n    x: str = s.peek()"), Expect.TypeMismatch);

        yield return new Cell("Stack[int].Peek()-correct-clr",
            Src("Stack", "s = Stack[int]()\n    s.push(1)\n    n: int = s.Peek()"), Expect.Compiles);

        yield return new Cell("Stack[int].Peek()-wrong-clr",
            Src("Stack", "s = Stack[int]()\n    s.push(1)\n    x: str = s.Peek()"), Expect.TypeMismatch);

        yield return new Cell("Stack[int].count-correct-pythonic",
            Src("Stack", "s = Stack[int]()\n    n: int = s.count"), Expect.Compiles);

        yield return new Cell("Stack[int].count-wrong-pythonic",
            Src("Stack", "s = Stack[int]()\n    x: str = s.count"), Expect.TypeMismatch);

        yield return new Cell("Stack[int].Count-correct-clr",
            Src("Stack", "s = Stack[int]()\n    n: int = s.Count"), Expect.Compiles);

        yield return new Cell("Stack[int].Count-wrong-clr",
            Src("Stack", "s = Stack[int]()\n    x: str = s.Count"), Expect.TypeMismatch);

        yield return new Cell("Stack[int].bogus-pythonic",
            Src("Stack", "s = Stack[int]()\n    print(s.no_such_member_xyz)"), Expect.AbsentMember);

        yield return new Cell("Stack[int].bogus-clr",
            Src("Stack", "s = Stack[int]()\n    print(s.NoSuchMemberXyz)"), Expect.AbsentMember);

        // Callee position on a property: `s.count()` collapses onto the property access and the CALL
        // carries the property's type.

        yield return new Cell("Stack[int].count()-callee-correct-pythonic",
            Src("Stack", "s = Stack[int]()\n    n: int = s.count()"), Expect.Compiles);

        yield return new Cell("Stack[int].count()-callee-wrong-pythonic",
            Src("Stack", "s = Stack[int]()\n    x: str = s.count()"), Expect.TypeMismatch);

        yield return new Cell("Stack[int].Count()-callee-correct-clr",
            Src("Stack", "s = Stack[int]()\n    n: int = s.Count()"), Expect.Compiles);

        yield return new Cell("Stack[int].Count()-callee-wrong-clr",
            Src("Stack", "s = Stack[int]()\n    x: str = s.Count()"), Expect.TypeMismatch);

        // ── Unmapped generic: Queue[int] ──

        yield return new Cell("Queue[int].peek()-correct",
            Src("Queue", "q = Queue[int]()\n    q.enqueue(1)\n    n: int = q.peek()"), Expect.Compiles);

        yield return new Cell("Queue[int].peek()-wrong",
            Src("Queue", "q = Queue[int]()\n    q.enqueue(1)\n    x: str = q.peek()"), Expect.TypeMismatch);

        yield return new Cell("Queue[int].count-correct",
            Src("Queue", "q = Queue[int]()\n    n: int = q.count"), Expect.Compiles);

        yield return new Cell("Queue[int].count-wrong",
            Src("Queue", "q = Queue[int]()\n    x: str = q.count"), Expect.TypeMismatch);

        // ── Nested generic: Stack[Queue[int]] ──

        yield return new Cell("Stack[Queue[int]].peek-nested-correct",
            "from system.collections.generic import Stack, Queue\n\ndef _use() -> None:\n"
            + "    s = Stack[Queue[int]]()\n    s.push(Queue[int]())\n    q: Queue[int] = s.peek()\n"
            + "    print(q.count)\n", Expect.Compiles);

        yield return new Cell("Stack[Queue[int]].peek-nested-wrong",
            "from system.collections.generic import Stack, Queue\n\ndef _use() -> None:\n"
            + "    s = Stack[Queue[int]]()\n    s.push(Queue[int]())\n    x: str = s.peek()\n",
            Expect.TypeMismatch);

        // ── Honest-identity CLR generics: the bridge keeps their CLR identity (#1517), so their
        //    members are ordinary CLR members and reflection types them. ──

        yield return new Cell("Dictionary[str,int].count-correct-pythonic",
            Src("Dictionary", "d = Dictionary[str, int]()\n    n: int = d.count"), Expect.Compiles);

        yield return new Cell("Dictionary[str,int].count-wrong-pythonic",
            Src("Dictionary", "d = Dictionary[str, int]()\n    x: str = d.count"), Expect.TypeMismatch);

        yield return new Cell("Dictionary[str,int].Count-correct-clr",
            Src("Dictionary", "d = Dictionary[str, int]()\n    n: int = d.Count"), Expect.Compiles);

        yield return new Cell("Dictionary[str,int].Count-wrong-clr",
            Src("Dictionary", "d = Dictionary[str, int]()\n    x: str = d.Count"), Expect.TypeMismatch);

        yield return new Cell("List[int].index_of()-correct-pythonic",
            Src("List", "v = List[int]()\n    v.add(3)\n    n: int = v.index_of(3)"), Expect.Compiles);

        yield return new Cell("List[int].index_of()-wrong-pythonic",
            Src("List", "v = List[int]()\n    v.add(3)\n    x: str = v.index_of(3)"), Expect.TypeMismatch);

        yield return new Cell("List[int].IndexOf()-correct-clr",
            Src("List", "v = List[int]()\n    v.add(3)\n    n: int = v.IndexOf(3)"), Expect.Compiles);

        yield return new Cell("List[int].IndexOf()-wrong-clr",
            Src("List", "v = List[int]()\n    v.add(3)\n    x: str = v.IndexOf(3)"), Expect.TypeMismatch);

        // ── Non-generic class: StringBuilder ──

        yield return new Cell("StringBuilder.to_string()-correct-pythonic",
            SrcText("sb = StringBuilder()\n    x: str = sb.to_string()"), Expect.Compiles);

        yield return new Cell("StringBuilder.to_string()-wrong-pythonic",
            SrcText("sb = StringBuilder()\n    n: int = sb.to_string()"), Expect.TypeMismatch);

        yield return new Cell("StringBuilder.ToString()-correct-clr",
            SrcText("sb = StringBuilder()\n    x: str = sb.ToString()"), Expect.Compiles);

        yield return new Cell("StringBuilder.ToString()-wrong-clr",
            SrcText("sb = StringBuilder()\n    n: int = sb.ToString()"), Expect.TypeMismatch);

        yield return new Cell("StringBuilder.length-correct-pythonic",
            SrcText("sb = StringBuilder()\n    n: int = sb.length"), Expect.Compiles);

        yield return new Cell("StringBuilder.length-wrong-pythonic",
            SrcText("sb = StringBuilder()\n    x: str = sb.length"), Expect.TypeMismatch);

        yield return new Cell("StringBuilder.Length-correct-clr",
            SrcText("sb = StringBuilder()\n    n: int = sb.Length"), Expect.Compiles);

        yield return new Cell("StringBuilder.Length-wrong-clr",
            SrcText("sb = StringBuilder()\n    x: str = sb.Length"), Expect.TypeMismatch);

        yield return new Cell("StringBuilder.length()-callee-correct",
            SrcText("sb = StringBuilder()\n    n: int = sb.length()"), Expect.Compiles);

        yield return new Cell("StringBuilder.length()-callee-wrong",
            SrcText("sb = StringBuilder()\n    x: str = sb.length()"), Expect.TypeMismatch);

        yield return new Cell("StringBuilder.bogus-pythonic",
            SrcText("sb = StringBuilder()\n    print(sb.no_such_member_xyz)"), Expect.AbsentMember);

        yield return new Cell("StringBuilder.bogus-clr",
            SrcText("sb = StringBuilder()\n    print(sb.NoSuchMemberXyz)"), Expect.AbsentMember);

        // ── Non-generic class with an overload GROUP: Random.Next() / Next(int) / Next(int, int) ──

        yield return new Cell("Random.next()-group-correct-pythonic",
            SrcSystem("Random", "r = Random()\n    n: int = r.next(10)"), Expect.Compiles);

        yield return new Cell("Random.next()-group-wrong-pythonic",
            SrcSystem("Random", "r = Random()\n    x: str = r.next(10)"), Expect.TypeMismatch);

        yield return new Cell("Random.Next()-group-correct-clr",
            SrcSystem("Random", "r = Random()\n    n: int = r.Next(10)"), Expect.Compiles);

        yield return new Cell("Random.Next()-group-wrong-clr",
            SrcSystem("Random", "r = Random()\n    x: str = r.Next(10)"), Expect.TypeMismatch);

        yield return new Cell("Random.bogus-pythonic",
            SrcSystem("Random", "r = Random()\n    print(r.no_such_member_xyz())"), Expect.AbsentMember);

        // ── Struct: DateTime (instance) ──

        yield return new Cell("DateTime.year-correct-pythonic",
            SrcSystem("DateTime", "dt = DateTime(2020, 1, 2)\n    n: int = dt.year"), Expect.Compiles);

        yield return new Cell("DateTime.year-wrong-pythonic",
            SrcSystem("DateTime", "dt = DateTime(2020, 1, 2)\n    x: str = dt.year"), Expect.TypeMismatch);

        yield return new Cell("DateTime.Year-correct-clr",
            SrcSystem("DateTime", "dt = DateTime(2020, 1, 2)\n    n: int = dt.Year"), Expect.Compiles);

        yield return new Cell("DateTime.Year-wrong-clr",
            SrcSystem("DateTime", "dt = DateTime(2020, 1, 2)\n    x: str = dt.Year"), Expect.TypeMismatch);

        yield return new Cell("DateTime.add_days()-correct-pythonic",
            SrcSystem("DateTime", "dt = DateTime(2020, 1, 2)\n    d2: DateTime = dt.add_days(1.0)"),
            Expect.Compiles);

        yield return new Cell("DateTime.add_days()-wrong-pythonic",
            SrcSystem("DateTime", "dt = DateTime(2020, 1, 2)\n    x: str = dt.add_days(1.0)"),
            Expect.TypeMismatch);

        yield return new Cell("DateTime.AddDays()-correct-clr",
            SrcSystem("DateTime", "dt = DateTime(2020, 1, 2)\n    d2: DateTime = dt.AddDays(1.0)"),
            Expect.Compiles);

        yield return new Cell("DateTime.AddDays()-wrong-clr",
            SrcSystem("DateTime", "dt = DateTime(2020, 1, 2)\n    x: str = dt.AddDays(1.0)"),
            Expect.TypeMismatch);

        yield return new Cell("DateTime.bogus-pythonic",
            SrcSystem("DateTime", "dt = DateTime(2020, 1, 2)\n    print(dt.no_such_member_xyz)"),
            Expect.AbsentMember);

        yield return new Cell("DateTime.bogus-clr",
            SrcSystem("DateTime", "dt = DateTime(2020, 1, 2)\n    print(dt.NoSuchMemberXyz)"),
            Expect.AbsentMember);

        // ── Struct: TimeSpan — property and STATIC FIELD ──

        yield return new Cell("TimeSpan.hours-correct-pythonic",
            SrcSystem("TimeSpan", "ts = TimeSpan(1, 2, 3)\n    n: int = ts.hours"), Expect.Compiles);

        yield return new Cell("TimeSpan.hours-wrong-pythonic",
            SrcSystem("TimeSpan", "ts = TimeSpan(1, 2, 3)\n    x: str = ts.hours"), Expect.TypeMismatch);

        yield return new Cell("TimeSpan.Hours-correct-clr",
            SrcSystem("TimeSpan", "ts = TimeSpan(1, 2, 3)\n    n: int = ts.Hours"), Expect.Compiles);

        yield return new Cell("TimeSpan.Hours-wrong-clr",
            SrcSystem("TimeSpan", "ts = TimeSpan(1, 2, 3)\n    x: str = ts.Hours"), Expect.TypeMismatch);

        yield return new Cell("TimeSpan.zero-static-field-correct-pythonic",
            SrcSystem("TimeSpan", "z: TimeSpan = TimeSpan.zero"), Expect.Compiles);

        yield return new Cell("TimeSpan.zero-static-field-wrong-pythonic",
            SrcSystem("TimeSpan", "x: str = TimeSpan.zero"), Expect.TypeMismatch);

        yield return new Cell("TimeSpan.Zero-static-field-correct-clr",
            SrcSystem("TimeSpan", "z: TimeSpan = TimeSpan.Zero"), Expect.Compiles);

        yield return new Cell("TimeSpan.Zero-static-field-wrong-clr",
            SrcSystem("TimeSpan", "x: str = TimeSpan.Zero"), Expect.TypeMismatch);

        // ── Struct: Vector2 — INSTANCE FIELD, both spellings ──
        // Was rostered N/A on "the CONSTRUCTOR ICEs first" (#1688). It does not any more:
        // `Vector2(1.0, 2.0)` runs and `print(v.X)` prints 1.0 since the store seam narrows the
        // float literals at the CLR argument position — measured @ 080fb4b03, and asserted by
        // execution in StorePositionReachTests.ClrArgumentRoutes_ApplyTheAcceptedVerdict, which is
        // where that claim belongs: this harness compiles to a LIBRARY and never reaches the C#
        // stage, so the CS1503 the N/A cited could not have failed a cell here either way.
        // What these cells measure is the field's REFLECTED type — the wrong-destination twin is
        // the discriminating half (with the field left Unknown it draws no error at all).

        yield return new Cell("Vector2.x-field-correct-pythonic",
            SrcFrom("system.numerics", "Vector2", "v = Vector2(1.0, 2.0)\n    f: float32 = v.x"),
            Expect.Compiles);

        yield return new Cell("Vector2.x-field-wrong-pythonic",
            SrcFrom("system.numerics", "Vector2", "v = Vector2(1.0, 2.0)\n    x: str = v.x"),
            Expect.TypeMismatch);

        yield return new Cell("Vector2.X-field-correct-clr",
            SrcFrom("system.numerics", "Vector2", "v = Vector2(1.0, 2.0)\n    f: float32 = v.X"),
            Expect.Compiles);

        yield return new Cell("Vector2.X-field-wrong-clr",
            SrcFrom("system.numerics", "Vector2", "v = Vector2(1.0, 2.0)\n    x: str = v.X"),
            Expect.TypeMismatch);

        yield return new Cell("Vector2.bogus-field",
            SrcFrom("system.numerics", "Vector2", "v = Vector2(1.0, 2.0)\n    print(v.no_such_member_xyz)"),
            Expect.AbsentMember);

        // ── User class INHERITING a CLR type: class IntList(List[int]) ──

        yield return new Cell("IntList(List[int]).count-correct-pythonic",
            SrcInherited("v = IntList()\n    v.add(1)\n    n: int = v.count"), Expect.Compiles);

        yield return new Cell("IntList(List[int]).count-wrong-pythonic",
            SrcInherited("v = IntList()\n    x: str = v.count"), Expect.TypeMismatch);

        yield return new Cell("IntList(List[int]).Count-correct-clr",
            SrcInherited("v = IntList()\n    n: int = v.Count"), Expect.Compiles);

        yield return new Cell("IntList(List[int]).Count-wrong-clr",
            SrcInherited("v = IntList()\n    x: str = v.Count"), Expect.TypeMismatch);

        yield return new Cell("IntList(List[int]).index_of()-correct-pythonic",
            SrcInherited("v = IntList()\n    v.add(7)\n    n: int = v.index_of(7)"), Expect.Compiles);

        yield return new Cell("IntList(List[int]).index_of()-wrong-pythonic",
            SrcInherited("v = IntList()\n    v.add(7)\n    x: str = v.index_of(7)"), Expect.TypeMismatch);

        yield return new Cell("IntList(List[int]).IndexOf()-correct-clr",
            SrcInherited("v = IntList()\n    v.add(7)\n    n: int = v.IndexOf(7)"), Expect.Compiles);

        yield return new Cell("IntList(List[int]).IndexOf()-wrong-clr",
            SrcInherited("v = IntList()\n    v.add(7)\n    x: str = v.IndexOf(7)"), Expect.TypeMismatch);

        yield return new Cell("IntList(List[int]).bogus-pythonic",
            SrcInherited("v = IntList()\n    print(v.no_such_member_xyz)"), Expect.AbsentMember);

        // ── STATIC type receiver: Environment (property) and DateTime (field, callee property) ──

        yield return new Cell("Environment.processor_count-correct-pythonic",
            SrcSystem("Environment", "n: int = Environment.processor_count"), Expect.Compiles);

        yield return new Cell("Environment.processor_count-wrong-pythonic",
            SrcSystem("Environment", "x: str = Environment.processor_count"), Expect.TypeMismatch);

        yield return new Cell("Environment.ProcessorCount-correct-clr",
            SrcSystem("Environment", "n: int = Environment.ProcessorCount"), Expect.Compiles);

        yield return new Cell("Environment.ProcessorCount-wrong-clr",
            SrcSystem("Environment", "x: str = Environment.ProcessorCount"), Expect.TypeMismatch);

        yield return new Cell("Environment.bogus-pythonic",
            SrcSystem("Environment", "print(Environment.no_such_member_xyz)"), Expect.AbsentMember);

        yield return new Cell("Environment.bogus-clr",
            SrcSystem("Environment", "print(Environment.NoSuchMemberXyz)"), Expect.AbsentMember);

        yield return new Cell("DateTime.max_value-static-field-correct-pythonic",
            SrcSystem("DateTime", "d: DateTime = DateTime.max_value"), Expect.Compiles);

        yield return new Cell("DateTime.max_value-static-field-wrong-pythonic",
            SrcSystem("DateTime", "x: str = DateTime.max_value"), Expect.TypeMismatch);

        yield return new Cell("DateTime.MaxValue-static-field-correct-clr",
            SrcSystem("DateTime", "d: DateTime = DateTime.MaxValue"), Expect.Compiles);

        yield return new Cell("DateTime.MaxValue-static-field-wrong-clr",
            SrcSystem("DateTime", "x: str = DateTime.MaxValue"), Expect.TypeMismatch);

        yield return new Cell("DateTime.now()-static-callee-property-correct-pythonic",
            SrcSystem("DateTime", "d: DateTime = DateTime.now()"), Expect.Compiles);

        yield return new Cell("DateTime.now()-static-callee-property-wrong-pythonic",
            SrcSystem("DateTime", "x: str = DateTime.now()"), Expect.TypeMismatch);

        yield return new Cell("DateTime.Now()-static-callee-property-correct-clr",
            SrcSystem("DateTime", "d: DateTime = DateTime.Now()"), Expect.Compiles);

        yield return new Cell("DateTime.static-bogus-pythonic",
            SrcSystem("DateTime", "print(DateTime.no_such_member_xyz)"), Expect.AbsentMember);

        // ── Declared NRT nullability (#1705) ──
        // A reflected Type is NRT-blind, so a member's `T?` must be read from the MEMBER. Axes:
        // receiver {static, instance} × member {property, method return, parameter} × declaration
        // {nullable, non-nullable twin} × position {read, None store}. The `-wrong` store cells are the
        // positive controls proving the member is typed at all (a permissive Unknown accepts both).
        yield return new Cell("nrt.static-property-nullable-read",
            SrcSystem("Environment", "n: int = Environment.process_path"), Expect.TypeMismatchNullable);
        yield return new Cell("nrt.static-property-nonnullable-read",
            SrcSystem("Environment", "n: int = Environment.current_directory"), Expect.TypeMismatchNonNullable);
        yield return new Cell("nrt.static-property-nullable-store-none",
            SrcFrom("system.globalization", "CultureInfo", "CultureInfo.default_thread_current_culture = None"), Expect.Compiles);
        yield return new Cell("nrt.static-property-nullable-store-wrong",
            SrcFrom("system.globalization", "CultureInfo", "CultureInfo.default_thread_current_culture = 1"), Expect.TypeMismatch);
        yield return new Cell("nrt.static-property-nonnullable-store-none",
            SrcFrom("system.globalization", "CultureInfo", "CultureInfo.current_culture = None"), Expect.NoneRefused);
        yield return new Cell("nrt.instance-property-nullable-read",
            SrcFrom("system.io", "DirectoryInfo", "d = DirectoryInfo(\".\")\n    n: int = d.parent"), Expect.TypeMismatchNullable);
        yield return new Cell("nrt.instance-property-nonnullable-read",
            SrcFrom("system.io", "DirectoryInfo", "d = DirectoryInfo(\".\")\n    n: int = d.full_name"), Expect.TypeMismatchNonNullable);
        yield return new Cell("nrt.instance-property-nullable-store-none",
            SrcFrom("system.threading", "Thread", "t: Thread = Thread.current_thread\n    t.name = None"), Expect.Compiles);
        yield return new Cell("nrt.instance-property-nullable-store-wrong",
            SrcFrom("system.threading", "Thread", "t: Thread = Thread.current_thread\n    t.name = 1"), Expect.TypeMismatch);
        yield return new Cell("nrt.static-method-group-return-nullable",
            SrcSystem("Environment", "n: int = Environment.get_environment_variable(\"SHARPY_NRT\")"), Expect.TypeMismatchNullable);
        yield return new Cell("nrt.static-method-single-return-nullable",
            SrcFrom("system.io", "Directory", "n: int = Directory.get_parent(\".\")"), Expect.TypeMismatchNullable);
        yield return new Cell("nrt.static-method-single-return-nonnullable",
            SrcFrom("system.io", "Directory", "n: int = Directory.get_current_directory()"), Expect.TypeMismatchNonNullable);

        // ── Member kinds the bridge once DECLINED: enum and char (#1705) ──
        // A declined member reached the permissive channel, and the DP drain turned that channel
        // into `UnmappedClrType` — displayed `object`, assignable nowhere. Each refusal cell asserts
        // the TYPE NAME as well as the code: `object` and the right answer are both SPY0220.
        yield return new Cell("enum.instance-property-correct-slot",
            SrcSystem("DateTime, DayOfWeek", "d: DayOfWeek = DateTime.now.day_of_week\n    print(d)"), Expect.Compiles);
        yield return new Cell("enum.instance-property-wrong-slot",
            SrcSystem("DateTime", "b: bool = DateTime.now.day_of_week"), Expect.TypeMismatch, "'DayOfWeek'");
        yield return new Cell("enum.instance-property-compare-with-enum-value",
            SrcSystem("DateTime, DayOfWeek", "print(DateTime.now.day_of_week == DayOfWeek.Monday)"), Expect.Compiles);
        yield return new Cell("enum.instance-property-into-enum-parameter",
            "from system import DateTime, DayOfWeek\n\ndef take(d: DayOfWeek) -> str:\n    return str(d)\n\n"
            + "def _use() -> None:\n    print(take(DateTime.now.day_of_week))\n", Expect.Compiles);
        yield return new Cell("enum.instance-property-print",
            SrcSystem("DateTime", "print(DateTime.now.kind)"), Expect.Compiles);
        yield return new Cell("enum.instance-property-match-subject",
            SrcSystem("DateTime", "d = DateTime.now.day_of_week\n    match d:\n        case _:\n            print(\"any\")"), Expect.Compiles);
        yield return new Cell("enum.static-field-value-correct-slot",
            SrcSystem("DayOfWeek", "d: DayOfWeek = DayOfWeek.Monday\n    print(d)"), Expect.Compiles);
        yield return new Cell("enum.static-field-value-wrong-slot",
            SrcSystem("DayOfWeek", "b: bool = DayOfWeek.Monday"), Expect.TypeMismatch, "'DayOfWeek'");
        yield return new Cell("enum.instance-property-exception-guard",
            "from system.net.sockets import SocketException, SocketError\n\ndef _use() -> None:\n"
            + "    try:\n        raise SocketException(10060)\n"
            + "    except SocketException as ex when ex.socket_error_code == SocketError.TimedOut:\n"
            + "        print(\"timeout\")\n", Expect.Compiles);

        yield return new Cell("char.static-field-compare-with-str",
            SrcFrom("system.io", "Path as IoPath", "print(IoPath.DirectorySeparatorChar == \"/\")"), Expect.Compiles);
        yield return new Cell("char.static-field-compare-with-char-field",
            SrcFrom("system.io", "Path as IoPath",
                "print(IoPath.AltDirectorySeparatorChar == IoPath.DirectorySeparatorChar)"), Expect.Compiles);
        yield return new Cell("char.static-field-correct-slot",
            SrcFrom("system.io", "Path as IoPath", "s: str = IoPath.DirectorySeparatorChar\n    print(s)"), Expect.Compiles);
        yield return new Cell("char.static-field-wrong-slot",
            SrcFrom("system.io", "Path as IoPath", "b: bool = IoPath.DirectorySeparatorChar"), Expect.TypeMismatch, "'str'");
        yield return new Cell("char.static-field-str-builtin",
            SrcFrom("system.io", "Path as IoPath", "print(str(IoPath.DirectorySeparatorChar))"), Expect.Compiles);
        yield return new Cell("char.origin-value-into-clr-char-slot",
            SrcFrom("system.io", "Path as IoPath",
                "t: str = \"/tmp/x/\"\n    print(t.trim_end(IoPath.DirectorySeparatorChar))"), Expect.Compiles);
        yield return new Cell("char.origin-value-into-params-char-tail",
            SrcFrom("system.io", "Path as IoPath",
                "t: str = \"/tmp/x/\"\n    print(t.trim_end(IoPath.DirectorySeparatorChar, IoPath.AltDirectorySeparatorChar))"),
            Expect.Compiles);

        // ── A member declared with a BARE type parameter on a generic DEFINITION (#1705 B1) ──
        // The overload index reflects over `List<T>`, where NullabilityInfoContext calls an
        // unconstrained `T` Nullable. Typing it `T | None` refused stores that used to compile and
        // broke union-constructor inference through `append`'s slot.
        yield return new Cell("generic-definition-T.method-return-not-nullable",
            "def _use() -> None:\n    xs: list[str] = [\"a\"]\n    b: bool = xs.pop(0)\n",
            Expect.TypeMismatchNonNullable, "'str'");
        yield return new Cell("generic-definition-T.method-return-correct-slot",
            "def _use() -> None:\n    xs: list[str] = [\"a\"]\n    s: str = xs.pop(0)\n    print(s)\n", Expect.Compiles);
        yield return new Cell("generic-definition-T.optional-unwrap-or-not-nullable",
            "def _use() -> None:\n    v: str? = Some(\"x\")\n    b: bool = v.unwrap_or(\"\")\n",
            Expect.TypeMismatchNonNullable, "'str'");
        yield return new Cell("generic-definition-T.parameter-slot-refuses-none",
            "def _use() -> None:\n    xs: list[str] = [\"a\"]\n    xs.append(None)\n", Expect.NoneRefused);
        yield return new Cell("generic-definition-T.parameter-slot-takes-a-union-case",
            "union Box[T]:\n    case Full(v: T)\n    case Empty()\n\ndef _use() -> None:\n"
            + "    xs = [Box.Full(1), Box.Full(2)]\n    xs.append(Box.Empty())\n    print(len(xs))\n", Expect.Compiles);
    }

    /// <summary>
    /// The axis totality pin. The member-kind and declaration axes are spelled as LITERALS here, not
    /// derived from the cells, so a cell set that silently loses an axis value fails instead of
    /// shrinking quietly (a count taken from the same source it checks is vacuous).
    /// </summary>
    [Fact]
    [Trait("Category", "Conformance")]
    public void FidelityMatrix_CoversEveryMemberKindAndDeclarationAxisValue()
    {
        string[] memberKindPrefixes =
        {
            "enum.instance-property", "enum.static-field", "char.static-field",
            "char.origin-value", "generic-definition-T.method-return",
            "generic-definition-T.parameter-slot", "nrt.instance-property", "nrt.static-property",
            "nrt.static-method"
        };

        var labels = GenerateCells().Select(c => c.Label).ToList();
        var naLabels = NotApplicableCells().Select(n => n.Label).ToList();

        var missing = memberKindPrefixes
            .Where(prefix => !labels.Any(l => l.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        Assert.True(missing.Count == 0,
            "member-kind axis value(s) with no cell: " + string.Join(", ", missing));

        // Every axis value that CANNOT be measured is named, with an issue or a design rule.
        Assert.Contains(naLabels, l => l == "nrt.closed-generic-annotated-T-return");
        Assert.Contains(naLabels, l => l == "nrt.extension-annotated-T-return");
        Assert.Contains(naLabels, l => l == "route.warm-incremental");
    }

    private static string Src(string type, string body) =>
        $"from system.collections.generic import {type}\n\ndef _use() -> None:\n    {body}\n";

    private static string SrcText(string body) =>
        $"from system.text import StringBuilder\n\ndef _use() -> None:\n    {body}\n";

    private static string SrcSystem(string type, string body) =>
        $"from system import {type}\n\ndef _use() -> None:\n    {body}\n";

    private static string SrcFrom(string ns, string type, string body) =>
        $"from {ns} import {type}\n\ndef _use() -> None:\n    {body}\n";

    private static string SrcInherited(string body) =>
        "from system.collections.generic import List\n\nclass IntList(List[int]):\n    pass\n\n"
        + $"def _use() -> None:\n    {body}\n";
}
