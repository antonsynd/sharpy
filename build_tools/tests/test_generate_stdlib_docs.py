"""Tests for generate_stdlib_docs.py — stdlib API reference generator."""

import textwrap
from pathlib import Path

import pytest

from build_tools.generate_stdlib_docs import (
    DocMember,
    DocModule,
    DocParam,
    DocType,
    _collect_doc_lines,
    _parse_params,
    _parse_xml_doc,
    _split_generic_args,
    map_type,
    parse_cs_file,
    pascal_to_snake,
    render_index_page,
    render_module_page,
)


# ---------------------------------------------------------------------------
# pascal_to_snake
# ---------------------------------------------------------------------------


class TestPascalToSnake:
    """Name mangling: PascalCase -> snake_case with special cases."""

    def test_simple(self):
        assert pascal_to_snake("FloorDiv") == "floor_div"

    def test_single_word(self):
        assert pascal_to_snake("Append") == "append"

    def test_multiple_words(self):
        assert pascal_to_snake("IsClose") == "is_close"

    def test_acronym(self):
        assert pascal_to_snake("XMLParser") == "xml_parser"

    def test_already_lowercase(self):
        assert pascal_to_snake("items") == "items"

    def test_special_case_isinstance(self):
        assert pascal_to_snake("IsInstance") == "isinstance"

    def test_special_case_isinstance_alt(self):
        assert pascal_to_snake("Isinstance") == "isinstance"

    def test_special_case_issubclass(self):
        assert pascal_to_snake("Issubclass") == "issubclass"

    def test_special_case_log2(self):
        assert pascal_to_snake("Log2") == "log2"

    def test_special_case_log10(self):
        assert pascal_to_snake("Log10") == "log10"

    def test_special_case_atan2(self):
        assert pascal_to_snake("Atan2") == "atan2"

    def test_special_case_expm1(self):
        assert pascal_to_snake("Expm1") == "expm1"

    def test_special_case_toString(self):
        assert pascal_to_snake("ToString") == "__str__"

    def test_special_case_getHashCode(self):
        assert pascal_to_snake("GetHashCode") == "__hash__"

    def test_special_case_equals(self):
        assert pascal_to_snake("Equals") == "__eq__"

    def test_special_case_factorial(self):
        assert pascal_to_snake("Factorial") == "factorial"

    def test_special_case_fsum(self):
        assert pascal_to_snake("Fsum") == "fsum"

    def test_digit_boundary(self):
        assert pascal_to_snake("Log1P") == "log1p"

    def test_consecutive_uppercase(self):
        assert pascal_to_snake("HTTPSConnection") == "https_connection"

    def test_trailing_digits(self):
        assert pascal_to_snake("GetItem2") == "get_item2"


# ---------------------------------------------------------------------------
# map_type
# ---------------------------------------------------------------------------


class TestMapType:
    """C# type -> Sharpy type mapping."""

    def test_void(self):
        assert map_type("void") == "None"

    def test_int(self):
        assert map_type("int") == "int"

    def test_int32(self):
        assert map_type("Int32") == "int"

    def test_system_int32(self):
        assert map_type("System.Int32") == "int"

    def test_long(self):
        assert map_type("long") == "long"

    def test_double(self):
        assert map_type("double") == "float"

    def test_float_to_float32(self):
        assert map_type("float") == "float32"

    def test_single_to_float32(self):
        assert map_type("Single") == "float32"

    def test_string(self):
        assert map_type("string") == "str"

    def test_bool(self):
        assert map_type("bool") == "bool"

    def test_object(self):
        assert map_type("object") == "object"

    def test_nullable(self):
        assert map_type("int?") == "int?"

    def test_nullable_string(self):
        assert map_type("string?") == "str?"

    def test_generic_list(self):
        assert map_type("List<int>") == "list[int]"

    def test_generic_dict(self):
        assert map_type("Dict<string, int>") == "dict[str, int]"

    def test_generic_set(self):
        assert map_type("Set<string>") == "set[str]"

    def test_sharpy_list(self):
        assert map_type("Sharpy.List<int>") == "list[int]"

    def test_ienumerable(self):
        assert map_type("IEnumerable<int>") == "Iterable[int]"

    def test_nested_generics(self):
        assert map_type("List<Dict<string, int>>") == "list[dict[str, int]]"

    def test_array(self):
        assert map_type("int[]") == "list[int]"

    def test_single_type_param(self):
        assert map_type("T") == "T"

    def test_empty(self):
        assert map_type("") == ""

    def test_whitespace(self):
        assert map_type("  int  ") == "int"

    def test_unknown_type_passthrough(self):
        assert map_type("SomeCustomType") == "SomeCustomType"

    def test_tuple(self):
        assert map_type("Tuple<int, string>") == "tuple[int, str]"

    def test_value_tuple(self):
        assert map_type("ValueTuple<int, string>") == "tuple[int, str]"


