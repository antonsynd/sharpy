using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// <c>[SharpyName]</c> is stamped on a user type ONLY when its emitted CLR name differs from its
/// source name (#2006, R-CF — the <c>[SharpyFieldName]</c> attribute-when-different precedent,
/// #1607): the runtime reads the python name from it, and a PascalCase program carries none, so
/// the snapshot corpus does not churn. The stamp is read from the type's <c>CodeGenInfo</c>
/// (Rule 2) at every type-declaration site: class, struct, interface, int enum, string enum,
/// union, union case, delegate — top-level and nested. An interface keeps its source casing and a
/// keyword-named type differs only by C#'s <c>@</c> escape (metadata name = source), so neither is
/// stamped.
/// </summary>
public class SharpyNameStampTests
{
    private static readonly string FixturesRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "Sharpy.Compiler.Tests", "Integration", "TestFixtures"));

    private const string Program = @"
class Plain:
    pass

class my_thing:
    class inner_cls:
        pass
    class Inner:
        pass

class `esc_thing`:
    pass

class `event`:
    pass

struct my_pt:
    x: int
    def __init__(self, x: int) -> None:
        self.x = x

interface shape_like:
    def area(self) -> float: ...

enum my_color:
    RED = 1

enum mood_kind:
    HAPPY = ""h""

union my_shape:
    case circle(r: float)
    case Square(s: float)

delegate my_fn(v: int) -> None

def main() -> None:
    pass
";

    /// <summary>Emitted identifier → the python name the stamp must carry (null: no stamp).</summary>
    private static readonly Dictionary<string, string?> Expected = new(StringComparer.Ordinal)
    {
        ["Plain"] = null,
        ["MyThing"] = "my_thing",
        ["InnerCls"] = "inner_cls",
        ["Inner"] = null,
        ["esc_thing"] = null,
        ["MyPt"] = "my_pt",
        ["shape_like"] = null,
        ["event"] = null,
        ["MyColor"] = "my_color",
        ["MoodKind"] = "mood_kind",
        ["MyShape"] = "my_shape",
        ["Circle"] = "circle",
        ["Square"] = null,
        ["MyFn"] = "my_fn",
    };

    [Fact]
    public void Stamp_OnlyWhenTheEmittedNameDiffers_AtEveryTypeDeclarationSite()
    {
        var unit = EmitterTestPipeline.EmitCompilationUnit(Program, isEntryPoint: true, requireNoErrors: true);

        var declared = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var node in unit.DescendantNodes())
        {
            var (identifier, attributeLists) = node switch
            {
                BaseTypeDeclarationSyntax t => (t.Identifier.ValueText, t.AttributeLists),
                DelegateDeclarationSyntax d => (d.Identifier.ValueText, d.AttributeLists),
                _ => (null, default),
            };
            if (identifier == null || !Expected.ContainsKey(identifier))
                continue;
            var stamps = attributeLists.SelectMany(l => l.Attributes)
                .Where(a => a.Name.ToString() == "global::Sharpy.SharpyName").ToList();
            Assert.True(stamps.Count <= 1, $"{identifier}: {stamps.Count} [SharpyName] stamps");
            declared[identifier] = stamps.Count == 0
                ? null
                : ((LiteralExpressionSyntax)stamps[0].ArgumentList!.Arguments[0].Expression).Token.ValueText;
        }

        // Every expected declaration was found — an absent identifier would pass the null rows vacuously.
        Assert.Equal(Expected.Keys.OrderBy(k => k), declared.Keys.OrderBy(k => k));
        foreach (var (identifier, pythonName) in Expected)
            Assert.True(declared[identifier] == pythonName,
                $"{identifier}: expected [SharpyName({pythonName ?? "<none>"})], emitted [SharpyName({declared[identifier] ?? "<none>"})]");
    }

    /// <summary>
    /// The snapshot count: no <c>.expected.cs</c> snapshot declares a type whose emitted name
    /// differs, so the corpus carries ZERO stamps — a stamp emitted unconditionally reddens every
    /// snapshot and this count. The scan's positive control is the <c>[SharpyModule]</c> stamp the
    /// multi-file snapshots do carry (the same attribute-name read).
    /// </summary>
    [Fact]
    public void SnapshotCorpus_CarriesNoStampOnAPascalCaseType()
    {
        var snapshots = Directory.GetFiles(FixturesRoot, "*.expected.cs", SearchOption.AllDirectories);
        Assert.True(snapshots.Length >= 100, $"only {snapshots.Length} snapshots under {FixturesRoot} — the scan is vacuous");

        int sharpyName = 0, sharpyModule = 0;
        var stamped = new List<string>();
        foreach (var file in snapshots)
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
            foreach (var attribute in root.DescendantNodes().OfType<AttributeSyntax>())
            {
                var name = attribute.Name.ToString();
                if (name == "global::Sharpy.SharpyName")
                {
                    sharpyName++;
                    stamped.Add(Path.GetRelativePath(FixturesRoot, file));
                }
                else if (name == "global::Sharpy.SharpyModule")
                    sharpyModule++;
            }
        }

        Assert.True(sharpyModule > 0, "no [SharpyModule] in any snapshot — the attribute scan reads nothing");
        Assert.True(sharpyName == 0, $"{sharpyName} [SharpyName] stamps in the snapshot corpus: {string.Join(", ", stamped.Distinct())}");
    }
}
