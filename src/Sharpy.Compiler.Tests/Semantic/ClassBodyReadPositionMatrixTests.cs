using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The generator of the <b>class-body read contract</b> (P21h D8/D9 (v); #2384, ruling R-JJ): <b>a bare class-body
/// name read from a position that is not a method body resolves exactly the siblings C# resolves in that position,
/// and refuses the rest with ONE diagnostic whose steer names the position</b>. Before it, a bare sibling read from a
/// field initializer, a property default, a <c>const</c> initializer or a parameter default had no decided verdict: a
/// field sibling bound and crashed the emitter (SPY0909 "No CodeGenInfo for local"), a <c>@static</c> property or
/// method that C# admits was refused with the method-body steer, an event and a method got no description, and a
/// parameter default drew two codes (measured @ 9aae96977).
///
/// <para><b>Matrix.</b> host {<c>class</c>, <c>@dataclass class</c>, <c>struct</c>} × read position
/// {instance-field-initializer, static-field-initializer, instance-property-default, static-property-default,
/// const-initializer, parameter-default, method-body} × sibling {const, static-field, instance-field,
/// static-property, instance-property, event, static-method, instance-method}; ids
/// <c>class-body-read × &lt;host&gt; × &lt;position&gt; × &lt;sibling&gt;</c>. Each cell declares ONE sibling and ONE
/// reader (<c>b</c>, or <c>B</c> for the const reader) whose initializer, default or return value is the bare read,
/// annotated with the sibling's type, and prints through the reader. The siblings carry distinct values (const 1,
/// <c>@static</c> field 2, instance field 3, <c>@static</c> property 4, instance property 5, <c>@static</c> method 6,
/// instance method 7), so an admitted read that resolved the wrong member prints the wrong number.</para>
///
/// <para><b>The contract in code.</b> <see cref="Classify"/> and <see cref="Admits"/> are this test's OWN literal
/// copy of D8 — never a call into the checker. The classification is host-route-aware for ONE slot: an instance FIELD
/// initializer in a struct or a <c>@dataclass</c> is the synthesized constructor's parameter default
/// (<c>ParameterDefault</c>); an instance PROPERTY default is a C# property initializer in every host
/// (<c>InstanceInitializer</c>); a <c>const</c> initializer that is not a compile-time constant emits
/// <c>static readonly</c> (<c>ConstInitializer</c>, admitting what a static initializer admits). An admitted cell
/// analyses clean and RUNS printing the sibling's value; a refused cell is exactly ONE error — SPY0401 for a
/// <c>@static</c> sibling in a <c>ParameterDefault</c> (the name resolves, the constant-position rule refuses it), else
/// SPY0200 at the read whose message carries its position's phrase (<see cref="FieldInitializerPhrase"/> …) and no
/// other position's, plus the steer D8 names. Never two codes; an SPY09xx under <c>Analyze</c> is hard.</para>
///
/// <para><b>The table's oracle.</b> <see cref="AdmissionTable_AgreesWithCSharp"/> compiles, with Roslyn, the C# shape
/// the emitter writes for every non-method position (<see cref="CSharpShape"/>, a literal derived from the emission
/// route, independent of <see cref="Classify"/>) reading each sibling, and requires the table to admit exactly what
/// C# compiles. That is what makes a flipped table row red while the matrix cells are still red at the base. The
/// method body is outside the oracle: there C# admits every sibling and Sharpy refuses every one by design (#1786, R-Y
/// — Python's class-scope rule); its rows are pinned by the matrix alone.</para>
///
/// <para><b>Ratchet.</b> The cells ratchet over the declaration-position generator's allowlist
/// (<c>Semantic/declaration-position-allowlist.txt</c>, <see cref="TypeDefinitionOwnSymbolMatrixTests.LoadAllowlist"/>,
/// whose id universe includes <see cref="CellIds"/>): a listed cell that passes is STALE, an unlisted failing cell is
/// red. The controls (each reader with a literal and no sibling runs; each sibling alone analyses clean) and the
/// contract self-checks are never exempted.</para>
/// </summary>
public class ClassBodyReadPositionMatrixTests : IntegrationTestBase
{
    public ClassBodyReadPositionMatrixTests(ITestOutputHelper output) : base(output)
    {
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The position phrases — the contract P21h Phase 4 implements to. A refused SPY0200 names its position with exactly
    // one of these substrings and no other; they are pairwise non-overlapping (no phrase contains another).
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>A plain class's instance field initializer (<c>InstanceInitializer</c>, field form).</summary>
    internal const string FieldInitializerPhrase = "field initializer";

    /// <summary>An instance property's default in any host (<c>InstanceInitializer</c>, property form).</summary>
    internal const string PropertyDefaultPhrase = "property default";

    /// <summary>A <c>@static</c> field's initializer or a <c>@static</c> property's default (<c>StaticInitializer</c>).</summary>
    internal const string StaticInitializerPhrase = "'@static' initializer";

    /// <summary>A <c>const</c> member's initializer (<c>ConstInitializer</c> — a <c>static readonly</c> initializer).</summary>
    internal const string ConstInitializerPhrase = "'const' initializer";

    /// <summary>A method's parameter default, and a struct/dataclass instance field's initializer (<c>ParameterDefault</c>).</summary>
    internal const string ParameterDefaultPhrase = "parameter default";

    /// <summary>A method body (#1786's text, unchanged).</summary>
    internal const string MethodBodyPhrase = "inside methods";

    private static readonly string[] AllPhrases =
    {
        FieldInitializerPhrase, PropertyDefaultPhrase, StaticInitializerPhrase, ConstInitializerPhrase,
        ParameterDefaultPhrase, MethodBodyPhrase,
    };

    /// <summary>D8's classified read position.</summary>
    internal enum ReadPosition
    {
        MethodBody,
        InstanceInitializer,
        StaticInitializer,
        ConstInitializer,
        ParameterDefault,
    }

    /// <summary>A host: its id and its header lines.</summary>
    internal sealed record Host(string Name, string[] Header);

    /// <summary>A sibling: its id, its declaration lines (body-relative), the bare read, the member name the read
    /// denotes, the read's type, the value it prints when admitted (null for the event, never admitted), and whether
    /// it is type-level (<c>const</c> or <c>@static</c>).</summary>
    internal sealed record Sibling(string Name, string[] Lines, string Read, string Member, string Type, int? Value, bool TypeLevel);

    /// <summary>A read position: its id, its reader lines (body-relative; <c>§</c> marks the read, <c>T</c> the type)
    /// and the <c>main</c> statement that prints through it.</summary>
    internal sealed record Position(string Name, string[] Lines, string Use);

    /// <summary>One cell: its source, its classified position, the exact expected errors (code, line, column), the
    /// phrase and steer a refused SPY0200 must carry, and the stdout of an admitted cell.</summary>
    internal sealed record ReadCell(
        string Id, string Host, string Position, string Sibling, string Source, ReadPosition Classified,
        IReadOnlyList<(string Code, int Line, int Column)> ExpectedErrors, string? Phrase, string? Steer, string? ExpectedStdout);

    internal static readonly Host[] Hosts =
    {
        new("class", new[] { "class Host:" }),
        new("dataclass", new[] { "@dataclass", "class Host:" }),
        new("struct", new[] { "struct Host:" }),
    };

    internal static readonly Sibling[] Siblings =
    {
        new("const", new[] { "const K: int = 1" }, "K", "K", "int", 1, TypeLevel: true),
        new("static-field", new[] { "@static", "s: int = 2" }, "s", "s", "int", 2, TypeLevel: true),
        new("instance-field", new[] { "f: int = 3" }, "f", "f", "int", 3, TypeLevel: false),
        new("static-property", new[] { "@static", "property sp: int = 4" }, "sp", "sp", "int", 4, TypeLevel: true),
        new("instance-property", new[] { "property p: int = 5" }, "p", "p", "int", 5, TypeLevel: false),
        new("event", new[] { "event e: Action[int]" }, "e", "e", "Action[int]", null, TypeLevel: false),
        new("static-method", new[] { "@static", "def sm() -> int:", "    return 6" }, "sm()", "sm", "int", 6, TypeLevel: true),
        new("instance-method", new[] { "def im(self) -> int:", "    return 7" }, "im()", "im", "int", 7, TypeLevel: false),
    };

    internal static readonly Position[] Positions =
    {
        new("instance-field-initializer", new[] { "b: T = §" }, "print(Host().b)"),
        new("static-field-initializer", new[] { "@static", "b: T = §" }, "print(Host.b)"),
        new("instance-property-default", new[] { "property b: T = §" }, "print(Host().b)"),
        new("static-property-default", new[] { "@static", "property b: T = §" }, "print(Host.b)"),
        new("const-initializer", new[] { "const B: T = §" }, "print(Host.B)"),
        new("parameter-default", new[] { "def m(self, x: T = §) -> T:", "    return x" }, "print(Host().m())"),
        new("method-body", new[] { "def m(self) -> T:", "    return §" }, "print(Host().m())"),
    };

    /// <summary>D8's classifier: the position alone, except the ONE host-route slot — a struct/dataclass instance
    /// FIELD initializer is the synthesized constructor's parameter default.</summary>
    internal static ReadPosition Classify(string host, string position) => position switch
    {
        "instance-field-initializer" => host is "struct" or "dataclass" ? ReadPosition.ParameterDefault : ReadPosition.InstanceInitializer,
        "static-field-initializer" or "static-property-default" => ReadPosition.StaticInitializer,
        "instance-property-default" => ReadPosition.InstanceInitializer,
        "const-initializer" => ReadPosition.ConstInitializer,
        "parameter-default" => ReadPosition.ParameterDefault,
        "method-body" => ReadPosition.MethodBody,
        _ => throw new ArgumentOutOfRangeException(nameof(position), position, null),
    };

    /// <summary>D8's admission table (the measured C# rule): the siblings each classified position resolves.</summary>
    internal static readonly IReadOnlyDictionary<ReadPosition, HashSet<string>> Admits = new Dictionary<ReadPosition, HashSet<string>>
    {
        [ReadPosition.InstanceInitializer] = new(StringComparer.Ordinal) { "const", "static-field", "static-property", "static-method" },
        [ReadPosition.StaticInitializer] = new(StringComparer.Ordinal) { "const", "static-field", "static-property", "static-method" },
        [ReadPosition.ConstInitializer] = new(StringComparer.Ordinal) { "const", "static-field", "static-property", "static-method" },
        [ReadPosition.ParameterDefault] = new(StringComparer.Ordinal) { "const" },
        [ReadPosition.MethodBody] = new(StringComparer.Ordinal),
    };

    /// <summary>The phrase a refused SPY0200 carries: the classified position's, split by the declaring member's form
    /// only in <c>InstanceInitializer</c> (a field initializer and a property default are different slots to a reader).</summary>
    internal static string PhraseOf(ReadPosition classified, string position) => classified switch
    {
        ReadPosition.InstanceInitializer => position == "instance-property-default" ? PropertyDefaultPhrase : FieldInitializerPhrase,
        ReadPosition.StaticInitializer => StaticInitializerPhrase,
        ReadPosition.ConstInitializer => ConstInitializerPhrase,
        ReadPosition.ParameterDefault => ParameterDefaultPhrase,
        _ => MethodBodyPhrase,
    };

    /// <summary>The steer a refused SPY0200 carries, or null where D8 names none: in a method body the crossing steer
    /// (<c>'Host.s</c> for a type-level sibling, <c>'self.f</c> for an instance one — #1786; after P21h the event and
    /// the methods get theirs too); in an instance initializer or a parameter default an instance sibling's
    /// <c>self.f</c> (assign it in <c>__init__</c> / the body); a static or const initializer has no instance to steer
    /// to.</summary>
    internal static string? SteerOf(ReadPosition classified, Sibling sibling) => classified switch
    {
        ReadPosition.MethodBody => sibling.TypeLevel ? $"'Host.{sibling.Member}" : $"'self.{sibling.Member}",
        ReadPosition.InstanceInitializer or ReadPosition.ParameterDefault when !sibling.TypeLevel => $"self.{sibling.Member}",
        _ => null,
    };

    private static string Indent(string line) => line.Length == 0 ? line : "    " + line;

    /// <summary>The program of one cell: the host, ONE sibling, ONE reader whose read is <paramref name="read"/> typed
    /// <paramref name="type"/> (no sibling when <paramref name="sibling"/> is null — the reader control), and
    /// <c>main</c>. Returns the source with the read's line and column and the reader's first line.</summary>
    internal static (string Source, int ReadLine, int ReadColumn, int ReaderLine) Program(
        Host host, Sibling? sibling, Position position, string read, string type)
    {
        var lines = new List<string>(host.Header);
        if (sibling != null)
        {
            lines.AddRange(sibling.Lines.Select(Indent));
            lines.Add("");
        }

        var readerLine = lines.Count + 1;
        int readLine = 0, readColumn = 0;
        foreach (var template in position.Lines)
        {
            var line = Indent(template.Replace("T", type, StringComparison.Ordinal));
            var mark = line.IndexOf('§', StringComparison.Ordinal);
            if (mark >= 0)
            {
                readLine = lines.Count + 1;
                readColumn = mark + 1;
                line = line.Replace("§", read, StringComparison.Ordinal);
            }

            lines.Add(line);
        }

        lines.AddRange(new[] { "", "", "def main() -> None:", "    " + position.Use, "" });
        return (string.Join("\n", lines), readLine, readColumn, readerLine);
    }

    internal static IEnumerable<ReadCell> Cells()
    {
        foreach (var host in Hosts)
        {
            foreach (var position in Positions)
            {
                foreach (var sibling in Siblings)
                {
                    var (source, readLine, readColumn, readerLine) = Program(host, sibling, position, sibling.Read, sibling.Type);
                    var classified = Classify(host.Name, position.Name);
                    var id = $"class-body-read × {host.Name} × {position.Name} × {sibling.Name}";
                    if (Admits[classified].Contains(sibling.Name))
                    {
                        yield return new ReadCell(id, host.Name, position.Name, sibling.Name, source, classified,
                            Array.Empty<(string, int, int)>(), null, null, $"{sibling.Value}\n");
                    }
                    else if (classified == ReadPosition.ParameterDefault && sibling.TypeLevel)
                    {
                        // The @static sibling resolves (as C# resolves it before CS1736); the constant-position rule is
                        // the one verdict, at the parameter's name — or, for the struct/dataclass field slot, the field's.
                        var (line, column) = position.Name == "parameter-default"
                            ? (readerLine, Indent(position.Lines[0]).IndexOf("x:", StringComparison.Ordinal) + 1)
                            : (readerLine, 5);
                        yield return new ReadCell(id, host.Name, position.Name, sibling.Name, source, classified,
                            new[] { ("SPY0401", line, column) }, null, null, null);
                    }
                    else
                    {
                        yield return new ReadCell(id, host.Name, position.Name, sibling.Name, source, classified,
                            new[] { ("SPY0200", readLine, readColumn) }, PhraseOf(classified, position.Name),
                            SteerOf(classified, sibling), null);
                    }
                }
            }
        }
    }

    /// <summary>Every ratcheted id of this class, as the shared allowlist spells it.</summary>
    internal static IEnumerable<string> CellIds() => Cells().Select(c => c.Id);

    [Fact]
    public void Cells_CoverEveryAxis_AnchoredToLiterals()
    {
        Assert.Equal(3, Hosts.Length);
        Assert.Equal(7, Positions.Length);
        Assert.Equal(8, Siblings.Length);
        var cells = Cells().ToList();
        Assert.Equal(3 * 7 * 8, cells.Count);
        Assert.Equal(3 * 7 * 8, cells.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count());
        // Admitted (run): the class's five initializer rows × 4 type-level siblings + the struct's and the dataclass's
        // four (their instance FIELD initializer is a parameter default) + one const per parameter-default row (the
        // class's parameter default; the struct's and dataclass's parameter default and field slot).
        Assert.Equal(5 * 4 + 1 + 2 * (4 * 4 + 2), cells.Count(c => c.ExpectedStdout != null));
        // SPY0401 alone: 3 non-const @static siblings per parameter-default row (class 1 row, struct/dataclass 2).
        Assert.Equal(3 * (1 + 2 * 2), cells.Count(c => c.ExpectedErrors.Any(e => e.Code == "SPY0401")));
        // SPY0200 alone: everything else — the 8 method-body cells, 4 instance siblings per other row, per host.
        Assert.Equal(3 * (8 + 6 * 4), cells.Count(c => c.ExpectedErrors.Any(e => e.Code == "SPY0200")));
        Assert.Equal(2 * 8, cells.Count(c => c.Classified == ReadPosition.ParameterDefault && c.Position == "instance-field-initializer"));
        // The phrases are pairwise non-overlapping, so "carries exactly one" is decidable.
        foreach (var a in AllPhrases)
            foreach (var b in AllPhrases.Where(b => !ReferenceEquals(a, b)))
                Assert.False(a.Contains(b, StringComparison.Ordinal), $"phrase '{a}' contains '{b}'");
    }

    /// <summary>Hard self-checks on the contract in code: one verdict per cell — never two codes, never an internal
    /// code, a refused SPY0200 always names a phrase, an admitted cell always prints.</summary>
    [Fact]
    public void Contract_IsOneVerdictPerCell()
    {
        foreach (var cell in Cells())
        {
            Assert.True(cell.ExpectedErrors.Count <= 1, $"{cell.Id}: the expected set names {cell.ExpectedErrors.Count} codes");
            Assert.DoesNotContain(cell.ExpectedErrors, e => e.Code.StartsWith("SPY09", StringComparison.Ordinal));
            Assert.True((cell.ExpectedErrors.Count == 0) == (cell.ExpectedStdout != null), $"{cell.Id}: an admitted cell must run, a refused one must not");
            if (cell.ExpectedErrors.Any(e => e.Code == "SPY0200"))
                Assert.NotNull(cell.Phrase);
        }
    }

    [Fact]
    public void Read_HasOneVerdictPerPositionAndSibling()
    {
        var allowlist = TypeDefinitionOwnSymbolMatrixTests.LoadAllowlist(out var problems);
        var api = new CompilerApi(null, new[] { SharpyCoreReference.Location });
        var red = new List<string>();

        // Controls (hard, never allowlisted): every reader with a literal and no sibling analyses clean and runs; every
        // sibling alone analyses clean. A cell's red is then the READ's, not its declarations'.
        foreach (var host in Hosts)
        {
            foreach (var position in Positions)
            {
                var (source, _, _, _) = Program(host, null, position, "0", "int");
                var control = api.Analyze(source, CancellationToken.None);
                if (control.Diagnostics.Any(d => d.IsError))
                    problems.Add($"control {host.Name} × {position.Name} (reader alone) does not analyse clean: {Fingerprint(control.Diagnostics.Where(d => d.IsError))}");
                var run = CompileAndExecute(source);
                if (!run.Success || run.StandardOutput != "0\n")
                    problems.Add($"control {host.Name} × {position.Name} (reader alone) does not run 0: success={run.Success} "
                        + $"stdout=[{run.StandardOutput.ReplaceLineEndings("\\n")}] {string.Join(" | ", run.CompilationErrors).ReplaceLineEndings(" ")}");
            }

            foreach (var sibling in Siblings)
            {
                var source = string.Join("\n", host.Header.Concat(sibling.Lines.Select(Indent))
                    .Concat(new[] { "", "", "def main() -> None:", "    print(\"ok\")", "" }));
                var control = api.Analyze(source, CancellationToken.None);
                if (control.Diagnostics.Any(d => d.IsError))
                    problems.Add($"control {host.Name} × {sibling.Name} (sibling alone) does not analyse clean: {Fingerprint(control.Diagnostics.Where(d => d.IsError))}");
            }
        }

        var ran = 0;
        foreach (var cell in Cells())
        {
            var result = api.Analyze(cell.Source, CancellationToken.None);
            var errors = result.Diagnostics.Where(d => d.IsError).ToList();
            Output.WriteLine($"{cell.Id}: {Fingerprint(result.Diagnostics)}");
            var failures = new List<string>();

            var internalUnderAnalyze = result.Diagnostics.Where(d => (d.Code ?? "").StartsWith("SPY09", StringComparison.Ordinal)).ToList();
            if (internalUnderAnalyze.Count > 0)
                problems.Add($"{cell.Id}: an internal error under Analyze (hard, never allowlisted): "
                    + string.Join(" | ", internalUnderAnalyze.Select(d => d.Message)).ReplaceLineEndings(" "));

            var expected = cell.ExpectedErrors.Select(e => $"{e.Code}@{e.Line}:{e.Column}").OrderBy(s => s, StringComparer.Ordinal).ToList();
            var actual = errors.Select(d => $"{d.Code ?? "-"}@{d.Line}:{d.Column}").OrderBy(s => s, StringComparer.Ordinal).ToList();
            if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
            {
                // A refusal the analysis does not make: say what the program does under run (an ICE, or it runs).
                var underRun = "";
                if (actual.Count == 0 && cell.ExpectedStdout is null)
                {
                    var silent = CompileAndExecute(cell.Source);
                    underRun = silent.Success
                        ? $" — under run it RUNS, stdout=[{silent.StandardOutput.ReplaceLineEndings("\\n")}]"
                        : $" — under run: {RunFingerprint(silent)}";
                }

                failures.Add($"errors are [{string.Join(" ", actual)}], expected [{string.Join(" ", expected)}]{underRun}");
            }

            // The refusal's text: its position's phrase, no other position's, and the steer D8 names.
            if (cell.Phrase is { } phrase && errors.FirstOrDefault(d => d.Code == "SPY0200") is { } refusal)
            {
                if (!refusal.Message.Contains(phrase, StringComparison.Ordinal))
                    failures.Add($"the SPY0200 does not name the position ('{phrase}'): \"{refusal.Message}\"");
                var others = AllPhrases.Where(p => p != phrase && refusal.Message.Contains(p, StringComparison.Ordinal)).ToList();
                if (others.Count > 0)
                    failures.Add($"the SPY0200 names another position ({string.Join(", ", others.Select(o => $"'{o}'"))})");
                if (cell.Steer is { } steer && !refusal.Message.Contains(steer, StringComparison.Ordinal))
                    failures.Add($"the SPY0200 carries no steer '{steer}'");
            }

            if (cell.ExpectedStdout is { } stdout && actual.Count == 0)
            {
                ran++;
                var run = CompileAndExecute(cell.Source);
                var internalCodes = InternalCodes(run);
                if (internalCodes.Count > 0)
                    failures.Add($"an internal error under run: {RunFingerprint(run)} "
                        + string.Join(" | ", internalCodes.Select(d => d.Message)).ReplaceLineEndings(" "));
                if (!run.Success || run.StandardOutput != stdout)
                    failures.Add($"run: success={run.Success} stdout=[{run.StandardOutput.ReplaceLineEndings("\\n")}], expected [{stdout.ReplaceLineEndings("\\n")}] "
                        + string.Join(" | ", run.CompilationErrors).ReplaceLineEndings(" "));
            }

            if (failures.Count > 0)
                Output.WriteLine($"    red: {string.Join("; ", failures)}");
            TypeDefinitionOwnSymbolMatrixTests.Ratchet(cell.Id, failures, allowlist, problems, red);
        }

        Output.WriteLine($"{Cells().Count()} class-body-read cells, {ran} executed, {red.Count} allowlisted red");
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The admission table's C# oracle.
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>The C# member each sibling is emitted as, and the C# spelling of its bare read.</summary>
    private static (string Declaration, string Read, string Type) CSharpSibling(string sibling) => sibling switch
    {
        "const" => ("public const int K = 1;", "K", "int"),
        "static-field" => ("public static int S = 2;", "S", "int"),
        "instance-field" => ("public int F = 3;", "F", "int"),
        "static-property" => ("public static int Sp { get; set; } = 4;", "Sp", "int"),
        "instance-property" => ("public int P { get; set; } = 5;", "P", "int"),
        "event" => ("public event System.Action<int> E;", "E", "System.Action<int>"),
        "static-method" => ("public static int Sm() => 6;", "Sm()", "int"),
        "instance-method" => ("public int Im() => 7;", "Im()", "int"),
        _ => throw new ArgumentOutOfRangeException(nameof(sibling), sibling, null),
    };

    /// <summary>The C# shape the emission route writes for a read position in a host — a literal read off the emitter
    /// (a plain class's field initializer is a field initializer; a struct's or dataclass's instance field is the
    /// synthesized constructor's parameter default, <c>StructConstructorRoster</c> / <c>IsSynthesizedConstructorField</c>;
    /// a property default is a property initializer in every host, #1938; a non-constant <c>const</c> is
    /// <c>static readonly</c>, <c>ConstEligibility</c>). Deliberately NOT derived from <see cref="Classify"/>.</summary>
    private static string CSharpShape(string host, string position, string type, string read) => position switch
    {
        "instance-field-initializer" when host is "struct" or "dataclass" => $"public {type} B; public Host({type} b = {read}) {{ B = b; }}",
        "instance-field-initializer" => $"public {type} B = {read};",
        "static-field-initializer" => $"public static {type} B = {read};",
        "instance-property-default" => $"public {type} B {{ get; set; }} = {read};",
        "static-property-default" => $"public static {type} B {{ get; set; }} = {read};",
        "const-initializer" => $"public static readonly {type} B = {read};",
        "parameter-default" => $"public {type} M({type} x = {read}) => x;",
        _ => throw new ArgumentOutOfRangeException(nameof(position), position, null),
    };

    private static readonly Lazy<MetadataReference[]> CSharpReferences = new(() =>
    {
        var coreLib = typeof(object).Assembly.Location;
        return new MetadataReference[]
        {
            MetadataReference.CreateFromFile(coreLib),
            MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(coreLib)!, "System.Runtime.dll")),
        };
    });

    /// <summary>The C# errors of one sibling and one reader in one host, compiled ALONE (in one compilation the
    /// declaration-phase errors suppress the initializer-binding ones — measured by the plan's csprobe).</summary>
    private static List<string> CSharpErrors(string host, string position, string sibling)
    {
        var (declaration, read, type) = CSharpSibling(sibling);
        var keyword = host == "struct" ? "struct" : "class";
        var ctor = host == "struct" ? "public Host() { } " : "";
        var source = $"public {keyword} Host {{ {ctor}{declaration} {CSharpShape(host, position, type, read)} }}";
        var compilation = CSharpCompilation.Create(
            "ClassBodyReadOracle", new[] { CSharpSyntaxTree.ParseText(source) }, CSharpReferences.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.Id).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList();
    }

    [Fact]
    public void AdmissionTable_AgreesWithCSharp()
    {
        // Positive control: the oracle compiles a shape it should accept and refuses one it should refuse, so neither
        // "C# admits everything" (a broken reference set yields errors everywhere) nor "C# refuses nothing" passes.
        Assert.Empty(CSharpErrors("class", "static-field-initializer", "static-field"));
        Assert.Contains("CS0236", CSharpErrors("class", "instance-field-initializer", "instance-field"));

        var disagreements = new List<string>();
        foreach (var host in Hosts)
        {
            foreach (var position in Positions.Where(p => p.Name != "method-body"))
            {
                foreach (var sibling in Siblings)
                {
                    var csErrors = CSharpErrors(host.Name, position.Name, sibling.Name);
                    var csAdmits = csErrors.Count == 0;
                    var tableAdmits = Admits[Classify(host.Name, position.Name)].Contains(sibling.Name);
                    Output.WriteLine($"{host.Name} × {position.Name} × {sibling.Name}: C# [{string.Join(" ", csErrors)}], table {(tableAdmits ? "admits" : "refuses")}");
                    if (csAdmits != tableAdmits)
                        disagreements.Add($"{host.Name} × {position.Name} × {sibling.Name}: C# {(csAdmits ? "admits" : $"refuses [{string.Join(" ", csErrors)}]")}, "
                            + $"the table {(tableAdmits ? "admits" : "refuses")} ({Classify(host.Name, position.Name)})");
                }
            }
        }

        Assert.True(disagreements.Count == 0, string.Join("\n", disagreements));
    }

    private static List<CompilerDiagnostic> InternalCodes(ExecutionResult run)
        => run.RawDiagnostics.Where(d => (d.Code ?? "").StartsWith("SPY05", StringComparison.Ordinal)
            || (d.Code ?? "").StartsWith("SPY09", StringComparison.Ordinal)).ToList();

    private static string Fingerprint(IEnumerable<CompilerDiagnostic> diagnostics)
        => string.Join(" ", diagnostics.Select(d => $"{d.Code ?? "-"}@{d.Line}:{d.Column}").OrderBy(s => s, StringComparer.Ordinal));

    /// <summary>A failed run's internal codes with the C# error ids their messages carry (<c>SPY0908[CS1736]</c>).</summary>
    private static string RunFingerprint(ExecutionResult run)
    {
        var internalCodes = InternalCodes(run);
        if (internalCodes.Count == 0)
            return $"refused [{string.Join(" | ", run.CompilationErrors).ReplaceLineEndings(" ")}]";
        return string.Join(" ", internalCodes.Select(d =>
        {
            var cs = Regex.Matches(d.Message, @"CS\d{4}").Select(m => m.Value).Distinct(StringComparer.Ordinal).OrderBy(c => c, StringComparer.Ordinal);
            return $"{d.Code}[{string.Join(",", cs)}]";
        }).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal));
    }
}
