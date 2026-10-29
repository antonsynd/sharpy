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
/// The generator of the <b>class-body read contract</b> (P21h D8/D9 (v); #2384, ruling R-JJ; owner rulings F1/F2
/// 2026-10-29): <b>a bare class-body name read from a position that is not a method body resolves exactly the
/// siblings C# resolves in that position, declared EARLIER in the body, even past a module binding of the same name,
/// and refuses the rest with ONE diagnostic whose steer names the position</b>. Before it, a bare sibling read from a
/// field initializer, a property default, a <c>const</c> initializer or a parameter default had no decided verdict: a
/// field sibling bound and crashed the emitter (SPY0909 "No CodeGenInfo for local"), a <c>@static</c> property or
/// method that C# admits was refused with the method-body steer, an event and a method got no description, and a
/// parameter default drew two codes (measured @ 9aae96977).
///
/// <para><b>Matrix.</b> host {<c>class</c>, <c>@dataclass class</c>, <c>struct</c>} × read position
/// {instance-field-initializer, static-field-initializer, instance-property-default, static-property-default,
/// const-initializer, parameter-default, method-body} × sibling {const, static-field, instance-field,
/// static-property, instance-property, event, static-method, instance-method} × order {before, after} × shadow
/// {none, module}, plus the reader reading ITSELF (sibling <c>itself</c>, order <c>self</c>) per host × position ×
/// shadow; ids <c>class-body-read × &lt;host&gt; × &lt;position&gt; × &lt;sibling&gt; × &lt;order&gt; ×
/// &lt;shadow&gt;</c>. Each cell declares ONE sibling and ONE reader (<c>b</c>, <c>B</c> for the const reader, <c>m</c>
/// for the method readers) whose initializer, default or return value is the bare read, annotated with the sibling's
/// type, and presents the reader in <c>main</c>. ORDER puts the sibling before or after the reader; <c>self</c> reads
/// the reader's own name (<c>@static b: int = b + 1</c>, <c>def m(self) -> int: return m()</c>). SHADOW
/// <c>module</c> declares a module binding of the read name above the class. Every value is distinct — the members
/// const 1, <c>@static</c> field 2, instance field 3, <c>@static</c> property 4, instance property 5, <c>@static</c>
/// method 6, instance method 7; the module bindings 90–98 — so a read that resolved the wrong binding, or read a
/// later member before its initializer ran (<c>0</c>), prints the wrong number.</para>
///
/// <para><b>The contract in code.</b> <see cref="Classify"/> and <see cref="Admits"/> are this test's OWN literal
/// copy of D8 — never a call into the checker. The classification is host-route-aware for ONE slot: an instance FIELD
/// initializer in a struct or a <c>@dataclass</c> is the synthesized constructor's parameter default
/// (<c>ParameterDefault</c>); an instance PROPERTY default is a C# property initializer in every host
/// (<c>InstanceInitializer</c>); a <c>const</c> initializer that is not a compile-time constant emits
/// <c>static readonly</c> (<c>ConstInitializer</c>, admitting what a static initializer admits). Outside a method
/// body: F2 — a class member declared EARLIER outranks the module binding, so the verdict is the one without the
/// shadow, while a non-const member declared LATER, or the reader itself, yields to it (lead rulings (y)/(b), owner
/// ruling (a) on the self read — a const is not
/// ordered, so it outranks the module binding in any order; Python's rule for the rest: the read runs the
/// module's value, or is SPY0401 in a <c>ParameterDefault</c>, a module binding not being a constant); F1 — a
/// <c>@static</c> field, property or method declared later, or a <c>@static</c> reader reading itself, is exactly ONE
/// SPY0200 at the read carrying <see cref="DeclaredLaterPhrase"/> and the position's phrase (<see cref="IsOrdered"/>:
/// a <c>const</c> is not ordered, F1-const, and an instance member keeps its kind refusal whatever the order; the
/// <c>const</c> reader reading itself is a constant cycle, <see cref="ConstCycleCode"/>); otherwise the table decides.
/// In a method body ORDER changes nothing and the module binding wins (#1786, R-Y — Python's class-scope rule): with
/// no shadow, one SPY0200 with the crossing steer; with one, the program RUNS and prints the module's value. An
/// admitted cell analyses clean and RUNS printing the expected value; a refused cell is exactly ONE error — SPY0401
/// for an earlier <c>@static</c> sibling in a <c>ParameterDefault</c> (the name resolves, the constant-position rule
/// refuses it), else SPY0200 at the read whose message carries its position's phrase
/// (<see cref="FieldInitializerPhrase"/> …) and no other position's, <see cref="DeclaredLaterPhrase"/> exactly when
/// the order refuses, plus the steer D8 names. Never two codes; an SPY09xx under <c>Analyze</c> is hard.</para>
///
/// <para><b>The table's oracle.</b> <see cref="AdmissionTable_AgreesWithCSharp"/> compiles, with Roslyn, the C# shape
/// the emitter writes for every non-method position (<see cref="CSharpShape"/>, a literal derived from the emission
/// route, independent of <see cref="Classify"/>) reading each sibling, and requires the table to admit exactly what
/// C# compiles. That is what makes a flipped table row red while the matrix cells are still red at the base. The
/// oracle judges the KIND table only: C# binds a class member before any outer name and has no declaration-order
/// rule, so F1's order rule (Python's — a later class-body name is not yet bound) and the method body (#1786) are
/// pinned by the matrix alone.</para>
///
/// <para><b>Ratchet.</b> The cells ratchet over the declaration-position generator's allowlist
/// (<c>Semantic/declaration-position-allowlist.txt</c>, <see cref="TypeDefinitionOwnSymbolMatrixTests.LoadAllowlist"/>,
/// whose id universe includes <see cref="CellIds"/>): a listed cell that passes is STALE, an unlisted failing cell is
/// red. The controls (each reader with a literal and no sibling runs; each sibling alone and each module binding
/// alone analyse clean) and the contract self-checks are never exempted.</para>
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

    /// <summary>F1 (owner ruling 2026-10-29): a non-method read of a sibling declared LATER in the body, or of the
    /// reader itself, is refused with this phrase beside the position's; never present on any other refusal.</summary>
    internal const string DeclaredLaterPhrase = "declared later";

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
    /// denotes, the read's type, the value it prints when admitted (null for the event, never admitted), whether it is
    /// type-level (<c>const</c> or <c>@static</c>), its module binding's lines (SHADOW <c>module</c>), the value the
    /// module binding presents, and how <c>main</c> presents the reader (<c>{0}</c> is the reader expression: printed,
    /// or — for the event — invoked, so a module lambda prints its argument and a null member event throws).</summary>
    internal sealed record Sibling(
        string Name, string[] Lines, string Read, string Member, string Type, int? Value, bool TypeLevel,
        string[] ModuleLines, int ModuleValue, string Present = "print({0})", bool IsConst = false);

    /// <summary>A read position: its id, its reader lines (body-relative; <c>§</c> marks the read, <c>T</c> the type),
    /// the reader expression <c>main</c> presents, the reader's own name and its self-read (ORDER <c>self</c>).</summary>
    internal sealed record Position(string Name, string[] Lines, string Use, string ReaderName, string SelfRead);

    /// <summary>One cell: its source, its classified position, the exact expected errors (code, line, column), the
    /// phrases and steer a refused SPY0200 must carry, and the stdout of an admitted cell.</summary>
    internal sealed record ReadCell(
        string Id, string Host, string Position, string Sibling, string Order, string Shadow, string Source,
        ReadPosition Classified, IReadOnlyList<(string Code, int Line, int Column)> ExpectedErrors, string? Phrase,
        bool DeclaredLater, string? Steer, string? ExpectedStdout);

    internal static readonly Host[] Hosts =
    {
        new("class", new[] { "class Host:" }),
        new("dataclass", new[] { "@dataclass", "class Host:" }),
        new("struct", new[] { "struct Host:" }),
    };

    internal static readonly Sibling[] Siblings =
    {
        new("const", new[] { "const K: int = 1" }, "K", "K", "int", 1, TypeLevel: true, new[] { "K: int = 91" }, 91, IsConst: true),
        new("static-field", new[] { "@static", "s: int = 2" }, "s", "s", "int", 2, TypeLevel: true, new[] { "s: int = 92" }, 92),
        new("instance-field", new[] { "f: int = 3" }, "f", "f", "int", 3, TypeLevel: false, new[] { "f: int = 93" }, 93),
        new("static-property", new[] { "@static", "property sp: int = 4" }, "sp", "sp", "int", 4, TypeLevel: true, new[] { "sp: int = 94" }, 94),
        new("instance-property", new[] { "property p: int = 5" }, "p", "p", "int", 5, TypeLevel: false, new[] { "p: int = 95" }, 95),
        new("event", new[] { "event e: Action[int]" }, "e", "e", "Action[int]", null, TypeLevel: false,
            new[] { "e: Action[int] = lambda v: print(v)" }, 90, Present: "{0}(90)"),
        new("static-method", new[] { "@static", "def sm() -> int:", "    return 6" }, "sm()", "sm", "int", 6, TypeLevel: true,
            new[] { "def sm() -> int:", "    return 96" }, 96),
        new("instance-method", new[] { "def im(self) -> int:", "    return 7" }, "im()", "im", "int", 7, TypeLevel: false,
            new[] { "def im() -> int:", "    return 97" }, 97),
    };

    internal static readonly Position[] Positions =
    {
        new("instance-field-initializer", new[] { "b: T = §" }, "Host().b", "b", "b + 1"),
        new("static-field-initializer", new[] { "@static", "b: T = §" }, "Host.b", "b", "b + 1"),
        new("instance-property-default", new[] { "property b: T = §" }, "Host().b", "b", "b + 1"),
        new("static-property-default", new[] { "@static", "property b: T = §" }, "Host.b", "b", "b + 1"),
        new("const-initializer", new[] { "const B: T = §" }, "Host.B", "B", "B + 1"),
        new("parameter-default", new[] { "def m(self, x: T = §) -> T:", "    return x" }, "Host().m()", "m", "m()"),
        new("method-body", new[] { "def m(self) -> T:", "    return §" }, "Host().m()", "m", "m()"),
    };

    /// <summary>ORDER: where the sibling sits relative to the reader.</summary>
    internal static readonly string[] Orders = { "before", "after" };

    /// <summary>SHADOW: no outer binding, or a module binding of the read name above the class.</summary>
    internal static readonly string[] Shadows = { "none", "module" };

    /// <summary>The reader reading itself (ORDER <c>self</c>): a sibling of the reader's own kind, named like it, typed
    /// <c>int</c>, valued 98 at module level (<c>b</c>/<c>B</c> a variable, <c>m</c> a function); a module-bound self
    /// read prints 99 (<c>b + 1</c>).</summary>
    private static Sibling Itself(Position position) => new(
        "itself", Array.Empty<string>(), position.SelfRead, position.ReaderName, "int", null,
        TypeLevel: position.Name is "static-field-initializer" or "static-property-default" or "const-initializer",
        position.ReaderName == "m" ? new[] { "def m() -> int:", "    return 98" } : new[] { $"{position.ReaderName}: int = 98" },
        // What the self read evaluates to when the module binding is the one it reads (owner ruling (a)): `b + 1` over
        // the module's 98; the method readers' `m()` is the module function's 98.
        position.ReaderName == "m" ? 98 : 99,
        IsConst: position.Name == "const-initializer");

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

    /// <summary>F1 (with the F1-const ruling, 2026-10-29): outside a method body a <c>@static</c> field, property or
    /// method is read only from an EARLIER declaration — one declared later, or the <c>@static</c> reader itself, is
    /// refused. A <c>const</c> is not ordered (#1795: <c>const A: int = B; const B: int = 3</c> runs 3) and an instance
    /// member keeps its kind refusal whatever the order. The method body has no order rule (#1786 refuses every bare
    /// sibling read).</summary>
    internal static bool OrderRefuses(ReadPosition classified, string order, Sibling sibling)
        => classified != ReadPosition.MethodBody && order is "after" or "self" && IsOrdered(sibling);

    /// <summary>The ordered kinds: <c>@static</c> field, property and method (a type-level member that is not a
    /// <c>const</c>; the self-read of a <c>@static</c> field or property reader).</summary>
    internal static bool IsOrdered(Sibling sibling) => sibling.TypeLevel && !sibling.IsConst;

    /// <summary>The const reader reading itself is a constant CYCLE, refused as at module level (SPY0278 "Circular
    /// constant reference"; lead ruling (a) 2026-10-29 — one rule for const cycles wherever they are written). Pinned
    /// by CODE only (line 0 in the expected set) until its location is measured on a fixed binary.</summary>
    internal const string ConstCycleCode = "SPY0278";

    /// <summary>F2 + lead rulings (y) and (b), 2026-10-29, and owner ruling (a) on the self read (P21h verify,
    /// 2026-10-29): the module binding of the read name decides the read in a method body whatever the order (#1786 —
    /// the walk crosses the class body), and outside one when a NON-const class member is declared LATER than the
    /// reader or IS the reader (Python's rule: the class-body name is not bound until its own statement has run —
    /// <c>x = 5</c>, <c>@static x: int = x + 1</c> gives 6). Otherwise — an earlier member, or a <c>const</c> in any
    /// order (F1-const: a const is not ordered, so a later const is not "later") — the class member outranks it.</summary>
    internal static bool ModuleBindingWins(ReadPosition classified, string shadow, string order, Sibling sibling)
        => shadow == "module" && (classified == ReadPosition.MethodBody || (order is "after" or "self" && !sibling.IsConst));

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
    /// to; an order refusal's cure is moving the declaration, so it names no steer.</summary>
    internal static string? SteerOf(ReadPosition classified, Sibling sibling) => classified switch
    {
        ReadPosition.MethodBody => sibling.TypeLevel ? $"'Host.{sibling.Member}" : $"'self.{sibling.Member}",
        ReadPosition.InstanceInitializer or ReadPosition.ParameterDefault when !sibling.TypeLevel => $"self.{sibling.Member}",
        _ => null,
    };

    private static string Indent(string line) => line.Length == 0 ? line : "    " + line;

    /// <summary>The program of one cell: the module binding (when <paramref name="moduleLines"/> is given), the host,
    /// ONE sibling before or after ONE reader whose read is <paramref name="read"/> typed <paramref name="type"/> (no
    /// sibling when <paramref name="sibling"/> is null — the reader control, and the self-read), and <c>main</c>
    /// presenting the reader through <paramref name="present"/>. Returns the source with the read's line and column and
    /// the reader's first line.</summary>
    internal static (string Source, int ReadLine, int ReadColumn, int ReaderLine) Program(
        Host host, string[]? siblingLines, string order, Position position, string read, string type,
        string[]? moduleLines = null, string present = "print({0})")
    {
        var lines = new List<string>();
        if (moduleLines != null)
        {
            lines.AddRange(moduleLines);
            lines.Add("");
            lines.Add("");
        }

        lines.AddRange(host.Header);
        if (siblingLines != null && order == "before")
        {
            lines.AddRange(siblingLines.Select(Indent));
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

        if (siblingLines != null && order == "after")
        {
            lines.Add("");
            lines.AddRange(siblingLines.Select(Indent));
        }

        lines.AddRange(new[] { "", "", "def main() -> None:", "    " + string.Format(present, position.Use), "" });
        return (string.Join("\n", lines), readLine, readColumn, readerLine);
    }

    internal static IEnumerable<ReadCell> Cells()
    {
        foreach (var host in Hosts)
        {
            foreach (var position in Positions)
            {
                foreach (var shadow in Shadows)
                {
                    foreach (var sibling in Siblings)
                    {
                        foreach (var order in Orders)
                            yield return Cell(host, position, sibling, order, shadow);
                    }

                    yield return Cell(host, position, Itself(position), "self", shadow);
                }
            }
        }
    }

    private static ReadCell Cell(Host host, Position position, Sibling sibling, string order, string shadow)
    {
        var (source, readLine, readColumn, readerLine) = Program(
            host, order == "self" ? null : sibling.Lines, order, position, sibling.Read, sibling.Type,
            shadow == "module" ? sibling.ModuleLines : null, sibling.Present);
        var classified = Classify(host.Name, position.Name);
        var id = $"class-body-read × {host.Name} × {position.Name} × {sibling.Name} × {order} × {shadow}";
        var none = Array.Empty<(string, int, int)>();
        var phrase = PhraseOf(classified, position.Name);
        var refusedAtRead = new[] { ("SPY0200", readLine, readColumn) };

        // The @static sibling (or a module variable or function) resolves in a ParameterDefault, as C# resolves it before
        // CS1736; the constant-position rule is the one verdict, at the parameter's name — or, for the struct/dataclass
        // field slot, the field's.
        var constantSlot = new[]
        {
            position.Name == "parameter-default"
                ? ("SPY0401", readerLine, Indent(position.Lines[0]).IndexOf("x:", StringComparison.Ordinal) + 1)
                : ("SPY0401", readerLine, 5),
        };

        if (ModuleBindingWins(classified, shadow, order, sibling))
        {
            // A module binding is a variable or a function, never a constant: a ParameterDefault refuses it.
            return classified == ReadPosition.ParameterDefault
                ? new ReadCell(id, host.Name, position.Name, sibling.Name, order, shadow, source, classified, constantSlot, null, false, null, null)
                : new ReadCell(id, host.Name, position.Name, sibling.Name, order, shadow, source, classified, none, null, false, null, $"{sibling.ModuleValue}\n");
        }

        if (OrderRefuses(classified, order, sibling))
            return new ReadCell(id, host.Name, position.Name, sibling.Name, order, shadow, source, classified, refusedAtRead, phrase, true, null, null);
        if (order == "self" && sibling.IsConst)
            return new ReadCell(id, host.Name, position.Name, sibling.Name, order, shadow, source, classified,
                new[] { (ConstCycleCode, 0, 0) }, null, false, null, null);
        if (order != "self" && Admits[classified].Contains(sibling.Name))
            return new ReadCell(id, host.Name, position.Name, sibling.Name, order, shadow, source, classified, none, null, false, null, $"{sibling.Value}\n");
        if (classified == ReadPosition.ParameterDefault && sibling.TypeLevel)
            return new ReadCell(id, host.Name, position.Name, sibling.Name, order, shadow, source, classified, constantSlot, null, false, null, null);

        return new ReadCell(id, host.Name, position.Name, sibling.Name, order, shadow, source, classified, refusedAtRead,
            phrase, false, SteerOf(classified, sibling), null);
    }

    /// <summary>Every ratcheted id of this class, as the shared allowlist spells it.</summary>
    internal static IEnumerable<string> CellIds() => Cells().Select(c => c.Id);

    [Fact]
    public void Cells_CoverEveryAxis_AnchoredToLiterals()
    {
        Assert.Equal(3, Hosts.Length);
        Assert.Equal(7, Positions.Length);
        Assert.Equal(8, Siblings.Length);
        Assert.Equal(2, Orders.Length);
        Assert.Equal(2, Shadows.Length);
        var cells = Cells().ToList();
        // Per host × position × shadow: 8 siblings × {before, after} + the self-read.
        Assert.Equal(3 * 7 * 2 * (8 * 2 + 1), cells.Count);
        Assert.Equal(3 * 7 * 2 * (8 * 2 + 1), cells.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count());
        bool Runs(ReadCell c) => c.ExpectedStdout != null;
        bool Has(ReadCell c, string code) => c.ExpectedErrors.Any(e => e.Code == code);
        // Non-method positions per host: class 5 initializer rows + 1 parameter default; struct/dataclass 4 + 2 (their
        // instance FIELD initializer is a parameter default).
        // Runs the MEMBER's value: an earlier type-level sibling in an initializer row (4) or an earlier const in a
        // parameter default (1), both shadows; a const declared later (F1-const, ruling (b)), both shadows, all 6 rows.
        Assert.Equal(2 * (5 * 4 + 1 + 2 * (4 * 4 + 2)) + 2 * 3 * 6,
            cells.Count(c => Runs(c) && c.Position != "method-body" && c.ExpectedStdout == $"{Siblings.FirstOrDefault(s => s.Name == c.Sibling)?.Value}\n"));
        // Runs the MODULE's value: every later non-const sibling under a module shadow in an initializer row (ruling
        // (y)), and every method-body cell under a module shadow (8 × 2 orders + the self-read).
        Assert.Equal(7 * (5 + 2 * 4), cells.Count(c => Runs(c) && c.Position != "method-body" && c.Order == "after" && c.Shadow == "module"
            && c.Sibling != "const"));
        // Runs the MODULE's value for the self read (owner ruling (a)): every non-const reader reading itself under a
        // module shadow in an initializer row — class 4, struct/dataclass 3 (their instance field slot is a parameter default).
        Assert.Equal(4 + 2 * 3, cells.Count(c => Runs(c) && c.Position != "method-body" && c.Order == "self" && c.Shadow == "module"
            && c.ExpectedStdout == "99\n"));
        Assert.Equal(2 * 3 * 6, cells.Count(c => Runs(c) && c.Sibling == "const" && c.Order == "after" && c.ExpectedStdout == "1\n"));
        Assert.Equal(3 * (8 * 2 + 1), cells.Count(c => Runs(c) && c.Position == "method-body"));
        Assert.Equal(2 * (5 * 4 + 1 + 2 * (4 * 4 + 2)) + 2 * 3 * 6 + 7 * (5 + 2 * 4) + (4 + 2 * 3) + 3 * (8 * 2 + 1), cells.Count(Runs));
        // SPY0401 alone: the 3 earlier non-const @static siblings per parameter-default row, both shadows; every later
        // non-const sibling under a module shadow per parameter-default row (the module binding is not a constant), and
        // the non-const reader reading itself under a module shadow per parameter-default row (owner ruling (a)).
        Assert.Equal(2 * 3 * (1 + 2 * 2) + 7 * (1 + 2 * 2) + (1 + 2 * 2), cells.Count(c => Has(c, "SPY0401")));
        // Declared later (unshadowed, or the reader itself): the 3 later @static siblings in every non-method row, + the
        // @static field and property readers reading themselves with no shadow (under one they read it, ruling (a)).
        Assert.Equal(3 * (6 * 3 + 2), cells.Count(c => c.DeclaredLater));
        // The const reader reading itself: the constant cycle, per host × shadow.
        Assert.Equal(2 * 3, cells.Count(c => Has(c, ConstCycleCode)));
        // SPY0200 for a member kind: per non-method row the 4 instance siblings before (both shadows) and after
        // (unshadowed), + the instance readers reading themselves (field, property, parameter default) with no shadow;
        // the method body without a shadow, every sibling in both orders and the self-read.
        Assert.Equal(3 * (6 * 4 * 3 + 3 + (8 * 2 + 1)), cells.Count(c => Has(c, "SPY0200") && !c.DeclaredLater));
        Assert.Equal(2 * 8, cells.Count(c => c.Classified == ReadPosition.ParameterDefault && c.Position == "instance-field-initializer"
            && c.Order == "before" && c.Shadow == "none"));
        // The phrases are pairwise non-overlapping, so "carries exactly one" is decidable.
        foreach (var a in AllPhrases.Append(DeclaredLaterPhrase))
            foreach (var b in AllPhrases.Append(DeclaredLaterPhrase).Where(b => !ReferenceEquals(a, b)))
                Assert.False(a.Contains(b, StringComparison.Ordinal), $"phrase '{a}' contains '{b}'");
        // Every expected stdout is the value of ONE binding, and the members' values are disjoint from the modules'.
        var memberValues = Siblings.Select(s => s.Value).OfType<int>().ToHashSet();
        Assert.Empty(memberValues.Intersect(Siblings.Select(s => s.ModuleValue).Append(98)));
        Assert.DoesNotContain(0, memberValues);
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
            Assert.True(!cell.DeclaredLater || cell.ExpectedErrors.Any(e => e.Code == "SPY0200"), $"{cell.Id}: an order refusal is a SPY0200");
        }
    }

    [Fact]
    public void Read_HasOneVerdictPerPositionAndSibling()
    {
        var allowlist = TypeDefinitionOwnSymbolMatrixTests.LoadAllowlist(out var problems);
        var api = new CompilerApi(null, new[] { SharpyCoreReference.Location });
        var red = new List<string>();

        // Controls (hard, never allowlisted): every reader with a literal and no sibling analyses clean and runs; every
        // sibling alone and every module binding alone analyses clean. A cell's red is then the READ's, not its
        // declarations'.
        var okMain = new[] { "", "", "def main() -> None:", "    print(\"ok\")", "" };
        foreach (var host in Hosts)
        {
            foreach (var position in Positions)
            {
                var (source, _, _, _) = Program(host, null, "before", position, "0", "int");
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
                var source = string.Join("\n", host.Header.Concat(sibling.Lines.Select(Indent)).Concat(okMain));
                var control = api.Analyze(source, CancellationToken.None);
                if (control.Diagnostics.Any(d => d.IsError))
                    problems.Add($"control {host.Name} × {sibling.Name} (sibling alone) does not analyse clean: {Fingerprint(control.Diagnostics.Where(d => d.IsError))}");
            }
        }

        foreach (var module in Siblings.Select(s => s.ModuleLines).Concat(Positions.Select(p => Itself(p).ModuleLines)))
        {
            var source = string.Join("\n", module.Concat(okMain));
            var control = api.Analyze(source, CancellationToken.None);
            if (control.Diagnostics.Any(d => d.IsError))
                problems.Add($"control module binding '{module[0]}' alone does not analyse clean: {Fingerprint(control.Diagnostics.Where(d => d.IsError))}");
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

            // An expectation at line 0 pins the code alone (ConstCycleCode); every other one pins code@line:col.
            var codeOnly = cell.ExpectedErrors.Any(e => e.Line == 0);
            var expected = cell.ExpectedErrors.Select(e => codeOnly ? e.Code : $"{e.Code}@{e.Line}:{e.Column}").OrderBy(s => s, StringComparer.Ordinal).ToList();
            var actual = errors.Select(d => codeOnly ? d.Code ?? "-" : $"{d.Code ?? "-"}@{d.Line}:{d.Column}").OrderBy(s => s, StringComparer.Ordinal).ToList();
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

            // The refusal's text: its position's phrase, no other position's, "declared later" exactly when the order
            // refuses, and the steer D8 names.
            if (cell.Phrase is { } phrase && errors.FirstOrDefault(d => d.Code == "SPY0200") is { } refusal)
            {
                if (!refusal.Message.Contains(phrase, StringComparison.Ordinal))
                    failures.Add($"the SPY0200 does not name the position ('{phrase}'): \"{refusal.Message}\"");
                var others = AllPhrases.Where(p => p != phrase && refusal.Message.Contains(p, StringComparison.Ordinal)).ToList();
                if (others.Count > 0)
                    failures.Add($"the SPY0200 names another position ({string.Join(", ", others.Select(o => $"'{o}'"))})");
                if (refusal.Message.Contains(DeclaredLaterPhrase, StringComparison.Ordinal) != cell.DeclaredLater)
                    failures.Add(cell.DeclaredLater
                        ? $"the SPY0200 does not say '{DeclaredLaterPhrase}'"
                        : $"the SPY0200 says '{DeclaredLaterPhrase}' for an earlier declaration");
                if (cell.Steer is { } steer && !refusal.Message.Contains(steer, StringComparison.Ordinal))
                    failures.Add($"the SPY0200 carries no steer '{steer}'");
            }

            // An admitted cell must RUN and print exactly the expected binding's value — a wrong binding (the module's,
            // or a later member read before its initializer ran, 0) is red.
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