# ---------------------------------------------------------------------------
# _split_generic_args
# ---------------------------------------------------------------------------


class TestSplitGenericArgs:
    """Splitting generic type arguments respecting nesting."""

    def test_simple(self):
        assert _split_generic_args("int, string") == ["int", "string"]

    def test_nested(self):
        assert _split_generic_args("Dict<string, int>, bool") == [
            "Dict<string, int>",
            "bool",
        ]

    def test_deeply_nested(self):
        result = _split_generic_args("List<Dict<string, int>>, Set<bool>")
        assert result == ["List<Dict<string, int>>", "Set<bool>"]

    def test_single(self):
        assert _split_generic_args("int") == ["int"]

    def test_empty(self):
        assert _split_generic_args("") == []


# ---------------------------------------------------------------------------
# _parse_xml_doc
# ---------------------------------------------------------------------------


class TestParseXmlDoc:
    """XML doc comment extraction."""

    def test_summary(self):
        lines = [
            "/// <summary>",
            "/// Returns the absolute value of a number.",
            "/// </summary>",
        ]
        result = _parse_xml_doc(lines)
        assert "absolute value" in result["summary"]

    def test_summary_single_line(self):
        lines = ["/// <summary>Sorts the list in-place.</summary>"]
        result = _parse_xml_doc(lines)
        assert result["summary"] == "Sorts the list in-place."

    def test_params(self):
        lines = [
            '/// <param name="x">The input value.</param>',
            '/// <param name="y">The other value.</param>',
        ]
        result = _parse_xml_doc(lines)
        assert len(result["params"]) == 2
        assert result["params"][0] == ("x", "The input value.")
        assert result["params"][1] == ("y", "The other value.")

    def test_returns(self):
        lines = ["/// <returns>The computed result.</returns>"]
        result = _parse_xml_doc(lines)
        assert result["returns"] == "The computed result."

    def test_example(self):
        lines = [
            "/// <example>",
            "/// <code>",
            "/// x = math.sqrt(4)",
            "/// </code>",
            "/// </example>",
        ]
        result = _parse_xml_doc(lines)
        assert "math.sqrt" in result["example"]

    def test_remarks(self):
        lines = ["/// <remarks>This is O(n log n).</remarks>"]
        result = _parse_xml_doc(lines)
        assert "O(n log n)" in result["remarks"]

    def test_exception(self):
        lines = ['/// <exception cref="ValueError">If x is negative.</exception>']
        result = _parse_xml_doc(lines)
        assert len(result["exceptions"]) == 1
        assert result["exceptions"][0][0] == "ValueError"
        assert "negative" in result["exceptions"][0][1]

    def test_empty_lines(self):
        result = _parse_xml_doc([])
        assert result == {}

    def test_non_doc_lines_ignored(self):
        lines = [
            "// This is a regular comment",
            "/// <summary>Real doc.</summary>",
        ]
        result = _parse_xml_doc(lines)
        assert result["summary"] == "Real doc."

    def test_see_cref_converted(self):
        lines = ['/// <summary>See <see cref="Math.Sqrt"/> for details.</summary>']
        result = _parse_xml_doc(lines)
        assert "`Math.Sqrt`" in result["summary"]

    def test_malformed_xml_fallback(self):
        lines = ["/// <summary>Unclosed tag"]
        result = _parse_xml_doc(lines)
        # Should not crash; may extract partial summary via regex fallback
        assert isinstance(result, dict)


# ---------------------------------------------------------------------------
# _parse_params
# ---------------------------------------------------------------------------


