using FluentAssertions;
using Sharpy.Compiler.Tests.Helpers;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// #2006 (R-CF): the python-name channel. An instance of a Sharpy type that does not render itself
/// prints python's default <c>&lt;mod.Outer.C object&gt;</c> (the address omitted) on every
/// str/repr site — <c>mod</c> read off the <c>[SharpyModule]</c> of its module class
/// (<c>__main__</c> for the unstamped entry module), each qualname segment through
/// <c>[SharpyName]</c>, which the compiler stamps only when the emitted CLR name differs from the
/// source spelling (<c>my_thing</c> → <c>MyThing</c>). Before the fix every cell printed the CLR
/// name (<c>Inst+D</c>, <c>SharpyApp.Pkg+Shapes+Box</c>).
/// </summary>
/// <remarks>
/// <para>Cells: type kind ×13 (plain, nested, snake-named, backtick-escaped, generic, struct,
/// <c>__str__</c>-only, <c>__repr__</c>-only, dataclass, nested snake, subclass, interface
/// implementation, three-deep nesting) × site ×14 (print, str, repr, f"{x}", f"{x!r}", f"{x!s}",
/// format(x, ""), "{}".format, "{!r}".format, [x], (x, 1), {"k": x}, [[x]], str([x])) × module ×3
/// (the entry file, an imported module <c>shapes</c>, a package module <c>pkg.shapes</c>), plus
/// module ×4: a class merged with its module (<c>thing.spy</c>'s <c>Thing</c> and a type nested in
/// it) × the 14 sites.</para>
/// <para>Every expected line is python3 3.12's output for the same program with the address
/// stripped, recorded by the generator into the literals below. ONE recorded deviation: a class
/// declaring <c>__str__</c> only repr's as its str in Sharpy (both dunders are
/// <c>ToString</c>) — python prints <c>&lt;mod.S object&gt;</c> for those repr sites; the
/// deviation is <c>docs/deviations.yaml</c> <c>str-only-class-repr-is-str</c>.</para>
/// </remarks>
[Collection("HeavyCompilation")]
public class PythonNameChannelMatrixTests : IntegrationTestBase
{
    public PythonNameChannelMatrixTests(ITestOutputHelper output) : base(output) { }

    private static readonly (string Kind, string Declaration, string Construction)[] Kinds =
    {
        ("plain", "class D:\n    pass\n", "D()"),
        ("nested", "class Outer:\n    class C:\n        pass\n", "Outer.C()"),
        ("snake", "class my_thing:\n    pass\n", "my_thing()"),
        ("escaped", "class `esc_thing`:\n    pass\n", "`esc_thing`()"),
        ("generic", "class Box[T]:\n    v: T\n\n    def __init__(self, v: T) -> None:\n        self.v = v\n", "Box[int](1)"),
        ("struct", "struct Pt:\n    x: int\n\n    def __init__(self, x: int) -> None:\n        self.x = x\n", "Pt(1)"),
        ("str_only", "class S:\n    def __str__(self) -> str:\n        return \"S-str\"\n", "S()"),
        ("repr_only", "class R:\n    def __repr__(self) -> str:\n        return \"R-repr\"\n", "R()"),
        ("dataclass", "@dataclass\nclass P:\n    n: int\n", "P(1)"),
        ("nested_snake", "class Outer2:\n    class inner_cls:\n        pass\n", "Outer2.inner_cls()"),
        ("subclass", "class Base:\n    pass\n\nclass Sub(Base):\n    pass\n", "Sub()"),
        ("interface_impl", "interface IShape:\n    def area(self) -> int: ...\n\nclass Sq(IShape):\n    def area(self) -> int:\n        return 4\n", "Sq()"),
        ("deep_nested", "class A1:\n    class B1:\n        class C1:\n            pass\n", "A1.B1.C1()"),
    };

    private static readonly (string Site, string Statement)[] Sites =
    {
        ("print", "print(x)"),
        ("str", "print(str(x))"),
        ("repr", "print(repr(x))"),
        ("fstring", "print(f\"{x}\")"),
        ("fstring_r", "print(f\"{x!r}\")"),
        ("fstring_s", "print(f\"{x!s}\")"),
        ("format_empty", "print(format(x, \"\"))"),
        ("str_format", "print(\"{}\".format(x))"),
        ("str_format_r", "print(\"{!r}\".format(x))"),
        ("list", "print([x])"),
        ("tuple", "print((x, 1))"),
        ("dict", "print({\"k\": x})"),
        ("nested_list", "print([[x]])"),
        ("str_of_list", "print(str([x]))"),
    };

