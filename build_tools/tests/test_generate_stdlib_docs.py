"""Tests for generate_stdlib_docs.py — stdlib API reference generator."""

import textwrap
from pathlib import Path

import pytest

from build_tools.generate_stdlib_docs import (
    HAND_AUTHORED_MODULES,
    DocMember,
    DocModule,
    DocParam,
    DocType,
    _collect_doc_lines,
    _count_code_braces,
    _find_nonpublic_class_ranges,
    _parse_params,
    _parse_xml_doc,
    _replace_child_block,
    _split_generic_args,
    build_nav_blocks,
    check_docs,
    compute_mkdocs_nav,
    discover_modules,
    generate,
    map_type,
    parse_cs_file,
    pascal_to_snake,
    render_index_page,
    render_module_page,
    update_mkdocs_nav,
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
        assert map_type("int?") == "int | None"

    def test_nullable_string(self):
        assert map_type("string?") == "str | None"

    def test_nullable_str(self):
        assert map_type("str?") == "str | None"

    def test_nullable_int(self):
        # int is not in _TYPE_MAP but the nullable stripping works regardless
        assert map_type("int?") == "int | None"

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

    # --- New tests for fixes ---

    def test_func_two_args(self):
        assert map_type("Func<int, str>") == "(int) -> str"

    def test_func_three_args(self):
        assert map_type("Func<T, T, int>") == "(T, T) -> int"

    def test_func_single_arg(self):
        assert map_type("Func<bool>") == "() -> bool"

    def test_action_one_arg(self):
        assert map_type("Action<str>") == "(str) -> None"

    def test_action_no_args(self):
        assert map_type("Action") == "() -> None"

    def test_global_func(self):
        assert map_type("global::System.Func<T, bool>") == "(T) -> bool"

    def test_system_ienumerable_generic(self):
        assert map_type("System.Collections.Generic.IEnumerable<int>") == "Iterable[int]"

    def test_system_value_tuple_generic(self):
        assert map_type("System.ValueTuple<str, str>") == "tuple[str, str]"

    def test_value_tuple_cs_syntax(self):
        """C# value-tuple syntax (T1, T2) should map to tuple[T1, T2]."""
        assert map_type("(string, int)") == "tuple[str, int]"

    def test_ienumerable_of_tuple(self):
        """IEnumerable<(TKey, TValue)> should map to Iterable[tuple[TKey, TValue]]."""
        assert map_type("IEnumerable<(string, int)>") == "Iterable[tuple[str, int]]"

    def test_global_system_stripped(self):
        assert map_type("global::System.Func<T, bool>") == "(T) -> bool"


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
        assert params[0].type == "str | None"

    def test_null_default_mapped_to_none(self):
        params = _parse_params("string? name = null")
        assert params[0].default == "None"

    def test_false_default_mapped_to_False(self):
        params = _parse_params("bool reverse = false")
        assert params[0].default == "False"

    def test_true_default_mapped_to_True(self):
        params = _parse_params("bool enable = true")
        assert params[0].default == "True"

    def test_non_literal_default_preserved(self):
        params = _parse_params("int x = 0")
        assert params[0].default == "0"


# ---------------------------------------------------------------------------
# parse_cs_file
# ---------------------------------------------------------------------------


class TestParseCsFile:
    """Parse actual C# files for public members."""

    def test_method_extraction(self, tmp_path):
        cs = textwrap.dedent(
            """\
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
        """
        )
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
        cs = textwrap.dedent(
            """\
            public partial class MathModule
            {
                /// <summary>The ratio of a circle's circumference.</summary>
                public const double Pi = 3.14159265358979;
            }
        """
        )
        f = tmp_path / "Math.cs"
        f.write_text(cs)
        members = parse_cs_file(f)
        assert len(members) == 1
        assert members[0].kind == "constant"
        assert members[0].name == "pi"

    def test_property_extraction(self, tmp_path):
        cs = textwrap.dedent(
            """\
            public partial class SysModule
            {
                /// <summary>The current platform name.</summary>
                public static string Platform => "sharpy";
            }
        """
        )
        f = tmp_path / "Sys.cs"
        f.write_text(cs)
        members = parse_cs_file(f)
        assert len(members) == 1
        assert members[0].kind == "property"
        assert members[0].name == "platform"

    def test_skips_operator(self, tmp_path):
        cs = textwrap.dedent(
            """\
            public partial class MyList
            {
                public static MyList operator+(MyList a, MyList b)
                {
                    return a;
                }
            }
        """
        )
        f = tmp_path / "List.cs"
        f.write_text(cs)
        members = parse_cs_file(f)
        assert len(members) == 0

    def test_skips_inheritdoc(self, tmp_path):
        cs = textwrap.dedent(
            """\
            public partial class MyList
            {
                /// <inheritdoc/>
                public int Count => 0;
            }
        """
        )
        f = tmp_path / "List.cs"
        f.write_text(cs)
        members = parse_cs_file(f)
        assert len(members) == 0

    def test_skips_private(self, tmp_path):
        cs = textwrap.dedent(
            """\
            public partial class MyClass
            {
                private void InternalHelper() { }
                internal int Secret => 42;
            }
        """
        )
        f = tmp_path / "MyClass.cs"
        f.write_text(cs)
        members = parse_cs_file(f)
        assert len(members) == 0

    def test_skips_class_declarations(self, tmp_path):
        cs = textwrap.dedent(
            """\
            public static partial class MathModule
            {
                public static int Abs(int x) => x < 0 ? -x : x;
            }
        """
        )
        f = tmp_path / "Math.cs"
        f.write_text(cs)
        members = parse_cs_file(f)
        # Should get the method, not the class declaration
        assert len(members) == 1
        assert members[0].name == "abs"

    def test_extension_method(self, tmp_path):
        cs = textwrap.dedent(
            """\
            public static class StringExtensions
            {
                /// <summary>Returns the uppercased string.</summary>
                public static string Upper(this string s)
                {
                    return s.ToUpper();
                }
            }
        """
        )
        f = tmp_path / "String.cs"
        f.write_text(cs)
        members = parse_cs_file(f, is_extension=True)
        assert len(members) == 1
        assert members[0].name == "upper"
        # Extension 'this' param should be stripped
        assert len(members[0].params) == 0

    def test_method_with_default_params(self, tmp_path):
        cs = textwrap.dedent(
            """\
            public partial class ListModule
            {
                public static void Sort(int key = 0, bool reverse = false)
                {
                }
            }
        """
        )
        f = tmp_path / "List.cs"
        f.write_text(cs)
        members = parse_cs_file(f)
        assert len(members) == 1
        assert members[0].params[0].default == "0"
        assert members[0].params[1].default == "False"  # false -> False


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
                    summary="Punctuation: !\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~",
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
        table_lines = [
            l for l in output.splitlines() if l.startswith("| `punctuation`")
        ]
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

    def test_type_scoped_properties_rendered(self):
        """Per-type properties should render as a `### Properties` table."""
        mod = DocModule(
            name="collections",
            kind="module",
            types=[
                DocType(
                    name="Counter",
                    cs_name="Counter",
                    summary="A counter.",
                    members=[
                        DocMember(
                            kind="property",
                            name="total",
                            cs_name="Total",
                            signature="total",
                            return_type="int",
                            summary="Total count.",
                        ),
                    ],
                ),
            ],
        )
        output = render_module_page(mod)
        assert "## Counter" in output
        assert "### Properties" in output
        assert "`total`" in output

    def test_type_scoped_constants_use_h3(self):
        """Per-type constants must use H3 (not H2) so they stay nested."""
        mod = DocModule(
            name="datetime",
            kind="module",
            types=[
                DocType(
                    name="timezone",
                    cs_name="Timezone",
                    summary="Timezone info.",
                    members=[
                        DocMember(
                            kind="constant",
                            name="UTC",
                            cs_name="UTC",
                            signature="UTC",
                            return_type="Timezone",
                            summary="UTC timezone.",
                        ),
                    ],
                ),
            ],
        )
        output = render_module_page(mod)
        assert "## timezone" in output
        assert "### Constants" in output
        # No top-level (H2) Constants heading — the constants belong to the type.
        assert "\n## Constants" not in output


class TestDiscoverModulesTypeAnnotations:
    """Discovery of SharpyModuleType-annotated classes (1-arg and 2-arg forms)."""

    def _write_module(
        self, tmp_path: Path, mod_dir: str, mod_name: str, filename: str, body: str
    ) -> Path:
        subdir = tmp_path / mod_dir
        subdir.mkdir(parents=True, exist_ok=True)
        (subdir / "__Init__.cs").write_text(
            textwrap.dedent(
                f"""\
                using Sharpy.Core.Shared;
                namespace Sharpy.Core.{mod_dir};

                [SharpyModule("{mod_name}")]
                public static class {mod_dir}Module {{ }}
                """
            ),
            encoding="utf-8",
        )
        (subdir / filename).write_text(body, encoding="utf-8")
        return tmp_path

    def test_two_arg_form_uses_display_name(self, tmp_path: Path):
        """`[SharpyModuleType("mod", "display")]` should use the second arg."""
        body = textwrap.dedent(
            """\
            using Sharpy.Core.Shared;
            namespace Sharpy.Core.Collections;

            [SharpyModuleType("collections", "ChainMap")]
            public sealed class ChainMap<K, V>
            {
                /// <summary>Gets value.</summary>
                public V Get(K key) => default!;
            }
            """
        )
        self._write_module(tmp_path, "Collections", "collections", "ChainMap.cs", body)
        modules = discover_modules(tmp_path)
        assert len(modules) == 1
        assert modules[0].name == "collections"
        assert len(modules[0].types) == 1
        assert modules[0].types[0].name == "ChainMap"

    def test_multiple_two_arg_annotations_in_one_file(self, tmp_path: Path):
        """A single .cs file with multiple 2-arg annotations yields multiple types."""
        body = textwrap.dedent(
            """\
            using Sharpy.Core.Shared;
            namespace Sharpy.Core.Datetime;

            [SharpyModuleType("datetime", "date")]
            public sealed class Date
            {
                /// <summary>Today.</summary>
                public static Date Today() => default!;
            }

            [SharpyModuleType("datetime", "timedelta")]
            public sealed class Timedelta
            {
                /// <summary>Days.</summary>
                public int Days => 0;
            }
            """
        )
        self._write_module(tmp_path, "Datetime", "datetime", "Datetime.cs", body)
        modules = discover_modules(tmp_path)
        assert len(modules) == 1
        assert modules[0].name == "datetime"
        type_names = sorted(t.name for t in modules[0].types)
        assert type_names == ["date", "timedelta"]

    def test_one_arg_form_still_works(self, tmp_path: Path):
        """`[SharpyModuleType("mod")]` should use the C# class name as display name."""
        body = textwrap.dedent(
            """\
            using Sharpy.Core.Shared;
            namespace Sharpy.Core.Argparse;

            [SharpyModuleType("argparse")]
            public sealed class ArgumentParser
            {
                /// <summary>Parses.</summary>
                public void ParseArgs() { }
            }
            """
        )
        self._write_module(tmp_path, "Argparse", "argparse", "ArgumentParser.cs", body)
        modules = discover_modules(tmp_path)
        assert len(modules) == 1
        assert modules[0].types[0].name == "ArgumentParser"


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
        cs = textwrap.dedent(
            """\
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
        """
        )
        f = tmp_path / "Foo.cs"
        f.write_text(cs)
        members = parse_cs_file(f)
        names = [m.name for m in members]
        assert "true" not in names
        assert "false" not in names
        assert "do_stuff" in names

    def test_implicit_operator_skipped(self, tmp_path):
        cs = textwrap.dedent(
            """\
            namespace Test {
            public class Bar<T> {
                /// <summary>Convert array.</summary>
                public static implicit operator Bar<T>(T[] array) => new Bar<T>();
                /// <summary>Count items.</summary>
                public int Count => 0;
            }
            }
        """
        )
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
        cs = textwrap.dedent(
            """\
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
        """
        )
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
                DocMember(
                    kind="property",
                    name="year",
                    cs_name="Year",
                    signature="",
                    summary="The year component.",
                    return_type="int",
                ),
                DocMember(
                    kind="property",
                    name="month",
                    cs_name="Month",
                    signature="",
                    summary="The month (1-12).",
                    return_type="int",
                ),
                DocMember(
                    kind="property",
                    name="year",
                    cs_name="Year",
                    signature="",
                    summary="The year component.",
                    return_type="int",
                ),
            ],
        )
        output = render_module_page(mod)
        # "year" should appear only once in the properties table
        year_count = output.count("| `year` |")
        assert year_count == 1