class TestParseParams:
    """C# parameter list parsing."""

    def test_simple(self):
        params = _parse_params("int x, string y")
        assert len(params) == 2
        assert params[0].name == "x"
        assert params[0].type == "int"
        assert params[1].name == "y"
        assert params[1].type == "str"

    def test_default_value(self):
        params = _parse_params("int x = 0")
        assert params[0].default == "0"

    def test_empty(self):
        params = _parse_params("")
        assert params == []

    def test_generic_param(self):
        params = _parse_params("List<int> items")
        assert params[0].type == "list[int]"

    def test_extension_method_skips_this(self):
        params = _parse_params("this string s, int start", is_extension=True)
        assert len(params) == 1
        assert params[0].name == "start"

    def test_params_keyword(self):
        params = _parse_params("params int[] values")
        assert len(params) == 1
        assert params[0].name == "values"

    def test_nullable(self):
        params = _parse_params("string? name")
        assert params[0].type == "str?"


# ---------------------------------------------------------------------------
# parse_cs_file
# ---------------------------------------------------------------------------


class TestParseCsFile:
    """Parse actual C# files for public members."""

    def test_method_extraction(self, tmp_path):
        cs = textwrap.dedent("""\
            using System;

            public partial class MathModule
            {
                /// <summary>
                /// Returns the square root of x.
                /// </summary>
                /// <param name="x">The input value.</param>
                /// <returns>The square root.</returns>
                public static double Sqrt(double x)
                {
                    return Math.Sqrt(x);
                }
            }
        """)
        f = tmp_path / "Math.cs"
        f.write_text(cs)
        members = parse_cs_file(f)
        assert len(members) == 1
        m = members[0]
        assert m.name == "sqrt"
        assert m.kind == "method"
        assert m.return_type == "float"
        assert "square root" in m.summary

    def test_constant_extraction(self, tmp_path):
        cs = textwrap.dedent("""\
            public partial class MathModule
            {
                /// <summary>The ratio of a circle's circumference.</summary>
                public const double Pi = 3.14159265358979;
            }
        """)
        f = tmp_path / "Math.cs"
        f.write_text(cs)
        members = parse_cs_file(f)
        assert len(members) == 1
        assert members[0].kind == "constant"
        assert members[0].name == "pi"

    def test_property_extraction(self, tmp_path):
        cs = textwrap.dedent("""\
            public partial class SysModule
            {
                /// <summary>The current platform name.</summary>
                public static string Platform => "sharpy";
            }
        """)
        f = tmp_path / "Sys.cs"
        f.write_text(cs)
        members = parse_cs_file(f)
        assert len(members) == 1
        assert members[0].kind == "property"
        assert members[0].name == "platform"

    def test_skips_operator(self, tmp_path):
        cs = textwrap.dedent("""\
            public partial class MyList
            {
                public static MyList operator+(MyList a, MyList b)
                {
                    return a;
                }
            }
        """)
        f = tmp_path / "List.cs"
        f.write_text(cs)
        members = parse_cs_file(f)
        assert len(members) == 0

    def test_skips_inheritdoc(self, tmp_path):
        cs = textwrap.dedent("""\
            public partial class MyList
            {
                /// <inheritdoc/>
                public int Count => 0;
            }
        """)
        f = tmp_path / "List.cs"
        f.write_text(cs)
        members = parse_cs_file(f)
        assert len(members) == 0

    def test_skips_private(self, tmp_path):
        cs = textwrap.dedent("""\
            public partial class MyClass
            {
                private void InternalHelper() { }
                internal int Secret => 42;
            }
        """)
        f = tmp_path / "MyClass.cs"
        f.write_text(cs)
        members = parse_cs_file(f)
        assert len(members) == 0

    def test_skips_class_declarations(self, tmp_path):
        cs = textwrap.dedent("""\
            public static partial class MathModule
            {
                public static int Abs(int x) => x < 0 ? -x : x;
            }
        """)
        f = tmp_path / "Math.cs"
        f.write_text(cs)
        members = parse_cs_file(f)
        # Should get the method, not the class declaration
        assert len(members) == 1
        assert members[0].name == "abs"

    def test_extension_method(self, tmp_path):
        cs = textwrap.dedent("""\
            public static class StringExtensions
            {
                /// <summary>Returns the uppercased string.</summary>
                public static string Upper(this string s)
                {
                    return s.ToUpper();
                }
            }
        """)
        f = tmp_path / "String.cs"
        f.write_text(cs)
        members = parse_cs_file(f, is_extension=True)
        assert len(members) == 1
        assert members[0].name == "upper"
        # Extension 'this' param should be stripped
        assert len(members[0].params) == 0

    def test_method_with_default_params(self, tmp_path):
        cs = textwrap.dedent("""\
            public partial class ListModule
            {
                public static void Sort(int key = 0, bool reverse = false)
                {
                }
            }
        """)
        f = tmp_path / "List.cs"
        f.write_text(cs)
        members = parse_cs_file(f)
        assert len(members) == 1
        assert members[0].params[0].default == "0"
        assert members[0].params[1].default == "false"


