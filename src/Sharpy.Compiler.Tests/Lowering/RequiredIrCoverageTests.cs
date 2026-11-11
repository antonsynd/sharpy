using System.Collections;
using System.Reflection;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Lowering;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Lowering;

/// <summary>
/// Ties the emitter's REQUIRED IR reads (<c>RoslynEmitter.RequiredIrNodeFor</c>, #2443) to the
/// lowering pass's coverage: every AST node of a required shape — an <see cref="IndexAccess"/>, an
/// <c>==</c>/<c>!=</c> <see cref="BinaryOp"/>, a list/set/dict comprehension, a
/// <see cref="GeneratorExpression"/> — that sits ANYWHERE in a module's syntax tree has an index
/// entry of its kind.
///
/// <para><b>Anywhere, not the child walk.</b> Lowering walks <see cref="Node.GetChildNodes"/>; the
/// emitter is not limited to it (a decorator's arguments are not child nodes, yet bracket-attribute
/// arguments are emitted — the SPY0909 this guard was written for). So the walk here is reflective:
/// every public property of every AST record, through non-node records (<see cref="Decorator"/>,
/// <see cref="KeywordArgument"/>, parameters, clauses) and arrays. A position lowering skips is a
/// miss here before it is a crash on a valid program.</para>
///
/// <para>Two arms: the fixture corpus (every fixture, GapDiscovery), and a generated probe matrix —
/// each required shape in each position lowering's child walk does not obviously reach (decorator
/// positional and keyword arguments, parameter defaults, class-body initialisers, enum values,
/// lambda bodies, f-string interpolations, match guards). A probe the front end refuses is counted,
/// not checked; an internal compiler error anywhere fails. The corpus arm compiles every fixture and
/// is categorised <c>GapDiscovery</c>; the probe matrix is an ordinary test.</para>
/// </summary>
public class RequiredIrCoverageTests
{
    private readonly ITestOutputHelper _output;

    public RequiredIrCoverageTests(ITestOutputHelper output) => _output = output;

    /// <summary>The required shapes and the IR kind each lowers to unconditionally.</summary>
    internal static Type? RequiredIrKind(Node node) => node switch
    {
        IndexAccess => typeof(IrIndexAccess),
        BinaryOp { Operator: BinaryOperator.Equal or BinaryOperator.NotEqual } => typeof(IrEqualityComparison),
        ListComprehension or SetComprehension or DictComprehension => typeof(IrLoweredLoop),
        GeneratorExpression => typeof(IrLoweredGenerator),
        _ => null,
    };

    private static readonly (string Kind, string Expr)[] Shapes =
    {
        ("index", "XS[0]"),
        ("equality", "1 == 2"),
        ("inequality", "1 != 2"),
        ("list-comp", "[x for x in XS]"),
        ("set-comp", "{x for x in XS}"),
        ("dict-comp", "{x: x for x in XS}"),
        ("generator", "sum(x for x in XS)"),
    };

    /// <summary>Positions, each a module template with <c>{E}</c> for the shape.</summary>
    private static readonly (string Position, string Template)[] Positions =
    {
        ("attribute-positional", "@[obsolete(\"m\", {E})]\ndef f() -> int:\n    return 1\n"),
        ("attribute-conditional", "@[obsolete(\"a\" if {E} else \"b\")]\ndef f() -> int:\n    return 1\n"),
        ("attribute-on-class", "@[obsolete(\"a\" if {E} else \"b\")]\nclass K:\n    pass\n"),
        ("decorator-keyword", "@[dll_import(\"lib\", entry_point=\"a\" if {E} else \"b\")]\ndef ext() -> None: ...\n"),
        ("parameter-default", "def f(p: object = {E}) -> object:\n    return p\n"),
        ("class-initialiser", "class K:\n    v: object = {E}\n"),
        ("enum-value", "enum Col:\n    A = {E}\n"),
        ("lambda-body", "def f() -> object:\n    g = lambda: {E}\n    return g()\n"),
        ("f-string", "def f() -> str:\n    return f\"{{{E}}}\"\n"),
        ("match-guard", "def f(n: int) -> int:\n    match n:\n        case 1 if bool({E}):\n            return 1\n        case _:\n            return 0\n"),
        ("default-in-method", "class K:\n    def m(self, p: object = {E}) -> object:\n        return p\n"),
    };