# ---------------------------------------------------------------------------
# Brace-depth state machine: _count_code_braces / _find_nonpublic_class_ranges
# ---------------------------------------------------------------------------


class TestCountCodeBraces:
    """Line-level state machine for code-vs-non-code brace counting."""

    def test_plain_code_braces(self):
        opens, closes, in_block = _count_code_braces("if (x) { foo(); }", False)
        assert (opens, closes, in_block) == (1, 1, False)

    def test_line_comment_braces_ignored(self):
        opens, closes, in_block = _count_code_braces("int x = 0; // { } {", False)
        assert (opens, closes, in_block) == (0, 0, False)

    def test_block_comment_single_line(self):
        opens, closes, in_block = _count_code_braces("/* { } */ int y = 0;", False)
        assert (opens, closes, in_block) == (0, 0, False)

    def test_block_comment_open_stays_open(self):
        opens, closes, in_block = _count_code_braces("foo(); /* { ", False)
        assert (opens, closes, in_block) == (0, 0, True)

    def test_block_comment_continuation_closes(self):
        opens, closes, in_block = _count_code_braces(" } */ bar(); {", True)
        assert (opens, closes, in_block) == (1, 0, False)

    def test_regular_string_braces_ignored(self):
        opens, closes, in_block = _count_code_braces('var s = "{not a brace}";', False)
        assert (opens, closes, in_block) == (0, 0, False)

    def test_regular_string_escaped_quote(self):
        opens, closes, in_block = _count_code_braces('var s = "a\\"b{";', False)
        assert (opens, closes, in_block) == (0, 0, False)

    def test_verbatim_string_braces_ignored(self):
        opens, closes, in_block = _count_code_braces('var s = @"path\\{dir}";', False)
        assert (opens, closes, in_block) == (0, 0, False)

    def test_verbatim_string_escaped_double_quote(self):
        opens, closes, in_block = _count_code_braces('var s = @"say ""{}""";', False)
        assert (opens, closes, in_block) == (0, 0, False)

    def test_interpolated_string_escaped_braces(self):
        # "{{" and "}}" are literal braces inside interpolated strings
        opens, closes, in_block = _count_code_braces('var s = $"{{literal}}";', False)
        assert (opens, closes, in_block) == (0, 0, False)

    def test_interpolated_string_hole_is_code(self):
        # Inside $"...{ expr }..." the hole opens/closes count (they balance)
        opens, closes, in_block = _count_code_braces('var s = $"hello {name}!";', False)
        assert (opens, closes, in_block) == (1, 1, False)

    def test_interpolated_verbatim_string(self):
        opens, closes, in_block = _count_code_braces('var s = $@"line {x} end";', False)
        assert (opens, closes, in_block) == (1, 1, False)

    def test_char_literal_brace_ignored(self):
        opens, closes, in_block = _count_code_braces("var c = '{';", False)
        assert (opens, closes, in_block) == (0, 0, False)

    def test_char_literal_escaped(self):
        opens, closes, in_block = _count_code_braces("var c = '\\'';", False)
        assert (opens, closes, in_block) == (0, 0, False)


