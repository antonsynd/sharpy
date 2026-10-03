using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// A comprehension's for-target binds identically in every comprehension form (#2181): list, set and
/// dict comprehensions and generator expressions. Before #2181 the generator form named a temp lambda
/// parameter for any non-name target and bound nothing, so every read of a tuple-target name was
/// SPY0908 CS0103; and a comprehension nested in a generator's element was hoisted OUT of the lambda,
/// away from the target it reads (CS0103 even for a plain-name target).
/// </summary>
/// <remarks>
/// <para>Axes: form {list, set, dict, generator} × target shape {name, tuple, parenthesized tuple, nested
/// tuple, starred} × use {call argument, bound to a name, nested in an outer comprehension, an inner
/// comprehension in the element reading the target}. Every cell is EXECUTED and compared with python3's
/// output for the same program; by construction the value depends only on (shape, use), so the
/// expectation table is keyed on those two axes and every form must print the same thing — the
/// contract itself. Results are passed through <c>sorted()</c> so set/dict ordering is not an axis.</para>
/// </remarks>
public class ComprehensionTargetBindingMatrixTests : IntegrationTestBase
{
    public ComprehensionTargetBindingMatrixTests(ITestOutputHelper output) : base(output)
    {
    }

    public enum Form { List, Set, Dict, Generator }

    public enum Shape { Name, Tuple, ParenthesizedTuple, NestedTuple, Starred }

    public enum Use { CallArgument, BoundToName, NestedComprehension, InnerComprehension }

    /// <summary>Source binding, target spelling, element over the target, and the target's first name.</summary>
    private static (string Source, string Target, string Element, string First) Spelling(Shape shape) => shape switch
    {
        Shape.Name => ("s = [(1, 2), (3, 4)]", "p", "p[0] + p[1]", "p[0]"),
        Shape.Tuple => ("s = [(1, 2), (3, 4)]", "a, b", "a + b", "a"),
        Shape.ParenthesizedTuple => ("s = [(1, 2), (3, 4)]", "(a, b)", "a + b", "a"),
        Shape.NestedTuple => ("s = [((1, 2), 3), ((3, 4), 5)]", "(a, b), c", "a + b + c", "a"),
        Shape.Starred => ("s = [[1, 2, 3], [4, 5]]", "a, *rest", "a + len(rest)", "a"),
        _ => throw new ArgumentOutOfRangeException(nameof(shape)),
    };

    private static string Comprehension(Form form, string element, string target) => form switch
    {
        Form.List => $"[{element} for {target} in s]",
        Form.Set => $"{{{element} for {target} in s}}",
        Form.Dict => $"{{{element}: {element} for {target} in s}}",
        Form.Generator => $"({element} for {target} in s)",
        _ => throw new ArgumentOutOfRangeException(nameof(form)),
    };

    private static string Program(Form form, Shape shape, Use use)
    {
        var (source, target, element, first) = Spelling(shape);
        var body = use switch
        {
            Use.CallArgument => $"    print(sorted({Comprehension(form, element, target)}))",
            Use.BoundToName => $"    r = {Comprehension(form, element, target)}\n    print(sorted(r))",
            Use.NestedComprehension =>
                $"    print([sorted({Comprehension(form, $"({element}) * k", target)}) for k in range(1, 3)])",
            Use.InnerComprehension =>
                $"    print(sorted({Comprehension(form, $"sum([x for x in range({first})])", target)}))",
            _ => throw new ArgumentOutOfRangeException(nameof(use)),
        };
        return $"def main() -> None:\n    {source}\n{body}\n";
    }

