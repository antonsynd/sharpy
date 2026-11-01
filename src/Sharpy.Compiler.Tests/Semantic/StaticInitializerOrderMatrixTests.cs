using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The generator of the <b>static-initializer order contract</b> (P75; #2396, owner rulings R-JT and R-JU): <b>a
/// type's static initializers run in SOURCE order</b>. A <c>@static</c> field, a <c>const</c> whose initializer is not
/// a compile-time constant (emitted <c>static readonly</c>) and a <c>@static</c> property default that read a static
/// sibling declared EARLIER see its initialized value, in every spelling (bare, qualified <c>Host.x</c>, alias
/// <c>H.x</c>) and every host (class, <c>@dataclass</c>, struct); a FORWARD read — or a read of the reader itself — of
/// a sibling whose storage is ordered (a <c>@static</c> field, property or method, a non-compile-time <c>const</c>) is
/// refused with ONE diagnostic at the read; a compile-time <c>const</c> is order-free in every spelling (#1795).
///
/// <para><b>Matrix.</b> host {class, dataclass, struct} × reader {static-field, const, static-property-default} × read
/// {static-property, static-field, nonliteral-const, literal-const, static-method} × spelling {bare, qualified, alias} ×
/// order {earlier, forward}, plus the reader reading ITSELF (read <c>itself</c>, order <c>self</c>) per host × reader ×
/// spelling — a self cell has no sibling, so it is not multiplied by the read axis; ids <c>static-init-order ×
/// &lt;host&gt; × &lt;reader&gt; × &lt;read&gt; × &lt;spelling&gt; × &lt;order&gt;</c>. Beside the generated grid,
/// twelve cells change one axis each: a later non-literal const SHADOWED by a module binding (pinned at SPY0618,
/// decision #2415), the NESTED receiver (<c>Outer.Inner.s</c>, earlier and forward), a later
/// method read as a method GROUP (admitted: nothing runs), the INTERFACE host (a non-literal const read through its name, earlier and
/// forward), the GENERIC host (<c>class Host[T]</c> read through the constructed reference
/// <c>Host[int].x</c>, earlier and forward, a <c>@static</c> property and a <c>@static</c> field), and the two SPLIT
/// property anchors (a split reader whose default sits on its later half, a split sibling whose default does) — a
/// split property is ONE C# property emitted at its FIRST half, so both are anchored there. Every value is distinct
/// — <c>@static</c> property 4, <c>@static</c> field 2, non-literal const 7 (<c>seven()</c>), literal const 1,
/// <c>@static</c> method 6 — and an uninitialized read is <c>0</c>, so a wrong order, a wrong binding or an un-run
/// initializer each print a different number.</para>
///
/// <para><b>The contract in code.</b> <see cref="Cell"/> is this test's OWN literal copy of R-JT, never a call into
/// the compiler: an earlier read runs printing the sibling's value; a forward literal const runs <c>1</c>; a forward
/// BARE read of a <c>@static</c> field, property or method, and a bare self read, are P21h's F1 refusal (one SPY0200
/// carrying <see cref="ClassBodyReadPositionMatrixTests.DeclaredLaterPhrase"/>, unchanged by P75); the const reader
/// reading itself is the constant cycle (SPY0278, either spelling); every other forward or self read — a qualified
/// or alias spelling of an ordered sibling, and the bare non-literal const F1 exempts — is exactly ONE SPY0618 at the
/// read. A refusal names its position's phrase and says "declared later".</para>
///
/// <para><b>The oracle.</b> <see cref="CSharpRunsStaticInitializersInTextualOrder"/> compiles AND RUNS, with Roslyn,
/// the C# shapes the layout decides between, so the fact this contract rests on — C# runs static initializers in the
/// generated text's order, and an earlier method reads the statics initialized so far — is measured, not
/// asserted.</para>
///
/// <para><b>Ratchet.</b> The cells ratchet over <c>Semantic/declaration-position-allowlist.txt</c>
/// (<see cref="TypeDefinitionOwnSymbolMatrixTests.LoadAllowlist"/>, whose id universe includes
/// <see cref="CellIds"/>). The controls — each reader with a literal runs, the module host reads an earlier module
/// property in source order, a const reader of a non-literal read is emitted <c>static readonly</c> — are never
/// exempted.</para>
/// </summary>
public class StaticInitializerOrderMatrixTests : IntegrationTestBase
{
    public StaticInitializerOrderMatrixTests(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>The SPY0618 code (<c>DiagnosticCodes.SemanticOverflow.StaticInitializerForwardRead</c>), spelled as a
    /// literal so the contract does not read the compiler's table.</summary>
    internal const string ForwardReadCode = "SPY0618";

    /// <summary>The const reader reading itself — the constant cycle, pinned by code alone.</summary>
    internal const string ConstCycleCode = "SPY0278";

    internal const string StaticInitializerPhrase = "'@static' initializer";
    internal const string ConstInitializerPhrase = "'const' initializer";

    /// <summary>A host: its id, its header lines, the type-name qualifier, and whether it admits the alias spelling.</summary>
    internal sealed record Host(string Name, string[] Header, string Qualifier);

    /// <summary>A reader: its id, its lines (body-relative; <c>§</c> marks the read), its own name, whether it is the
    /// const reader, and the position phrase its refusal names.</summary>
    internal sealed record Reader(string Name, string[] Lines, string ReaderName, bool IsConst, string Phrase);

    /// <summary>A read sibling: its id, its declaration lines, its member name, the call suffix (<c>()</c> for the
    /// method), the value it prints, and whether its storage is ordered (everything but the literal const).</summary>
    internal sealed record Read(string Name, string[] Lines, string Member, string Call, int Value, bool Ordered);

    /// <summary>One cell: its id, source, the exact expected errors (code, line, column; line 0 pins the code alone),
    /// the phrase a refusal carries, and the stdout of an admitted cell.</summary>
    internal sealed record OrderCell(
        string Id, string Source, IReadOnlyList<(string Code, int Line, int Column)> ExpectedErrors, string? Phrase,
        string? ExpectedStdout);

    internal static readonly Host[] Hosts =
    {
        new("class", new[] { "class Host:" }, "Host"),
        new("dataclass", new[] { "@dataclass", "class Host:" }, "Host"),
        new("struct", new[] { "struct Host:" }, "Host"),
    };

    internal static readonly Reader[] Readers =
    {
        new("static-field", new[] { "@static", "b: int = §" }, "b", IsConst: false, StaticInitializerPhrase),
        new("const", new[] { "const B: int = § + 0" }, "B", IsConst: true, ConstInitializerPhrase),
        new("static-property-default", new[] { "@static", "property b: int = §" }, "b", IsConst: false, StaticInitializerPhrase),
    };

    internal static readonly Read[] Reads =
    {
        new("static-property", new[] { "@static", "property sp: int = 4" }, "sp", "", 4, Ordered: true),
        new("static-field", new[] { "@static", "s: int = 2" }, "s", "", 2, Ordered: true),
        new("nonliteral-const", new[] { "const K: int = seven()" }, "K", "", 7, Ordered: true),
        new("literal-const", new[] { "const L: int = 1" }, "L", "", 1, Ordered: false),
        new("static-method", new[] { "@static", "def sm() -> int:", "    return 6" }, "sm", "()", 6, Ordered: true),
    };

    /// <summary>SPELLING: how the read names the sibling — bare, through the type name, through a module-level alias
    /// of the type (<c>type H = Host</c>).</summary>
    internal static readonly string[] Spellings = { "bare", "qualified", "alias" };

    /// <summary>ORDER: the sibling declared before the reader, or after it.</summary>
    internal static readonly string[] Orders = { "earlier", "forward" };

    private static string Indent(string line) => line.Length == 0 ? line : "    " + line;

    private static string Prefix(string spelling, string qualifier) => spelling switch
    {
        "bare" => "",
        "qualified" => qualifier + ".",
        "alias" => "H.",
        _ => throw new ArgumentOutOfRangeException(nameof(spelling), spelling, null),
    };

    /// <summary>The program of one cell: the module function <c>seven()</c> the non-literal const calls, the alias
    /// (alias spelling only), the host with the sibling before or after the reader (no sibling for a self read), and
    /// <c>main</c> printing the reader. Returns the source and the read's line and column.</summary>
    internal static (string Source, int ReadLine, int ReadColumn) Program(
        string[] header, string[]? before, string[] readerLines, string read, string[]? after, bool alias, string use)
    {
        var lines = new List<string> { "def seven() -> int:", "    return 7", "", "" };
        if (alias)
            lines.AddRange(new[] { "type H = Host", "", "" });
        lines.AddRange(header);
        if (before != null)
            lines.AddRange(before.Select(Indent));

        int readLine = 0, readColumn = 0;
        foreach (var template in readerLines)
        {
            var line = Indent(template);
            var mark = line.IndexOf('§', StringComparison.Ordinal);
            if (mark >= 0)
            {
                readLine = lines.Count + 1;
                readColumn = mark + 1;
                line = line.Replace("§", read, StringComparison.Ordinal);
            }

            lines.Add(line);
        }

        if (after != null)
            lines.AddRange(after.Select(Indent));
        lines.AddRange(new[] { "", "", "def main() -> None:", $"    print({use})", "" });
        return (string.Join("\n", lines), readLine, readColumn);
    }

    internal static IEnumerable<OrderCell> Cells()
    {
        foreach (var host in Hosts)
        {
            foreach (var reader in Readers)
            {
                foreach (var spelling in Spellings)
                {
                    foreach (var read in Reads)
                    {
                        foreach (var order in Orders)
                            yield return Cell(host, reader, read, spelling, order);
                    }

                    yield return SelfCell(host, reader, spelling);
                }
            }
        }

        foreach (var cell in AxisChangeCells())
            yield return cell;
    }

    private static string Id(string host, string reader, string read, string spelling, string order)
        => $"static-init-order × {host} × {reader} × {read} × {spelling} × {order}";

    /// <summary>R-JT for one sibling read: earlier runs; a forward literal const runs; a forward bare read of a
    /// <c>@static</c> member is F1's SPY0200; every other forward read is SPY0618.</summary>
    private static OrderCell Cell(Host host, Reader reader, Read read, string spelling, string order)
    {
        var expression = Prefix(spelling, host.Qualifier) + read.Member + read.Call;
        var (source, line, column) = Program(host.Header, order == "earlier" ? read.Lines : null, reader.Lines, expression,
            order == "forward" ? read.Lines : null, spelling == "alias", $"Host.{reader.ReaderName}");
        var id = Id(host.Name, reader.Name, read.Name, spelling, order);
        if (order == "earlier" || !read.Ordered)
            return new OrderCell(id, source, Array.Empty<(string, int, int)>(), null, $"{read.Value}\n");

        // F1 (P21h) refuses the bare forward read of a @static member; a const is exempt from F1, so the bare
        // non-literal const is P75's.
        var code = spelling == "bare" && read.Name != "nonliteral-const" ? "SPY0200" : ForwardReadCode;
        return new OrderCell(id, source, new[] { (code, line, column) }, reader.Phrase, null);
    }

    /// <summary>The reader reading itself: the const reader is a constant cycle; a bare self read is F1's SPY0200; a
    /// qualified or alias self read is SPY0618 (it would read its own default, 0).</summary>
    private static OrderCell SelfCell(Host host, Reader reader, string spelling)
    {
        var expression = Prefix(spelling, host.Qualifier) + reader.ReaderName;
        var (source, line, column) = Program(host.Header, null, reader.Lines, expression, null, spelling == "alias",
            $"Host.{reader.ReaderName}");
        var id = Id(host.Name, reader.Name, "itself", spelling, "self");
        if (reader.IsConst)
            return new OrderCell(id, source, new[] { (ConstCycleCode, 0, 0) }, null, null);
        var code = spelling == "bare" ? "SPY0200" : ForwardReadCode;
        return new OrderCell(id, source, new[] { (code, line, column) }, reader.Phrase, null);
    }

    /// <summary>The cells one axis away from the grid: the generic host read through its constructed reference, and the
    /// split-property anchors.</summary>
    private static IEnumerable<OrderCell> AxisChangeCells()
    {
        // GENERIC host: `Host[int].x` inside `class Host[T]` reads `Host<int>`'s static — for the instantiation being
        // initialized the same storage, so a forward read sees 0 (measured @ 6d1c9021f: printing Host[int] first
        // reads 0, printing Host[str] first reads 4 — initialization-order dependent), and an earlier one must see the
        // value. The constructed reference is the enclosing type (D2).
        var generic = new[] { "class Host[T]:" };
        var staticField = Readers[0];
        foreach (var read in Reads.Where(r => r.Name is "static-property" or "static-field"))
        {
            foreach (var order in Orders)
            {
                var (source, line, column) = Program(generic, order == "earlier" ? read.Lines : null, staticField.Lines,
                    $"Host[int].{read.Member}", order == "forward" ? read.Lines : null, alias: false, "Host[int].b");
                var id = Id("generic-class", staticField.Name, read.Name, "constructed", order);
                yield return order == "earlier"
                    ? new OrderCell(id, source, Array.Empty<(string, int, int)>(), null, $"{read.Value}\n")
                    : new OrderCell(id, source, new[] { (ForwardReadCode, line, column) }, staticField.Phrase, null);
            }
        }

        // INTERFACE host (the prober's optional axis change): an interface body admits consts, emitted in source order
        // (GenerateInterfaceMembers); a non-literal const read through the interface's name is ordered like any static
        // initializer — at 6d1c9021f the forward read ran `0` (nobody refused it). Its @static field and property ICE
        // (CS0120) — #2383's class, not this one.
        var nonliteral = Reads.Single(r => r.Name == "nonliteral-const");
        var constReader = Readers.Single(r => r.Name == "const");
        foreach (var order in Orders)
        {
            var (source, line, column) = Program(new[] { "interface Host:" }, order == "earlier" ? nonliteral.Lines : null,
                constReader.Lines, "Host.K", order == "forward" ? nonliteral.Lines : null, alias: false, "Host.B");
            var id = Id("interface", constReader.Name, nonliteral.Name, "qualified", order);
            yield return order == "earlier"
                ? new OrderCell(id, source, Array.Empty<(string, int, int)>(), null, "7\n")
                : new OrderCell(id, source, new[] { (ForwardReadCode, line, column) }, constReader.Phrase, null);
        }

        // NESTED receiver (P75 verify F1): inside `class Outer:` / `class Inner:`, the reader names its own type through
        // the declaring type's chain, `Outer.Inner.s` — a qualifier the member-typing arms leave untyped (#1941). At
        // 332117e2d the forward read ran `0` (nobody refused it); spelled `Inner.s` it was SPY0618.
        var field = Reads.Single(r => r.Name == "static-field");
        foreach (var order in Orders)
        {
            string[] Nest(string[] lines) => lines.Select(l => "    " + l).ToArray();
            var (source, line, column) = Program(new[] { "class Outer:", "    class Inner:" },
                order == "earlier" ? Nest(field.Lines) : null, Nest(staticField.Lines), "Outer.Inner.s",
                order == "forward" ? Nest(field.Lines) : null, alias: false, "Outer.Inner.b");
            var id = Id("nested-class", staticField.Name, field.Name, "nested-qualified", order);
            yield return order == "earlier"
                ? new OrderCell(id, source, Array.Empty<(string, int, int)>(), null, "2\n")
                : new OrderCell(id, source, new[] { (ForwardReadCode, line, column) }, staticField.Phrase, null);
        }

        // METHOD GROUP (P75 verify F3): a later @static method read as a VALUE (`g: () -> int = Host.sm`) creates a
        // delegate and runs nothing, so C# reads no static there — admitted; the call arm refuses only a call.
        {
            var method = Reads.Single(r => r.Name == "static-method");
            var (source, _, _) = Program(new[] { "class Host:" }, null, new[] { "@static", "g: () -> int = §" }, "Host.sm",
                method.Lines, alias: false, "Host.g()");
            yield return new OrderCell(Id("class", "static-field", "static-method-group", "qualified", "forward"),
                source, Array.Empty<(string, int, int)>(), null, "6\n");
        }

        // SHADOWED later non-literal const (P75 verify F2; decision #2415): a module binding `K` above the class, and
        // `@static b: int = K + 0` above `const K: int = seven()`. A later @static FIELD yields to the module binding
        // (P21h F2 — python3 prints 9); the const is pinned at the implemented verdict, SPY0618 at the read, until #2415
        // rules (option (a), two-way).
        {
            var (source, line, column) = Program(new[] { "class Host:" }, null, new[] { "@static", "b: int = § + 0" }, "K",
                new[] { "const K: int = seven()" }, alias: false, "Host.b");
            source = "K: int = 9\n\n\n" + source;
            yield return new OrderCell(Id("class", "static-field", "shadowed-nonliteral-const", "bare", "forward"),
                source, new[] { (ForwardReadCode, line + 3, column) }, StaticInitializerPhrase, null);
        }

        // SPLIT READER: `@static property get b`, then the sibling, then `@static property set b: int = Host.sp` — the
        // reader is emitted at its FIRST half, above the sibling, so the read is forward (P21h verify C2): SPY0618.
        {
            var (source, line, column) = Program(new[] { "class Host:" }, new[] { "@static", "property get b: int", "@static", "property sp: int = 4" },
                new[] { "@static", "property set b: int = §" }, "Host.sp", null, alias: false, "Host.b");
            yield return new OrderCell(Id("class", "split-static-property-default", "static-property", "qualified", "forward"),
                source, new[] { (ForwardReadCode, line, column) }, StaticInitializerPhrase, null);
        }

        // SPLIT SIBLING: `@static property get sp`, then the reader, then `@static property set sp: int = 4` — the
        // sibling is emitted at its FIRST half, above the reader, so the read is earlier and sees 4.
        {
            var (source, _, _) = Program(new[] { "class Host:" }, new[] { "@static", "property get sp: int" },
                staticField.Lines, "Host.sp", new[] { "@static", "property set sp: int = 4" }, alias: false, "Host.b");
            yield return new OrderCell(Id("class", "static-field", "split-static-property", "qualified", "earlier"),
                source, Array.Empty<(string, int, int)>(), null, "4\n");
        }
    }

    /// <summary>Every ratcheted id of this class, as the shared allowlist spells it.</summary>
    internal static IEnumerable<string> CellIds() => Cells().Select(c => c.Id);

    [Fact]
    public void Cells_CoverEveryAxis_AnchoredToLiterals()
    {
        Assert.Equal(3, Hosts.Length);
        Assert.Equal(3, Readers.Length);
        Assert.Equal(5, Reads.Length);
        Assert.Equal(3, Spellings.Length);
        Assert.Equal(2, Orders.Length);
        var cells = Cells().ToList();
        // Per host × reader × spelling: 5 reads × {earlier, forward} + the self read; + 12 axis-change cells.
        Assert.Equal(3 * 3 * 3 * (5 * 2 + 1) + 12, cells.Count);
        Assert.Equal(cells.Count, cells.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count());
        bool Has(OrderCell c, string code) => c.ExpectedErrors.Any(e => e.Code == code);
        // Runs: every earlier read (3 × 3 × 3 spellings × 5 reads), every forward literal const (27), the two
        // generic earlier cells, the interface earlier cell, the nested earlier cell, the method group and the split
        // sibling.
        Assert.Equal(3 * 3 * 3 * 5 + 27 + 2 + 1 + 1 + 1 + 1, cells.Count(c => c.ExpectedStdout != null));
        Assert.Equal(27, cells.Count(c => c.Id.Contains("× literal-const ×", StringComparison.Ordinal)
            && c.Id.EndsWith("× forward", StringComparison.Ordinal) && c.ExpectedStdout == "1\n"));
        // F1 (unchanged): the bare forward @static field, property and method per host × reader (27), and the bare self
        // read of the two non-const readers per host (6).
        Assert.Equal(27 + 6, cells.Count(c => Has(c, "SPY0200")));
        // SPY0618: the bare forward non-literal const per host × reader (9); every qualified and alias forward read of
        // the 4 ordered siblings (3 × 3 × 2 × 4 = 72); the qualified and alias self read of the two non-const readers
        // per host (12); the two generic forward cells, the interface, nested and shadowed forward cells and the split
        // reader.
        Assert.Equal(9 + 72 + 12 + 2 + 1 + 1 + 1 + 1, cells.Count(c => Has(c, ForwardReadCode)));
        // The const reader reading itself, per host × spelling.
        Assert.Equal(9, cells.Count(c => Has(c, ConstCycleCode)));
        foreach (var cell in cells)
        {
            Assert.True(cell.ExpectedErrors.Count <= 1, $"{cell.Id}: the expected set names {cell.ExpectedErrors.Count} codes");
            Assert.True((cell.ExpectedErrors.Count == 0) == (cell.ExpectedStdout != null), $"{cell.Id}: an admitted cell must run, a refused one must not");
        }

        // The values are distinct and none is the uninitialized 0.
        Assert.Equal(Reads.Length, Reads.Select(r => r.Value).Distinct().Count());
        Assert.DoesNotContain(0, Reads.Select(r => r.Value));
    }

    [Fact]
    public void Read_HasOneVerdictPerCell()
    {
        var allowlist = TypeDefinitionOwnSymbolMatrixTests.LoadAllowlist(out var problems);
        var api = new CompilerApi(null, new[] { SharpyCoreReference.Location });
        var red = new List<string>();

        // Controls (hard, never allowlisted). Each reader with a literal runs in each host, so a cell's red is its
        // READ's.
        foreach (var host in Hosts)
        {
            foreach (var reader in Readers)
            {
                var (source, _, _) = Program(host.Header, null, reader.Lines, "0", null, alias: false, $"Host.{reader.ReaderName}");
                var run = CompileAndExecute(source);
                if (!run.Success || run.StandardOutput != "0\n")
                    problems.Add($"control {host.Name} × {reader.Name} (literal) does not run 0: success={run.Success} "
                        + $"stdout=[{run.StandardOutput.ReplaceLineEndings("\\n")}] {string.Join(" | ", run.CompilationErrors).ReplaceLineEndings(" ")}");
            }
        }

        // The module host already emits in source order: a module variable reading an earlier module property sees it.
        var module = CompileAndExecute("property sp: int = 4\ng: int = sp\n\n\ndef main() -> None:\n    print(g)\n");
        if (!module.Success || module.StandardOutput != "4\n")
            problems.Add($"control module host does not run 4: success={module.Success} stdout=[{module.StandardOutput.ReplaceLineEndings("\\n")}]");

        // The const reader of a non-literal read is `static readonly` storage — ordered like a @static field, which is
        // why the const reader is a row of this matrix at all.
        var (constSource, _, _) = Program(Hosts[0].Header, null, Readers[1].Lines, "seven()", null, alias: false, "Host.B");
        var constRun = CompileAndExecute(constSource);
        if (!constRun.Success || constRun.StandardOutput != "7\n"
            || constRun.GeneratedCSharp?.Contains("static readonly int B", StringComparison.Ordinal) != true)
            problems.Add($"control: 'const B: int = seven() + 0' is not emitted 'static readonly' or does not run 7: success={constRun.Success} "
                + $"stdout=[{constRun.StandardOutput.ReplaceLineEndings("\\n")}]");

        var ran = 0;
        var cells = Cells().ToList();
        foreach (var cell in cells)
        {
            var result = api.Analyze(cell.Source, CancellationToken.None);
            var errors = result.Diagnostics.Where(d => d.IsError).ToList();
            Output.WriteLine($"{cell.Id}: {Fingerprint(errors)}");
            var failures = new List<string>();

            var internalUnderAnalyze = result.Diagnostics.Where(d => (d.Code ?? "").StartsWith("SPY09", StringComparison.Ordinal)).ToList();
            if (internalUnderAnalyze.Count > 0)
                problems.Add($"{cell.Id}: an internal error under Analyze (hard, never allowlisted): "
                    + string.Join(" | ", internalUnderAnalyze.Select(d => d.Message)).ReplaceLineEndings(" "));

            var codeOnly = cell.ExpectedErrors.Any(e => e.Line == 0);
            var expected = cell.ExpectedErrors.Select(e => codeOnly ? e.Code : $"{e.Code}@{e.Line}:{e.Column}").OrderBy(s => s, StringComparer.Ordinal).ToList();
            var actual = errors.Select(d => codeOnly ? d.Code ?? "-" : $"{d.Code ?? "-"}@{d.Line}:{d.Column}").OrderBy(s => s, StringComparer.Ordinal).ToList();
            if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
            {
                // A refusal the analysis does not make: say what the program does under run (it runs, and prints what).
                var underRun = "";
                if (actual.Count == 0 && cell.ExpectedStdout is null)
                {
                    var silent = CompileAndExecute(cell.Source);
                    underRun = silent.Success
                        ? $" — under run it RUNS, stdout=[{silent.StandardOutput.ReplaceLineEndings("\\n")}]"
                        : $" — under run: refused [{string.Join(" | ", silent.CompilationErrors).ReplaceLineEndings(" ")}]";
                }

                failures.Add($"errors are [{string.Join(" ", actual)}], expected [{string.Join(" ", expected)}]{underRun}");
            }

            // A refusal names its position and says the sibling is declared later.
            if (cell.Phrase is { } phrase && errors.Count == 1 && expected.SequenceEqual(actual, StringComparer.Ordinal))
            {
                var message = errors[0].Message;
                if (!message.Contains(phrase, StringComparison.Ordinal))
                    failures.Add($"the refusal does not name the position ('{phrase}'): \"{message}\"");
                if (!message.Contains(ClassBodyReadPositionMatrixTests.DeclaredLaterPhrase, StringComparison.Ordinal))
                    failures.Add($"the refusal does not say '{ClassBodyReadPositionMatrixTests.DeclaredLaterPhrase}': \"{message}\"");
            }

            // An admitted cell must RUN and print exactly the sibling's value — an uninitialized read prints 0.
            if (cell.ExpectedStdout is { } stdout && actual.Count == 0)
            {
                ran++;
                var run = CompileAndExecute(cell.Source);
                if (!run.Success || run.StandardOutput != stdout)
                    failures.Add($"run: success={run.Success} stdout=[{run.StandardOutput.ReplaceLineEndings("\\n")}], expected [{stdout.ReplaceLineEndings("\\n")}] "
                        + string.Join(" | ", run.CompilationErrors).ReplaceLineEndings(" "));
            }

            if (failures.Count > 0)
                Output.WriteLine($"    red: {string.Join("; ", failures)}");
            TypeDefinitionOwnSymbolMatrixTests.Ratchet(cell.Id, failures, allowlist, problems, red);
        }

        Output.WriteLine($"{cells.Count} static-init-order cells, {ran} executed, {red.Count} allowlisted red");
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The C# oracle: C# runs static initializers in the generated text's order.
    // ---------------------------------------------------------------------------------------------------------------

    private static readonly Lazy<MetadataReference[]> CSharpReferences = new(() =>
    {
        var coreLib = typeof(object).Assembly.Location;
        return new MetadataReference[]
        {
            MetadataReference.CreateFromFile(coreLib),
            MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(coreLib)!, "System.Runtime.dll")),
        };
    });

    /// <summary>Compiles <paramref name="members"/> as the body of <c>public static class Host</c>, loads the assembly
    /// and returns the value of its static field <paramref name="field"/> — which reading triggers the type
    /// initializer.</summary>
    private static int RunCSharpStatic(string members, string field)
    {
        var source = $"public static class Host {{ {members} }}";
        var compilation = CSharpCompilation.Create(
            $"StaticInitOrderOracle{Guid.NewGuid():N}", new[] { CSharpSyntaxTree.ParseText(source) }, CSharpReferences.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        var assembly = Assembly.Load(stream.ToArray());
        var value = assembly.GetType("Host")!.GetField(field, BindingFlags.Public | BindingFlags.Static)!.GetValue(null);
        return (int)value!;
    }

    [Fact]
    public void CSharpRunsStaticInitializersInTextualOrder()
    {
        // The layout the emitter wrote before P75 (fields, then properties): the field reads the property's backing
        // field before its initializer ran.
        Assert.Equal(0, RunCSharpStatic("public static int G = Sp; public static int Sp { get; set; } = 4;", "G"));
        // Source order (the property above the field): the field sees the property's value — the positive control.
        Assert.Equal(4, RunCSharpStatic("public static int Sp { get; set; } = 4; public static int G = Sp;", "G"));
        // An EARLIER method whose body reads a LATER static: C# admits it and the method reads the statics initialized
        // so far — 0 (c8; P75 admits it as C#'s rule and the spec names it).
        Assert.Equal(0, RunCSharpStatic("public static int A = Seven(); public static int Seven() => Sp; public static int Sp { get; set; } = 4;", "A"));
        // The same method call with the property above the reader reads 4.
        Assert.Equal(4, RunCSharpStatic("public static int Sp { get; set; } = 4; public static int A = Seven(); public static int Seven() => Sp;", "A"));
    }

    private static string Fingerprint(IEnumerable<CompilerDiagnostic> diagnostics)
        => string.Join(" ", diagnostics.Select(d => $"{d.Code ?? "-"}@{d.Line}:{d.Column}").OrderBy(s => s, StringComparer.Ordinal));
}