class TestFindNonpublicClassRanges:
    """Pre-scan finds non-public class bodies while ignoring brace hazards."""

    def test_simple_internal_class(self):
        src = textwrap.dedent(
            """\
            public class Foo
            {
                public int A;
            }
            internal class Bar
            {
                public int B;
            }
            """
        ).split("\n")
        ranges = _find_nonpublic_class_ranges(src)
        # Only the internal class should be flagged
        assert len(ranges) == 1
        start, end = ranges[0]
        assert "internal class Bar" in src[start]
        assert src[end].strip() == "}"

    def test_braces_in_line_comment(self):
        """Braces in // comments must not confuse depth tracking."""
        src = textwrap.dedent(
            """\
            internal class Bar
            {
                public int A; // stray { and }
                public int B;
            }
            public class Foo { }
            """
        ).split("\n")
        ranges = _find_nonpublic_class_ranges(src)
        assert len(ranges) == 1
        start, end = ranges[0]
        assert src[start].startswith("internal class Bar")
        # Close-brace line for Bar
        assert src[end].strip() == "}"

    def test_braces_in_block_comment(self):
        """Braces in /* ... */ comments must not confuse depth tracking."""
        src = textwrap.dedent(
            """\
            internal class Bar
            {
                /* { { { */
                public int A;
                /* } } } */
            }
            """
        ).split("\n")
        ranges = _find_nonpublic_class_ranges(src)
        assert len(ranges) == 1
        _, end = ranges[0]
        # Must close on the final '}' line, not midway through the block comment
        assert src[end].strip() == "}"

    def test_braces_in_string_literal(self):
        """Braces in "..." strings must not confuse depth tracking."""
        src = textwrap.dedent(
            """\
            internal class Bar
            {
                public string S = "}}}";
                public int A;
            }
            """
        ).split("\n")
        ranges = _find_nonpublic_class_ranges(src)
        assert len(ranges) == 1

    def test_braces_in_verbatim_string(self):
        """Braces in @"..." verbatim strings must not confuse depth tracking."""
        src = textwrap.dedent(
            """\
            internal class Bar
            {
                public string S = @"path {with} braces";
                public int A;
            }
            """
        ).split("\n")
        ranges = _find_nonpublic_class_ranges(src)
        assert len(ranges) == 1

    def test_braces_in_interpolated_string(self):
        """$"...{..}..." holes should net zero even though we count them."""
        src = textwrap.dedent(
            """\
            internal class Bar
            {
                public string S = $"name={name}";
                public int A;
            }
            """
        ).split("\n")
        ranges = _find_nonpublic_class_ranges(src)
        assert len(ranges) == 1
        _, end = ranges[0]
        assert src[end].strip() == "}"

    def test_interpolated_escaped_braces(self):
        """{{ and }} are literal braces inside interpolated strings."""
        src = textwrap.dedent(
            """\
            internal class Bar
            {
                public string S = $"{{x}}";
                public int A;
            }
            """
        ).split("\n")
        ranges = _find_nonpublic_class_ranges(src)
        assert len(ranges) == 1

    def test_multiple_nonpublic_classes(self):
        src = textwrap.dedent(
            """\
            internal class A
            {
                public int X;
            }
            public class B { }
            private class C
            {
                public int Y;
            }
            """
        ).split("\n")
        ranges = _find_nonpublic_class_ranges(src)
        assert len(ranges) == 2

    def test_public_class_not_flagged(self):
        src = textwrap.dedent(
            """\
            public class Foo
            {
                public int A;
            }
            """
        ).split("\n")
        ranges = _find_nonpublic_class_ranges(src)
        assert ranges == []

    def test_class_keyword_in_string_not_flagged(self):
        """A string literal containing "internal class" must not trigger a range."""
        src = textwrap.dedent(
            """\
            public class Foo
            {
                public string S = "internal class Bar { }";
            }
            """
        ).split("\n")
        ranges = _find_nonpublic_class_ranges(src)
        assert ranges == []


