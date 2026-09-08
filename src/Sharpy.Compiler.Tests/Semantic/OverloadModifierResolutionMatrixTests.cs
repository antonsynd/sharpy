using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Project;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Resolution matrix for overload pairs that differ only by a signature MODIFIER (#1721,
/// plan-499995 Phase 2). The pairs used to collapse to one signature (SPY0204/SPY0355/SPY0701);
/// once they declare, resolution among them must have ONE winner by construction or by
/// identity-first betterness, and the winner must be the overload the emitted C# binds:
/// <list type="bullet">
/// <item>a slot-typed argument (<c>None()</c>, <c>Some(v)</c>, <c>Ok(v)</c>) is typed by the
/// SELECTED overload's slot, never by whichever candidate was declared first;</item>
/// <item><c>None()</c> selects the Optional slot or is refused by name (SPY0244), and a bare value
/// never selects an Optional slot (strict Optional, R-G);</item>
/// <item>a bare value bound to a <c>T | None</c> slot of an overloaded callee is emitted cast to
/// that slot — Roslyn admits <c>T → Optional&lt;T&gt;</c> implicitly and would otherwise report
/// CS0121 (behind SPY0908) for <c>{T?, T | None}</c>;</item>
/// <item>the rule is the same at every host: module def, method, <c>__init__</c>, imported def.</item>
/// </list>
/// Axes: pair (5) × argument (8) × host (4). Every accepted cell PRINTS the winner's tag; every
/// refused cell names its code and never reaches Roslyn. The resolver has no tie anywhere in this
/// matrix, so SPY0353 is asserted absent on every cell — a tie that appears is a red anchor, not a
/// cell to allowlist. The refused half is a literal count.
/// </summary>
[Collection("HeavyCompilation")]
public class OverloadModifierResolutionMatrixTests : IntegrationTestBase, IDisposable
{
    private const int PairCount = 5;
    private const int ArgCount = 8;
    private const int HostCount = 4;
    private const int CellCount = PairCount * ArgCount * HostCount;
    private const int RefusedCellCount = 23 * HostCount;

    private readonly string _tempDir;

    public OverloadModifierResolutionMatrixTests(ITestOutputHelper output) : base(output)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sharpy-overload-mod-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    private const string TypeMismatch = DiagnosticCodes.Semantic.TypeMismatch;
    private const string InvalidNoneConstructor = DiagnosticCodes.Semantic.InvalidNoneConstructor;
    private const string AmbiguousOverload = DiagnosticCodes.Semantic.AmbiguousOverload;
    private const string Ice = DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError;

    /// <summary>One overload pair: two slots that differ only by a modifier, and the tag each prints.</summary>
    private sealed record Pair(string Name, string Slot1, string Tag1, string Slot2, string Tag2);

    /// <summary>
    /// One argument: the lines that set it up inside <c>main</c> (already indented four spaces),
    /// the expression, and the indent the call line takes (eight inside a narrowing <c>if</c>).
    /// </summary>
    private sealed record Arg(string Name, string Setup, string Expr, int CallIndent = 4);

    private sealed record Outcome(string? Tag, string? RefusalCode)
    {
        public static Outcome Prints(string tag) => new(tag, null);
        public static Outcome Refused(string code) => new(null, code);
    }

    private static readonly Pair[] Pairs =
    {
        new("IntOptional", "int", "int", "int?", "opt"),
        new("IntNullable", "int", "int", "int | None", "nullable"),
        new("OptionalNullable", "int?", "opt", "int | None", "nullable"),
        new("IntResult", "int", "int", "int!str", "result"),
        new("ListOptionalList", "list[int]", "ints", "list[int?]", "opts"),
    };

    private static readonly Arg[] Args =
    {
        new("Bare", "", "1"),
        new("BareNone", "", "None"),
        new("Some", "", "Some(1)"),
        new("NoneCall", "", "None()"),
        new("Ok", "", "Ok(1)"),
        new("NarrowedRead", "    y: int | None = 3\n    if y is not None:\n", "y", CallIndent: 8),
        new("ListRead", "    a: list[int] = [1]\n", "a"),
        new("OptListRead", "    b: list[int?] = [Some(1)]\n", "b"),
    };

    /// <summary>
    /// The contract, cell by cell. Written as a table rather than derived from the slots, so a
    /// rule that drifts shows up as a red cell and not as a matrix that drifted with it.
    /// </summary>
    private static Outcome Expected(Pair pair, Arg arg)
    {
        var (tag1, tag2) = (pair.Tag1, pair.Tag2);
        return (pair.Name, arg.Name) switch
        {
            // {int, int?}: a bare value is the identity match; Some/None() are the Optional's.
            ("IntOptional", "Bare" or "NarrowedRead") => Outcome.Prints(tag1),
            ("IntOptional", "Some" or "NoneCall") => Outcome.Prints(tag2),
            ("IntOptional", "BareNone" or "Ok" or "ListRead" or "OptListRead") => Outcome.Refused(TypeMismatch),

            // {int, int | None}: None goes to the nullable slot; an Optional value fits neither.
            ("IntNullable", "Bare" or "NarrowedRead") => Outcome.Prints(tag1),
            ("IntNullable", "BareNone") => Outcome.Prints(tag2),
            ("IntNullable", "NoneCall") => Outcome.Refused(InvalidNoneConstructor),
            ("IntNullable", "Some" or "Ok" or "ListRead" or "OptListRead") => Outcome.Refused(TypeMismatch),

            // {int?, int | None}: strict Optional sends the bare value to the nullable slot
            // (emitted with the cast that breaks Roslyn's tie); Some/None() are the Optional's.
            ("OptionalNullable", "Bare" or "BareNone" or "NarrowedRead") => Outcome.Prints(tag2),
            ("OptionalNullable", "Some" or "NoneCall") => Outcome.Prints(tag1),
            ("OptionalNullable", "Ok" or "ListRead" or "OptListRead") => Outcome.Refused(TypeMismatch),

            // {int, int!str}: Ok(v) is typed by the Result slot it selects.
            ("IntResult", "Bare" or "NarrowedRead") => Outcome.Prints(tag1),
            ("IntResult", "Ok") => Outcome.Prints(tag2),
            ("IntResult", "NoneCall") => Outcome.Refused(InvalidNoneConstructor),
            ("IntResult", "BareNone" or "Some" or "ListRead" or "OptListRead") => Outcome.Refused(TypeMismatch),

            // {list[int], list[int?]}: the element modifier is part of the signature.
            ("ListOptionalList", "ListRead") => Outcome.Prints(tag1),
            ("ListOptionalList", "OptListRead") => Outcome.Prints(tag2),
            ("ListOptionalList", "NoneCall") => Outcome.Refused(InvalidNoneConstructor),
            ("ListOptionalList", _) => Outcome.Refused(TypeMismatch),

            _ => throw new InvalidOperationException($"{pair.Name} × {arg.Name}"),
        };
    }

    // ── hosts ────────────────────────────────────────────────────────────────────────────

    private static string Overloads(Pair pair, string receiver, string name, string body1, string body2)
        => $"{receiver}def {name}(x: {pair.Slot1}) -> str:\n{receiver}    return \"{body1}\"\n"
           + $"{receiver}def {name}(x: {pair.Slot2}) -> str:\n{receiver}    return \"{body2}\"\n";

    private static string Call(Arg arg, string callee)
        => arg.Setup + new string(' ', arg.CallIndent) + $"print({callee}({arg.Expr}))\n";

    private static string ModuleDef(Pair pair, Arg arg)
        => Overloads(pair, "", "f", pair.Tag1, pair.Tag2) + "\ndef main():\n" + Call(arg, "f");

    private static string Method(Pair pair, Arg arg)
        => "class C:\n"
           + $"    def m(self, x: {pair.Slot1}) -> str:\n        return \"{pair.Tag1}\"\n"
           + $"    def m(self, x: {pair.Slot2}) -> str:\n        return \"{pair.Tag2}\"\n"
           + "\ndef main():\n    c = C()\n" + Call(arg, "c.m");

    private static string Init(Pair pair, Arg arg)
        => "class C:\n"
           + $"    def __init__(self, x: {pair.Slot1}) -> None:\n        print(\"{pair.Tag1}\")\n"
           + $"    def __init__(self, x: {pair.Slot2}) -> None:\n        print(\"{pair.Tag2}\")\n"
           + "\ndef main():\n" + arg.Setup + new string(' ', arg.CallIndent) + $"C({arg.Expr})\n";

    private static string ImportedLib(Pair pair) => Overloads(pair, "", "f", pair.Tag1, pair.Tag2);

    private static string ImportedMain(Arg arg) => "from lib import f\n\ndef main():\n" + Call(arg, "f");

    private static readonly string[] Hosts = { "ModuleDef", "Method", "Init", "ImportedDef" };

    public static IEnumerable<object[]> Cells
        => from p in Pairs
           from a in Args
           from h in Hosts
           select new object[] { p.Name, a.Name, h };

    [Fact]
    public void Axes_AreAnchored_AndTheRefusedHalfIsWrittenDown()
    {
        Pairs.Length.Should().Be(PairCount);
        Args.Length.Should().Be(ArgCount);
        Hosts.Length.Should().Be(HostCount);
        Cells.Count().Should().Be(CellCount);
        (from p in Pairs from a in Args from h in Hosts where Expected(p, a).RefusalCode != null select 1)
            .Count().Should().Be(RefusedCellCount,
                "the refused half is a literal: a refusal that starts compiling, or an accepted cell "
                + "that starts refusing, moves this count");
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void Cell_SelectsOneWinner_OrIsRefusedByName(string pairName, string argName, string host)
    {
        var pair = Pairs.Single(p => p.Name == pairName);
        var arg = Args.Single(a => a.Name == argName);
        var expected = Expected(pair, arg);
        var label = $"[{pairName} × {argName} × {host}]";

        ExecutionResult result;
        string source;
        if (host == "ImportedDef")
        {
            var dir = Path.Combine(_tempDir, pairName + "_" + argName);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "lib.spy"), ImportedLib(pair));
            source = ImportedMain(arg);
            File.WriteAllText(Path.Combine(dir, "main.spy"), source);
            result = CompileAndExecuteProject(dir, "main.spy");
        }
        else
        {
            source = host switch
            {
                "ModuleDef" => ModuleDef(pair, arg),
                "Method" => Method(pair, arg),
                "Init" => Init(pair, arg),
                _ => throw new InvalidOperationException(host),
            };
            result = CompileAndExecute(source);
        }

        var diagnostics = string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}@{d.Line}: {d.Message}"));
        result.RawDiagnostics.Should().NotContain(d => d.Code == Ice,
            $"{label} must never reach Roslyn — the winner is decided here: {diagnostics}\n{source}");
        result.RawDiagnostics.Should().NotContain(d => d.Code == AmbiguousOverload,
            $"{label}: the resolver has no tie in this matrix; a tie is a red anchor: {diagnostics}\n{source}");

        if (expected.Tag is { } tag)
        {
            result.Success.Should().BeTrue($"{label} must compile and run: {diagnostics}\n{source}");
            result.StandardOutput.Should().Be(tag + "\n", $"{label} must dispatch to the winner\n{source}");
            return;
        }

        result.Success.Should().BeFalse($"{label} must be refused by name; it printed '{result.StandardOutput}'\n{source}");
        result.RawDiagnostics.Should().Contain(d => d.Code == expected.RefusalCode,
            $"{label} must report {expected.RefusalCode}: {diagnostics}\n{source}");
    }

    // ── the CLR-collision positive control ───────────────────────────────────────────────

    /// <summary>
    /// The pairs above declare because their CLR-mapped signatures differ. A pair whose Sharpy
    /// spellings differ but whose C# parameter types coincide is refused at the declaration
    /// (SPY0701): <c>float</c>/<c>double</c> are one C# <c>double</c>; <c>str</c>/<c>str | None</c>
    /// are one C# <c>string</c>. Without this control an SPY0701 that stopped firing would look
    /// like a matrix that merely got more permissive.
    /// </summary>
    [Theory]
    [InlineData("float", "double", "1.0")]
    [InlineData("str", "str | None", "\"a\"")]
    public void ClrCollidingPair_IsRefusedAtTheDeclaration(string slot1, string slot2, string arg)
    {
        var source = $"def f(x: {slot1}) -> str:\n    return \"a\"\ndef f(x: {slot2}) -> str:\n    return \"b\"\n\ndef main():\n    print(f({arg}))\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse(source);
        result.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.ValidationOverflow.DuplicateDunderSignature,
            "the CLR-mapped key is the declaration's identity; diagnostics: "
            + string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message)));
    }

    // ── warm/cold ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The <c>{int, int?}</c> module-def pair under <c>--incremental</c>: the cold build prints
    /// the tags; the warm build, served from the symbol cache whose format carries the total
    /// SignatureKey, emits byte-identical C# for the unchanged files — so it prints identically
    /// by construction. A cache that still keyed both overloads on <c>int</c> would restore one
    /// of them and the warm main would bind (or refuse) differently.
    /// </summary>
    [Fact]
    public void IntOptionalPair_ColdAndWarm_EmitTheSameDispatch()
    {
        var pair = Pairs.Single(p => p.Name == "IntOptional");
        var dir = Path.Combine(_tempDir, "warm");
        Directory.CreateDirectory(dir);
        var lib = Path.Combine(dir, "lib.spy");
        var main = Path.Combine(dir, "main.spy");
        File.WriteAllText(lib, ImportedLib(pair));
        File.WriteAllText(main, "from lib import f\n\ndef main():\n    print(f(1))\n    print(f(Some(1)))\n    print(f(None()))\n");

        var run = CompileAndExecuteProject(dir, "main.spy");
        run.Success.Should().BeTrue(string.Join(" | ", run.RawDiagnostics.Select(d => d.Code + " " + d.Message)));
        run.StandardOutput.Should().Be("int\nopt\nopt\n");

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(dir, "warm.spyproj"),
            ProjectDirectory = dir,
            RootNamespace = "Warm",
            SourceFiles = new List<string> { lib, main },
            Configuration = "Debug",
        };
        var cold = Build(config);
        var warm = Build(config);

        cold.Success.Should().BeTrue(Diagnostics(cold));
        warm.Success.Should().BeTrue(Diagnostics(warm));
        warm.Metrics!.SkippedFiles.Should().NotBeEmpty("the second build must be served from the cache, or the wire is not under test");
        foreach (var (path, coldCs) in cold.GeneratedCSharpFiles)
        {
            warm.GeneratedCSharpFiles.Should().ContainKey(path);
            warm.GeneratedCSharpFiles[path].Should().Be(coldCs, $"{Path.GetFileName(path)} must emit identically warm and cold");
        }
    }

    private static ProjectCompilationResult Build(ProjectConfig config)
        => new Compiler(new CompilerOptions { Incremental = true }, NullLogger.Instance).CompileProject(config);

    private static string Diagnostics(ProjectCompilationResult result)
        => string.Join(" | ", result.Diagnostics.GetAll().Select(d => $"{d.Code}: {d.Message}"));
}
