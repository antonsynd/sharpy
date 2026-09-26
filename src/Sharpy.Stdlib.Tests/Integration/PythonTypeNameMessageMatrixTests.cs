using Sharpy.Compiler.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Integration;

/// <summary>
/// Every runtime message names the python type (#2035, Decision 26), and a SPY0453-named class is
/// named by its source spelling on the runtime AND the static column (#2030). Each cell is executed
/// and its stdout compared byte-for-byte with python3 3.12's output for the same program (the
/// struct cell is a class in python, <c>Deque</c> is <c>collections.deque</c>). The one recorded
/// difference: python spells the deque type <c>deque</c> / <c>collections.deque</c>, Sharpy its
/// surface name <c>Deque</c> / <c>collections.Deque</c> (Decision 26).
///
/// <para>Axes: site {exception repr, <c>str.format</c> attribute error, comparison refusal (sort),
/// single-element sort (python never compares), <c>json.dumps</c> refusal, dynamic-spec format
/// refusal, <c>type(x).__name__</c>, <c>str</c>/<c>repr</c>/<c>print</c> of <c>type(x)</c>,
/// <c>deque</c> indexing} × type {builtin, PascalCase class, snake class, nested snake class, snake
/// struct, snake exception, C-implemented Stdlib type}. A C-implemented python type is named by its
/// module-qualified tp_name in a C-level message (<c>datetime.timedelta.__format__</c>) and by its
/// simple <c>__name__</c> in <c>type(x).__name__</c> and json's message (<c>timedelta</c>) — the
/// <c>[SharpyModuleType]</c> <c>MessageName</c> vs <c>PythonName</c>. Before the fix every non-builtin cell printed the CLR name
/// (<c>MyThing</c>, <c>MyErr('boom')</c>, <c>'String' object</c>, <c>Int32</c>, <c>List`1</c>,
/// <c>Prog+A</c>), <c>sorted</c> raised an uncatchable TypeInitializationException (#2085), and deque
/// indexing was SPY0320.</para>
/// </summary>
public class PythonTypeNameMessageMatrixTests : StdlibIntegrationTestBase
{
    public PythonTypeNameMessageMatrixTests(ITestOutputHelper output) : base(output) { }

    private const string Prelude = """
        import datetime
        import json
        from collections import Deque, OrderedDict

        class A:
            pass

        class my_thing:
            pass

        class Outer:
            class in_ner:
                pass

        struct my_pt:
            x: int

            def __init__(self, x: int) -> None:
                self.x = x

        class my_err(Exception):
            pass

        def main() -> None:

        """;

    /// <summary>(label, main-body lines, python3 stdout).</summary>
    public static TheoryData<string, string, string> RuntimeCells => new()
    {
        { "repr.builtin_exception", "print(repr(ValueError(\"boom\")))", "ValueError('boom')" },
        { "repr.snake_exception", "print(repr(my_err(\"boom\")))", "my_err('boom')" },
        { "format.attribute_error", "try:\n    print(\"{0.real}\".format(\"ab\"))\nexcept AttributeError as e:\n    print(e)", "'str' object has no attribute 'real'" },
        { "sort.refused", "try:\n    print(sorted([A(), A()]))\nexcept TypeError as e:\n    print(e)", "'<' not supported between instances of 'A' and 'A'" },
        { "sort.refused_snake", "try:\n    print(sorted([my_thing(), my_thing()]))\nexcept TypeError as e:\n    print(e)", "'<' not supported between instances of 'my_thing' and 'my_thing'" },
        { "sort.single_never_compares", "print(len(sorted([A()])))", "1" },
        { "json.class", "try:\n    print(json.dumps(A()))\nexcept TypeError as e:\n    print(e)", "Object of type A is not JSON serializable" },
        { "json.snake", "try:\n    print(json.dumps(my_thing()))\nexcept TypeError as e:\n    print(e)", "Object of type my_thing is not JSON serializable" },
        { "json.nested_snake", "try:\n    print(json.dumps(Outer.in_ner()))\nexcept TypeError as e:\n    print(e)", "Object of type in_ner is not JSON serializable" },
        { "json.snake_struct", "try:\n    print(json.dumps(my_pt(1)))\nexcept TypeError as e:\n    print(e)", "Object of type my_pt is not JSON serializable" },
        { "format.dynamic_snake", "sp: str = \"x\"\ntry:\n    print(format(my_thing(), sp))\nexcept TypeError as e:\n    print(e)", "unsupported format string passed to my_thing.__format__" },
        { "format.dynamic_nested_snake", "sp: str = \"x\"\ntry:\n    print(format(Outer.in_ner(), sp))\nexcept TypeError as e:\n    print(e)", "unsupported format string passed to in_ner.__format__" },
        { "format.dynamic_snake_struct", "sp: str = \"x\"\ntry:\n    print(format(my_pt(1), sp))\nexcept TypeError as e:\n    print(e)", "unsupported format string passed to my_pt.__format__" },
        { "type.dunder_name", "print(type(5).__name__)\nprint(type(\"a\").__name__)\nprint(type([1]).__name__)\nprint(type(A()).__name__)\nprint(type(my_thing()).__name__)\nprint(type(Outer.in_ner()).__name__)\nprint(type(my_pt(1)).__name__)", "int\nstr\nlist\nA\nmy_thing\nin_ner\nmy_pt" },
        { "type.class_repr", "print(str(type(5)))\nprint(type(\"a\"))\nprint(repr(type([1])))\nprint(str(type(A())))\nprint(type(my_thing()))\nprint(repr(type(Outer.in_ner())))\nprint(type(my_pt(1)))", "<class 'int'>\n<class 'str'>\n<class 'list'>\n<class '__main__.A'>\n<class '__main__.my_thing'>\n<class '__main__.Outer.in_ner'>\n<class '__main__.my_pt'>" },
        { "deque.index", "d = Deque[int]([1, 2, 3])\nprint(d[-1])\nprint(d[0])\nd[1] = 20\nprint(d)\ntry:\n    print(d[3])\nexcept IndexError as e:\n    print(e)", "3\n1\ndeque([1, 20, 3])\ndeque index out of range" },
        // C-implemented Stdlib types: tp_name in C-level messages, __name__ elsewhere. python spells
        // the deque `collections.deque` / `deque` (Decision 26: Sharpy's surface name is Deque).
        { "format.dynamic_timedelta", "sp: str = \"x\"\ntry:\n    print(format(datetime.timedelta(days=1), sp))\nexcept TypeError as e:\n    print(e)", "unsupported format string passed to datetime.timedelta.__format__" },
        { "format.dynamic_deque", "sp: str = \"x\"\ntry:\n    print(format(Deque[int]([1, 2]), sp))\nexcept TypeError as e:\n    print(e)", "unsupported format string passed to collections.Deque.__format__" },
        { "format.dynamic_ordered_dict", "sp: str = \"x\"\ntry:\n    print(format(OrderedDict[str, int](), sp))\nexcept TypeError as e:\n    print(e)", "unsupported format string passed to collections.OrderedDict.__format__" },
        { "format.attribute_error_timedelta", "try:\n    print(\"{0.zz}\".format(datetime.timedelta(days=1)))\nexcept AttributeError as e:\n    print(e)", "'datetime.timedelta' object has no attribute 'zz'" },
        { "json.timedelta", "try:\n    print(json.dumps(datetime.timedelta(days=1)))\nexcept TypeError as e:\n    print(e)", "Object of type timedelta is not JSON serializable" },
        { "type.dunder_name_stdlib", "print(type(datetime.timedelta(days=1)).__name__)\nprint(type(Deque[int]([1, 2])).__name__)\nprint(type(OrderedDict[str, int]()).__name__)", "timedelta\nDeque\nOrderedDict" },
        { "type.class_repr_stdlib", "print(str(type(datetime.timedelta(days=1))))\nprint(type(Deque[int]([1, 2])))\nprint(repr(type(OrderedDict[str, int]())))", "<class 'datetime.timedelta'>\n<class 'collections.Deque'>\n<class 'collections.OrderedDict'>" },
    };