# ---------------------------------------------------------------------------
# Hand-authored allowlist
# ---------------------------------------------------------------------------


class TestHandAuthoredModules:
    """The hand-authored allowlist controls index merging and page skipping."""

    def test_allowlist_seeded(self):
        for name in ("numpy", "requests", "sqlite3"):
            assert name in HAND_AUTHORED_MODULES
            entry = HAND_AUTHORED_MODULES[name]
            assert entry["description"].strip()
            assert entry["nav_title"]

    def test_index_merges_hand_authored(self):
        """Hand-authored modules appear in the index even with no generated module."""
        builtins = DocModule(name="builtins", kind="builtins", members=[])
        core_types = [DocModule(name="list", kind="type", summary="A list.")]
        modules = [DocModule(name="math", kind="module", summary="Math functions.")]
        output = render_index_page(builtins, core_types, modules)
        assert "[`math`](math.md)" in output
        # numpy is hand-authored: must still be linked from the index.
        assert "[`numpy`](numpy.md)" in output
        assert "[`requests`](requests.md)" in output

    def test_index_modules_sorted(self):
        builtins = DocModule(name="builtins", kind="builtins", members=[])
        modules = [
            DocModule(name="zzz", kind="module", summary="Z."),
            DocModule(name="aaa", kind="module", summary="A."),
        ]
        output = render_index_page(builtins, [], modules)
        # aaa before zzz, and hand-authored numpy slotted in sorted order.
        i_aaa = output.index("[`aaa`]")
        i_numpy = output.index("[`numpy`]")
        i_zzz = output.index("[`zzz`]")
        assert i_aaa < i_numpy < i_zzz

    def test_generate_skips_hand_authored_pages(self, tmp_path: Path):
        """Even with force, a hand-authored page is never (re)written."""
        src = tmp_path / "src"
        mod_dir = src / "Numpy"
        mod_dir.mkdir(parents=True)
        (mod_dir / "__Init__.cs").write_text(
            textwrap.dedent(
                """\
                namespace Sharpy;
                [SharpyModule("numpy")]
                public static partial class NumpyModule { }
                """
            ),
            encoding="utf-8",
        )
        out = tmp_path / "out"
        out.mkdir()
        sentinel = "HAND AUTHORED — DO NOT OVERWRITE\n"
        (out / "numpy.md").write_text(sentinel, encoding="utf-8")

        generate(source_dir=src, output_dir=out, force=True, update_nav=False)

        # The hand-authored page must be untouched.
        assert (out / "numpy.md").read_text(encoding="utf-8") == sentinel


