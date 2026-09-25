"""Tests for generate_stdlib_docs.py — stdlib API reference generator."""

import re
import textwrap
from pathlib import Path

import pytest

import build_tools.generate_stdlib_docs as generator
from build_tools.generate_stdlib_docs import (
    HAND_AUTHORED_MODULES,
    DocMember,
    DocModule,
    DocParam,
    DocType,
    _collect_doc_lines,
    _count_code_braces,
    _find_nonpublic_class_ranges,
    _find_public_class_ranges,
    _fixup_prose,
    _parse_params,
    _parse_xml_doc,
    _replace_child_block,
    _split_generic_args,
    _strip_xml_tags,
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

    def test_scg_list(self):
        assert map_type("SCG.List<string>") == "list[str]"

    def test_scg_dictionary(self):
        """CLR Dictionary keeps its honest identity in docs (#1517) — it is not a Sharpy dict."""
        assert map_type("SCG.Dictionary<string, int>") == "Dictionary[str, int]"

    def test_scg_dictionary_nested(self):
        assert map_type("SCG.Dictionary<string, SCG.List<int>>") == "Dictionary[str, list[int]]"

    def test_scg_ienumerable(self):
        assert map_type("SCG.IEnumerable<IPv4Address>") == "Iterable[IPv4Address]"

    def test_system_collections_generic_list(self):
        assert map_type("System.Collections.Generic.List<object>") == "list[object]"

    def test_system_collections_generic_dictionary(self):
        assert map_type("System.Collections.Generic.Dictionary<string, string>") == "Dictionary[str, str]"

    def test_unknown_alias_passthrough(self):
        assert map_type("UnknownAlias.Something<int>") == "UnknownAlias.Something[int]"

    # #1911: a module-defined type reads as its Sharpy name — bare on its own page,
    # module-qualified elsewhere — never the leaked C# `Sharpy.<M>Module.<T>`.
    def test_module_type_on_own_page_is_bare(self):
        assert map_type("Sharpy.OsModule.StatResult", current_module="os") == "StatResult"

    def test_module_type_on_other_page_is_qualified(self):
        # Cross-module positive control: no such reference exists in the docs today, so this cell
        # is the one that proves the else-branch fires (os.StatResult on the csv page).
        assert map_type("Sharpy.OsModule.StatResult", current_module="csv") == "os.StatResult"


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

    def test_verbatim_prefix_stripped_from_name(self):
        # `@default`/`@base` are C# verbatim identifiers; the Sharpy name has no `@` (#2034).
        params = _parse_params("K key, V @default")
        assert [p.name for p in params] == ["key", "default"]
        assert params[1].default is None

    def test_null_forgiving_default_mapped_to_none(self):
        # `default!` / `default` / `null!` are C# spellings of "no value" (#2034).
        assert _parse_params("V value = default!")[0].default == "None"
        assert _parse_params("V value = default")[0].default == "None"
        assert _parse_params("string s = null!")[0].default == "None"

    def test_true_default_mapped_to_True(self):
        params = _parse_params("bool enable = true")
        assert params[0].default == "True"

    def test_non_literal_default_preserved(self):
        params = _parse_params("int x = 0")
        assert params[0].default == "0"

    def test_parameter_attribute_is_not_part_of_the_type(self):
        params = _parse_params('object? value, [FormatSpec("value")] string formatSpec = ""')
        assert [(p.name, p.type, p.default) for p in params] == [
            ("value", "object | None", None),
            ("format_spec", "str", '""'),
        ]

    def test_attribute_on_extension_this_is_still_skipped(self):
        params = _parse_params("[FormatTemplate] this string s, params object[] args", is_extension=True)
        assert [p.name for p in params] == ["args"]

    def test_attribute_argument_with_equals_and_comma_is_stripped(self):
        params = _parse_params('[Foo(X = 1, Y = "a,b]")] int count = 3')
        assert [(p.name, p.type, p.default) for p in params] == [("count", "int", "3")]


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

    def test_method_with_globally_qualified_return_type(self, tmp_path):
        # Since #1830/#1831/R-AQ, every CLR-backed/Sharpy-namespace type is
        # emitted fully qualified in the generated stdlib C# — a method whose
        # return type reads `global::Sharpy.Iterator<T>` must still be parsed
        # (the signature regex accepts ':') and rendered bare (`Iterator[T]`).
        # Regression guard for the itertools docs dropping every function.
        cs = textwrap.dedent(
            """\
            public partial class ItertoolsModule
            {
                /// <summary>Make an iterator of evenly spaced values.</summary>
                public static global::Sharpy.Iterator<int> Count(int start = 0, int step = 1)
                {
                    return null;
                }
            }
        """
        )
        f = tmp_path / "Itertools.cs"
        f.write_text(cs)
        members = parse_cs_file(f)
        assert len(members) == 1, "global::-qualified return type must not drop the method"
        assert members[0].name == "count"
        assert members[0].return_type == "Iterator[int]"

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

    def test_private_readonly_struct_is_nonpublic(self):
        """`private readonly struct` must be flagged — the `readonly` modifier is not one of
        sealed/abstract/static/partial, and missing it leaked the json/yaml IDocumentNode view
        adapters' members (is_mapping, get_child, ...) into docs/stdlib as phantom module API."""
        src = textwrap.dedent(
            """\
            public class Foo
            {
                public int A;
            }
            private readonly struct ViewAdapter
            {
                public bool IsMapping => true;
            }
            internal ref struct Cursor
            {
                public int Position;
            }
            """
        ).split("\n")
        ranges = _find_nonpublic_class_ranges(src)
        assert len(ranges) == 2
        assert "private readonly struct ViewAdapter" in src[ranges[0][0]]
        assert "internal ref struct Cursor" in src[ranges[1][0]]

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


class TestXmlEntitiesAndKeywordScoping:
    """
    #1305 — two artifacts of the extraction pipeline, both untested before this.

    Neither is cosmetic. An entity left raw renders as literal `&lt;` in the published page, so
    every generic mentioned in prose read as `List&lt;char&gt;`. And a keyword mapping applied to
    running English silently edits documentation: `true` and `null` are ordinary words.
    """

    def test_entities_are_unescaped_after_tags_are_stripped(self):
        # Order matters: unescaping first would turn `&lt;c&gt;` into a tag for the stripper to eat.
        assert _strip_xml_tags("A <c>List&lt;char&gt;</c> of things") == "A `List<char>` of things"

    def test_ampersand_entity_is_unescaped(self):
        assert _strip_xml_tags("this &amp; that") == "this & that"

    def test_nested_generics_survive(self):
        assert _strip_xml_tags(
            "<c>Dict&lt;str, List&lt;int&gt;&gt;</c>") == "`Dict<str, List<int>>`"

    def test_camel_case_param_keeps_its_description(self, tmp_path):
        """A documented parameter whose C# name is more than one word (#1421).

        The description is merged onto the parsed parameter BY NAME, and the two sides used to
        spell it differently: `_parse_params` stores `pascal_to_snake(pname)` while the merge
        keyed on the raw XML `name`. So `doc_params["allowNan"]` was never found under
        `"allow_nan"` and the text was replaced with "". Single-word names were unaffected,
        which is why the result read as "some parameters just aren't documented".
        """
        cs = textwrap.dedent(
            """\
            public partial class JsonModule
            {
                /// <summary>Serialize obj.</summary>
                /// <param name="obj">The object to serialize.</param>
                /// <param name="allowNan">When true, Infinity and NaN are emitted as tokens.</param>
                public static string Dumps(object obj, bool allowNan = true)
                {
                    return "";
                }
            }
        """
        )
        f = tmp_path / "Json.cs"
        f.write_text(cs)
        members = parse_cs_file(f)
        params = {p.name: p.description for p in members[0].params}

        assert params["allow_nan"] == "When true, Infinity and NaN are emitted as tokens."
        # The single-word control: it never broke, and must not start.
        assert params["obj"] == "The object to serialize."

    def test_keyword_mapping_applies_to_all_prose_deliberately(self):
        # NOT scoped to code spans, and measured rather than assumed (#1305). The source is C#
        # XML doc comments: `true` and `null` in running prose mean the literal far more often
        # than the English word, so scoping regressed real sentences —
        #   "Return True if all elements of the iterable are True" -> "... are true"
        #   "Returns False if the string is None or empty"         -> "... is null or empty"
        # — while protecting a hazard a full regeneration sweep found zero live instances of.
        assert _fixup_prose("`x is true`") == "`x is True`"
        assert _fixup_prose("returns true when empty") == "returns True when empty"
        assert _fixup_prose("the value is null") == "the value is None"

    def test_global_prefix_is_stripped_everywhere(self):
        assert _fixup_prose("see global::Sharpy.List") == "see Sharpy.List"
        assert _fixup_prose("`global::Sharpy.List`") == "`Sharpy.List`"


# ---------------------------------------------------------------------------
# EditorBrowsable(Never) members are not public surface (#1614 InPlaceRepeat)
# ---------------------------------------------------------------------------


def test_editor_browsable_never_member_is_skipped(tmp_path: Path) -> None:
    src = textwrap.dedent(
        """
        namespace Sharpy
        {
            public partial class List<T>
            {
                /// <summary>Extend the list.</summary>
                public void Extend(IEnumerable<T> items) { }

                /// <summary>Compiler-only mutator for `list *=`.</summary>
                [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
                public void InPlaceRepeat(int n) { }

                [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
                /// <summary>Attribute above the doc block is seen too.</summary>
                public int HiddenCount { get; }

                /// <summary>Visible property.</summary>
                public int Count { get; }
            }
        }
        """
    )
    f = tmp_path / "List.Methods.cs"
    f.write_text(src)
    names = [m.name for m in parse_cs_file(f)]
    assert "extend" in names
    assert "count" in names
    # Mutation: drop `_is_hidden_from_surface` from any member site -> this goes red.
    assert "in_place_repeat" not in names
    assert "hidden_count" not in names


class TestVariadicAndKeywordReferenceRendering:
    """
    Two renderings that published a signature and a sentence no reader could act on
    (2026-09-11 P9 audit, plan-a79696 §7).

    A `params T[]` formal is a VARIADIC at the Sharpy surface: the caller writes
    `s.union(a, b)`. It rendered as `union(others: list[Iterable[T]])` because the array-suffix
    branch of `map_type` ran before the `params`-prefix branch, stripping the `[]` and then
    wrapping the result in `list[...]` — the exact opposite of what the method accepts.

    A `<see langword="..."/>` is a C# keyword reference. Nothing handled it, so the catch-all
    tag stripper ate it whole and `builtins.md` read "Sums a sequence of booleans, counting  as
    1." with the subject of the sentence gone.
    """

    def test_params_array_renders_as_a_variadic_not_a_list(self):
        assert map_type("params IEnumerable<T>[]") == "*Iterable[T]"

    def test_params_of_a_scalar_renders_as_a_variadic(self):
        assert map_type("params int[]") == "*int"

    def test_a_plain_array_is_still_a_list(self):
        # The positive control for the branch ORDER: moving `params` first must not stop an
        # ordinary array from rendering as list[...].
        assert map_type("int[]") == "list[int]"
        assert map_type("IEnumerable<T>[]") == "list[Iterable[T]]"

    def test_langword_reference_survives_tag_stripping(self):
        assert _strip_xml_tags('counting <see langword="true"/> as 1') == "counting `true` as 1"

    def test_langword_reference_is_pythonified_in_prose(self):
        # The rendered page runs prose through _fixup_prose, which is what turns the C# keyword
        # into the Sharpy spelling. Asserted through both stages because either alone would leave
        # a C# keyword in the docs.
        assert _fixup_prose(
            _strip_xml_tags('counting <see langword="true"/> as 1')) == "counting `True` as 1"

    def test_cref_reference_still_renders(self):
        # Positive control: the new langword rule sits beside the cref rule and must not shadow it.
        assert _strip_xml_tags('see <see cref="Foo.Bar"/> for more') == "see `Foo.Bar` for more"

    def test_variadic_star_moves_to_the_parameter_name(self, tmp_path):
        """`*others: Iterable[T]`, never `others: *Iterable[T]`.

        map_type marks the TYPE because that is where C# puts the `params` keyword; the signature
        renderer moves the star onto the name, which is where Python puts it. Asserted through
        parse_cs_file so the signature is the one the generator BUILDS, not one restated here.
        """
        cs = textwrap.dedent(
            """\
            public partial class SetModule
            {
                /// <summary>Union with every other iterable.</summary>
                /// <param name="others">The iterables to union with.</param>
                public static int Union(params int[] others)
                {
                    return 0;
                }
            }
        """
        )
        f = tmp_path / "SetModule.cs"
        f.write_text(cs)
        members = parse_cs_file(f)
        assert len(members) == 1
        assert members[0].signature == "union(*others: int) -> int"


# ---------------------------------------------------------------------------
# #1980: members attributed by declaring type; per-class summaries/remarks;
# `=>`-bodied and tuple-returning signatures; ToString renders as __str__
# ---------------------------------------------------------------------------


class TestDeclaringTypeAttribution:
    """A member's owner is a parse fact (the brace range it is declared in), not a file position."""

    # Shaped like src/Sharpy.Stdlib/Collections/Collections.cs: three annotated classes, each with
    # its own summary, then an UN-annotated module-exports class trailing the last annotated one.
    _MULTI = textwrap.dedent(
        """\
        using System;
        namespace Sharpy
        {
            /// <summary>A double-ended queue.</summary>
            [SharpyModuleType("collections", "Deque")]
            public class Deque<T>
            {
                /// <summary>Append x.</summary>
                public void Append(T x) { }
            }

            /// <summary>A dict subclass for counting hashable objects.</summary>
            /// <remarks>Iterates over the KEYS in first-seen order.</remarks>
            [SharpyModuleType("collections", "Counter")]
            public class Counter<T> where T : notnull
            {
                /// <summary>Return the n most common elements and their counts.</summary>
                public Sharpy.List<(T, int)> MostCommon(int? n = null)
                {
                    return new Sharpy.List<(T, int)>();
                }

                /// <summary>The (element, count) pairs.</summary>
                public List<(T, int)> Items() => new List<(T, int)>(_counts.Select(kv => (kv.Key, kv.Value)));

                /// <summary>The keys.</summary>
                public List<T> Keys() => new List<T>(_counts.Keys);

                /// <summary>Whether key is counted.</summary>
                public bool Contains(T key) => _counts.ContainsKey(key);

                /// <summary>Python's repr.</summary>
                /// <remarks>Most-common order.</remarks>
                public override string ToString() => "Counter()";

                /// <inheritdoc/>
                public override int GetHashCode() => 0;

                /// <summary>Content equality.</summary>
                public override bool Equals(object? obj) => false;
            }

            /// <summary>A dict that calls a factory for missing keys.</summary>
            [SharpyModuleType("collections", "DefaultDict")]
            public class DefaultDict<K, V>
            {
                /// <summary>The default factory.</summary>
                public Func<V> DefaultFactory { get; }
            }

            /// <summary>Module exports for collections.</summary>
            public static partial class Collections
            {
                /// <summary>The Deque type.</summary>
                public static Type DequeType => typeof(Deque<>);
            }
        }
        """
    )

    def _module(self, tmp_path: Path) -> DocModule:
        TestDiscoverModulesTypeAnnotations._write_module(
            None, tmp_path, "Collections", "collections", "Collections.cs", self._MULTI
        )
        modules = discover_modules(tmp_path)
        assert len(modules) == 1
        return modules[0]

    def _type(self, module: DocModule, name: str) -> DocType:
        return next(t for t in module.types if t.name == name)

    def _sigs(self, doc_type: DocType) -> list[str]:
        return [m.signature for m in doc_type.members if m.kind == "method"]

    def test_find_public_class_ranges_nested_and_trailing(self):
        lines = textwrap.dedent(
            """\
            namespace N
            {
                public class Outer
                {
                    public int A() => 1;
                    public struct Inner
                    {
                        public int B() => 2;
                    }
                }
                public static class Trailing
                {
                }
            }
            """
        ).split("\n")
        assert _find_public_class_ranges(lines) == [
            ("Outer", 2, 9),
            ("Inner", 5, 8),
            ("Trailing", 10, 12),
        ]

    def test_each_class_reads_its_own_summary(self, tmp_path: Path):
        module = self._module(tmp_path)
        assert [t.name for t in module.types] == ["Deque", "Counter", "DefaultDict"]
        assert self._type(module, "Deque").summary == "A double-ended queue."
        assert self._type(module, "Counter").summary == "A dict subclass for counting hashable objects."
        assert self._type(module, "DefaultDict").summary == "A dict that calls a factory for missing keys."

    def test_unannotated_trailing_class_is_module_level_not_folded(self, tmp_path: Path):
        module = self._module(tmp_path)
        default_dict = self._type(module, "DefaultDict")
        assert [m.name for m in default_dict.members] == ["default_factory"]
        assert [m.name for m in module.members] == ["deque_type"]

    def test_expression_bodied_signatures_stop_at_the_arrow(self, tmp_path: Path):
        sigs = self._sigs(self._type(self._module(tmp_path), "Counter"))
        assert "keys() -> list[T]" in sigs
        assert "contains(key: T) -> bool" in sigs
        assert not any("=>" in s or "= >" in s or ")) ->" in s for s in sigs)

    def test_tuple_returning_members_are_present(self, tmp_path: Path):
        sigs = self._sigs(self._type(self._module(tmp_path), "Counter"))
        assert "most_common(n: int | None = None) -> list[tuple[T, int]]" in sigs
        assert "items() -> list[tuple[T, int]]" in sigs

    def test_tostring_renders_one_str_row_with_its_remarks(self, tmp_path: Path):
        counter = self._type(self._module(tmp_path), "Counter")
        names = [m.name for m in counter.members if m.kind == "method"]
        # Positive control and absence in one input: ToString renders, Equals/GetHashCode do not.
        assert names.count("__str__") == 1
        assert "__eq__" not in names and "__hash__" not in names

        page = render_module_page(self._module(tmp_path))
        section = page.split("## Counter", 1)[1].split("## DefaultDict", 1)[0]
        assert "### `__str__() -> str`\n\n`repr()` uses the same method. Python's repr.\n" in section
        assert "!!! note\n    Most-common order." in section

    def test_iformattable_tostring_renders_as_format_not_a_second_str(self, tmp_path: Path):
        """`ToString(format, provider)` is IFormattable's method — the CLR spelling of `__format__`
        (dunder_methods.md) — so it is its own `__format__` row, not a second `__str__` carrying the
        repr note. The arity-0 `ToString` in the same class is the positive control."""
        body = textwrap.dedent(
            """\
            using System;
            namespace Sharpy
            {
                /// <summary>A complex number.</summary>
                [SharpyModuleType("cmath", "Complex")]
                public class Complex : IFormattable
                {
                    /// <summary>Python's repr.</summary>
                    public override string ToString() => "";

                    /// <summary>Python's complex.__format__.</summary>
                    /// <param name="format">The format spec.</param>
                    /// <param name="formatProvider">Ignored.</param>
                    public string ToString(string? format, IFormatProvider? formatProvider) => "";
                }
            }
            """
        )
        TestDiscoverModulesTypeAnnotations._write_module(None, tmp_path, "Cmath", "cmath", "Complex.cs", body)
        modules = discover_modules(tmp_path)
        complex_type = self._type(modules[0], "Complex")
        sigs = self._sigs(complex_type)
        assert "__str__() -> str" in sigs
        assert "__format__(format_spec: str) -> str" in sigs
        assert [m.name for m in complex_type.members if m.kind == "method"].count("__str__") == 1
        fmt = next(m for m in complex_type.members if m.name == "__format__")
        assert "repr()" not in fmt.summary
        assert fmt.params[0].description == "The format spec."

    def test_class_remarks_render_as_a_note_under_the_heading(self, tmp_path: Path):
        module = self._module(tmp_path)
        assert self._type(module, "Counter").remarks == "Iterates over the KEYS in first-seen order."
        page = render_module_page(module)
        assert (
            "## Counter\n\nA dict subclass for counting hashable objects.\n\n"
            "!!! note\n    Iterates over the KEYS in first-seen order.\n"
        ) in page

    def test_nested_public_type_members_are_not_the_parents(self, tmp_path: Path):
        body = textwrap.dedent(
            """\
            namespace Sharpy
            {
                /// <summary>Outer summary.</summary>
                [SharpyModuleType("mod", "Outer")]
                public class Outer
                {
                    /// <summary>Outer method.</summary>
                    public int Before() => 1;

                    /// <summary>Nested.</summary>
                    public struct KeyEnumerator
                    {
                        /// <summary>Nested method.</summary>
                        public bool MoveNext() => false;
                    }

                    /// <summary>Outer method after the nested type.</summary>
                    public int After() => 2;
                }
            }
            """
        )
        TestDiscoverModulesTypeAnnotations._write_module(None, tmp_path, "Mod", "mod", "Outer.cs", body)
        outer = discover_modules(tmp_path)[0].types[0]
        assert [m.name for m in outer.members] == ["before", "after"]

    def test_named_tuple_elements_render_their_types_only(self):
        assert map_type("(int day, int weekday)") == "tuple[int, int]"
        assert map_type("List<(int a, int b, int size)>") == "list[tuple[int, int, int]]"
        assert map_type("(string? stdout, string? stderr)") == "tuple[str | None, str | None]"
        assert map_type("(Bytes data, (string host, int port) addr)") == "tuple[Bytes, tuple[str, int]]"
        assert map_type("(T, int)") == "tuple[T, int]"

    def test_module_level_tostring_is_not_a_module_function(self, tmp_path: Path):
        """An un-annotated class's members render at module level; its ToString must not become
        `mod.__str__()`. The annotated class's ToString in the same file is the positive control."""
        body = textwrap.dedent(
            """\
            namespace Sharpy
            {
                /// <summary>A message.</summary>
                [SharpyModuleType("email", "EmailMessage")]
                public class EmailMessage
                {
                    /// <summary>Message repr.</summary>
                    public override string ToString() => "";
                }

                /// <summary>An attachment.</summary>
                public class Attachment
                {
                    /// <summary>The file name.</summary>
                    public string Filename { get; }

                    /// <summary>Attachment repr.</summary>
                    public override string ToString() => "";
                }
            }
            """
        )
        TestDiscoverModulesTypeAnnotations._write_module(None, tmp_path, "Email", "email", "EmailMessage.cs", body)
        (tmp_path / "Email" / "Plain.cs").write_text(
            textwrap.dedent(
                """\
                namespace Sharpy
                {
                    public class Policy
                    {
                        /// <summary>Policy repr.</summary>
                        public override string ToString() => "";

                        /// <summary>Clone.</summary>
                        public Policy Clone() => this;
                    }
                }
                """
            ),
            encoding="utf-8",
        )
        module = discover_modules(tmp_path)[0]
        assert [m.name for m in module.types[0].members] == ["__str__"]
        assert sorted(m.name for m in module.members) == ["clone", "filename"]

    def test_core_type_page_excludes_a_nested_enumerator(self, tmp_path: Path):
        from build_tools.generate_stdlib_docs import discover_core_types

        subdir = tmp_path / "Partial.List"
        subdir.mkdir()
        (subdir / "List.Enumerator.cs").write_text(
            textwrap.dedent(
                """\
                namespace Sharpy
                {
                    public partial class List<T>
                    {
                        /// <summary>Append x.</summary>
                        public void Append(T x) { }

                        public struct Enumerator
                        {
                            /// <summary>Reset the enumerator.</summary>
                            public void Reset() { }
                        }

                        /// <summary>Clear.</summary>
                        public void Clear() { }
                    }
                }
                """
            ),
            encoding="utf-8",
        )
        page = next(t for t in discover_core_types(tmp_path) if t.name == "list")
        assert [m.name for m in page.members] == ["append", "clear"]


# ---------------------------------------------------------------------------
# C#-spelling scan over rendered signatures (#2034)
# ---------------------------------------------------------------------------

_REPO_ROOT = Path(__file__).resolve().parents[2]

# A rendered signature is a `### \`...\`` heading line. The scan is anchored to those lines: a
# bare `@\w+:` over whole pages also matches prose such as urllib's `user:pass@host:8080`.
_SIGNATURE_HEADING = "### `"

_CSHARP_SPELLINGS = {
    "verbatim-identifier": re.compile(r"@\w+:"),
    "default!": re.compile(r"= default!"),
    "default": re.compile(r"= default\b"),
    "null-forgiving": re.compile(r"\w!(?=[,)])"),
}


def _csharp_spelling_hits(page_name: str, text: str) -> list[str]:
    hits = []
    for lineno, line in enumerate(text.splitlines(), start=1):
        if not line.startswith(_SIGNATURE_HEADING):
            continue
        for label, pattern in _CSHARP_SPELLINGS.items():
            if pattern.search(line):
                hits.append(f"{page_name}:{lineno} [{label}] {line}")
    return hits


def _render_real_stdlib(out_dir: Path) -> list[Path]:
    """Every generator-owned page, rendered from the repository's Core + Stdlib source."""
    generate(
        source_dir=_REPO_ROOT / "src" / "Sharpy.Core",
        output_dir=out_dir,
        force=True,
        verbose=False,
        stdlib_dir=_REPO_ROOT / "src" / "Sharpy.Stdlib",
        update_nav=False,
    )
    return sorted(out_dir.glob("*.md"))


def _render_synthetic(tmp_path: Path) -> str:
    src = tmp_path / "src"
    mod_dir = src / "Probe"
    mod_dir.mkdir(parents=True)
    (mod_dir / "__Init__.cs").write_text(
        textwrap.dedent(
            """\
            namespace Sharpy;
            /// <summary>Probe.</summary>
            [SharpyModule("probe")]
            public static partial class ProbeModule
            {
                /// <summary>Lookup.</summary>
                public static V Lookup<V>(string key, V @default = default!) => @default;
                /// <summary>Parse.</summary>
                public static int Parse(string s, int @base = 10, string? tag = default) => 0;
                /// <summary>Name.</summary>
                public static string Name(string s = null!) => s;
            }
            """
        ),
        encoding="utf-8",
    )
    out = tmp_path / "docs"
    generate(source_dir=src, output_dir=out, force=True, verbose=False, update_nav=False)
    return (out / "probe.md").read_text(encoding="utf-8")


class TestCSharpSpellingScan:
    """No rendered signature on a generator-owned page carries a C# spelling (#2034)."""

    def test_generator_owned_pages_have_no_csharp_spellings(self, tmp_path: Path):
        pages = _render_real_stdlib(tmp_path / "stdlib")
        assert pages, "the generator rendered no pages from the repository source"
        assert not any(p.stem in HAND_AUTHORED_MODULES for p in pages)
        hits = [h for p in pages for h in _csharp_spelling_hits(p.name, p.read_text(encoding="utf-8"))]
        assert hits == []

    def test_synthetic_csharp_spellings_render_as_sharpy(self, tmp_path: Path):
        page = _render_synthetic(tmp_path)
        assert "### `probe.lookup(key: str, default: V = None) -> V`" in page
        assert "### `probe.parse(s: str, base: int = 10, tag: str | None = None) -> int`" in page
        assert "### `probe.name(s: str = None) -> str`" in page
        assert _csharp_spelling_hits("probe.md", page) == []

    def test_positive_control_scan_hits_when_the_mapping_is_disabled(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ):
        # The same synthetic source with the Sharpy-spelling rules turned off: every C# spelling
        # the scan names must be found, or the scan is vacuous.
        monkeypatch.setattr(generator, "_sharpy_param_name", generator.pascal_to_snake)
        monkeypatch.setattr(generator, "_sharpy_default", lambda raw: raw)
        hits = _csharp_spelling_hits("probe.md", _render_synthetic(tmp_path))
        labels = {h.split("[", 1)[1].split("]", 1)[0] for h in hits}
        assert labels == set(_CSHARP_SPELLINGS), hits