    private static readonly (string Kind, string Construction)[] MergedKinds =
    {
        ("merged", "Thing()"),
        ("merged_nested", "Thing.Inner()"),
    };

    // python3 3.12, entry layout, address stripped (generator: scratchpad oracle/gen.py).
    private static readonly string[] PythonEntry =
    {
        "<__main__.D object>",
        "<__main__.D object>",
        "<__main__.D object>",
        "<__main__.D object>",
        "<__main__.D object>",
        "<__main__.D object>",
        "<__main__.D object>",
        "<__main__.D object>",
        "<__main__.D object>",
        "[<__main__.D object>]",
        "(<__main__.D object>, 1)",
        "{'k': <__main__.D object>}",
        "[[<__main__.D object>]]",
        "[<__main__.D object>]",
        "<__main__.Outer.C object>",
        "<__main__.Outer.C object>",
        "<__main__.Outer.C object>",
        "<__main__.Outer.C object>",
        "<__main__.Outer.C object>",
        "<__main__.Outer.C object>",
        "<__main__.Outer.C object>",
        "<__main__.Outer.C object>",
        "<__main__.Outer.C object>",
        "[<__main__.Outer.C object>]",
        "(<__main__.Outer.C object>, 1)",
        "{'k': <__main__.Outer.C object>}",
        "[[<__main__.Outer.C object>]]",
        "[<__main__.Outer.C object>]",
        "<__main__.my_thing object>",
        "<__main__.my_thing object>",
        "<__main__.my_thing object>",
        "<__main__.my_thing object>",
        "<__main__.my_thing object>",
        "<__main__.my_thing object>",
        "<__main__.my_thing object>",
        "<__main__.my_thing object>",
        "<__main__.my_thing object>",
        "[<__main__.my_thing object>]",
        "(<__main__.my_thing object>, 1)",
        "{'k': <__main__.my_thing object>}",
        "[[<__main__.my_thing object>]]",
        "[<__main__.my_thing object>]",
        "<__main__.esc_thing object>",
        "<__main__.esc_thing object>",
        "<__main__.esc_thing object>",
        "<__main__.esc_thing object>",
        "<__main__.esc_thing object>",
        "<__main__.esc_thing object>",
        "<__main__.esc_thing object>",
        "<__main__.esc_thing object>",
        "<__main__.esc_thing object>",
        "[<__main__.esc_thing object>]",
        "(<__main__.esc_thing object>, 1)",
        "{'k': <__main__.esc_thing object>}",
        "[[<__main__.esc_thing object>]]",
        "[<__main__.esc_thing object>]",
        "<__main__.Box object>",
        "<__main__.Box object>",
        "<__main__.Box object>",
        "<__main__.Box object>",
        "<__main__.Box object>",
        "<__main__.Box object>",
        "<__main__.Box object>",
        "<__main__.Box object>",
        "<__main__.Box object>",
        "[<__main__.Box object>]",
        "(<__main__.Box object>, 1)",
        "{'k': <__main__.Box object>}",
        "[[<__main__.Box object>]]",
        "[<__main__.Box object>]",
        "<__main__.Pt object>",
        "<__main__.Pt object>",
        "<__main__.Pt object>",
        "<__main__.Pt object>",
        "<__main__.Pt object>",
        "<__main__.Pt object>",
        "<__main__.Pt object>",
        "<__main__.Pt object>",
        "<__main__.Pt object>",
        "[<__main__.Pt object>]",
        "(<__main__.Pt object>, 1)",
        "{'k': <__main__.Pt object>}",
        "[[<__main__.Pt object>]]",
        "[<__main__.Pt object>]",
        "S-str",
        "S-str",
        "<__main__.S object>",
        "S-str",
        "<__main__.S object>",
        "S-str",
        "S-str",
        "S-str",
        "<__main__.S object>",
        "[<__main__.S object>]",
        "(<__main__.S object>, 1)",
        "{'k': <__main__.S object>}",
        "[[<__main__.S object>]]",
        "[<__main__.S object>]",
        "R-repr",
        "R-repr",
        "R-repr",
        "R-repr",
        "R-repr",
        "R-repr",
        "R-repr",
        "R-repr",
        "R-repr",
        "[R-repr]",
        "(R-repr, 1)",
        "{'k': R-repr}",
        "[[R-repr]]",
        "[R-repr]",
        "P(n=1)",
        "P(n=1)",
        "P(n=1)",
        "P(n=1)",
        "P(n=1)",
        "P(n=1)",
        "P(n=1)",
        "P(n=1)",
        "P(n=1)",
        "[P(n=1)]",
        "(P(n=1), 1)",
        "{'k': P(n=1)}",
        "[[P(n=1)]]",
        "[P(n=1)]",
        "<__main__.Outer2.inner_cls object>",
        "<__main__.Outer2.inner_cls object>",
        "<__main__.Outer2.inner_cls object>",
        "<__main__.Outer2.inner_cls object>",
        "<__main__.Outer2.inner_cls object>",
        "<__main__.Outer2.inner_cls object>",
        "<__main__.Outer2.inner_cls object>",
        "<__main__.Outer2.inner_cls object>",
        "<__main__.Outer2.inner_cls object>",
        "[<__main__.Outer2.inner_cls object>]",
        "(<__main__.Outer2.inner_cls object>, 1)",
        "{'k': <__main__.Outer2.inner_cls object>}",
        "[[<__main__.Outer2.inner_cls object>]]",
        "[<__main__.Outer2.inner_cls object>]",
        "<__main__.Sub object>",
        "<__main__.Sub object>",
        "<__main__.Sub object>",
        "<__main__.Sub object>",
        "<__main__.Sub object>",
        "<__main__.Sub object>",
        "<__main__.Sub object>",
        "<__main__.Sub object>",
        "<__main__.Sub object>",
        "[<__main__.Sub object>]",
        "(<__main__.Sub object>, 1)",
        "{'k': <__main__.Sub object>}",
        "[[<__main__.Sub object>]]",
        "[<__main__.Sub object>]",
        "<__main__.Sq object>",
        "<__main__.Sq object>",
        "<__main__.Sq object>",
        "<__main__.Sq object>",
        "<__main__.Sq object>",
        "<__main__.Sq object>",
        "<__main__.Sq object>",
        "<__main__.Sq object>",
        "<__main__.Sq object>",
        "[<__main__.Sq object>]",
        "(<__main__.Sq object>, 1)",
        "{'k': <__main__.Sq object>}",
        "[[<__main__.Sq object>]]",
        "[<__main__.Sq object>]",
        "<__main__.A1.B1.C1 object>",
        "<__main__.A1.B1.C1 object>",
        "<__main__.A1.B1.C1 object>",
        "<__main__.A1.B1.C1 object>",
        "<__main__.A1.B1.C1 object>",
        "<__main__.A1.B1.C1 object>",
        "<__main__.A1.B1.C1 object>",
        "<__main__.A1.B1.C1 object>",
        "<__main__.A1.B1.C1 object>",
        "[<__main__.A1.B1.C1 object>]",
        "(<__main__.A1.B1.C1 object>, 1)",
        "{'k': <__main__.A1.B1.C1 object>}",
        "[[<__main__.A1.B1.C1 object>]]",
        "[<__main__.A1.B1.C1 object>]",
    };