# ---------------------------------------------------------------------------
# mkdocs nav generation
# ---------------------------------------------------------------------------


SAMPLE_MKDOCS = textwrap.dedent(
    """\
    site_name: Sample
    nav:
      - Home: index.md
      - Standard Library:
        - Overview: stdlib/index.md
        - Core Types:
          - list: stdlib/list.md
          - dict: stdlib/dict.md
        - Modules:
          - argparse: stdlib/argparse.md
          - zlib: stdlib/zlib.md
      - Tooling:
        - LSP Server: tooling/lsp-server.md
    """
)


class TestNavGeneration:
    """Targeted mkdocs nav child-block replacement."""

    def _write(self, tmp_path: Path) -> Path:
        p = tmp_path / "mkdocs.yml"
        p.write_text(SAMPLE_MKDOCS, encoding="utf-8")
        return p

    def test_replace_child_block_preserves_siblings(self):
        lines = SAMPLE_MKDOCS.split("\n")
        new, changed = _replace_child_block(
            lines, "- Modules:", ["      - foo: stdlib/foo.md"]
        )
        assert changed
        text = "\n".join(new)
        assert "- foo: stdlib/foo.md" in text
        assert "argparse: stdlib/argparse.md" not in text
        # Unrelated sections are preserved.
        assert "- Tooling:" in text
        assert "LSP Server: tooling/lsp-server.md" in text
        assert "list: stdlib/list.md" in text

    def test_build_nav_blocks_sorted_and_merged(self):
        core_types = [
            DocModule(name="list", kind="type"),
            DocModule(name="dict", kind="type"),
        ]
        modules = [
            DocModule(name="zlib", kind="module"),
            DocModule(name="argparse", kind="module"),
        ]
        blocks = build_nav_blocks(core_types, modules)
        # Core types keep discovery order.
        assert blocks["- Core Types:"] == [
            "      - list: stdlib/list.md",
            "      - dict: stdlib/dict.md",
        ]
        module_block = blocks["- Modules:"]
        # Sorted, and hand-authored names merged in.
        assert module_block[0] == "      - argparse: stdlib/argparse.md"
        assert "      - numpy: stdlib/numpy.md" in module_block
        assert module_block == sorted(module_block)

    def test_update_mkdocs_nav_writes_and_is_idempotent(self, tmp_path: Path):
        p = self._write(tmp_path)
        core_types = [DocModule(name="list", kind="type")]
        modules = [
            DocModule(name="argparse", kind="module"),
            DocModule(name="zlib", kind="module"),
            DocModule(name="middle", kind="module"),
        ]
        changed1 = update_mkdocs_nav(p, core_types, modules)
        assert changed1
        after_first = p.read_text(encoding="utf-8")
        assert "- middle: stdlib/middle.md" in after_first
        # numpy (hand-authored) merged into nav too.
        assert "- numpy: stdlib/numpy.md" in after_first
        # Unrelated sections preserved.
        assert "- Tooling:" in after_first

        # Second run must not change anything (idempotency).
        changed2 = update_mkdocs_nav(p, core_types, modules)
        assert not changed2
        assert p.read_text(encoding="utf-8") == after_first

    def test_compute_nav_does_not_mutate_file(self, tmp_path: Path):
        p = self._write(tmp_path)
        before = p.read_text(encoding="utf-8")
        compute_mkdocs_nav(
            p,
            [DocModule(name="list", kind="type")],
            [DocModule(name="x", kind="module")],
        )
        assert p.read_text(encoding="utf-8") == before

    def test_nav_preserves_trailing_newline(self, tmp_path: Path):
        p = self._write(tmp_path)
        update_mkdocs_nav(
            p,
            [DocModule(name="list", kind="type")],
            [DocModule(name="x", kind="module")],
        )
        assert p.read_text(encoding="utf-8").endswith("\n")