# ---------------------------------------------------------------------------
# Markdown rendering
# ---------------------------------------------------------------------------


class TestRenderModulePage:
    """Module page markdown generation."""

    def test_module_has_import(self):
        mod = DocModule(name="math", kind="module", summary="Math functions.")
        output = render_module_page(mod)
        assert "import math" in output
        assert "# math" in output

    def test_type_no_import(self):
        mod = DocModule(name="list", kind="type", summary="The list type.")
        output = render_module_page(mod)
        assert "import list" not in output
        assert "# list" in output

    def test_constants_table(self):
        mod = DocModule(
            name="math",
            kind="module",
            members=[
                DocMember(
                    kind="constant",
                    name="pi",
                    cs_name="Pi",
                    signature="",
                    summary="Pi constant.",
                    return_type="float",
                    is_static=True,
                ),
            ],
        )
        output = render_module_page(mod)
        assert "| `pi` | `float` |" in output

    def test_constants_table_escapes_special_chars(self):
        mod = DocModule(
            name="string",
            kind="module",
            members=[
                DocMember(
                    kind="constant",
                    name="punctuation",
                    cs_name="punctuation",
                    signature="",
                    summary='Punctuation: !"#$%&\'()*+,-./:;<=>?@[\\]^_`{|}~',
                    return_type="str",
                    is_static=True,
                ),
            ],
        )
        output = render_module_page(mod)
        # Pipes and backticks must be escaped in table cells
        assert "\\|" in output
        assert "\\`" in output
        # Must be a single table row (no line breaks in cell)
        table_lines = [l for l in output.splitlines() if l.startswith("| `punctuation`")]
        assert len(table_lines) == 1

    def test_constants_table_collapses_multiline_summary(self):
        mod = DocModule(
            name="test",
            kind="module",
            members=[
                DocMember(
                    kind="constant",
                    name="value",
                    cs_name="Value",
                    signature="",
                    summary="Line one.\nLine two.\nLine three.",
                    return_type="str",
                    is_static=True,
                ),
            ],
        )
        output = render_module_page(mod)
        table_lines = [l for l in output.splitlines() if l.startswith("| `value`")]
        assert len(table_lines) == 1
        assert "Line one. Line two. Line three." in table_lines[0]

    def test_methods_section(self):
        mod = DocModule(
            name="math",
            kind="module",
            members=[
                DocMember(
                    kind="method",
                    name="sqrt",
                    cs_name="Sqrt",
                    signature="sqrt(x: float) -> float",
                    summary="Square root.",
                    is_static=True,
                ),
            ],
        )
        output = render_module_page(mod)
        assert "## Functions" in output
        assert "math.sqrt" in output

    def test_type_methods_section(self):
        mod = DocModule(
            name="list",
            kind="type",
            members=[
                DocMember(
                    kind="method",
                    name="append",
                    cs_name="Append",
                    signature="append(item: T)",
                    summary="Add item.",
                ),
            ],
        )
        output = render_module_page(mod)
        assert "## Methods" in output

    def test_module_types_section(self):
        mod = DocModule(
            name="argparse",
            kind="module",
            types=[
                DocType(
                    name="ArgumentParser",
                    cs_name="ArgumentParser",
                    summary="Parses arguments.",
                    members=[],
                ),
            ],
        )
        output = render_module_page(mod)
        assert "## ArgumentParser" in output