    /// <summary>
    /// Matrix cells that already ICE on origin/dev (a553d7bcc) for a reason that is not a lowering-IR
    /// read: an enum member whose value holds a generator expression has no CodeGenInfo for the
    /// generator's loop variable. Not an IR fact; drains when that is fixed.
    /// </summary>
    private static readonly (string Cell, string Message)[] PreexistingInternalErrors =
    {
        ("enum-value×generator", "No CodeGenInfo for local 'x'"),
    };

    private const string Prelude = "XS: list[int] = [1, 2, 3]\n\n";
    private const string Main = "\ndef main() -> None:\n    print(\"ok\")\n";

    [Fact]
    public void ProbeMatrix_EveryRequiredShapeInEveryPosition_IsLowered()
    {
        var misses = new List<string>();
        var internalErrors = new List<string>();
        int checkedCells = 0, refused = 0, nodes = 0;
        var clean = new List<string>();

        foreach (var (position, template) in Positions)
        {
            foreach (var (kind, expr) in Shapes)
            {
                var name = $"{position}×{kind}";
                var source = Prelude + template.Replace("{E}", expr) + Main;
                LoweredCompilationCapture.Result result;
                try
                {
                    result = LoweredCompilationCapture.CompileProgram(source);
                }
                catch (Exception ex)
                {
                    internalErrors.Add($"{name}: compile threw {ex.GetType().Name}: {ex.Message}"
                        + (ex.InnerException is { } inner ? $" <- {inner.GetType().Name}: {inner.Message}" : ""));
                    continue;
                }

                if (result.InternalCompilerErrors.Count > 0)
                {
                    internalErrors.Add($"{name}: {string.Join(" | ", result.InternalCompilerErrors)}");
                    continue;
                }

                if (!result.FrontEndClean)
                {
                    refused++;
                    continue;
                }

                checkedCells++;
                clean.Add(name);
                nodes += Check(name, result, misses);
            }
        }

        _output.WriteLine($"probe cells {Positions.Length * Shapes.Length}: checked {checkedCells}, refused by "
            + $"the front end {refused}; required nodes checked {nodes}\nclean: {string.Join(", ", clean)}");

        // Probe cells whose SPY0909 predates the IR work and is not an IR read: listed so the rest of
        // the matrix stays strict, and asserted to still fail the same way so an entry cannot go stale.
        var preexisting = internalErrors
            .Where(e => PreexistingInternalErrors.Any(p => e.StartsWith(p.Cell + ":", StringComparison.Ordinal)
                                                           && e.Contains(p.Message, StringComparison.Ordinal)))
            .ToList();
        var unexpected = internalErrors.Except(preexisting).ToList();
        Assert.True(unexpected.Count == 0,
            $"{unexpected.Count} probe(s) hit an internal compiler error:\n" + string.Join("\n", unexpected));
        var stale = PreexistingInternalErrors
            .Where(p => !preexisting.Any(e => e.StartsWith(p.Cell + ":", StringComparison.Ordinal)))
            .Select(p => p.Cell)
            .ToList();
        Assert.True(stale.Count == 0,
            "listed pre-existing internal errors no longer occur; delete the entries: " + string.Join(", ", stale));
        Assert.True(misses.Count == 0,
            $"{misses.Count} required-shape node(s) the emitter can reach have no IR of their kind:\n"
            + string.Join("\n", misses));

        // Positive controls: the attribute cells that motivated the guard compile, so they were checked.
        Assert.Contains("attribute-positional×equality", clean);
        Assert.Contains("attribute-conditional×inequality", clean);
        Assert.Contains("attribute-on-class×equality", clean);
    }

