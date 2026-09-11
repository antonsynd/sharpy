using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The standing applicability matrix for overload resolution (#1810's close criterion): <b>the
/// keyword column equals the positional column at every step — applicability, inference AND
/// betterness — on every route</b>.
///
/// <para>Axes: host/route (10) × argument spelling (4) × candidate shape (5) = 200 cells. Every cell
/// is a program that COMPILES AND RUNS and prints the label of the candidate that won, and every
/// cell asserts twice: the winner is the one the oracle names, and all four spellings of the same
/// call produce the SAME output. The four spellings differ in nothing but spelling — same overload
/// pair, same argument expression — so a difference between two of them is a defect by
/// construction.</para>
///
/// <para>The oracle is C# (Axiom 1). The two shapes that turn on betterness were measured against
/// csc on net10.0 (2026-09-10): <c>G&lt;V&gt;(V)</c> against <c>G&lt;K,V&gt;(Dictionary&lt;K,V&gt;)</c>
/// called with a <c>Dictionary&lt;string,int&gt;</c> picks the constructed overload in BOTH the
/// positional and the named spelling, and <c>F&lt;T&gt;(List&lt;T&gt;)</c> against
/// <c>F&lt;T&gt;(List&lt;List&lt;T&gt;&gt;)</c> called with a <c>List&lt;List&lt;int&gt;&gt;</c> picks
/// the nested one in both. Sharpy printed <c>bare</c> for <c>f(v=d)</c> and <c>structured</c> for
/// <c>f(d)</c> before this matrix existed — a silent wrong pick, because a keyword
/// <c>ArgumentRef</c> carries no ordinal and the betterness arms derived the parameter index from
/// it. The cure is the resolved parameter index on <c>BoundArgument</c>; this matrix is what makes
/// the class un-recurrable.</para>
///
/// <para>Why the expected label depends on the SHAPE alone, never on the host or the spelling: that
/// is the contract. A host-specific or spelling-specific expectation would be a matrix that drifted
/// with the defect.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class OverloadApplicabilityMatrixTests : IntegrationTestBase, IDisposable
{
    private const int HostCount = 10;
    private const int SpellingCount = 4;
    private const int ShapeCount = 5;
    private const int CellCount = HostCount * SpellingCount * ShapeCount;

    /// <summary>
    /// <c>ExplicitGeneric × BareVsConstructed</c>, in all four spellings: the only combination in the
    /// matrix that cannot be written down. See <see cref="Shape.ExplicitTypeArgs"/>.
    /// </summary>
    private const int UndeclarableCellCount = SpellingCount;

    private const string Ice = DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError;
    private const string AmbiguousOverload = DiagnosticCodes.Semantic.AmbiguousOverload;

    private readonly string _tempDir;

    public OverloadApplicabilityMatrixTests(ITestOutputHelper output) : base(output)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sharpy-overload-applic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    // ── the candidate shapes ─────────────────────────────────────────────────────────────

    /// <summary>
    /// One overload PAIR and the call that measures it. Candidate A prints <c>A</c>, candidate B
    /// prints <c>B</c>; both take a second parameter <c>tag</c> so that the mixed and
    /// out-of-order keyword spellings exist at all.
    /// </summary>
    /// <param name="MethodTypeParamsA">The type parameters candidate A declares when the pair is
    /// hosted on a FUNCTION or a METHOD (<c>""</c>, <c>"[V]"</c>, …).</param>
    /// <param name="ClassTypeParams">The type parameters the pair needs when it is hosted on a
    /// CONSTRUCTOR, where they must live on the CLASS: a constructor is not generic in C#, so a
    /// method-level type parameter on <c>__init__</c> is refused by name (SPY0705, #1836). It used
    /// to be accepted and emitted nowhere, reaching Roslyn as CS0246 behind SPY0908 — which is why
    /// these hosts were written this way before the refusal existed.</param>
    /// <param name="ClassTypeArgs">The type arguments written at the construction site for
    /// <paramref name="ClassTypeParams"/>.</param>
    /// <param name="ExplicitTypeArgs">The type-argument list written at the call site for the
    /// <c>ExplicitGeneric</c> host, or null when no single list closes both candidates — which is
    /// exactly when the two candidates declare DIFFERENT numbers of type parameters, and is asserted
    /// to be so in <see cref="Axes_AreAnchored_AndTheUndeclarableCellIsJustified"/>.</param>
    private sealed record Shape(
        string Name,
        string Prelude,
        string[] PreludeExports,
        string MethodTypeParamsA,
        string MethodTypeParamsB,
        string FormalA,
        string FormalB,
        string ClassTypeParams,
        string ClassTypeArgs,
        string ClassFormalA,
        string ClassFormalB,
        string ArgType,
        string Setup,
        string Arg,
        string Expected,
        string? ExplicitTypeArgs);

    private const string AnimalPrelude =
        "class Animal:\n    def __init__(self) -> None:\n        pass\n\n"
        + "class Dog(Animal):\n    def __init__(self) -> None:\n        pass\n\n";

    private static readonly Shape[] Shapes =
    {
        // A non-generic pair: nothing but applicability decides, and it must decide the same way in
        // all four spellings. The control arm of the matrix — if this cell ever diverges, the defect
        // is not in betterness at all.
        new("NonGenericPair", Prelude: "", PreludeExports: new string[0],
            MethodTypeParamsA: "", MethodTypeParamsB: "",
            FormalA: "int", FormalB: "str",
            ClassTypeParams: "", ClassTypeArgs: "", ClassFormalA: "int", ClassFormalB: "str",
            ArgType: "int", Setup: "", Arg: "1", Expected: "A", ExplicitTypeArgs: "[int]"),

        // C# §12.6.4.5 criterion 3, the #1810 regression cell: per-candidate inference closes BOTH
        // formals to dict[str, int32], so only the DECLARED shape separates them — a constructed
        // type beats a bare type parameter. csc: structured, in both spellings.
        new("BareVsConstructed", Prelude: "", PreludeExports: new string[0],
            MethodTypeParamsA: "[V]", MethodTypeParamsB: "[K, V]",
            FormalA: "V", FormalB: "dict[K, V]",
            ClassTypeParams: "[V]", ClassTypeArgs: "[int]",
            ClassFormalA: "V", ClassFormalB: "dict[str, V]",
            ArgType: "dict[str, int]", Setup: "    d0: dict[str, int] = {\"a\": 1}\n", Arg: "d0",
            Expected: "B", ExplicitTypeArgs: null),

        // The nested twin, which overload_resolution.md states as criterion 3's own example: both
        // formals close to list[list[int32]] and list[list[T]] is the more specific declared shape.
        // csc: nested, in both spellings.
        new("NestedConstructed", Prelude: "", PreludeExports: new string[0],
            MethodTypeParamsA: "[T]", MethodTypeParamsB: "[T]",
            FormalA: "list[T]", FormalB: "list[list[T]]",
            ClassTypeParams: "[T]", ClassTypeArgs: "[int]",
            ClassFormalA: "list[T]", ClassFormalB: "list[list[T]]",
            ArgType: "list[list[int]]", Setup: "    x0: list[list[int]] = [[1], [2]]\n", Arg: "x0",
            Expected: "B", ExplicitTypeArgs: "[int, int]"),

        // A derived argument against a base/derived pair: the derived parameter is better (§12.6.4.4).
        new("DerivedBase", Prelude: AnimalPrelude, PreludeExports: new[] { "Animal", "Dog" },
            MethodTypeParamsA: "", MethodTypeParamsB: "",
            FormalA: "Animal", FormalB: "Dog",
            ClassTypeParams: "", ClassTypeArgs: "", ClassFormalA: "Animal", ClassFormalB: "Dog",
            ArgType: "Dog", Setup: "    g0 = Dog()\n", Arg: "g0", Expected: "B",
            ExplicitTypeArgs: "[int]"),

        // Identity beats a widening numeric conversion (§12.6.4.6): an int argument picks int, not
        // float, and the same in every spelling.
        new("IntFloatWidening", Prelude: "", PreludeExports: new string[0],
            MethodTypeParamsA: "", MethodTypeParamsB: "",
            FormalA: "int", FormalB: "float",
            ClassTypeParams: "", ClassTypeArgs: "", ClassFormalA: "int", ClassFormalB: "float",
            ArgType: "int", Setup: "", Arg: "1", Expected: "A", ExplicitTypeArgs: "[int]"),
    };

    // ── the spellings ────────────────────────────────────────────────────────────────────

    private static readonly string[] Spellings =
        { "Positional", "Keyword", "Mixed", "KeywordOutOfOrder" };

    /// <summary>
    /// The argument list, in one of the four spellings. <paramref name="arg"/> is the SAME expression
    /// in all four; only where the names go changes.
    /// </summary>
    private static string Arguments(string spelling, string arg) => spelling switch
    {
        "Positional" => $"{arg}, 0",
        "Keyword" => $"v={arg}, tag=0",
        "Mixed" => $"{arg}, tag=0",
        "KeywordOutOfOrder" => $"tag=0, v={arg}",
        _ => throw new InvalidOperationException(spelling),
    };

    // ── the hosts ────────────────────────────────────────────────────────────────────────

    private static readonly string[] Hosts =
    {
        "ModuleDef", "Method", "StaticMethod", "DunderCall", "Init",
        "SuperInit", "ExplicitGeneric", "InferredGeneric", "GenericReceiver", "ImportedDef",
    };

    /// <summary>The pair's type parameters plus a fresh <c>U</c>, for the two hosts that need a type
    /// parameter to infer or to write even when the shape itself is not generic.</summary>
    private static string WithU(string typeParams)
        => typeParams.Length == 0 ? "[U]" : typeParams[..^1] + ", U]";

    private static string FunctionPair(
        Shape shape, string name, string indent, string selfParam, string tagType,
        string bodyPrefix, string tpA, string tpB)
        => $"{indent}def {name}{tpA}({selfParam}v: {shape.FormalA}, tag: {tagType}) -> str:\n"
           + $"{indent}    {bodyPrefix}\"A\"\n"
           + $"{indent}def {name}{tpB}({selfParam}v: {shape.FormalB}, tag: {tagType}) -> str:\n"
           + $"{indent}    {bodyPrefix}\"B\"\n";

    private static string InitPair(Shape shape, string indent)
        => $"{indent}def __init__(self, v: {shape.ClassFormalA}, tag: int) -> None:\n"
           + $"{indent}    print(\"A\")\n"
           + $"{indent}def __init__(self, v: {shape.ClassFormalB}, tag: int) -> None:\n"
           + $"{indent}    print(\"B\")\n";

    /// <summary>The program for one cell, or null when the cell is not declarable.</summary>
    private static string? Program(string host, Shape shape, string spelling)
    {
        var args = Arguments(spelling, shape.Arg);
        switch (host)
        {
            case "ModuleDef":
                return shape.Prelude
                    + FunctionPair(shape, "f", "", "", "int", "return ",
                        shape.MethodTypeParamsA, shape.MethodTypeParamsB)
                    + "\ndef main():\n" + shape.Setup + $"    print(f({args}))\n";

            case "Method":
                return shape.Prelude + "class H:\n"
                    + FunctionPair(shape, "m", "    ", "self, ", "int", "return ",
                        shape.MethodTypeParamsA, shape.MethodTypeParamsB)
                    + "\ndef main():\n    h = H()\n" + shape.Setup + $"    print(h.m({args}))\n";

            case "StaticMethod":
                // A method without `self` IS static in Sharpy (there is no @staticmethod).
                return shape.Prelude + "class H:\n"
                    + FunctionPair(shape, "m", "    ", "", "int", "return ",
                        shape.MethodTypeParamsA, shape.MethodTypeParamsB)
                    + "\ndef main():\n" + shape.Setup + $"    print(H.m({args}))\n";

            case "DunderCall":
                return shape.Prelude + "class H:\n"
                    + FunctionPair(shape, "__call__", "    ", "self, ", "int", "return ",
                        shape.MethodTypeParamsA, shape.MethodTypeParamsB)
                    + "\ndef main():\n    h = H()\n" + shape.Setup + $"    print(h({args}))\n";

            case "Init":
                return shape.Prelude + $"class C{shape.ClassTypeParams}:\n" + InitPair(shape, "    ")
                    + "\ndef main():\n" + shape.Setup + $"    C{shape.ClassTypeArgs}({args})\n";

            case "SuperInit":
                // The derived initializer takes the argument as a PARAMETER rather than declaring a
                // local: a local read inside a super().__init__ argument is CS0103 behind SPY0908
                // today, on a single base initializer too, so it is not this matrix's subject.
                return shape.Prelude + $"class Base{shape.ClassTypeParams}:\n" + InitPair(shape, "    ")
                    + $"\nclass D(Base{shape.ClassTypeArgs}):\n"
                    + $"    def __init__(self, v0: {shape.ArgType}) -> None:\n"
                    + $"        super().__init__({Arguments(spelling, "v0")})\n"
                    + "\ndef main():\n" + shape.Setup + $"    D({shape.Arg})\n";

            case "ExplicitGeneric":
                if (shape.ExplicitTypeArgs is not { } written)
                    return null;
                return shape.Prelude
                    + FunctionPair(shape, "f", "", "", "U", "return ",
                        WithU(shape.MethodTypeParamsA), WithU(shape.MethodTypeParamsB))
                    + "\ndef main():\n" + shape.Setup + $"    print(f{written}({args}))\n";

            case "InferredGeneric":
                return shape.Prelude
                    + FunctionPair(shape, "f", "", "", "U", "return ",
                        WithU(shape.MethodTypeParamsA), WithU(shape.MethodTypeParamsB))
                    + "\ndef main():\n" + shape.Setup + $"    print(f({args}))\n";

            case "GenericReceiver":
                return shape.Prelude + "class Box[T]:\n"
                    + FunctionPair(shape, "m", "    ", "self, ", "int", "return ",
                        shape.MethodTypeParamsA, shape.MethodTypeParamsB)
                    + "\ndef main():\n    b = Box[int]()\n" + shape.Setup
                    + $"    print(b.m({args}))\n";

            case "ImportedDef":
                return null; // two files; see ImportedLib/ImportedMain.
            default:
                throw new InvalidOperationException(host);
        }
    }

    private static string ImportedLib(Shape shape)
        => shape.Prelude
           + FunctionPair(shape, "f", "", "", "int", "return ",
               shape.MethodTypeParamsA, shape.MethodTypeParamsB);

    private static string ImportedMain(Shape shape, string spelling)
    {
        var imports = string.Join(", ", new[] { "f" }.Concat(shape.PreludeExports));
        return $"from lib import {imports}\n\ndef main():\n" + shape.Setup
            + $"    print(f({Arguments(spelling, shape.Arg)}))\n";
    }

    // ── the matrix ───────────────────────────────────────────────────────────────────────

    public static IEnumerable<object[]> Cells
        => from h in Hosts
           from s in Shapes
           select new object[] { h, s.Name };

    [Fact]
    public void Axes_AreAnchored_AndTheUndeclarableCellIsJustified()
    {
        Hosts.Length.Should().Be(HostCount);
        Spellings.Length.Should().Be(SpellingCount);
        Shapes.Length.Should().Be(ShapeCount);
        (Hosts.Length * Spellings.Length * Shapes.Length).Should().Be(CellCount);

        // Exactly one shape is not writable at the explicit-substitution host, and the REASON is a
        // checked property of the shape rather than a sentence: a single written type-argument list
        // cannot close two candidates that declare different numbers of type parameters — the
        // explicit route filters by type-parameter arity before betterness is ever consulted.
        foreach (var shape in Shapes)
        {
            var sameArity = shape.MethodTypeParamsA == shape.MethodTypeParamsB;
            (shape.ExplicitTypeArgs != null).Should().Be(sameArity,
                $"{shape.Name}: a written type-argument list exists exactly when both candidates "
                + "declare the same type parameters");
        }

        Shapes.Count(s => s.ExplicitTypeArgs == null).Should().Be(
            UndeclarableCellCount / SpellingCount,
            "one shape × four spellings is the whole undeclarable part of the matrix");

        Shapes.Select(s => s.Expected).Should().OnlyContain(e => e == "A" || e == "B");
    }

    /// <summary>
    /// One (host × shape) cell, run in all four spellings. Two assertions per spelling: the winner is
    /// the one the C# oracle names, and the output is IDENTICAL to the positional spelling's. The
    /// second is the one #1810 is about — it is what <c>f(v=d)</c> printing <c>bare</c> while
    /// <c>f(d)</c> printed <c>structured</c> violated.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cells))]
    public void Cell_ResolvesIdentically_InEverySpelling(string host, string shapeName)
    {
        var shape = Shapes.Single(s => s.Name == shapeName);
        string? positionalOutput = null;
        int measured = 0;

        foreach (var spelling in Spellings)
        {
            var label = $"[{host} × {shapeName} × {spelling}]";
            string source;
            ExecutionResult result;

            if (host == "ImportedDef")
            {
                var dir = Path.Combine(_tempDir, $"{host}_{shapeName}_{spelling}");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "lib.spy"), ImportedLib(shape));
                source = ImportedMain(shape, spelling);
                File.WriteAllText(Path.Combine(dir, "main.spy"), source);
                result = CompileAndExecuteProject(dir, "main.spy");
            }
            else
            {
                if (Program(host, shape, spelling) is not { } program)
                    continue;
                source = program;
                result = CompileAndExecute(source);
            }

            measured++;
            var diagnostics = string.Join(" | ",
                result.RawDiagnostics.Select(d => $"{d.Code}@{d.Line}:{d.Column} {d.Message}"));

            result.RawDiagnostics.Should().NotContain(d => d.Code == Ice,
                $"{label} must be decided in semantic analysis, never by Roslyn: {diagnostics}\n{source}");
            result.RawDiagnostics.Should().NotContain(d => d.Code == AmbiguousOverload,
                $"{label} has one winner by construction; a tie is the defect: {diagnostics}\n{source}");
            result.Success.Should().BeTrue($"{label} must compile and run: {diagnostics}\n{source}");
            result.StandardOutput.Should().Be(shape.Expected + "\n",
                $"{label} must dispatch to candidate {shape.Expected}\n{source}");

            if (spelling == "Positional")
                positionalOutput = result.StandardOutput;
            else
                result.StandardOutput.Should().Be(positionalOutput,
                    $"{label} must produce exactly what the positional spelling produced — the "
                    + $"keyword column equals the positional column (#1810)\n{source}");
        }

        measured.Should().Be(
            shape.ExplicitTypeArgs == null && host == "ExplicitGeneric" ? 0 : SpellingCount,
            $"[{host} × {shapeName}] must measure every spelling it can declare");
    }

    // ── the refusal shapes, by code and span ─────────────────────────────────────────────

    /// <summary>
    /// A binding failure on an OVERLOADED callee is reported with the single-candidate route's own
    /// code at the keyword's own span (#1810, Decision 6(b)). The <c>.error</c> fixture sidecars pin
    /// the message and the location but CANNOT pin a code — <c>CompilationErrors</c> carries
    /// <c>Message</c> only — so the codes are pinned here, each against its single-candidate twin.
    /// A cell whose two halves disagree is the defect.
    /// </summary>
    [Theory]
    [InlineData("Unknown", "g(z=1)", DiagnosticCodes.Semantic.UnknownKeywordArgument)]
    [InlineData("PositionalOnly", "g(x=1)", DiagnosticCodes.Semantic.PositionalOnlyPassedByKeyword)]
    [InlineData("Duplicate", "g(1, x=2)", DiagnosticCodes.Semantic.DuplicateArgument)]
    [InlineData("NoneCall", "g(x=None())", DiagnosticCodes.Semantic.InvalidNoneConstructor)]
    public void KeywordBindingFailure_IsTheSameCodeAndSpan_OverloadedOrNot(
        string cell, string call, string expectedCode)
    {
        var (singleDecls, overloadedDecls) = cell switch
        {
            "PositionalOnly" => (
                "def g(x: int, /, y: int = 0) -> str:\n    return \"int\"\n",
                "def g(x: int, /, y: int = 0) -> str:\n    return \"int\"\n"
                + "def g(x: str, /) -> str:\n    return \"str\"\n"),
            "Duplicate" => (
                "def g(x: int, y: int = 0) -> str:\n    return \"int\"\n",
                "def g(x: int, y: int = 0) -> str:\n    return \"int\"\n"
                + "def g(x: str) -> str:\n    return \"str\"\n"),
            _ => (
                "def g(x: int) -> str:\n    return \"int\"\n",
                "def g(x: int) -> str:\n    return \"int\"\n"
                + "def g(x: str) -> str:\n    return \"str\"\n"),
        };

        var singleSource = singleDecls + $"\ndef main():\n    print({call})\n";
        var overloadedSource = overloadedDecls + $"\ndef main():\n    print({call})\n";
        var single = CompileAndExecute(singleSource);
        var overloaded = CompileAndExecute(overloadedSource);

        // The call is the last line of each program; the two programs differ only in how many
        // declarations precede it, so the LINE is compared against each program's own call line and
        // the COLUMN — the part that says "at the keyword, not at the call" — is compared across them.
        static int CallLineOf(string source) => source.TrimEnd('\n').Split('\n').Length;

        var singleDiag = single.RawDiagnostics.FirstOrDefault(d => d.Code == expectedCode);
        singleDiag.Should().NotBeNull(
            $"[{cell}] single-candidate control must report {expectedCode}: "
            + string.Join(" | ", single.RawDiagnostics.Select(d => d.Code + " " + d.Message)));

        var overloadedDiag = overloaded.RawDiagnostics.FirstOrDefault(d => d.Code == expectedCode);
        overloadedDiag.Should().NotBeNull(
            $"[{cell}] the overloaded callee must report {expectedCode} too, not SPY0354: "
            + string.Join(" | ", overloaded.RawDiagnostics.Select(d => d.Code + " " + d.Message)));

        overloaded.RawDiagnostics.Should().NotContain(d => d.Code == Ice,
            $"[{cell}] must not reach Roslyn");
        overloadedDiag!.Column.Should().Be(singleDiag!.Column,
            $"[{cell}] the overloaded refusal must point at the same column as the single-candidate one");
        singleDiag.Line.Should().Be(CallLineOf(singleSource),
            $"[{cell}] the single-candidate refusal belongs at its own call line");
        overloadedDiag.Line.Should().Be(CallLineOf(overloadedSource),
            $"[{cell}] the overloaded refusal belongs at its own call line, not inside a candidate");

        // The three keyword-binding refusals say exactly what their single-candidate twin says — the
        // message is one shared reporter. SPY0244 is the one that legitimately says MORE at an
        // overload set: it names every slot the candidates offer and adds the steer a single
        // candidate has no reason to give, so it is pinned to the shared opening instead.
        if (cell == "NoneCall")
        {
            const string opening = "'None()' can only construct Optional types, not ";
            singleDiag.Message.Should().StartWith(opening);
            overloadedDiag.Message.Should().StartWith(opening,
                "[NoneCall] the overloaded refusal is the constructor's own, not SPY0354");
            overloadedDiag.Message.Should().Contain("'int32'",
                "[NoneCall] and names every slot the candidates offer");
            overloadedDiag.Message.Should().Contain("'str'");
        }
        else
        {
            overloadedDiag.Message.Should().Be(singleDiag.Message, $"[{cell}] and say the same thing");
        }
    }

    /// <summary>
    /// Ambiguity is SPY0353 on the <c>super().__init__</c> route too (#1810, Decision 4), in both
    /// spellings, at the CALL — it used to be CS0121 behind SPY0908, reported inside the second
    /// overload's body. The positive control is the same base pair resolved uniquely.
    /// </summary>
    [Theory]
    [InlineData("x=1")]
    [InlineData("1")]
    public void SuperInitializerAmbiguity_IsRefusedByName_AtTheCall(string args)
    {
        var source = "class Base:\n"
            + "    def __init__(self, x: int, y: str = \"a\") -> None:\n        print(\"int-str\")\n"
            + "    def __init__(self, x: int, y: float = 1.0) -> None:\n        print(\"int-float\")\n"
            + "\nclass D(Base):\n    def __init__(self) -> None:\n"
            + $"        super().__init__({args})\n"
            + "\ndef main():\n    D()\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse($"the pair is ambiguous: {result.StandardOutput}");
        result.RawDiagnostics.Should().NotContain(d => d.Code == Ice,
            "the ambiguity is decided here, not by Roslyn: "
            + string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message)));
        var ambiguous = result.RawDiagnostics.FirstOrDefault(d => d.Code == AmbiguousOverload);
        ambiguous.Should().NotBeNull(string.Join(" | ",
            result.RawDiagnostics.Select(d => d.Code + " " + d.Message)));
        ambiguous!.Line.Should().Be(9,
            "the refusal belongs at the super().__init__ call (source line 9), not inside an "
            + "overload's body — CS0121 used to point at line 5");
    }

    /// <summary>
    /// The positive control for the test above: the same route, a base pair the resolver separates,
    /// in both spellings. Without it, an ambiguity refusal that swallowed every
    /// <c>super().__init__</c> call would look like a passing guard.
    /// </summary>
    [Theory]
    [InlineData("1", "int")]
    [InlineData("x=1", "int")]
    [InlineData("\"a\"", "str")]
    [InlineData("x=\"a\"", "str")]
    public void SuperInitializerWithAUniqueWinner_StillDispatches(string args, string expected)
    {
        var source = "class Base:\n"
            + "    def __init__(self, x: int) -> None:\n        print(\"int\")\n"
            + "    def __init__(self, x: str) -> None:\n        print(\"str\")\n"
            + "\nclass D(Base):\n    def __init__(self) -> None:\n"
            + $"        super().__init__({args})\n"
            + "\ndef main():\n    D()\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(string.Join(" | ",
            result.RawDiagnostics.Select(d => d.Code + " " + d.Message)) + "\n" + source);
        result.StandardOutput.Should().Be(expected + "\n", source);
    }
}