class TestRenderIndexPage:
    """Stdlib index page generation."""

    def test_has_sections(self):
        builtins = DocModule(name="builtins", kind="builtins", members=[])
        core_types = [DocModule(name="list", kind="type", summary="A list.")]
        modules = [DocModule(name="math", kind="module", summary="Math functions.")]
        output = render_index_page(builtins, core_types, modules)
        assert "## Built-in Functions" in output
        assert "## Core Types" in output
        assert "## Modules" in output
        assert "[`list`](list.md)" in output
        assert "[`math`](math.md)" in output


class TestCollectDocLines:
    """Tests for _collect_doc_lines edge cases."""

    def test_skips_regular_comment_between_doc_and_declaration(self):
        lines = [
            "        /// <summary>",
            "        /// Description here.",
            "        /// </summary>",
            "        // Note: some implementation note",
            "        public static readonly double Nan = double.NaN;",
        ]
        doc_lines = _collect_doc_lines(lines, 4)
        assert len(doc_lines) == 3
        assert "/// <summary>" in doc_lines[0]
        assert "Description here." in doc_lines[1]

    def test_no_doc_lines_returns_empty(self):
        lines = [
            "        // regular comment",
            "        public const int X = 1;",
        ]
        doc_lines = _collect_doc_lines(lines, 1)
        assert doc_lines == []


class TestOperatorSkipping:
    """Tests that operator declarations are skipped in parse_cs_file."""

    def test_operator_true_false_skipped(self, tmp_path):
        cs = textwrap.dedent("""\
            namespace Test {
            public class Foo {
                /// <summary>Check truthiness.</summary>
                public static bool operator true(Foo? x) => x != null;
                /// <summary>Check falsiness.</summary>
                public static bool operator false(Foo? x) => x == null;
                /// <summary>A real method.</summary>
                public void DoStuff() {}
            }
            }
        """)
        f = tmp_path / "Foo.cs"
        f.write_text(cs)
        members = parse_cs_file(f)
        names = [m.name for m in members]
        assert "true" not in names
        assert "false" not in names
        assert "do_stuff" in names

    def test_implicit_operator_skipped(self, tmp_path):
        cs = textwrap.dedent("""\
            namespace Test {
            public class Bar<T> {
                /// <summary>Convert array.</summary>
                public static implicit operator Bar<T>(T[] array) => new Bar<T>();
                /// <summary>Count items.</summary>
                public int Count => 0;
            }
            }
        """)
        f = tmp_path / "Bar.cs"
        f.write_text(cs)
        members = parse_cs_file(f)
        names = [m.name for m in members]
        # Should not have implicit operator as a member
        assert all("implicit" not in n and "operator" not in n for n in names)
        assert "count" in names


class TestInternalClassFiltering:
    """Tests that public members inside internal classes are excluded."""

    def test_internal_class_members_excluded(self, tmp_path):
        cs = textwrap.dedent("""\
            namespace Test {
            public static partial class MyModule {
                /// <summary>Public function.</summary>
                public static void PublicFunc() {}
            }
            internal sealed class InternalHelper {
                /// <summary>Should not appear.</summary>
                public static readonly InternalHelper Instance = new InternalHelper();
            }
            }
        """)
        f = tmp_path / "MyModule.cs"
        f.write_text(cs)
        members = parse_cs_file(f)
        names = [m.name for m in members]
        assert "public_func" in names
        assert "instance" not in names


class TestPropertyDeduplication:
    """Tests that duplicate properties are deduplicated."""

    def test_duplicate_properties_deduplicated(self):
        mod = DocModule(
            name="datetime",
            kind="module",
            members=[
                DocMember(kind="property", name="year", cs_name="Year",
                          signature="", summary="The year component.",
                          return_type="int"),
                DocMember(kind="property", name="month", cs_name="Month",
                          signature="", summary="The month (1-12).",
                          return_type="int"),
                DocMember(kind="property", name="year", cs_name="Year",
                          signature="", summary="The year component.",
                          return_type="int"),
            ],
        )
        output = render_module_page(mod)
        # "year" should appear only once in the properties table
        year_count = output.count("| `year` |")
        assert year_count == 1