    // python3 3.12, module layout, address stripped (generator: scratchpad oracle/gen.py).
    private static readonly string[] PythonModule =
    {
        "<shapes.D object>",
        "<shapes.D object>",
        "<shapes.D object>",
        "<shapes.D object>",
        "<shapes.D object>",
        "<shapes.D object>",
        "<shapes.D object>",
        "<shapes.D object>",
        "<shapes.D object>",
        "[<shapes.D object>]",
        "(<shapes.D object>, 1)",
        "{'k': <shapes.D object>}",
        "[[<shapes.D object>]]",
        "[<shapes.D object>]",
        "<shapes.Outer.C object>",
        "<shapes.Outer.C object>",
        "<shapes.Outer.C object>",
        "<shapes.Outer.C object>",
        "<shapes.Outer.C object>",
        "<shapes.Outer.C object>",
        "<shapes.Outer.C object>",
        "<shapes.Outer.C object>",
        "<shapes.Outer.C object>",
        "[<shapes.Outer.C object>]",
        "(<shapes.Outer.C object>, 1)",
        "{'k': <shapes.Outer.C object>}",
        "[[<shapes.Outer.C object>]]",
        "[<shapes.Outer.C object>]",
        "<shapes.my_thing object>",
        "<shapes.my_thing object>",
        "<shapes.my_thing object>",
        "<shapes.my_thing object>",
        "<shapes.my_thing object>",
        "<shapes.my_thing object>",
        "<shapes.my_thing object>",
        "<shapes.my_thing object>",
        "<shapes.my_thing object>",
        "[<shapes.my_thing object>]",
        "(<shapes.my_thing object>, 1)",
        "{'k': <shapes.my_thing object>}",
        "[[<shapes.my_thing object>]]",
        "[<shapes.my_thing object>]",
        "<shapes.esc_thing object>",
        "<shapes.esc_thing object>",
        "<shapes.esc_thing object>",
        "<shapes.esc_thing object>",
        "<shapes.esc_thing object>",
        "<shapes.esc_thing object>",
        "<shapes.esc_thing object>",
        "<shapes.esc_thing object>",
        "<shapes.esc_thing object>",
        "[<shapes.esc_thing object>]",
        "(<shapes.esc_thing object>, 1)",
        "{'k': <shapes.esc_thing object>}",
        "[[<shapes.esc_thing object>]]",
        "[<shapes.esc_thing object>]",
        "<shapes.Box object>",
        "<shapes.Box object>",
        "<shapes.Box object>",
        "<shapes.Box object>",
        "<shapes.Box object>",
        "<shapes.Box object>",
        "<shapes.Box object>",
        "<shapes.Box object>",
        "<shapes.Box object>",
        "[<shapes.Box object>]",
        "(<shapes.Box object>, 1)",
        "{'k': <shapes.Box object>}",
        "[[<shapes.Box object>]]",
        "[<shapes.Box object>]",
        "<shapes.Pt object>",
        "<shapes.Pt object>",
        "<shapes.Pt object>",
        "<shapes.Pt object>",
        "<shapes.Pt object>",
        "<shapes.Pt object>",
        "<shapes.Pt object>",
        "<shapes.Pt object>",
        "<shapes.Pt object>",
        "[<shapes.Pt object>]",
        "(<shapes.Pt object>, 1)",
        "{'k': <shapes.Pt object>}",
        "[[<shapes.Pt object>]]",
        "[<shapes.Pt object>]",
        "S-str",
        "S-str",
        "<shapes.S object>",
        "S-str",
        "<shapes.S object>",
        "S-str",
        "S-str",
        "S-str",
        "<shapes.S object>",
        "[<shapes.S object>]",
        "(<shapes.S object>, 1)",
        "{'k': <shapes.S object>}",
        "[[<shapes.S object>]]",
        "[<shapes.S object>]",
        "R-repr",
        "R-repr",
        "R-repr",
        "R-repr",
        "R-repr",
        "R-repr",
        "R-repr",
        "R-repr",
        "R-repr",
        "[R-repr]",
        "(R-repr, 1)",
        "{'k': R-repr}",
        "[[R-repr]]",
        "[R-repr]",
        "P(n=1)",
        "P(n=1)",
        "P(n=1)",
        "P(n=1)",
        "P(n=1)",
        "P(n=1)",
        "P(n=1)",
        "P(n=1)",
        "P(n=1)",
        "[P(n=1)]",
        "(P(n=1), 1)",
        "{'k': P(n=1)}",
        "[[P(n=1)]]",
        "[P(n=1)]",
        "<shapes.Outer2.inner_cls object>",
        "<shapes.Outer2.inner_cls object>",
        "<shapes.Outer2.inner_cls object>",
        "<shapes.Outer2.inner_cls object>",
        "<shapes.Outer2.inner_cls object>",
        "<shapes.Outer2.inner_cls object>",
        "<shapes.Outer2.inner_cls object>",
        "<shapes.Outer2.inner_cls object>",
        "<shapes.Outer2.inner_cls object>",
        "[<shapes.Outer2.inner_cls object>]",
        "(<shapes.Outer2.inner_cls object>, 1)",
        "{'k': <shapes.Outer2.inner_cls object>}",
        "[[<shapes.Outer2.inner_cls object>]]",
        "[<shapes.Outer2.inner_cls object>]",
        "<shapes.Sub object>",
        "<shapes.Sub object>",
        "<shapes.Sub object>",
        "<shapes.Sub object>",
        "<shapes.Sub object>",
        "<shapes.Sub object>",
        "<shapes.Sub object>",
        "<shapes.Sub object>",
        "<shapes.Sub object>",
        "[<shapes.Sub object>]",
        "(<shapes.Sub object>, 1)",
        "{'k': <shapes.Sub object>}",
        "[[<shapes.Sub object>]]",
        "[<shapes.Sub object>]",
        "<shapes.Sq object>",
        "<shapes.Sq object>",
        "<shapes.Sq object>",
        "<shapes.Sq object>",
        "<shapes.Sq object>",
        "<shapes.Sq object>",
        "<shapes.Sq object>",
        "<shapes.Sq object>",
        "<shapes.Sq object>",
        "[<shapes.Sq object>]",
        "(<shapes.Sq object>, 1)",
        "{'k': <shapes.Sq object>}",
        "[[<shapes.Sq object>]]",
        "[<shapes.Sq object>]",
        "<shapes.A1.B1.C1 object>",
        "<shapes.A1.B1.C1 object>",
        "<shapes.A1.B1.C1 object>",
        "<shapes.A1.B1.C1 object>",
        "<shapes.A1.B1.C1 object>",
        "<shapes.A1.B1.C1 object>",
        "<shapes.A1.B1.C1 object>",
        "<shapes.A1.B1.C1 object>",
        "<shapes.A1.B1.C1 object>",
        "[<shapes.A1.B1.C1 object>]",
        "(<shapes.A1.B1.C1 object>, 1)",
        "{'k': <shapes.A1.B1.C1 object>}",
        "[[<shapes.A1.B1.C1 object>]]",
        "[<shapes.A1.B1.C1 object>]",
    };