    [Theory]
    [MemberData(nameof(RuntimeCells))]
    [Trait("Category", "Conformance")]
    public void Runtime_NamesThePythonType(string label, string body, string python)
    {
        var program = Prelude + Indent(body) + "\n";
        var result = CompileAndExecute(program, executionTimeoutMs: 15_000);
        Assert.True(result.Success, $"{label}: " + string.Join("; ", result.CompilationErrors) + result.StandardError);
        Assert.Equal(python, result.StandardOutput.Replace("\r\n", "\n").TrimEnd('\n'));
    }

    /// <summary>
    /// The static column of #2030: a literal spec the operand's type cannot take is SPY0609 naming
    /// the type by its SOURCE spelling — the same name the runtime column prints.
    /// </summary>
    [Theory]
    [InlineData("static.snake", "print(format(my_thing(), \"x\"))", "unsupported format string passed to my_thing.__format__")]
    [InlineData("static.nested_snake", "print(format(Outer.in_ner(), \"x\"))", "unsupported format string passed to in_ner.__format__")]
    [InlineData("static.snake_struct", "print(format(my_pt(1), \"x\"))", "unsupported format string passed to my_pt.__format__")]
    [Trait("Category", "Conformance")]
    public void Static_NamesTheSourceSpelling(string label, string body, string message)
    {
        var result = CompileAndExecute(Prelude + Indent(body) + "\n", executionTimeoutMs: 15_000);
        Assert.False(result.Success, $"{label}: expected SPY0609");
        Assert.Contains(result.RawDiagnostics, d => d.Code == DiagnosticCodes.SemanticOverflow.InvalidFormatSpecification && d.Message == message);
    }

    /// <summary>
    /// <c>__name__</c> belongs to a class object, not an instance: python raises
    /// <c>AttributeError: 'PosixPath' object has no attribute '__name__'</c>. A name-keyed alias to
    /// the CLR <c>Name</c> member used to read <c>Path("a/b.txt").__name__</c> as <c>b.txt</c>; it is
    /// refused (SPY0203). Positive control: <c>type(p).__name__</c> on the same receiver runs.
    /// </summary>
    [Fact]
    [Trait("Category", "Conformance")]
    public void DunderName_OnAnInstance_IsRefused_OnItsClassObject_Runs()
    {
        const string head = "from pathlib import Path\n\ndef main() -> None:\n    p = Path(\"a/b.txt\")\n";

        var refused = CompileAndExecute(head + "    print(p.__name__)\n", executionTimeoutMs: 15_000);
        Assert.False(refused.Success, "p.__name__ must not compile");
        Assert.Contains(refused.RawDiagnostics, d => d.Code == DiagnosticCodes.Semantic.UndefinedMember && d.Message.Contains("__name__"));

        var control = CompileAndExecute(head + "    print(type(p).__name__)\n", executionTimeoutMs: 15_000);
        Assert.True(control.Success, string.Join("; ", control.CompilationErrors) + control.StandardError);
        Assert.Equal("Path", control.StandardOutput.Replace("\r\n", "\n").TrimEnd('\n'));
    }

    private static string Indent(string body) =>
        string.Join("\n", body.Split('\n').Select(line => "    " + line));
}