    [Fact]
    [Trait("Category", "GapDiscovery")]
    public void FixtureCorpus_EveryRequiredShape_IsLowered()
    {
        var fixtures = FixtureDiscoveryHelper.DiscoverFixturesFrom(FixtureRoots.CompilerTests)
            .Where(f => !f.IsSkipped)
            .OrderBy(f => f.TestName, StringComparer.Ordinal)
            .ToList();
        Assert.True(fixtures.Count > 0, "No fixtures discovered — corpus is empty, guard is vacuous");

        var misses = new List<string>();
        var internalErrors = new List<string>();
        var uncleanRunFixtures = new List<string>();
        int examined = 0, nodes = 0;

        foreach (var fixture in fixtures)
        {
            LoweredCompilationCapture.Result result;
            try
            {
                result = LoweredCompilationCapture.Compile(fixture);
            }
            catch (Exception ex)
            {
                internalErrors.Add($"{fixture.TestName}: compile threw {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            if (result.InternalCompilerErrors.Count > 0)
            {
                internalErrors.Add($"{fixture.TestName}: {string.Join(" | ", result.InternalCompilerErrors)}");
                continue;
            }

            if (!result.FrontEndClean)
            {
                if (fixture.ExpectedFile != null)
                    uncleanRunFixtures.Add(fixture.TestName);
                continue;
            }

            examined++;
            nodes += Check(fixture.TestName, result, misses);
        }

        _output.WriteLine($"examined {examined} clean fixture(s); required nodes checked {nodes}");

        Assert.True(internalErrors.Count == 0,
            $"{internalErrors.Count} fixture(s) hit an internal compiler error:\n"
            + string.Join("\n", internalErrors.Take(25)));
        // Non-vacuity, derived from the corpus: every fixture that runs (has an .expected) compiled
        // clean here, so all of them were examined.
        Assert.True(uncleanRunFixtures.Count == 0,
            $"{uncleanRunFixtures.Count} running fixture(s) did not compile clean in the capture pipeline, so the "
            + "sweep did not examine them:\n" + string.Join("\n", uncleanRunFixtures.Take(25)));
        Assert.True(misses.Count == 0,
            $"{misses.Count} required-shape node(s) have no IR of their kind:\n" + string.Join("\n", misses.Take(50)));
    }

    /// <summary>
    /// Every required-shape node reachable from each captured module has an index entry of its kind
    /// (or a wrapped element whose single child is of it, as <c>IrNodeFor</c> reads it). Returns the
    /// number of nodes checked.
    /// </summary>
    private static int Check(string name, LoweredCompilationCapture.Result result, List<string> misses)
    {
        var count = 0;
        foreach (var (context, module) in result.Units)
        {
            var index = context.Ir.Index;
            foreach (var node in ReachableNodes(module))
            {
                if (RequiredIrKind(node) is not { } kind)
                    continue;

                count++;
                var ir = index.TryGetValue(node, out var found) ? found : null;
                if (ir is IrOptionalWrappedElement { Children: [var child] })
                    ir = child;
                if (ir is null || !kind.IsInstanceOfType(ir))
                {
                    misses.Add($"{name}: {node.GetType().Name} @ {node.LineStart}:{node.ColumnStart} -> "
                        + (ir?.GetType().Name ?? "no index entry") + $", expected {kind.Name}");
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Every <see cref="Node"/> reachable from <paramref name="root"/> through ANY public instance
    /// property: nodes, arrays and the AST's non-node records (decorators, keyword arguments,
    /// parameters). Reference-deduplicated.
    /// </summary>
    internal static IEnumerable<Node> ReachableNodes(Node root)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<object>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!seen.Add(current))
                continue;
            if (current is Node node)
                yield return node;

            foreach (var property in AstProperties(current.GetType()))
            {
                var value = property.GetValue(current);
                switch (value)
                {
                    case null or string:
                        break;
                    case IEnumerable items:
                        foreach (var item in items)
                        {
                            if (item != null && IsAstType(item.GetType()))
                                stack.Push(item);
                        }

                        break;
                    default:
                        if (IsAstType(value.GetType()))
                            stack.Push(value);
                        break;
                }
            }
        }
    }

    private static readonly Dictionary<Type, PropertyInfo[]> PropertyCache = new();

    private static PropertyInfo[] AstProperties(Type type)
    {
        lock (PropertyCache)
        {
            if (!PropertyCache.TryGetValue(type, out var properties))
            {
                properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetIndexParameters().Length == 0 && p.Name != "EqualityContract")
                    .Where(p => !p.PropertyType.IsPrimitive && !p.PropertyType.IsEnum)
                    .ToArray();
                PropertyCache[type] = properties;
            }

            return properties;
        }
    }

    private static bool IsAstType(Type type)
        => type.IsClass && type.Namespace == typeof(Node).Namespace;
}