# ---------------------------------------------------------------------------
# --check drift detection
# ---------------------------------------------------------------------------


class TestCheckDocs:
    """check_docs reports drift without writing anything."""

    def _make_source(self, tmp_path: Path) -> Path:
        src = tmp_path / "src"
        mod_dir = src / "Greet"
        mod_dir.mkdir(parents=True)
        (mod_dir / "__Init__.cs").write_text(
            textwrap.dedent(
                """\
                namespace Sharpy;
                /// <summary>Greeting helpers.</summary>
                [SharpyModule("greet")]
                public static partial class GreetModule
                {
                    /// <summary>Say hello.</summary>
                    public static string Hello() => "hi";
                }
                """
            ),
            encoding="utf-8",
        )
        return src

    def test_check_passes_when_in_sync(self, tmp_path: Path):
        src = self._make_source(tmp_path)
        out = tmp_path / "docs"
        mkdocs = tmp_path / "mkdocs.yml"
        mkdocs.write_text(SAMPLE_MKDOCS, encoding="utf-8")
        generate(
            source_dir=src,
            output_dir=out,
            force=True,
            mkdocs_path=mkdocs,
            update_nav=True,
        )
        # A genuinely in-sync repo also has the hand-authored pages present.
        for name in HAND_AUTHORED_MODULES:
            (out / f"{name}.md").write_text(f"# {name}\n", encoding="utf-8")
        up_to_date, messages = check_docs(
            source_dir=src, output_dir=out, mkdocs_path=mkdocs
        )
        assert up_to_date, messages
        assert messages == []

    def test_check_detects_missing_hand_authored_page(self, tmp_path: Path):
        """A deleted hand-authored page is flagged as drift."""
        src = self._make_source(tmp_path)
        out = tmp_path / "docs"
        mkdocs = tmp_path / "mkdocs.yml"
        mkdocs.write_text(SAMPLE_MKDOCS, encoding="utf-8")
        generate(
            source_dir=src,
            output_dir=out,
            force=True,
            mkdocs_path=mkdocs,
            update_nav=True,
        )
        # Deliberately do NOT create the hand-authored pages.
        up_to_date, messages = check_docs(
            source_dir=src, output_dir=out, mkdocs_path=mkdocs
        )
        assert not up_to_date
        assert any("numpy.md" in m for m in messages)

    def test_check_detects_stale_page(self, tmp_path: Path):
        src = self._make_source(tmp_path)
        out = tmp_path / "docs"
        mkdocs = tmp_path / "mkdocs.yml"
        mkdocs.write_text(SAMPLE_MKDOCS, encoding="utf-8")
        generate(
            source_dir=src,
            output_dir=out,
            force=True,
            mkdocs_path=mkdocs,
            update_nav=True,
        )
        (out / "greet.md").write_text("stale content\n", encoding="utf-8")
        up_to_date, messages = check_docs(
            source_dir=src, output_dir=out, mkdocs_path=mkdocs
        )
        assert not up_to_date
        assert any("greet.md" in m for m in messages)

    def test_check_detects_stale_nav(self, tmp_path: Path):
        src = self._make_source(tmp_path)
        out = tmp_path / "docs"
        mkdocs = tmp_path / "mkdocs.yml"
        mkdocs.write_text(SAMPLE_MKDOCS, encoding="utf-8")
        generate(
            source_dir=src,
            output_dir=out,
            force=True,
            mkdocs_path=mkdocs,
            update_nav=False,  # leave nav stale on purpose
        )
        up_to_date, messages = check_docs(
            source_dir=src, output_dir=out, mkdocs_path=mkdocs
        )
        assert not up_to_date
        assert any("nav" in m for m in messages)

    def test_check_writes_nothing(self, tmp_path: Path):
        src = self._make_source(tmp_path)
        out = tmp_path / "docs"
        mkdocs = tmp_path / "mkdocs.yml"
        mkdocs.write_text(SAMPLE_MKDOCS, encoding="utf-8")
        generate(
            source_dir=src,
            output_dir=out,
            force=True,
            mkdocs_path=mkdocs,
            update_nav=True,
        )
        snapshot = {p: p.read_text(encoding="utf-8") for p in out.glob("*.md")}
        mk_before = mkdocs.read_text(encoding="utf-8")
        check_docs(source_dir=src, output_dir=out, mkdocs_path=mkdocs)
        for p, content in snapshot.items():
            assert p.read_text(encoding="utf-8") == content
        assert mkdocs.read_text(encoding="utf-8") == mk_before