    /// <summary>python3's output for the same program (identical for every form), hand-recorded.</summary>
    private static readonly Dictionary<(Shape, Use), string> Expected = new()
    {
        [(Shape.Name, Use.CallArgument)] = "[3, 7]",
        [(Shape.Name, Use.BoundToName)] = "[3, 7]",
        [(Shape.Name, Use.NestedComprehension)] = "[[3, 7], [6, 14]]",
        [(Shape.Name, Use.InnerComprehension)] = "[0, 3]",
        [(Shape.Tuple, Use.CallArgument)] = "[3, 7]",
        [(Shape.Tuple, Use.BoundToName)] = "[3, 7]",
        [(Shape.Tuple, Use.NestedComprehension)] = "[[3, 7], [6, 14]]",
        [(Shape.Tuple, Use.InnerComprehension)] = "[0, 3]",
        [(Shape.ParenthesizedTuple, Use.CallArgument)] = "[3, 7]",
        [(Shape.ParenthesizedTuple, Use.BoundToName)] = "[3, 7]",
        [(Shape.ParenthesizedTuple, Use.NestedComprehension)] = "[[3, 7], [6, 14]]",
        [(Shape.ParenthesizedTuple, Use.InnerComprehension)] = "[0, 3]",
        [(Shape.NestedTuple, Use.CallArgument)] = "[6, 12]",
        [(Shape.NestedTuple, Use.BoundToName)] = "[6, 12]",
        [(Shape.NestedTuple, Use.NestedComprehension)] = "[[6, 12], [12, 24]]",
        [(Shape.NestedTuple, Use.InnerComprehension)] = "[0, 3]",
        [(Shape.Starred, Use.CallArgument)] = "[3, 5]",
        [(Shape.Starred, Use.BoundToName)] = "[3, 5]",
        [(Shape.Starred, Use.NestedComprehension)] = "[[3, 5], [6, 10]]",
        [(Shape.Starred, Use.InnerComprehension)] = "[0, 6]",
    };

    public static TheoryData<Form, Shape, Use> Cells()
    {
        var data = new TheoryData<Form, Shape, Use>();
        foreach (var form in Enum.GetValues<Form>())
            foreach (var shape in Enum.GetValues<Shape>())
                foreach (var use in Enum.GetValues<Use>())
                    data.Add(form, shape, use);
        return data;
    }

    [Fact]
    public void Matrix_CoversEveryCell()
    {
        // Anchored to literals: 4 forms × 5 shapes × 4 uses, 5 × 4 python3 expectations.
        Cells().Count().Should().Be(80);
        Expected.Count.Should().Be(20);
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void Cell_BindsTheTarget_AndPrintsPythonsOutput(Form form, Shape shape, Use use)
    {
        var result = CompileAndExecute(Program(form, shape, use));

        result.Success.Should().BeTrue($"{string.Join("; ", result.CompilationErrors)} stderr: {result.StandardError}");
        result.StandardOutput.Trim().Should().Be(Expected[(shape, use)]);
    }

    /// <summary>The issue's own cells: a generator over a tuple target, every clause position.</summary>
    [Theory]
    [InlineData("print(sum(a for a, b in pairs))", "4")]
    [InlineData("print(list(b for a, b in pairs))", "[2, 4]")]
    [InlineData("print(list(a for a, b in pairs if b > 2))", "[3]")]
    [InlineData("print(list(a + c for a, b in pairs for c in range(b)))", "[1, 2, 3, 4, 5, 6]")]
    [InlineData("print(list(x for x in (a for a, b in pairs)))", "[1, 3]")]
    // A comprehension hoisted out of a condition / an inner for-clause's iterator reads the target too.
    [InlineData("print(list(a for a, b in pairs if len([x for x in range(b)]) > 2))", "[3]")]
    [InlineData("print(list(c for a, b in pairs for c in [y for y in range(b)]))", "[0, 1, 0, 1, 2, 3]")]
    public void GeneratorTupleTarget_EveryClausePosition(string statement, string expected)
    {
        var result = CompileAndExecute($"def main() -> None:\n    pairs = [(1, 2), (3, 4)]\n    {statement}\n");

        result.Success.Should().BeTrue($"{string.Join("; ", result.CompilationErrors)} stderr: {result.StandardError}");
        result.StandardOutput.Trim().Should().Be(expected);
    }
}