    // python3 3.12, package layout, address stripped (generator: scratchpad oracle/gen.py).
    private static readonly string[] PythonPackage =
    {
        "<pkg.shapes.D object>",
        "<pkg.shapes.D object>",
        "<pkg.shapes.D object>",
        "<pkg.shapes.D object>",
        "<pkg.shapes.D object>",
        "<pkg.shapes.D object>",
        "<pkg.shapes.D object>",
        "<pkg.shapes.D object>",
        "<pkg.shapes.D object>",
        "[<pkg.shapes.D object>]",
        "(<pkg.shapes.D object>, 1)",
        "{'k': <pkg.shapes.D object>}",
        "[[<pkg.shapes.D object>]]",
        "[<pkg.shapes.D object>]",
        "<pkg.shapes.Outer.C object>",
        "<pkg.shapes.Outer.C object>",
        "<pkg.shapes.Outer.C object>",
        "<pkg.shapes.Outer.C object>",
        "<pkg.shapes.Outer.C object>",
        "<pkg.shapes.Outer.C object>",
        "<pkg.shapes.Outer.C object>",
        "<pkg.shapes.Outer.C object>",
        "<pkg.shapes.Outer.C object>",
        "[<pkg.shapes.Outer.C object>]",
        "(<pkg.shapes.Outer.C object>, 1)",
        "{'k': <pkg.shapes.Outer.C object>}",
        "[[<pkg.shapes.Outer.C object>]]",
        "[<pkg.shapes.Outer.C object>]",
        "<pkg.shapes.my_thing object>",
        "<pkg.shapes.my_thing object>",
        "<pkg.shapes.my_thing object>",
        "<pkg.shapes.my_thing object>",
        "<pkg.shapes.my_thing object>",
        "<pkg.shapes.my_thing object>",
        "<pkg.shapes.my_thing object>",
        "<pkg.shapes.my_thing object>",
        "<pkg.shapes.my_thing object>",
        "[<pkg.shapes.my_thing object>]",
        "(<pkg.shapes.my_thing object>, 1)",
        "{'k': <pkg.shapes.my_thing object>}",
        "[[<pkg.shapes.my_thing object>]]",
        "[<pkg.shapes.my_thing object>]",
        "<pkg.shapes.esc_thing object>",
        "<pkg.shapes.esc_thing object>",
        "<pkg.shapes.esc_thing object>",
        "<pkg.shapes.esc_thing object>",
        "<pkg.shapes.esc_thing object>",
        "<pkg.shapes.esc_thing object>",
        "<pkg.shapes.esc_thing object>",
        "<pkg.shapes.esc_thing object>",
        "<pkg.shapes.esc_thing object>",
        "[<pkg.shapes.esc_thing object>]",
        "(<pkg.shapes.esc_thing object>, 1)",
        "{'k': <pkg.shapes.esc_thing object>}",
        "[[<pkg.shapes.esc_thing object>]]",
        "[<pkg.shapes.esc_thing object>]",
        "<pkg.shapes.Box object>",
        "<pkg.shapes.Box object>",
        "<pkg.shapes.Box object>",
        "<pkg.shapes.Box object>",
        "<pkg.shapes.Box object>",
        "<pkg.shapes.Box object>",
        "<pkg.shapes.Box object>",
        "<pkg.shapes.Box object>",
        "<pkg.shapes.Box object>",
        "[<pkg.shapes.Box object>]",
        "(<pkg.shapes.Box object>, 1)",
        "{'k': <pkg.shapes.Box object>}",
        "[[<pkg.shapes.Box object>]]",
        "[<pkg.shapes.Box object>]",
        "<pkg.shapes.Pt object>",
        "<pkg.shapes.Pt object>",
        "<pkg.shapes.Pt object>",
        "<pkg.shapes.Pt object>",
        "<pkg.shapes.Pt object>",
        "<pkg.shapes.Pt object>",
        "<pkg.shapes.Pt object>",
        "<pkg.shapes.Pt object>",
        "<pkg.shapes.Pt object>",
        "[<pkg.shapes.Pt object>]",
        "(<pkg.shapes.Pt object>, 1)",
        "{'k': <pkg.shapes.Pt object>}",
        "[[<pkg.shapes.Pt object>]]",
        "[<pkg.shapes.Pt object>]",
        "S-str",
        "S-str",
        "<pkg.shapes.S object>",
        "S-str",
        "<pkg.shapes.S object>",
        "S-str",
        "S-str",
        "S-str",
        "<pkg.shapes.S object>",
        "[<pkg.shapes.S object>]",
        "(<pkg.shapes.S object>, 1)",
        "{'k': <pkg.shapes.S object>}",
        "[[<pkg.shapes.S object>]]",
        "[<pkg.shapes.S object>]",
        "R-repr",
        "R-repr",
        "R-repr",
        "R-repr",
        "R-repr",
        "R-repr",
        "R-repr",
        "R-repr",
        "R-repr",
        "[R-repr]",
        "(R-repr, 1)",
        "{'k': R-repr}",
        "[[R-repr]]",
        "[R-repr]",
        "P(n=1)",
        "P(n=1)",
        "P(n=1)",
        "P(n=1)",
        "P(n=1)",
        "P(n=1)",
        "P(n=1)",
        "P(n=1)",
        "P(n=1)",
        "[P(n=1)]",
        "(P(n=1), 1)",
        "{'k': P(n=1)}",
        "[[P(n=1)]]",
        "[P(n=1)]",
        "<pkg.shapes.Outer2.inner_cls object>",
        "<pkg.shapes.Outer2.inner_cls object>",
        "<pkg.shapes.Outer2.inner_cls object>",
        "<pkg.shapes.Outer2.inner_cls object>",
        "<pkg.shapes.Outer2.inner_cls object>",
        "<pkg.shapes.Outer2.inner_cls object>",
        "<pkg.shapes.Outer2.inner_cls object>",
        "<pkg.shapes.Outer2.inner_cls object>",
        "<pkg.shapes.Outer2.inner_cls object>",
        "[<pkg.shapes.Outer2.inner_cls object>]",
        "(<pkg.shapes.Outer2.inner_cls object>, 1)",
        "{'k': <pkg.shapes.Outer2.inner_cls object>}",
        "[[<pkg.shapes.Outer2.inner_cls object>]]",
        "[<pkg.shapes.Outer2.inner_cls object>]",
        "<pkg.shapes.Sub object>",
        "<pkg.shapes.Sub object>",
        "<pkg.shapes.Sub object>",
        "<pkg.shapes.Sub object>",
        "<pkg.shapes.Sub object>",
        "<pkg.shapes.Sub object>",
        "<pkg.shapes.Sub object>",
        "<pkg.shapes.Sub object>",
        "<pkg.shapes.Sub object>",
        "[<pkg.shapes.Sub object>]",
        "(<pkg.shapes.Sub object>, 1)",
        "{'k': <pkg.shapes.Sub object>}",
        "[[<pkg.shapes.Sub object>]]",
        "[<pkg.shapes.Sub object>]",
        "<pkg.shapes.Sq object>",
        "<pkg.shapes.Sq object>",
        "<pkg.shapes.Sq object>",
        "<pkg.shapes.Sq object>",
        "<pkg.shapes.Sq object>",
        "<pkg.shapes.Sq object>",
        "<pkg.shapes.Sq object>",
        "<pkg.shapes.Sq object>",
        "<pkg.shapes.Sq object>",
        "[<pkg.shapes.Sq object>]",
        "(<pkg.shapes.Sq object>, 1)",
        "{'k': <pkg.shapes.Sq object>}",
        "[[<pkg.shapes.Sq object>]]",
        "[<pkg.shapes.Sq object>]",
        "<pkg.shapes.A1.B1.C1 object>",
        "<pkg.shapes.A1.B1.C1 object>",
        "<pkg.shapes.A1.B1.C1 object>",
        "<pkg.shapes.A1.B1.C1 object>",
        "<pkg.shapes.A1.B1.C1 object>",
        "<pkg.shapes.A1.B1.C1 object>",
        "<pkg.shapes.A1.B1.C1 object>",
        "<pkg.shapes.A1.B1.C1 object>",
        "<pkg.shapes.A1.B1.C1 object>",
        "[<pkg.shapes.A1.B1.C1 object>]",
        "(<pkg.shapes.A1.B1.C1 object>, 1)",
        "{'k': <pkg.shapes.A1.B1.C1 object>}",
        "[[<pkg.shapes.A1.B1.C1 object>]]",
        "[<pkg.shapes.A1.B1.C1 object>]",
    };

