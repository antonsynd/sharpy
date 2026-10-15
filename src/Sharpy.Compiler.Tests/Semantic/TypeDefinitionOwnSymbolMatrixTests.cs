using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// P54 (#2250) final verification F1/F2 @ effba7776, as a class: <b>a type definition writes only the symbol declared
/// for it</b>. NameResolver declares the type definitions of a module body and of a type body; one anywhere else — a
/// function body, or a block — has no symbol (C# has no local types). The checker used to look the NAME up and write
/// whatever it found: the master builtin <c>str</c> (a <c>_size</c> backing field; <c>IsDataclass</c>, which the
/// master freeze turned into a SPY0909 ICE) or an enclosing user type (<c>Foo()._size</c> lost its SPY0203 within ONE
/// compile). Now it is refused (SPY0202) and writes nothing.
///
/// <para><b>Matrix</b> (generated): definition kind (7: class, @dataclass class, struct, interface, union, delegate —
/// the class and struct carry the mixed auto/setter property whose backing field was the measured write — and a class
/// with a non-constant <c>const X</c>, whose body <c>ConstEligibility</c> walked with the enclosing <c>Foo</c> it found
/// by name, recording "not a compile-time constant" on <c>Foo.X</c>) × position (4:
/// function body, <c>if</c> in a function, <c>for</c> in a function, module-level <c>try</c>) × name (3: the builtin
/// <c>str</c>; <c>Foo</c>, a module-level user class; <c>Fresh</c>, bound to nothing — the column that was a SPY0202
/// refusal at the base too, the positive control). Each cell is analysed on ONE <see cref="CompilerApi"/> beside its
/// CONTROL — the same program with the definition's lines replaced by <c>pass</c> lines, so every line keeps its number.</para>
///
/// <para><b>Oracle per cell.</b> (1) No internal error (SPY09xx). (2) The definition is refused with SPY0202 at its
/// line (the positive control for (1): the checker reached it). (3) Every diagnostic off the definition's line (which also carries the SPY0453 shadowing warning for <c>str</c>) equals the control's —
/// <c>main</c> reads <c>s._size</c> / <c>f._size</c>, so a written <c>str</c> or <c>Foo</c> changes it. (4) The
/// module-level <c>Foo</c>'s members — names, and each field's folded constant and compile-time fact — equal the
/// control's.</para>
/// </summary>
public class TypeDefinitionOwnSymbolMatrixTests
{
    private readonly ITestOutputHelper _output;

    public TypeDefinitionOwnSymbolMatrixTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>(kind, the definition's lines for a name, unindented).</summary>
    private static readonly (string Kind, Func<string, string[]> Lines)[] Kinds =
    {
        ("class", n => new[]
        {
            $"class {n}:",
            "    property size: int = 0",
            "    property set size(self, v: int) -> None:",
            "        self._size = v",
        }),
        ("dataclass", n => new[]
        {
            "@dataclass",
            $"class {n}:",
            "    x: int",
        }),
        ("struct", n => new[]
        {
            $"struct {n}:",
            "    property size: int = 0",
            "    property set size(self, v: int) -> None:",
            "        self._size = v",
        }),
        ("interface", n => new[]
        {
            $"interface {n}:",
            "    def probe(self, value: int) -> int: ...",
        }),
        ("union", n => new[]
        {
            $"union {n}:",
            "    case Probe(value: int)",
        }),
        ("delegate", n => new[]
        {
            $"delegate {n}(value: int) -> int",
        }),
        ("class-const", n => new[]
        {
            $"class {n}:",
            "    const X: int = len(\"abc\")",
        }),
    };

    /// <summary>(position, the lines before the definition, its indentation, the lines after it).</summary>
    private static readonly (string Position, string[] Before, int Indent, string[] After)[] Positions =
    {
        ("def-body", new[] { "def helper() -> None:" }, 4, new[] { "    print(\"helper\")" }),
        ("if-in-def", new[] { "def helper() -> None:", "    if True:" }, 8, new[] { "    print(\"helper\")" }),
        ("for-in-def", new[] { "def helper() -> None:", "    for i in range(1):" }, 8, new[] { "    print(\"helper\")" }),
        ("module-try", new[] { "try:" }, 4, new[] { "except Exception:", "    pass" }),
    };

    /// <summary>(name, the module-level lines before the definition, main's body).</summary>
    private static readonly (string Name, string[] Prelude, string[] MainBody)[] Names =
    {
        ("str", Array.Empty<string>(), new[] { "    s = \"abc\"", "    print(s._size)" }),
        ("Foo", new[] { "class Foo:", "    const X: int = 5", "", "    def __init__(self) -> None:", "        pass", "" }, new[] { "    f: Foo = Foo()", "    print(f._size)" }),
        ("Fresh", Array.Empty<string>(), new[] { "    print(\"ok\")" }),
    };

    internal sealed record Cell(string Kind, string Position, string Name, string Source, string Control, int DefinitionLine)
    {
        public string Id => $"{Kind} × {Position} × {Name}";
    }

    internal static IEnumerable<Cell> Cells()
    {
        foreach (var (kind, lines) in Kinds)
        {
            foreach (var (position, before, indent, after) in Positions)
            {
                foreach (var (name, prelude, mainBody) in Names)
                {
                    var definition = lines(name).Select(l => new string(' ', indent) + l).ToArray();
                    var placeholder = definition.Select(_ => new string(' ', indent) + "pass").ToArray();
                    string Program(string[] body) => string.Join("\n",
                        prelude.Concat(before).Concat(body).Concat(after)
                            .Concat(new[] { "", "def main() -> None:" }).Concat(mainBody).Append(""));

                    // 1-based line of the definition's `class`/`struct`/… keyword: a decorator line comes first.
                    var keywordOffset = Array.FindIndex(definition, l => !l.TrimStart().StartsWith('@'));
                    yield return new Cell(kind, position, name, Program(definition), Program(placeholder),
                        prelude.Length + before.Length + keywordOffset + 1);
                }
            }
        }
    }

    [Fact]
    public void Cells_CoverEveryAxis_AnchoredToLiterals()
    {
        Assert.Equal(7, Kinds.Length);
        Assert.Equal(4, Positions.Length);
        Assert.Equal(3, Names.Length);
        Assert.Equal(7 * 4 * 3, Cells().Count());
        Assert.Equal(7 * 4 * 3, Cells().Select(c => c.Id).Distinct().Count());
    }

    [Fact]
    public void EveryTypeDefinitionWritesOnlyItsOwnSymbol()
    {
        var api = new CompilerApi();
        var problems = new List<string>();
        var refused = 0;
        foreach (var cell in Cells())
        {
            var result = api.Analyze(cell.Source, CancellationToken.None);
            var control = api.Analyze(cell.Control, CancellationToken.None);
            var codes = result.Diagnostics.Select(d => d.Code ?? "-").ToList();
            var line = $"{cell.Id}: {Fingerprint(result.Diagnostics)}";
            _output.WriteLine(line);

            if (codes.Any(c => c.StartsWith("SPY09", StringComparison.Ordinal)))
                problems.Add($"{line} — an internal error");

            var refusal = result.Diagnostics.Where(d => d.Code == "SPY0202" && d.Line == cell.DefinitionLine).ToList();
            if (refusal.Count == 1)
                refused++;
            else
                problems.Add($"{line} — expected one SPY0202 refusal at line {cell.DefinitionLine}");

            // The definition's own line carries its refusal and, for `str`, the builtin-shadowing warning (SPY0453).
            var others = Fingerprint(result.Diagnostics.Where(d => d.Line != cell.DefinitionLine));
            var controlFingerprint = Fingerprint(control.Diagnostics);
            if (others != controlFingerprint)
                problems.Add($"{line} — the rest differs from the control [{controlFingerprint}]: the definition wrote a symbol it did not declare");

            if (cell.Name == "Foo")
            {
                var members = FooMembers(result);
                var controlMembers = FooMembers(control);
                if (members != controlMembers)
                    problems.Add($"{cell.Id}: module-level Foo is [{members}] / control [{controlMembers}]");
            }
        }

        _output.WriteLine($"{refused} cells refused with SPY0202");
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    private static string Fingerprint(IEnumerable<global::Sharpy.Compiler.Diagnostics.CompilerDiagnostic> diagnostics)
        => string.Join(" ", diagnostics.Select(d => $"{d.Code ?? "-"}@{d.Line}:{d.Column}").OrderBy(s => s, StringComparer.Ordinal));

    /// <summary>The member names of the module-level <c>Foo</c> (the prelude's first line, column 1).</summary>
    private static string FooMembers(SemanticResult result)
    {
        var foo = result.SemanticQuery?.FindSymbolByDeclaration("Foo", 1, 1) as global::Sharpy.Compiler.Semantic.TypeSymbol
            ?? throw new InvalidOperationException("the module-level class Foo has no symbol");
        return string.Join(",", foo.Fields.Select(f => $"field:{f.Name}={f.ConstantValue}/{f.IsCompileTimeConstant}")
            .Concat(foo.Properties.Select(p => $"property:{p.Name}"))
            .Concat(foo.Methods.Select(m => $"method:{m.Name}"))
            .Concat(foo.Constructors.Select(c => $"ctor:{c.Parameters.Count}"))
            .Append($"dataclass:{foo.IsDataclass}")
            .OrderBy(s => s, StringComparer.Ordinal));
    }
}
