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
        /// SPY0220 whose offending type is spelled nullable (<c>'str?' to</c>): the member's declared
        /// <c>T?</c> reached the recorded type (#1705).
        /// </summary>
        TypeMismatchNullable,

        /// <summary>SPY0220 whose offending type is NOT spelled nullable — the non-nullable twin (#1705).</summary>
        TypeMismatchNonNullable,

        /// <summary>A <c>None</c> store into a member declared non-nullable: SPY0229 from the checker (#1705).</summary>
        NoneRefused
    }

    private sealed record Cell(string Label, string Source, Expect Expect);

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

        yield return new NotApplicable("Vector2.x-field-correct",
            "the CONSTRUCTOR ICEs first — a float literal bound to a CLR `float` (Single) parameter "
            + "is CS1503 behind SPY0908 (#1688), so no Vector2 value can be built to read a field "
            + "from. The field itself resolves: the wrong-destination twin draws SPY0220 (float32).");

        yield return new NotApplicable("Stack[int].field-any",
            "System.Collections.Generic.Stack<T> declares no public field, so the "
            + "receiver × field cells of the unmapped-generic row have no member to name.");

        yield return new NotApplicable("StringBuilder.static-member-any",
            "System.Text.StringBuilder declares no public static member, so the "
            + "static-receiver cells of the non-generic-class row have no member to name.");

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