    // python3 3.12, merged layout, address stripped (generator: scratchpad oracle/gen.py).
    private static readonly string[] PythonMerged =
    {
        "<thing.Thing object>",
        "<thing.Thing object>",
        "<thing.Thing object>",
        "<thing.Thing object>",
        "<thing.Thing object>",
        "<thing.Thing object>",
        "<thing.Thing object>",
        "<thing.Thing object>",
        "<thing.Thing object>",
        "[<thing.Thing object>]",
        "(<thing.Thing object>, 1)",
        "{'k': <thing.Thing object>}",
        "[[<thing.Thing object>]]",
        "[<thing.Thing object>]",
        "<thing.Thing.Inner object>",
        "<thing.Thing.Inner object>",
        "<thing.Thing.Inner object>",
        "<thing.Thing.Inner object>",
        "<thing.Thing.Inner object>",
        "<thing.Thing.Inner object>",
        "<thing.Thing.Inner object>",
        "<thing.Thing.Inner object>",
        "<thing.Thing.Inner object>",
        "[<thing.Thing.Inner object>]",
        "(<thing.Thing.Inner object>, 1)",
        "{'k': <thing.Thing.Inner object>}",
        "[[<thing.Thing.Inner object>]]",
        "[<thing.Thing.Inner object>]",
    };

    private static string ShowFunction(IEnumerable<(string Kind, string Construction)> cells)
    {
        var lines = new List<string> { "def show() -> None:" };
        var i = 0;
        foreach (var (_, construction) in cells)
        {
            var variable = "x" + i++;
            lines.Add($"    {variable} = {construction}");
            foreach (var (_, statement) in Sites)
                lines.Add("    " + System.Text.RegularExpressions.Regex.Replace(statement, @"\bx\b", variable));
        }
        return string.Join("\n", lines) + "\n";
    }

    private static string Declarations => string.Concat(Kinds.Select(k => k.Declaration + "\n"));

    private static IEnumerable<(string Kind, string Construction)> KindCells => Kinds.Select(k => (k.Kind, k.Construction));

    /// <summary>
    /// The python oracle with the one recorded deviation applied: a <c>__str__</c>-only class's repr
    /// sites print its str (<c>S-str</c>) where python prints <c>&lt;mod.S object&gt;</c>.
    /// </summary>
    private static List<string> Expected(string[] python, string module)
        => python.Select(line => line.Replace($"<{module}.S object>", "S-str")).ToList();

    private static void AssertCells(string layout, string stdout, List<string> expected, IReadOnlyList<(string Kind, string Construction)> cells)
    {
        var actual = stdout.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        var mismatches = new List<string>();
        for (var i = 0; i < expected.Count; i++)
        {
            var cell = $"{cells[i / Sites.Length].Kind} × {Sites[i % Sites.Length].Site}";
            var got = i < actual.Length ? actual[i] : "<missing>";
            if (got != expected[i])
                mismatches.Add($"[{layout}] {cell}: expected {expected[i]} but printed {got}");
        }
        mismatches.Should().BeEmpty();
        actual.Should().HaveCount(expected.Count, $"[{layout}] one line per cell");
    }

    [Fact]
    public void EntryModule_InstancesPrintAsMain()
    {
        var source = Declarations + ShowFunction(KindCells) + "\ndef main() -> None:\n    show()\n";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(result.StandardError);
        AssertCells("entry", result.StandardOutput, Expected(PythonEntry, "__main__"), KindCells.ToList());
    }

    [Fact]
    public void ImportedModule_InstancesNameTheirModule()
    {
        using var helper = new ProjectCompilationHelper(Output);
        helper.WithRootNamespace("NameChannel").WithEntryPoint("main.spy");
        helper.AddSourceFile("shapes.spy", Declarations + ShowFunction(KindCells));
        helper.AddSourceFile("main.spy", "import shapes\n\ndef main() -> None:\n    shapes.show()\n");
        helper.CreateProjectFile();
        var result = helper.CompileAndExecute();
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors) + result.StandardError);
        AssertCells("module", result.StandardOutput, Expected(PythonModule, "shapes"), KindCells.ToList());
    }

    [Fact]
    public void PackageModule_InstancesNameTheDottedModule()
    {
        using var helper = new ProjectCompilationHelper(Output);
        helper.WithRootNamespace("NameChannel").WithEntryPoint("main.spy");
        helper.AddSourceFile("pkg/shapes.spy", Declarations + ShowFunction(KindCells));
        helper.AddSourceFile("main.spy", "import pkg.shapes\n\ndef main() -> None:\n    pkg.shapes.show()\n");
        helper.CreateProjectFile();
        var result = helper.CompileAndExecute();
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors) + result.StandardError);
        AssertCells("package", result.StandardOutput, Expected(PythonPackage, "pkg.shapes"), KindCells.ToList());
    }

    [Fact]
    public void MergedModuleClass_NamesItsModule()
    {
        using var helper = new ProjectCompilationHelper(Output);
        helper.WithRootNamespace("NameChannel").WithEntryPoint("main.spy");
        helper.AddSourceFile("thing.spy", "class Thing:\n    class Inner:\n        pass\n\n" + ShowFunction(MergedKinds));
        helper.AddSourceFile("main.spy", "import thing\n\ndef main() -> None:\n    thing.show()\n");
        helper.CreateProjectFile();
        var result = helper.CompileAndExecute();
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors) + result.StandardError);
        AssertCells("merged", result.StandardOutput, PythonMerged.ToList(), MergedKinds);
    }

    [Fact]
    public void Matrix_IsTotal()
    {
        Kinds.Should().HaveCount(13);
        Sites.Should().HaveCount(14);
        PythonEntry.Should().HaveCount(13 * 14);
        PythonModule.Should().HaveCount(13 * 14);
        PythonPackage.Should().HaveCount(13 * 14);
        PythonMerged.Should().HaveCount(2 * 14);
    }
}
