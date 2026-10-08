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
    sharpy_field_name,
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


class TestSharpyFieldName:
    """The compiler's static-field rule (#2264): a recorded name wins, CONSTANT_CASE longer than one
    character is kept, anything else is snake-cased."""

    @pytest.mark.parametrize(
        "cs, sharpy",
        [
            ("PIPE", "PIPE"),
            ("TIMEOUT_MAX", "TIMEOUT_MAX"),
            ("Z_BEST_SPEED", "Z_BEST_SPEED"),
            ("NAMESPACE_X500", "NAMESPACE_X500"),
            ("Utc", "utc"),
            ("DayName", "day_name"),
            ("E", "e"),  # one character is never CONSTANT_CASE (math.e)
            ("Pi", "pi"),
        ],
    )
    def test_field_rule(self, cs: str, sharpy: str):
        assert sharpy_field_name(cs) == sharpy

    def test_recorded_name_wins(self):
        assert sharpy_field_name("I", ['[SharpyFieldName("i")]']) == "i"
        assert sharpy_field_name("Row", ['[SharpyFieldNameAttribute("row_factory")]']) == "row_factory"

    def test_other_attributes_do_not_rename(self):
        assert sharpy_field_name("PIPE", ["[Obsolete]"]) == "PIPE"

    def test_discovery_applies_the_field_rule_and_the_type_collision(self, tmp_path: Path):
        """A module const, a static property and a field named like a module type, through discovery."""
        sub = tmp_path / "Demo"
        sub.mkdir()
        (sub / "__Init__.cs").write_text(
            textwrap.dedent(
                """\
                using Sharpy.Core.Shared;
                namespace Sharpy.Core.Demo;

                [SharpyModule("demo")]
                public static partial class DemoModule
                {
                    /// <summary>A pipe.</summary>
                    public const int PIPE = -1;
                    /// <summary>A level.</summary>
                    public static int Z_BEST_SPEED => 1;
                    /// <summary>A day list.</summary>
                    public static readonly int DayCount = 7;
                    /// <summary>The row factory.</summary>
                    public static readonly int Row = 0;
                }
                """
            )
        )
        (sub / "Row.cs").write_text(
            textwrap.dedent(
                """\
                using Sharpy.Core.Shared;
                namespace Sharpy.Core.Demo;

                [SharpyModuleType("demo")]
                public sealed class Row
                {
                }
                """
            )
        )
        (module,) = discover_modules(tmp_path)
        names = {m.name for m in module.members}
        assert {"PIPE", "Z_BEST_SPEED", "day_count", "Row"} <= names
        assert not {"pipe", "z_best_speed", "row"} & names


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
        # The primary name the compiler prints, not the C# keyword alias (#2066).
        assert map_type("long") == "int64"

    @pytest.mark.parametrize(
        "cs,sharpy",
        [
            ("sbyte", "int8"),
            ("short", "int16"),
            ("Int64", "int64"),
            ("byte", "uint8"),
            ("ushort", "uint16"),
            ("uint", "uint32"),
            ("ulong", "uint64"),
            ("System.UInt64", "uint64"),
            ("Bytes", "bytes"),
            ("Sharpy.Bytes", "bytes"),
            ("Bytes?", "bytes | None"),
            ("Slice", "slice"),
        ],
    )
    def test_integer_widths_and_builtin_clr_names(self, cs: str, sharpy: str):
        assert map_type(cs) == sharpy

    def test_nullable_function_type_is_grouped(self):
        assert map_type("Func<object, object?>?") == "((object) -> object | None) | None"
        assert map_type("Action<int>?") == "((int) -> None) | None"
        assert map_type("Func<int, string?>") == "(int) -> str | None"
        # A parenthesized non-function (a value tuple) is not re-grouped.
        assert map_type("(int, string)?") == "tuple[int, str] | None"

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
        assert map_type("IEnumerable<int>") == "IEnumerable[int]"

    def test_nested_generics(self):
        assert map_type("List<Dict<string, int>>") == "list[dict[str, int]]"

    def test_array(self):
        assert map_type("int[]") == "array[int]"

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
        assert map_type("System.Collections.Generic.IEnumerable<int>") == "IEnumerable[int]"

    def test_system_value_tuple_generic(self):
        assert map_type("System.ValueTuple<str, str>") == "tuple[str, str]"

    def test_value_tuple_cs_syntax(self):
        """C# value-tuple syntax (T1, T2) should map to tuple[T1, T2]."""
        assert map_type("(string, int)") == "tuple[str, int]"

    def test_ienumerable_of_tuple(self):
        """IEnumerable<(TKey, TValue)> should map to IEnumerable[tuple[TKey, TValue]]."""
        assert map_type("IEnumerable<(string, int)>") == "IEnumerable[tuple[str, int]]"

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
        assert map_type("SCG.IEnumerable<IPv4Address>") == "IEnumerable[IPv4Address]"

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

    def test_receiver_outside_an_extension_parse_is_an_ordinary_parameter(self):
        # `this` is a C# modifier, never part of the rendered type (#2055).
        params = _parse_params("this NdArray<double> a, int axis")
        assert [(p.name, p.type) for p in params] == [("a", "NdArray[float]"), ("axis", "int")]

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
        self,
        tmp_path: Path,
        mod_dir: str,
        mod_name: str,
        filename: str,
        body: str,
        module_class: "str | None" = None,
    ) -> Path:
        # A module's functions are the `[SharpyModule]` class's, in whichever file declares a partial
        # of it (#2272): a fixture whose body declares the module's functions names that class.
        subdir = tmp_path / mod_dir
        subdir.mkdir(parents=True, exist_ok=True)
        (subdir / "__Init__.cs").write_text(
            textwrap.dedent(
                f"""\
                using Sharpy.Core.Shared;
                namespace Sharpy.Core.{mod_dir};

                [SharpyModule("{mod_name}")]
                public static partial class {module_class or mod_dir + "Module"} {{ }}
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

    def test_named_message_name_argument_is_accepted(self, tmp_path: Path):
        """A trailing `MessageName = "..."` (#2035) keeps the type discovered under its display name."""
        body = textwrap.dedent(
            """\
            using Sharpy.Core.Shared;
            namespace Sharpy.Core.Datetime;

            [SharpyModuleType("datetime", "timedelta", MessageName = "datetime.timedelta")]
            public sealed class Timedelta
            {
                /// <summary>Days.</summary>
                public int Days => 0;
            }

            [SharpyModuleType("datetime", "timezone")]
            public sealed class Timezone
            {
                /// <summary>Utc.</summary>
                public static Timezone Utc => default!;
            }
            """
        )
        self._write_module(tmp_path, "Datetime", "datetime", "Datetime.cs", body)
        modules = discover_modules(tmp_path)
        assert len(modules) == 1
        type_names = sorted(t.name for t in modules[0].types)
        assert type_names == ["timedelta", "timezone"]

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

    def test_emitted_global_qualified_spelling_is_a_type(self, tmp_path: Path):
        """A generated spy module's sibling types carry the compiler's `global::Sharpy.`-qualified
        stamp (#2039): each is a type section, its members are not the module's functions."""
        body = textwrap.dedent(
            """\
            namespace Sharpy.SocketModule
            {
                public static partial class SocketModuleModule
                {
                    /// <summary>Return the hostname.</summary>
                    public static string Gethostname() => "";
                }

                /// <summary>Base exception for socket errors.</summary>
                [global::Sharpy.SharpyModuleType("socket", "error")]
                [global::Sharpy.SharpyName("error")]
                public class Error : global::System.Exception
                {
                    /// <summary>The errno.</summary>
                    public int Errno => 0;
                }
            }
            """
        )
        self._write_module(
            tmp_path, "Socket", "socket", "SocketModule.cs", body, module_class="SocketModuleModule"
        )
        modules = discover_modules(tmp_path)
        assert len(modules) == 1
        assert [t.name for t in modules[0].types] == ["error"]
        assert modules[0].types[0].summary == "Base exception for socket errors."
        member_names = [m.name for m in modules[0].members]
        assert "Gethostname" in member_names or "gethostname" in member_names
        assert not any(n.lower() == "errno" for n in member_names)


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


# The real allowlist is empty since #2055 (every stdlib module is generator-owned), so the
# mechanism is exercised through a fabricated entry.
_HANDMADE = "handmade"


@pytest.fixture
def hand_authored_page(monkeypatch: pytest.MonkeyPatch) -> str:
    monkeypatch.setitem(
        generator.HAND_AUTHORED_MODULES,
        _HANDMADE,
        {"description": "A page written by hand.", "nav_title": _HANDMADE},
    )
    return _HANDMADE


class TestHandAuthoredModules:
    """The hand-authored allowlist controls index merging and page skipping."""

    def test_no_discovered_module_is_hand_authored(self):
        # A module the generator discovers must be generator-owned: only a generated page is
        # covered by the C#-spelling scan and the staleness check (#2055).
        discovered = {
            m.name
            for root in ("Sharpy.Core", "Sharpy.Stdlib")
            for m in discover_modules(_REPO_ROOT / "src" / root)
        } | {t.name for t in generator.discover_core_types(_REPO_ROOT / "src" / "Sharpy.Core")}
        # Positive control: the three modules that used to be hand-authored are discovered.
        assert {"numpy", "requests", "sqlite3"} <= discovered
        assert discovered & set(HAND_AUTHORED_MODULES) == set()

    @pytest.mark.usefixtures("hand_authored_page")
    def test_index_merges_hand_authored(self):
        """Hand-authored modules appear in the index even with no generated module."""
        builtins = DocModule(name="builtins", kind="builtins", members=[])
        core_types = [DocModule(name="list", kind="type", summary="A list.")]
        modules = [DocModule(name="math", kind="module", summary="Math functions.")]
        output = render_index_page(builtins, core_types, modules)
        assert "[`math`](math.md)" in output
        # A hand-authored page must still be linked from the index.
        assert "[`handmade`](handmade.md)" in output

    @pytest.mark.usefixtures("hand_authored_page")
    def test_index_modules_sorted(self):
        builtins = DocModule(name="builtins", kind="builtins", members=[])
        modules = [
            DocModule(name="zzz", kind="module", summary="Z."),
            DocModule(name="aaa", kind="module", summary="A."),
        ]
        output = render_index_page(builtins, [], modules)
        # aaa before zzz, and the hand-authored page slotted in sorted order.
        i_aaa = output.index("[`aaa`]")
        i_handmade = output.index("[`handmade`]")
        i_zzz = output.index("[`zzz`]")
        assert i_aaa < i_handmade < i_zzz

    @pytest.mark.usefixtures("hand_authored_page")
    def test_generate_skips_hand_authored_pages(self, tmp_path: Path):
        """Even with force, a hand-authored page is never (re)written."""
        src = tmp_path / "src"
        mod_dir = src / "Handmade"
        mod_dir.mkdir(parents=True)
        (mod_dir / "__Init__.cs").write_text(
            textwrap.dedent(
                """\
                namespace Sharpy;
                [SharpyModule("handmade")]
                public static partial class HandmadeModule { }
                """
            ),
            encoding="utf-8",
        )
        out = tmp_path / "out"
        out.mkdir()
        sentinel = "HAND AUTHORED — DO NOT OVERWRITE\n"
        (out / "handmade.md").write_text(sentinel, encoding="utf-8")

        generate(source_dir=src, output_dir=out, force=True, update_nav=False)

        # The hand-authored page must be untouched.
        assert (out / "handmade.md").read_text(encoding="utf-8") == sentinel


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

    @pytest.mark.usefixtures("hand_authored_page")
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
        assert "      - handmade: stdlib/handmade.md" in module_block
        assert module_block == sorted(module_block)

    @pytest.mark.usefixtures("hand_authored_page")
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
        # The hand-authored page is merged into the nav too.
        assert "- handmade: stdlib/handmade.md" in after_first
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

    @pytest.mark.usefixtures("hand_authored_page")
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

    @pytest.mark.usefixtures("hand_authored_page")
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
        assert any("handmade.md" in m for m in messages)

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
        assert map_type("params IEnumerable<T>[]") == "*IEnumerable[T]"

    def test_params_of_a_scalar_renders_as_a_variadic(self):
        assert map_type("params int[]") == "*int"

    def test_a_plain_array_is_still_an_array(self):
        # The positive control for the branch ORDER: moving `params` first must not stop an
        # ordinary array from rendering as array[...] (#2163: `T[]` is `array[T]`, never `list[T]`).
        assert map_type("int[]") == "array[int]"
        assert map_type("IEnumerable<T>[]") == "array[IEnumerable[T]]"

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
        """`*others: IEnumerable[T]`, never `others: *IEnumerable[T]`.

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
    # its own summary, then a partial of the module class trailing the last annotated one.
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
            None, tmp_path, "Collections", "collections", "Collections.cs", self._MULTI,
            module_class="Collections",
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

    def test_trailing_module_class_partial_is_module_level_not_folded(self, tmp_path: Path):
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
        # `Bytes` is the builtin `bytes` (#2066).
        assert map_type("(Bytes data, (string host, int port) addr)") == "tuple[bytes, tuple[str, int]]"
        assert map_type("(T, int)") == "tuple[T, int]"

    # --- ownership: a member renders where the COMPILER binds it (#2272) -------------------------

    _EMAIL = textwrap.dedent(
        """\
        namespace Sharpy
        {
            /// <summary>The email module.</summary>
            public static partial class EmailModule
            {
                /// <summary>Attach a file.</summary>
                public static Attachment Attach(string name) => new Attachment();
            }

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

                /// <summary>A static factory no Sharpy spelling reaches.</summary>
                public static Attachment Empty() => new Attachment();
            }

            /// <summary>A policy no documented member returns.</summary>
            public class Policy
            {
                /// <summary>Clone.</summary>
                public Policy Clone() => this;
            }
        }
        """
    )

    def _email(self, tmp_path: Path, body: str) -> DocModule:
        TestDiscoverModulesTypeAnnotations._write_module(
            None, tmp_path, "Email", "email", "EmailMessage.cs", body, module_class="EmailModule"
        )
        (module,) = discover_modules(tmp_path)
        return module

    def test_unexported_type_is_documented_only_when_a_member_returns_it(self, tmp_path: Path):
        """An un-annotated public class is not importable (SPY0301) but a member on a value of it
        binds: it renders as a section — instance members only, its ToString as `__str__` — when a
        documented member returns it, and not at all otherwise. Never at module level."""
        module = self._email(tmp_path, self._EMAIL)
        assert [m.name for m in module.members] == ["attach"]
        assert [t.name for t in module.types] == ["EmailMessage", "Attachment"]
        attachment = self._type(module, "Attachment")
        assert attachment.returned_by == "email.attach()"
        assert [m.name for m in attachment.members] == ["filename", "__str__"]
        page = render_module_page(module)
        assert (
            "## Attachment\n\n*Not importable by name.* A value of it is returned by `email.attach()`.\n"
        ) in page
        assert "Policy" not in page and "clone" not in page and "empty" not in page
        # Positive control: the annotated type gets no note.
        assert "## EmailMessage\n\nA message.\n" in page

    def test_unexported_type_no_member_returns_is_not_documented(self, tmp_path: Path):
        # Positive control for the reachability arm: the same source without the returning member.
        body = self._EMAIL.replace(
            "public static Attachment Attach(string name) => new Attachment();",
            "public static int Count() => 0;",
        )
        module = self._email(tmp_path, body)
        assert [m.name for m in module.members] == ["count"]
        assert [t.name for t in module.types] == ["EmailMessage"]

    def test_submodule_class_renders_as_its_own_section(self, tmp_path: Path):
        """A `[SharpyModule("<module>.<sub>")]` class in the module's directory is the submodule:
        `import numpy.fft` then `numpy.fft.fft(...)` binds, `numpy.fft(...)` does not."""
        body = textwrap.dedent(
            """\
            namespace Sharpy
            {
                /// <summary>The probe module.</summary>
                public static partial class ProbeModule
                {
                    /// <summary>Top.</summary>
                    public static int Top() => 0;
                }

                /// <summary>The fft submodule.</summary>
                [SharpyModule("probe.fft")]
                public static class ProbeFft
                {
                    /// <summary>Transform.</summary>
                    public static int Fft(int n) => n;
                }
            }
            """
        )
        TestDiscoverModulesTypeAnnotations._write_module(
            None, tmp_path, "Probe", "probe", "Probe.cs", body, module_class="ProbeModule"
        )
        (module,) = discover_modules(tmp_path)
        assert [m.name for m in module.members] == ["top"]
        (sub,) = module.submodules
        assert (sub.name, sub.summary, [m.name for m in sub.members]) == (
            "probe.fft", "The fft submodule.", ["fft"]
        )
        page = render_module_page(module)
        assert "## probe.fft\n\nThe fft submodule.\n\n```python\nimport probe.fft\n```\n" in page
        assert "### `probe.fft.fft(n: int) -> int`" in page
        assert "### `probe.fft(" not in page
        assert "### `probe.top() -> int`" in page

    def test_a_static_class_the_compiler_binds_nowhere_is_refused(self, tmp_path: Path):
        body = textwrap.dedent(
            """\
            namespace Sharpy
            {
                public static class Helpers
                {
                    /// <summary>Help.</summary>
                    public static int Help() => 0;
                }
            }
            """
        )
        TestDiscoverModulesTypeAnnotations._write_module(None, tmp_path, "Probe", "probe", "Helpers.cs", body)
        with pytest.raises(ValueError, match=r"static class Helpers.*#2272"):
            discover_modules(tmp_path)

    def test_a_submodule_of_another_module_is_refused(self, tmp_path: Path):
        body = '[SharpyModule("other.fft")]\npublic static class OtherFft\n{\n    public static int F() => 0;\n}\n'
        TestDiscoverModulesTypeAnnotations._write_module(None, tmp_path, "Probe", "probe", "Other.cs", body)
        with pytest.raises(ValueError, match=r"not a submodule of 'probe'"):
            discover_modules(tmp_path)

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
# C#-spelling scan over rendered signatures (#2034, #2066)
# ---------------------------------------------------------------------------

_REPO_ROOT = Path(__file__).resolve().parents[2]

# A rendered signature is a `### \`...\`` heading line. The scan is anchored to signature surfaces:
# a bare `@\w+:` over whole pages also matches prose such as urllib's `user:pass@host:8080`.
_SIGNATURE_HEADING = "### `"

# The C# keyword aliases of the integer widths (#2066). `int` is absent on purpose: it is Python's
# own spelling of int32 and the generator keeps it (see `_SHARPY_TYPE_NAMES` in the generator).
_INT_WIDTH_ALIAS = r"(?:sbyte|byte|short|ushort|uint|ulong|long)"
# Every C# keyword type — what a `<see cref>` parameter list is spelled in.
_CS_TYPE_KEYWORD = (
    r"(?:bool|byte|sbyte|short|ushort|int|uint|long|ulong|float|double|decimal|char|string|object)"
)

# Two scopes, one per axis family:
#   TYPE rows read the signature surfaces — every `### \`...\`` heading, plus the type of each
#   parameter bullet and properties/constants table row, read as `name: TYPE`.
#   PROSE rows (`_PROSE_ROWS`) read every inline code span on a non-heading, non-fenced line: a
#   `<see cref>` renders as a code span, so that is where a C# cref signature would surface.
_CSHARP_SPELLINGS = {
    # #2034 — the NAME and DEFAULT axes.
    "verbatim-identifier": re.compile(r"@\w+:"),
    "default!": re.compile(r"= default!"),
    "default": re.compile(r"= default\b"),
    "null-forgiving": re.compile(r"\w!(?=[,)])"),
    # #2066 — the TYPE axis.
    # A nullable function type must be grouped, `((object) -> object | None) | None`. Ungrouped it
    # reads as a function returning `... | None`: visibly as a doubled `| None | None`, or as a
    # function-typed parameter defaulting to `None` whose type is not nullable at the top level.
    "ungrouped-nullable-callable": re.compile(
        r"\| None \| None|\w: \((?:[^()]|\([^()]*\))*\) -> [^,()=]*= None"
    ),
    # A builtin type spelled as the Core CLR type behind it (`bytes` is `Sharpy.Bytes`).
    "builtin-clr-name": re.compile(r"\b(?:Sharpy\.)?(?:Bytes|Slice)\b"),
    # An integer width in a TYPE position (after `:`, `->`, `[`, `(`, `|`, `*` or `,`; never a
    # parameter name, which a `:` follows) spelled as its C# keyword alias.
    "int-width-alias": re.compile(r"(?:[:\[(|*,]\s*|-> )" + _INT_WIDTH_ALIAS + r"\b(?!:)"),
    # #2055 — the spellings frozen into the formerly hand-authored pages (numpy, requests,
    # sqlite3): an extension method's `this` receiver, C# angle-bracket generics, the C# nullable
    # suffix `T?`, a `null` default, a `System.` namespace, and a delegate type by its CLR name.
    "this-receiver": re.compile(r": this \w"),
    "angle-generic": re.compile(r"\w<\w"),
    "nullable-suffix": re.compile(r"[\w\]>)]\?(?=[\s,)\]|>`]|$)"),
    "null-default": re.compile(r"= null\b"),
    "clr-namespace": re.compile(r"\bSystem\."),
    "clr-delegate": re.compile(r"\b(?:Func|Action)\["),
    # #2066 — the PROSE axis: a code span that IS a C# cref member signature, `Dumps(object?, int)`.
    # The lookahead asks for a C# keyword type, a nullable `T?` or a generic `T{` in the parameter
    # list, so a Sharpy call written in prose (`Err(HTTPError)`, `Counter({...})`) is not one.
    "cref-signature": re.compile(
        r"^(?:[\w.]+\.)?[A-Z]\w*(?:\{[^{}]*\})?\((?=[^()]*(?:\b"
        + _CS_TYPE_KEYWORD
        + r"\b|\w\?|\w\{))[^()]*\)$"
    ),
    # A cref's generic-brace spelling, `List{T}` — C# XML-doc syntax, never Sharpy's.
    "cref-generic": re.compile(r"\b[A-Za-z_]\w*\{\w+(?:, ?\w+)*\}"),
}
_PROSE_ROWS = frozenset({"cref-signature", "cref-generic"})

# `- \`name\` (TYPE) -- description` and `| \`name\` | \`TYPE\` | description |`.
_PARAM_BULLET_RE = re.compile(r"^- `([^`]+)` \((.*?)\)(?: -- |$)")
_TABLE_TYPE_RE = re.compile(r"^\| `([^`]+)` \| `(.*?)` \| ")
_CODE_SPAN_RE = re.compile(r"`([^`]+)`")
_ESCAPED_PIPE = "\\|"


def _scan_fragments(text: str):
    """Yield ``(lineno, scope, fragment)`` for every surface the scan reads (see the rows above)."""
    fenced = False
    for lineno, line in enumerate(text.splitlines(), start=1):
        if line.lstrip().startswith("```"):
            fenced = not fenced
            continue
        if fenced:
            continue
        if line.startswith(_SIGNATURE_HEADING):
            yield lineno, "type", line
            continue
        typed = _PARAM_BULLET_RE.match(line) or _TABLE_TYPE_RE.match(line)
        if typed:
            # A table type cell escapes a union's `|` for GFM (#2163); the type it shows is unescaped.
            yield lineno, "type", f"{typed.group(1)}: {typed.group(2).replace(_ESCAPED_PIPE, '|')}"
        # A table cell escapes its backticks (`_escape_table_cell`); unescaped, they delimit spans.
        for span in _CODE_SPAN_RE.findall(line.replace("\\`", "`")):
            yield lineno, "prose", span


def _csharp_spelling_hits(page_name: str, text: str) -> list[str]:
    hits = []
    for lineno, scope, fragment in _scan_fragments(text):
        for label, pattern in _CSHARP_SPELLINGS.items():
            if (label in _PROSE_ROWS) != (scope == "prose"):
                continue
            if pattern.search(fragment):
                hits.append(f"{page_name}:{lineno} [{label}] {fragment}")
    return hits


def _hit_labels(hits: list[str]) -> set[str]:
    return {h.split("[", 1)[1].split("]", 1)[0] for h in hits}


# One fabricated page line per row, each carrying the leak its row names (#2066 positive control):
# a row that cannot flag its own leak is vacuous.
_FABRICATED_LEAKS = {
    "verbatim-identifier": "### `probe.get(key: K, @default: V) -> V`",
    "default!": "### `probe.get(key: K, default: V = default!) -> V`",
    "default": "### `probe.get(key: K, tag: str | None = default) -> V`",
    "null-forgiving": "### `probe.name(s: str = None!) -> str`",
    "ungrouped-nullable-callable": (
        "### `json.dumps(obj: object, default: (object) -> object | None | None = None) -> str`"
    ),
    "builtin-clr-name": "### `base64.b64encode(s: Bytes) -> Bytes`",
    "int-width-alias": "### `struct.widen(value: short) -> ulong`",
    "this-receiver": "### `numpy.sum(a: this NdArray[float]) -> float`",
    "angle-generic": "### `numpy.mean(a: NdArray<double>) -> float`",
    "nullable-suffix": "### `json() -> object?`",
    "null-default": "### `requests.get(url: str, json: object | None = null) -> Response`",
    "clr-namespace": "### `execute(sql: str, parameters: System.Collections.IEnumerable) -> Cursor`",
    "clr-delegate": "| `row_factory` | `Func[Cursor, list[object], object]` | The row factory. |",
    "cref-signature": (
        "defaults. See `Dumps(object?, int, bool, bool, ValueTuple{string, string}?, "
        "Func{object, object?}?)`."
    ),
    "cref-generic": "Returns a `List{T}` of sub-arrays.",
}

# Each fabricated leak also has a surface form the scan must read: a parameter bullet or a
# properties-table row (the TYPE rows' non-heading surfaces).
_FABRICATED_SURFACE_LEAKS = {
    "- `default` ((object) -> object | None | None) -- Optional callback.": "ungrouped-nullable-callable",
    "| `data` | `Bytes` | The payload. |": "builtin-clr-name",
    "- `width` (ushort) -- The width.": "int-width-alias",
    "| `map` | `dict[str, object]` | The underlying \\`Dict{K, V}\\` backing it. |": "cref-generic",
}


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
                /// <summary>Encode. See <see cref="Lookup{V}(string, V)"/> into a <see cref="List{T}"/>.</summary>
                public static Bytes Encode(Bytes data, short width, ulong count, Func<object, object?>? hook = null, Func<int, string>? fmt = null) => data;
            }
            """
        ),
        encoding="utf-8",
    )
    # The extension class sorts BEFORE the file of the type it extends, as numpy's
    # `NdArray.Reductions.cs` does before `NdArray.cs` (#2055).
    (mod_dir / "Gadget.Reductions.cs").write_text(
        textwrap.dedent(
            """\
            namespace Sharpy;
            /// <summary>Reductions on float gadgets.</summary>
            public static class GadgetReductionExtensions
            {
                /// <summary>Total.</summary>
                public static double Total(this Gadget<double> g, int axis) => 0;
            }
            """
        ),
        encoding="utf-8",
    )
    (mod_dir / "Gadget.cs").write_text(
        textwrap.dedent(
            """\
            namespace Sharpy;
            /// <summary>A gadget.</summary>
            [SharpyModuleType("probe", "gadget")]
            public class Gadget<T>
            {
                /// <summary>Size.</summary>
                public int Size() => 0;
            }
            """
        ),
        encoding="utf-8",
    )
    out = tmp_path / "docs"
    generate(source_dir=src, output_dir=out, force=True, verbose=False, update_nav=False)
    return (out / "probe.md").read_text(encoding="utf-8")


# Rows for spellings no generator rule produces — only a page frozen outside the generator carries
# them (#2055) — so disabling a generator rule cannot surface them. Their positive controls are the
# fabricated lines and the committed-page scan.
_FROZEN_PAGE_ROWS = frozenset({"nullable-suffix", "clr-namespace", "clr-delegate"})
_STDLIB_DOCS = _REPO_ROOT / "docs" / "stdlib"


class TestCSharpSpellingScan:
    """No rendered signature on a stdlib page carries a C# spelling (#2034, #2066, #2055)."""

    def test_generator_owned_pages_have_no_csharp_spellings(self, tmp_path: Path):
        pages = _render_real_stdlib(tmp_path / "stdlib")
        assert pages, "the generator rendered no pages from the repository source"
        assert not any(p.stem in HAND_AUTHORED_MODULES for p in pages)
        # The three formerly hand-authored pages are generator-owned, so this scan covers them.
        assert {"numpy", "requests", "sqlite3"} <= {p.stem for p in pages}
        hits = [h for p in pages for h in _csharp_spelling_hits(p.name, p.read_text(encoding="utf-8"))]
        assert hits == []

    def test_committed_stdlib_pages_have_no_csharp_spellings(self, tmp_path: Path):
        # The COMMITTED pages: every generator-owned page plus every hand-authored one, so a page
        # the generator never writes is still scanned (#2055).
        names = {p.stem for p in _render_real_stdlib(tmp_path / "stdlib")} | set(HAND_AUTHORED_MODULES)
        pages = [_STDLIB_DOCS / f"{n}.md" for n in sorted(names)]
        assert len(pages) > 50 and all(p.exists() for p in pages)
        hits = [h for p in pages for h in _csharp_spelling_hits(p.name, p.read_text(encoding="utf-8"))]
        assert hits == []

    def test_synthetic_csharp_spellings_render_as_sharpy(self, tmp_path: Path):
        page = _render_synthetic(tmp_path)
        assert "### `probe.lookup(key: str, default: V = None) -> V`" in page
        assert "### `probe.parse(s: str, base: int = 10, tag: str | None = None) -> int`" in page
        assert "### `probe.name(s: str = None) -> str`" in page
        assert (
            "### `probe.encode(data: bytes, width: int16, count: uint64, "
            "hook: ((object) -> object | None) | None = None, "
            "fmt: ((int) -> str) | None = None) -> bytes`"
        ) in page
        assert "Encode. See `lookup` into a `list[T]`." in page
        # The extension method is the extended type's instance method, receiver dropped (#2055).
        gadget = page.split("## gadget", 1)[1]
        assert "### `total(axis: int) -> float`" in gadget
        assert "probe.total" not in page
        assert _csharp_spelling_hits("probe.md", page) == []

    def test_positive_control_scan_hits_when_the_mapping_is_disabled(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ):
        # The same synthetic source with the Sharpy-spelling rules turned off: every C# spelling
        # a generator rule guards must be found, or the scan is vacuous.
        monkeypatch.setattr(generator, "_sharpy_param_name", generator.pascal_to_snake)
        monkeypatch.setattr(generator, "_sharpy_default", lambda raw: raw)
        monkeypatch.setattr(generator, "_SHARPY_TYPE_NAMES", {})
        monkeypatch.setattr(generator, "_group_nullable_callable", lambda mapped: mapped)
        monkeypatch.setattr(generator, "_render_cref", lambda cref: cref)
        monkeypatch.setattr(generator, "_extension_class_ranges", lambda lines: [])
        monkeypatch.setattr(generator, "_drop_receiver_modifier", lambda part: part)
        # With the extension rule off the receiver's class is a static class the module does not
        # own; the ownership rule (#2272) is switched off with it so the leak renders at module level.
        monkeypatch.setattr(
            generator,
            "_partition_by_owner",
            lambda subdir, mod_name, module_class, members, types: (generator._module_level(members), []),
        )
        hits = _csharp_spelling_hits("probe.md", _render_synthetic(tmp_path))
        assert _hit_labels(hits) == set(_CSHARP_SPELLINGS) - _FROZEN_PAGE_ROWS, hits

    def test_frozen_hand_authored_spellings_are_flagged(self):
        # Positive control for `_FROZEN_PAGE_ROWS` on the pages' own frozen text, verbatim from the
        # hand-authored requests.md / sqlite3.md / numpy.md before #2055.
        frozen = "\n".join(
            [
                "### `requests.get(url: str, headers: dict[str, str]? = null, json: object? = null) -> Result[Response, RequestException]`",
                "| `response` | `Response?` | The HTTP response associated with this error, if any. |",
                "### `execute(sql: str, parameters: System.Collections.IEnumerable? = null) -> Sqlite3Cursor`",
                "| `row` | `Func[Sqlite3Cursor, list[object?], object]` | A factory function. |",
                "### `numpy.sum(a: this NdArray<double>) -> float`",
            ]
        )
        labels = _hit_labels(_csharp_spelling_hits("frozen.md", frozen))
        assert _FROZEN_PAGE_ROWS | {"null-default", "this-receiver", "angle-generic"} <= labels

    def test_every_row_flags_its_fabricated_leak(self):
        # Row totality is anchored to the fabricated table, not derived from `_CSHARP_SPELLINGS`.
        assert set(_FABRICATED_LEAKS) == set(_CSHARP_SPELLINGS)
        for label, line in _FABRICATED_LEAKS.items():
            assert label in _hit_labels(_csharp_spelling_hits("fabricated.md", line)), (label, line)

    @pytest.mark.parametrize("line,label", sorted(_FABRICATED_SURFACE_LEAKS.items()))
    def test_bullet_and_table_surfaces_are_scanned(self, line: str, label: str):
        assert label in _hit_labels(_csharp_spelling_hits("fabricated.md", line))

    def test_sharpy_spellings_are_not_flagged(self):
        # Negative controls: the Sharpy spellings the rows must NOT flag.
        page = "\n".join(
            [
                "### `json.dumps(default: ((object) -> object | None) | None = None) -> str`",
                "### `difflib.ndiff(a: list[str], key: (str) -> bool, n: int = 3) -> IEnumerable[str]`",
                "### `long(m: decimal) -> int64`",
                "### `builtins.divmod(x: uint64, y: uint64) -> tuple[uint64, uint64]`",
                "### `struct.pack(fmt: str, *values: object) -> bytes`",
                "Returns `Ok(this)` for 2xx status codes, or `Err(HTTPError)` for 4xx/5xx.",
                "`repr()` returns `Counter({...})`, or `ChainMap({})` when empty.",
                "See `dumps`; the result is a `list[T]` of `Fraction(1, 2)` values.",
                "```python",
                "x = Dumps(object?, int)",
                "```",
            ]
        )
        assert _csharp_spelling_hits("negative.md", page) == []

    def test_every_cref_in_the_corpus_renders_without_csharp_signature_syntax(self):
        # The renderer over the WHOLE corpus, not only the crefs that reach a page today (#2066):
        # no rendered `<see cref>` keeps a C# parameter list or generic braces, except a .NET API
        # reference (`System.`), which keeps its CLR path with the parameter list dropped.
        crefs = sorted(
            {
                m.group(1)
                for root in ("Sharpy.Core", "Sharpy.Stdlib")
                for f in (_REPO_ROOT / "src" / root).rglob("*.cs")
                for m in re.finditer(r'<see\s+cref="([^"]+)"', f.read_text(encoding="utf-8"))
            }
        )
        assert len(crefs) > 100, "the cref corpus is unexpectedly small"
        leaks = [
            (c, r)
            for c in crefs
            for r in [generator._render_cref(c)]
            if "(" in r or "{" in r or "<" in r
        ]
        assert leaks == []


# ---------------------------------------------------------------------------
# Positive-roster totality check over every rendered type position (#2163)
# ---------------------------------------------------------------------------
#
# Three rounds (#2034 -> #2066 -> #2163) each added C#-spelling DENYLIST rows to `_CSHARP_SPELLINGS`
# after the leak had shipped. This check is the positive form: every identifier token in every TYPE
# position the pages render — each parameter and return type of a `### \`...\`` signature, and each
# properties/constants table type cell — must be one of
#
#   1. a Sharpy builtin type name (`_builtin_type_roster`, derived from the language spec and the
#      compiler's own registration code — never from the rendered output);
#   2. a CLR type the compiler resolves bare (`_CLR_BARE_TYPES`: each row names its namespace, and
#      the test checks that namespace is one `BuiltinRegistry.ClrFallbackNamespaces` searches), or a
#      CLR type spelled by its import path, `system.<snake namespace>.<Type>` (dotnet_interop.md);
#   3. a type the stdlib docs document — a section on THIS page, bare, or `<module>.<Type>` for a
#      section on another module's page (the #1911 rule);
#   4. a generic parameter of the enclosing member or type.
#
# A parameter type may also lead with a spec parameter modifier (`ref T`, parameter_modifiers.md).
# Anything else is a spelling a Sharpy user cannot write.

_SPEC_DIR = _REPO_ROOT / "docs" / "language_specification"
_COMPILER_SRC = _REPO_ROOT / "src" / "Sharpy.Compiler"
_CORE_SRC = _REPO_ROOT / "src" / "Sharpy.Core"
_STDLIB_SRC = _REPO_ROOT / "src" / "Sharpy.Stdlib"
_REGISTRY_CS = _COMPILER_SRC / "Semantic" / "Registry" / "BuiltinRegistry.cs"

# The Python spellings of int32 and float64. primitive_types.md lists them as ALIASES, and the
# generator renders them instead of the primary names on purpose (`_SHARPY_TYPE_NAMES`'s comment):
# they are Python's own spellings. Every other alias (`long`, `double`, `byte`, ...) is there "to
# ease C# developers" and reads as C#, so it is not on the roster.
_PYTHON_PRIMITIVE_SPELLINGS = frozenset({"int", "float"})

# CLR types outside the Sharpy namespace that a stdlib signature names bare, each with the namespace
# it lives in. The compiler resolves a bare annotation through `ClrFallbackNamespaces`
# (BuiltinRegistry.cs; dotnet_interop.md "Import Name Fidelity"); the test checks every namespace
# against that list. Rows are non-collection interop types only: a raw .NET collection in a public
# signature is #2164's API defect, allowlisted below, never a roster row.
_CLR_BARE_TYPES = {
    "BigInteger": "System.Numerics",
    "SocketException": "System.Net.Sockets",
    "Stream": "System.IO",
    "StreamReader": "System.IO",
    "StreamWriter": "System.IO",
    "TextReader": "System.IO",
    "TextWriter": "System.IO",
    "Type": "System",
}

# `system.<snake namespace>.<Type>` — the spelling `import system.<snake namespace>` makes valid
# (ModuleRegistry.MapModuleToNamespace PascalCases each snake segment).
_CLR_IMPORT_PATH_RE = re.compile(r"^system(?:\.[a-z][a-z0-9_]*)*\.[A-Z]\w*$")

# Tokens the roster rejects today, each citing the open issue that drains it. Never add a row
# without an issue; a row whose token no longer appears fails `test_roster_allowlist_is_live`.
_ROSTER_ALLOWLIST: dict[str, str] = {
    # #2164 (P39) — raw .NET collection / protocol interfaces in public Core/Stdlib signatures: an
    # API-surface defect the docs only render. P39 drains these rows; the renderer must not hide them.
    "Dictionary": "#2164",
    "HashSet": "#2164",
    "ICollection": "#2164",
    "IComparable": "#2164",
    "IComparer": "#2164",  # functools.cmp_to_key's return; a protocol interface like IComparable
    "IDictionary": "#2164",
    "IList": "#2164",
    "IReadOnlyDictionary": "#2164",
    "IReadOnlyList": "#2164",
    "KeyValuePair": "#2164",
}


def _spec_primitive_roster() -> set[str]:
    """primitive_types.md: the "Sharpy Type" column of the primitive table, plus `int`/`float`."""
    text = (_SPEC_DIR / "primitive_types.md").read_text(encoding="utf-8")
    primary_table = text.split("| Sharpy Type | .NET Type | Size |", 1)[1].split("\n\n", 1)[0]
    primary = set(re.findall(r"^\| `(\w+)` \|", primary_table, re.MULTILINE))
    alias_table = text.split("| Sharpy Alias | Sharpy Type |", 1)[1].split("\n\n", 1)[0]
    aliases = dict(re.findall(r"^\| `(\w+)` \| `(\w+)` \|", alias_table, re.MULTILINE))
    assert _PYTHON_PRIMITIVE_SPELLINGS <= set(aliases), aliases
    return primary | _PYTHON_PRIMITIVE_SPELLINGS


def _registered_builtin_types() -> dict[str, str]:
    """Every `RegisterType(<name>, typeof(<clr>))` in BuiltinRegistry.cs: Sharpy name -> CLR short name."""
    names = (_COMPILER_SRC / "Shared" / "BuiltinNames.cs").read_text(encoding="utf-8")
    consts = dict(re.findall(r'public const string (\w+) = "([^"]+)";', names))
    registry = _REGISTRY_CS.read_text(encoding="utf-8")
    registered = {}
    for m in re.finditer(
        r'RegisterType\(\s*(?:"(\w+)"|BuiltinNames\.(\w+))\s*,\s*typeof\(([^()]*)\)', registry
    ):
        sharpy = m.group(1) or consts[m.group(2)]
        clr = re.sub(r"<[^<>]*>$", "", m.group(3)).replace("::", ".").rsplit(".", 1)[-1]
        registered[sharpy] = clr
    return registered


def _registered_generic_builtins() -> set[str]:
    """The `RegisterType(..., isGeneric: true, ...)` names: written without type arguments, such a
    name does not denote the non-generic CLR type the C# meant (`System.Collections.IEnumerable`)."""
    names = (_COMPILER_SRC / "Shared" / "BuiltinNames.cs").read_text(encoding="utf-8")
    consts = dict(re.findall(r'public const string (\w+) = "([^"]+)";', names))
    registry = _REGISTRY_CS.read_text(encoding="utf-8")
    return {
        m.group(1) or consts[m.group(2)]
        for m in re.finditer(
            r'RegisterType\(\s*(?:"(\w+)"|BuiltinNames\.(\w+))\s*,[^;]*isGeneric:\s*true', registry
        )
    }


def _clr_fallback_namespaces() -> list[str]:
    body = _REGISTRY_CS.read_text(encoding="utf-8").split("ClrFallbackNamespaces =", 1)[1].split("};", 1)[0]
    namespaces = re.findall(r'"([\w.]+)"', body)
    if "ClrTypeBridge.SpecialCases.SharpyNamespace" in body:
        namespaces.append("Sharpy")
    return namespaces


def _sharpy_namespace_bare_types() -> set[tuple[str, int]]:
    """``(name, generic arity)`` of each public top-level type declared in `namespace Sharpy` (Core
    and Stdlib) with no Sharpy-name override — no `[SharpyModuleType]` on any of its partial
    declarations (a module type is spelled by its documented name) and no `RegisterType` under
    another name (`Sharpy.Bytes` is `bytes`). The compiler resolves these bare: `Sharpy` is the last
    `ClrFallbackNamespaces` entry, probed as `Name`arity` after the System namespaces — so the arity
    is part of the identity (`Sharpy.IList` is not `IList[str]`, which is SCG's)."""
    renamed = {clr for sharpy, clr in _registered_builtin_types().items() if sharpy != clr}
    declared: set[tuple[str, int]] = set()
    annotated: set[str] = set()
    for root in (_CORE_SRC, _STDLIB_SRC):
        for cs in root.rglob("*.cs"):
            if {"bin", "obj"} & set(cs.relative_to(root).parts):
                continue
            text = cs.read_text(encoding="utf-8")
            if not re.search(r"^\s*namespace Sharpy\s*[{;]?\s*$", text, re.MULTILINE):
                continue
            lines = text.split("\n")
            ranges = generator._find_public_class_ranges(lines)
            for name, start, end in ranges:
                if any(ps < start and end <= pe for _, ps, pe in ranges):
                    continue
                attrs = " ".join(generator._collect_attribute_lines(lines, start)) + lines[start]
                if "SharpyModuleType" in attrs:
                    annotated.add(name)
                elif name not in renamed:
                    declared.add((name, len(generator._class_type_params(lines[start]))))
    return {(name, arity) for name, arity in declared if name not in annotated}


def _spec_array_type() -> set[str]:
    """primitive_types.md "Array Type": the generic name the spec gives a .NET `T[]` (`array`)."""
    text = (_SPEC_DIR / "primitive_types.md").read_text(encoding="utf-8")
    return set(re.findall(r"^\| `(\w+)\[T\]` \| `T\[\]` \|", text, re.MULTILINE))


def _builtin_type_roster() -> set[str]:
    """Names valid at ANY arity: the spec primitives, the spec's array type, the registered builtins."""
    return _spec_primitive_roster() | _spec_array_type() | set(_registered_builtin_types())


def _type_tokens(text: str) -> list[tuple[str, int]]:
    """``(token, arity)`` for each identifier in a rendered type; arity counts a `[...]` argument list."""
    tokens = []
    for m in re.finditer(r"[A-Za-z_][\w.]*", text):
        arity = 0
        if text[m.end() : m.end() + 1] == "[":
            depth = 0
            for close in range(m.end(), len(text)):
                depth += {"[": 1, "]": -1}.get(text[close], 0)
                if depth == 0:
                    break
            arity = len(_split_top_level(text[m.end() + 1 : close]))
        tokens.append((m.group(0), arity))
    return tokens


def _spec_parameter_modifiers() -> set[str]:
    text = (_SPEC_DIR / "parameter_modifiers.md").read_text(encoding="utf-8")
    return set(re.findall(r"^\| `(\w+) T` \|", text, re.MULTILINE))


def _split_top_level(text: str, sep: str = ",") -> list[str]:
    """Split on *sep* outside brackets and string literals."""
    parts, current, depth, quote = [], [], 0, None
    for ch in text:
        if quote:
            current.append(ch)
            quote = None if ch == quote else quote
            continue
        if ch in "\"'":
            quote = ch
        elif ch in "([{":
            depth += 1
        elif ch in ")]}":
            depth -= 1
        elif ch == sep and depth == 0:
            parts.append("".join(current))
            current = []
            continue
        current.append(ch)
    if "".join(current).strip():
        parts.append("".join(current))
    return parts


def _signature_type_positions(signature: str) -> list[tuple[str, str]]:
    """``(kind, type)`` for each parameter type (``"param"``) and the return type (``"return"``) of a
    rendered signature `name(p: T = d, *q: U) -> R`. A default is a value, not a type: dropped."""
    open_paren = signature.index("(")
    depth = 0
    for close_paren in range(open_paren, len(signature)):
        depth += {"(": 1, ")": -1}.get(signature[close_paren], 0)
        if depth == 0:
            break
    positions = []
    for param in _split_top_level(signature[open_paren + 1 : close_paren]):
        if ":" in param:
            # ` = ` at top level starts the default; `->` inside a function type is not one.
            ptype = re.split(r" = ", param.split(":", 1)[1], maxsplit=1)[0]
            positions.append(("param", ptype.strip()))
    tail = signature[close_paren + 1 :].strip()
    if tail.startswith("->"):
        positions.append(("return", tail[2:].strip()))
    return positions


def _discover_pages() -> list[DocModule]:
    """Every generator-owned page's model, discovered as `generate` discovers it."""
    builtins, core_types, modules = generator.discover_all(_CORE_SRC, _STDLIB_SRC)
    return [builtins] + core_types + modules


def _rendered_type_positions(pages: list[DocModule]):
    """Yield ``(page, where, kind, type, type_params_in_scope)`` for every rendered type position."""
    for page in pages:
        owners = [(page.members, page.type_params, "")] + [
            (t.members, t.type_params, f"{t.name}.") for t in page.types
        ] + [(sub.members, [], f"{sub.name}.") for sub in page.submodules]
        for members, owner_params, owner in owners:
            for m in members:
                scope = set(owner_params) | set(m.type_params) | set(m.declaring_type_params)
                where = f"{page.name}.md {owner}{m.signature or m.name}"
                if m.kind == "method":
                    for kind, ptype in _signature_type_positions(m.signature):
                        yield page.name, where, kind, ptype, scope
                elif m.return_type:
                    yield page.name, where, "return", m.return_type, scope


def _roster_violations(pages: list[DocModule], allowlist: "dict[str, str] | None" = None) -> list[str]:
    roster = _builtin_type_roster() | set(_CLR_BARE_TYPES)
    generic_builtins = _registered_generic_builtins()
    sharpy_namespace = _sharpy_namespace_bare_types()
    modifiers = _spec_parameter_modifiers()
    documented = {p.name: {t.name for t in p.types} for p in pages if p.kind == "module"}
    allowlist = _ROSTER_ALLOWLIST if allowlist is None else allowlist
    hits = []
    for page, where, kind, ptype, scope in _rendered_type_positions(pages):
        text = ptype.lstrip("*")
        head = text.split(" ", 1)
        if kind == "param" and len(head) == 2 and head[0] in modifiers:
            text = head[1]
        for token, arity in _type_tokens(text):
            module, _, name = token.partition(".")
            if (
                (token in roster and not (arity == 0 and token in generic_builtins))
                or (token, arity) in sharpy_namespace
                or token in scope
                or token in allowlist
                or token in documented.get(page, ())
                or (name and name in documented.get(module, ()))
                or _CLR_IMPORT_PATH_RE.match(token)
            ):
                continue
            hits.append(f"{where}: `{token}` in `{ptype}`")
    return hits


@pytest.fixture(scope="module")
def stdlib_pages() -> list[DocModule]:
    return _discover_pages()


def _probe_pages(signature: str, type_params: "list[str] | None" = None) -> list[DocModule]:
    probe = DocModule(
        name="probe",
        kind="module",
        members=[
            DocMember(kind="method", name="f", cs_name="F", signature=signature, type_params=type_params or [])
        ],
    )
    return [probe, DocModule(name="os", kind="module", types=[DocType(name="StatResult", cs_name="StatResult")])]


class TestRenderedTypeRoster:
    """Every rendered type token is a Sharpy name (#2163) — the positive form of the #2034/#2066 rows."""

    def test_roster_sources_are_live(self):
        # The derived roster must contain these literal anchors: a parse that silently matched
        # nothing would leave an empty roster and a vacuous check (or, inverted, an everything-roster).
        roster = _builtin_type_roster()
        assert {"int", "float", "int64", "uint16", "float32", "str", "bool", "char", "decimal"} <= roster
        assert {"list", "dict", "set", "tuple", "bytes", "slice", "complex", "Optional", "Result"} <= roster
        assert {"IEnumerable", "IEnumerator", "Iterator", "object", "None"} <= roster
        assert _spec_array_type() == {"array"}
        # Sharpy namespace, resolved bare, at the declared arity; a module type is not bare.
        sharpy_namespace = _sharpy_namespace_bare_types()
        assert {("ISized", 0), ("IReverseEnumerable", 1), ("TextFile", 0), ("IList", 0)} <= sharpy_namespace
        assert not {n for n, _ in sharpy_namespace} & {"NdArray", "Date", "Sqlite3Cursor"}
        roster |= {n for n, _ in sharpy_namespace}
        # A builtin's CLR name is not a Sharpy spelling when the registry renames it.
        assert not {"List", "Dict", "Bytes", "Slice", "Complex", "FrozenSet"} & roster
        # Neither are the C# keyword aliases, nor names no Sharpy surface declares.
        assert not {"long", "double", "byte", "ushort", "ulong", "sbyte", "short", "Iterable"} & roster
        assert _spec_parameter_modifiers() == {"ref", "out", "in"}
        fallback = _clr_fallback_namespaces()
        assert "Sharpy" in fallback and "System.IO" in fallback
        for name, namespace in _CLR_BARE_TYPES.items():
            assert namespace in fallback, (name, namespace)
        # A bare CLR row must not collide with a Sharpy name, or the bare spelling would denote the other type.
        assert not set(_CLR_BARE_TYPES) & roster
        assert {"list", "dict", "IEnumerable", "Optional"} <= _registered_generic_builtins()
        assert not {"str", "bytes", "object"} & _registered_generic_builtins()

    def test_spec_primitives_are_the_compilers_primary_names(self):
        # The roster's primitive half comes from the spec table; it must be the compiler's own
        # primary names (PrimitiveCatalog `Register`, not `RegisterAlias`), or the two disagree.
        catalog = (_COMPILER_SRC / "Semantic" / "Registry" / "PrimitiveCatalog.cs").read_text(encoding="utf-8")
        primary = set(re.findall(r'\bRegister\(byName, byClr, new PrimitiveInfo\("(\w+)"', catalog))
        assert primary - {"None", "void"} == _spec_primitive_roster() - _PYTHON_PRIMITIVE_SPELLINGS
        # `array` is not a PrimitiveCatalog name: the bridge spells every CLR array `array[T]`.
        bridge = (_COMPILER_SRC / "Discovery" / "ClrTypeBridge.cs").read_text(encoding="utf-8")
        assert "var arrayName = BuiltinNames.Array;" in bridge
        assert 'public const string Array = "array";' in (_COMPILER_SRC / "Shared" / "BuiltinNames.cs").read_text(
            encoding="utf-8"
        )

    def test_csharp_keyword_types_map_to_a_roster_name_of_the_same_clr_type(self):
        # Every C# keyword type renders as a roster name OF THE SAME CLR TYPE (#2163): C# `float` is
        # System.Single, so it must be `float32` — Sharpy's `float` is float64, a different type.
        catalog = (_COMPILER_SRC / "Semantic" / "Registry" / "PrimitiveCatalog.cs").read_text(encoding="utf-8")
        entries = re.findall(r'new PrimitiveInfo\("(\w+)", "(\w+)", typeof\((\w+)\)', catalog)
        names_by_clr: dict[str, set[str]] = {}
        for sharpy, _, clr in entries:
            names_by_clr.setdefault(clr, set()).add(sharpy)
        roster = _builtin_type_roster()
        keywords = {cs: clr for _, cs, clr in entries if clr != "void"}
        assert {"float", "double", "long", "byte", "string"} <= set(keywords)
        wrong = {
            cs: map_type(cs)
            for cs, clr in keywords.items()
            if map_type(cs) not in names_by_clr[clr] & roster
        }
        assert wrong == {}
        # Through a parameter modifier too: `ref float` is `ref float32`.
        assert map_type("ref float") == "ref float32"
        assert map_type("out long") == "out int64"

    def test_generator_fallback_namespaces_match_the_compiler(self):
        assert list(generator._CLR_FALLBACK_NAMESPACES) == _clr_fallback_namespaces()

    def test_walk_covers_every_rendered_signature(self, stdlib_pages, tmp_path: Path):
        # The roster reads the model; this anchors the model to the pages: each page's rendered
        # signature headings are exactly the walked methods' signatures.
        rendered = {p.stem: p.read_text(encoding="utf-8") for p in _render_real_stdlib(tmp_path / "stdlib")}
        assert {p.name for p in stdlib_pages} == set(rendered) - {"index"}
        for page in stdlib_pages:
            prefix = f"{page.name}." if page.kind == "module" else ""
            walked = [f"### `{prefix}{m.signature}`" for m in page.members if m.kind == "method"]
            walked += [f"### `{m.signature}`" for t in page.types for m in t.members if m.kind == "method"]
            walked += [
                f"### `{sub.name}.{m.signature}`"
                for sub in page.submodules
                for m in sub.members
                if m.kind == "method"
            ]
            headings = [line for line in rendered[page.name].splitlines() if line.startswith("### `")]
            assert sorted(headings) == sorted(walked), page.name

    def test_every_rendered_type_token_is_on_the_roster(self, stdlib_pages):
        hits = _roster_violations(stdlib_pages)
        assert hits == [], f"{len(hits)} non-Sharpy type tokens:\n" + "\n".join(hits)

    def test_roster_allowlist_is_live(self, stdlib_pages):
        # Drain on fix: an allowlisted token the pages no longer carry must be deleted.
        assert all(re.fullmatch(r"#\d+", issue) for issue in _ROSTER_ALLOWLIST.values())
        unflagged = _roster_violations(stdlib_pages, allowlist={})
        stale = {t for t in _ROSTER_ALLOWLIST if not any(f": `{t}` in" in h for h in unflagged)}
        assert stale == set()

    @pytest.mark.parametrize(
        "signature,token",
        [
            ("u_int16(m: decimal) -> UInt16", "UInt16"),
            ("i_add(left: ref long, right: int64)", "long"),
            ("bool(tuple: Runtime.CompilerServices.ITuple) -> bool", "Runtime.CompilerServices.ITuple"),
            ("from_socket_exception(ex: Net.Sockets.SocketException) -> bool", "Net.Sockets.SocketException"),
            ("encode(encoding: str) -> list[byte]", "byte"),
            ("reshape(shape: tuple[int, int]) -> NdArray[float]", "NdArray"),
            ("total(a: T) -> T", "T"),
            ("take(x: ref int) -> ref int", "ref"),
            ("cmp(x: object) -> Comparer[int]", "Comparer"),
            ("each(x: Iterable[int]) -> None", "Iterable"),
            ("get(x: object) -> os.Bogus", "os.Bogus"),
            ("get(x: object) -> StatResult", "StatResult"),
            ("execute(parameters: IEnumerable | None = None) -> None", "IEnumerable"),
        ],
    )
    def test_fabricated_csharp_token_is_flagged(self, signature: str, token: str):
        # Synthetic positive control: a fabricated page whose one signature carries a token no Sharpy
        # user can write — a C# name, a C#-only modifier position, a bare type of ANOTHER page.
        hits = _roster_violations(_probe_pages(signature), allowlist={})
        assert any(f"`{token}` in" in h for h in hits), hits

    @pytest.mark.parametrize(
        "signature",
        [
            "lookup(key: str, default: V = None) -> V",
            "i_add(left: ref int, right: int)",
            "assigned(flag: ref bool, value: T) -> T",
            "stat() -> os.StatResult",
            "bool(tuple: system.runtime.compiler_services.ITuple) -> bool",
            'print(*values: object, file: TextWriter | None = None, sep: str = ", ")',
            "map(f: ((T) -> R) | None, items: IEnumerable[tuple[T, int]]) -> dict[str, list[float32]]",
        ],
    )
    def test_sharpy_spellings_are_on_the_roster(self, signature: str):
        # Negative controls: each token is a builtin, a CLR bare/import-path type, a documented type
        # of another page (`os.StatResult`) or a type parameter in scope.
        assert _roster_violations(_probe_pages(signature, ["T", "V", "R"]), allowlist={}) == []


class TestRenderedNameProseAndLayout:
    """The #2163 rows beside the type roster: builtin names, cref prose, table cells, member owners."""

    # --- NAME: a builtin function heading is the name users call -----------------------------------

    @staticmethod
    def _builtin_name_leaks(builtins: DocModule) -> list[str]:
        # The compiler aliases a builtin whose underscore-stripped name is a registered type name
        # to that spelling (#1637): `Builtins.UInt16` is called `uint16`, so a `u_int16` heading
        # names nothing a user can call.
        types = _builtin_type_roster()
        return [
            m.name
            for m in builtins.members
            if m.kind == "method" and "_" in m.name and m.name.replace("_", "") in types
        ]

    def test_builtin_function_headings_use_the_registered_spelling(self, stdlib_pages):
        builtins = next(p for p in stdlib_pages if p.kind == "builtins")
        names = {m.name for m in builtins.members}
        assert {"uint8", "uint16", "uint32", "uint64", "int8", "int16"} <= names
        assert self._builtin_name_leaks(builtins) == []

    def test_builtin_name_leak_is_flagged_when_the_alias_is_disabled(self, monkeypatch: pytest.MonkeyPatch):
        monkeypatch.setattr(generator, "_builtin_function_name", lambda snake: snake)
        leaks = self._builtin_name_leaks(generator.discover_builtins(_CORE_SRC))
        assert {"u_int8", "u_int16", "u_int32", "u_int64"} <= set(leaks)

    # --- PROSE: a `<see cref>` renders as the Sharpy name of what it references ---------------------

    @staticmethod
    def _rendered_crefs(monkeypatch: pytest.MonkeyPatch, out_dir: Path) -> list[tuple[str, str]]:
        seen: list[tuple[str, str]] = []
        real = generator._render_cref

        def recording(cref: str) -> str:
            rendered = real(cref)
            seen.append((cref, rendered))
            return rendered

        monkeypatch.setattr(generator, "_render_cref", recording)
        _render_real_stdlib(out_dir)
        return seen

    @staticmethod
    def _cref_leaks(crefs: list[tuple[str, str]]) -> list[str]:
        # A member reference must be the member's Sharpy name, and a type reference must not be a
        # CLR class name the docs spell differently (a module type's documented name, a registry
        # rename). A `System.` path names the .NET API itself and is exempt (#2066).
        renamed = {cls for cls, entries in generator._MODULE_TYPES.items() if all(d != cls for _, d in entries)}
        renamed |= {clr for sharpy, clr in _registered_builtin_types().items() if sharpy != clr}
        documented = {display for entries in generator._MODULE_TYPES.values() for _, display in entries}
        leaks = []
        for cref, rendered in crefs:
            if rendered.startswith("System."):
                continue
            for segment in re.findall(r"[A-Za-z_]\w*", rendered):
                member = (
                    segment in generator._DECLARED_MEMBERS
                    and segment not in generator._DECLARED_TYPES
                    and segment not in documented
                    and segment[0].isupper()
                    and segment not in generator._UNION_CASE_NAMES
                )
                if member or segment in renamed:
                    leaks.append(f"{cref!r} -> {rendered!r} (`{segment}`)")
        return leaks

    def test_every_rendered_cref_is_a_sharpy_name(self, monkeypatch, tmp_path: Path):
        crefs = self._rendered_crefs(monkeypatch, tmp_path / "stdlib")
        rendered = dict(crefs)
        assert len(crefs) > 100
        # Anchors from the issue's cells: a member is its Sharpy name, a module type its documented one.
        assert rendered["Ndim"] == "ndim" and rendered["Items"] == "items"
        assert rendered["Sqlite3Cursor"] == "Cursor" and rendered["NdArray{T}"] == "ndarray[T]"
        assert self._cref_leaks(crefs) == []

    def test_cref_leak_is_flagged_when_the_rendering_is_disabled(self, monkeypatch, tmp_path: Path):
        crefs = [(c, c) for c, _ in self._rendered_crefs(monkeypatch, tmp_path / "stdlib")]
        leaks = "\n".join(self._cref_leaks(crefs))
        for segment in ("Ndim", "Items", "Sqlite3Cursor", "NdArray"):
            assert f"(`{segment}`)" in leaks, segment

    # --- markdown: every table row keeps its header's cell count ------------------------------------

    @staticmethod
    def _cell_count(row: str) -> int:
        # GFM splits a row at every `|` not escaped `\|` — a code span included.
        return len(re.findall(r"(?<!\\)\|", row)) - 1

    @classmethod
    def _misaligned_rows(cls, page: str, text: str) -> list[str]:
        bad, header = [], None
        for lineno, line in enumerate(text.splitlines(), start=1):
            if not line.startswith("|"):
                header = None
                continue
            if header is None:
                header = cls._cell_count(line)
            elif cls._cell_count(line) != header:
                bad.append(f"{page}:{lineno} {line}")
        return bad

    def test_every_table_row_has_its_headers_cell_count(self, tmp_path: Path):
        pages = _render_real_stdlib(tmp_path / "stdlib")
        texts = {p.name: p.read_text(encoding="utf-8") for p in pages}
        # The union-typed cells the issue named are present (escaped), so the check is not vacuous.
        assert any("| `response` | `Response \\| None` |" in t for t in texts.values())
        assert [row for name, t in texts.items() for row in self._misaligned_rows(name, t)] == []

    def test_unescaped_union_type_cell_is_flagged(self, monkeypatch: pytest.MonkeyPatch):
        page = DocModule(
            name="probe",
            kind="module",
            members=[DocMember(kind="property", name="response", cs_name="Response", signature="", return_type="Response | None")],
        )
        assert self._misaligned_rows("probe.md", render_module_page(page)) == []
        monkeypatch.setattr(generator, "_type_cell", lambda t: t)
        assert self._misaligned_rows("probe.md", render_module_page(page)) != []

    # --- structure: a member renders under its declaring type ---------------------------------------

    @staticmethod
    def _misattributed(pages: list[DocModule]) -> list[str]:
        # A module function is declared by the module's own class, a submodule's by the submodule's
        # class (#2272); a type section's member by that type (or by a static extension class on it,
        # #2055). A member rendered anywhere else is misattributed.
        bad = []
        for page in (p for p in pages if p.kind == "module"):
            for m in page.members:
                if m.declaring_type != page.module_class:
                    bad.append(f"{page.name}.{m.name} (declared by {m.declaring_type})")
            for sub in page.submodules:
                for m in sub.members:
                    if m.declaring_type != sub.module_class:
                        bad.append(f"{sub.name}.{m.name} (declared by {m.declaring_type})")
            for t in page.types:
                for m in t.members:
                    if m.declaring_type != t.cs_name and not m.declaring_static:
                        bad.append(f"{page.name} {t.name}.{m.name} (declared by {m.declaring_type})")
        return bad

    def test_every_member_renders_under_its_declaring_type(self, stdlib_pages):
        numpy = next(p for p in stdlib_pages if p.name == "numpy")
        ndarray = next(t for t in numpy.types if t.name == "ndarray")
        assert {"reshape", "mat_mul"} <= {m.name for m in ndarray.members}
        assert not {"reshape", "mat_mul"} & {m.name for m in numpy.members}
        assert self._misattributed(stdlib_pages) == []

    def test_second_file_partial_members_attach_to_the_type(self, tmp_path: Path):
        mod = tmp_path / "Probe"
        mod.mkdir()
        (mod / "__Init__.cs").write_text(
            '[SharpyModule("probe")]\npublic static partial class ProbeModule\n{\n'
            "    /// <summary>Make.</summary>\n    public static int Make() => 0;\n}\n",
            encoding="utf-8",
        )
        # Sorts BEFORE the annotated file, as numpy's `NdArray.Shape.cs` does before `NdArray.cs`.
        (mod / "Gadget.Shape.cs").write_text(
            "public partial class Gadget<T>\n{\n    /// <summary>Reshape.</summary>\n"
            "    public Gadget<T> Reshape(int n) => this;\n}\n",
            encoding="utf-8",
        )
        (mod / "Gadget.cs").write_text(
            '[SharpyModuleType("probe", "gadget")]\npublic partial class Gadget<T>\n{\n'
            "    /// <summary>Size.</summary>\n    public int Size() => 0;\n}\n",
            encoding="utf-8",
        )
        page = discover_modules(tmp_path)[0]
        assert [m.name for m in page.members] == ["make"]
        assert [m.signature for m in page.types[0].members] == ["size() -> int", "reshape(n: int) -> gadget[T]"]
        assert self._misattributed([page]) == []
        # Positive control: the same member, rendered at module level, is flagged.
        page.members.append(page.types[0].members.pop())
        assert self._misattributed([page]) == ["probe.reshape (declared by Gadget)"]


# ---------------------------------------------------------------------------
# Mapping identity: each C# spelling renders as the Sharpy name OF THE SAME TYPE (#2163)
# ---------------------------------------------------------------------------
#
# The roster proves a rendered name is valid Sharpy; it cannot prove the name denotes the type the
# C# meant. `Iterator[T]` for `IEnumerator<T>`, `list[T]` for `T[]` and a bare `IList` for the
# non-generic `System.Collections.IList` (bare, that is Core's `Sharpy.IList`) are all on the roster
# and all name ANOTHER type. This table pins every mapping the generator makes, each with the
# provenance — where the compiler or the spec says that name is that type — that the generator's
# comment cites, and checks that citation is still in the cited file.
_MAPPING_IDENTITY = [
    # (C# spelling, rendered Sharpy, cited file, text that must be in it)
    ("IEnumerable<int>", "IEnumerable[int]", "src/Sharpy.Compiler/Semantic/Registry/BuiltinRegistry.cs",
     "RegisterType(BuiltinNames.IEnumerable, typeof(IEnumerable<>)"),
    ("System.Collections.Generic.IEnumerable<int>", "IEnumerable[int]",
     "docs/language_specification/collection_types.md", "`IEnumerable[T]` for a read-only"),
    ("IEnumerator<int>", "IEnumerator[int]", "src/Sharpy.Compiler/Semantic/Registry/BuiltinRegistry.cs",
     "RegisterType(BuiltinNames.IEnumerator, typeof(IEnumerator<>)"),
    ("Iterator<int>", "Iterator[int]", "src/Sharpy.Compiler/Semantic/Registry/BuiltinRegistry.cs",
     "RegisterType(BuiltinNames.Iterator, typeof(SharpyRT::Sharpy.Iterator<>)"),
    ("IComparer<int>", "IComparer[int]", "src/Sharpy.Compiler/Semantic/Registry/BuiltinRegistry.cs",
     '"System.Collections.Generic",'),
    ("int[]", "array[int]", "docs/language_specification/primitive_types.md", "| `array[T]` | `T[]` |"),
    ("string[][]", "array[array[str]]", "docs/language_specification/type_annotation_shorthand.md",
     "| `T[]` | array |"),
    ("params int[]", "*int", "docs/language_specification/function_variadic_arguments.md",
     "mapping to C#'s `params T[]`"),
    ("System.Collections.IList", "system.collections.IList",
     "docs/language_specification/dotnet_interop.md", "import system.collections.generic as scg"),
    ("System.Collections.ICollection", "system.collections.ICollection",
     "src/Sharpy.Compiler/Semantic/Registry/BuiltinRegistry.cs", '"System.Collections.Generic",'),
    ("System.Collections.IEnumerable", "system.collections.IEnumerable",
     "src/Sharpy.Compiler/Semantic/Registry/BuiltinRegistry.cs", '"System.Collections.Generic",'),
    ("System.Collections.IEnumerable?", "system.collections.IEnumerable | None",
     "docs/language_specification/nullable_types.md", "T?"),
    ("int?", "int | None", "docs/language_specification/nullable_types.md", "T?"),
    ("List<int>", "list[int]", "src/Sharpy.Compiler/Semantic/Registry/BuiltinRegistry.cs",
     'RegisterType("list", typeof(SharpyRT::Sharpy.List<>)'),
    ("Dict<string, int>", "dict[str, int]", "src/Sharpy.Compiler/Semantic/Registry/BuiltinRegistry.cs",
     'RegisterType("dict", typeof(SharpyRT::Sharpy.Dict<,>)'),
    ("Set<int>", "set[int]", "src/Sharpy.Compiler/Semantic/Registry/BuiltinRegistry.cs",
     'RegisterType("set", typeof(SharpyRT::Sharpy.Set<>)'),
    ("Bytes", "bytes", "src/Sharpy.Compiler/Semantic/Registry/BuiltinRegistry.cs",
     'RegisterType("bytes", typeof(SharpyRT::Sharpy.Bytes)'),
    ("Complex", "complex", "src/Sharpy.Compiler/Semantic/Registry/BuiltinRegistry.cs",
     "RegisterType(BuiltinNames.Complex, typeof(SharpyRT::Sharpy.Complex)"),
    ("Slice", "slice", "src/Sharpy.Compiler/Semantic/Registry/BuiltinRegistry.cs",
     'RegisterType("slice", typeof(SharpyRT::Sharpy.Slice)'),
    ("Optional<int>", "Optional[int]", "src/Sharpy.Compiler/Semantic/Registry/BuiltinRegistry.cs",
     'RegisterType("Optional", typeof(SharpyRT::Sharpy.Optional<>)'),
    ("Result<int, string>", "Result[int, str]", "src/Sharpy.Compiler/Semantic/Registry/BuiltinRegistry.cs",
     'RegisterType("Result", typeof(SharpyRT::Sharpy.Result<,>)'),
    ("ValueTuple<int, string>", "tuple[int, str]", "src/Sharpy.Compiler/Semantic/Registry/BuiltinRegistry.cs",
     "RegisterType(BuiltinNames.Tuple, typeof(System.ValueTuple)"),
    ("(int, string)", "tuple[int, str]", "src/Sharpy.Compiler/Semantic/Registry/BuiltinRegistry.cs",
     "RegisterType(BuiltinNames.Tuple, typeof(System.ValueTuple)"),
    ("void", "None", "src/Sharpy.Compiler/Semantic/Registry/BuiltinRegistry.cs",
     'RegisterType("None", typeof(void)'),
    ("System.Runtime.CompilerServices.ITuple", "system.runtime.compiler_services.ITuple",
     "src/Sharpy.Compiler/Semantic/Registry/ModuleRegistry.cs", '"system.text.regular_expressions"'),
    ("System.Net.Sockets.SocketException", "SocketException",
     "src/Sharpy.Compiler/Semantic/Registry/BuiltinRegistry.cs", '"System.Net.Sockets",'),
    ("ref float", "ref float32", "docs/language_specification/parameter_modifiers.md", "| `ref T` |"),
    # The C# keyword types (primitive_types.md's primary names; `int`/`float` its Python aliases).
    ("float", "float32", "docs/language_specification/primitive_types.md", "| `float32` | `System.Single` |"),
    ("double", "float", "docs/language_specification/primitive_types.md", "| `float` | `float64` |"),
    ("int", "int", "docs/language_specification/primitive_types.md", "| `int` | `int32` |"),
    ("long", "int64", "docs/language_specification/primitive_types.md", "| `int64` | `System.Int64` |"),
    ("short", "int16", "docs/language_specification/primitive_types.md", "| `int16` | `System.Int16` |"),
    ("sbyte", "int8", "docs/language_specification/primitive_types.md", "| `int8` | `System.SByte` |"),
    ("byte", "uint8", "docs/language_specification/primitive_types.md", "| `uint8` | `System.Byte` |"),
    ("ushort", "uint16", "docs/language_specification/primitive_types.md", "| `uint16` | `System.UInt16` |"),
    ("uint", "uint32", "docs/language_specification/primitive_types.md", "| `uint32` | `System.UInt32` |"),
    ("ulong", "uint64", "docs/language_specification/primitive_types.md", "| `uint64` | `System.UInt64` |"),
    ("string", "str", "docs/language_specification/primitive_types.md", "| `str` | `System.String` |"),
    ("char", "char", "docs/language_specification/primitive_types.md", "| `char` | `System.Char` |"),
    ("decimal", "decimal", "docs/language_specification/primitive_types.md", "| `decimal` | `System.Decimal` |"),
    ("bool", "bool", "docs/language_specification/primitive_types.md", "| `bool` | `System.Boolean` |"),
    ("object", "object", "docs/language_specification/primitive_types.md", "| `object` | `System.Object` |"),
]


class TestMappingIdentity:
    @pytest.mark.parametrize("cs,sharpy,cited,citation", _MAPPING_IDENTITY, ids=[row[0] for row in _MAPPING_IDENTITY])
    def test_csharp_spelling_renders_as_the_same_type(self, cs: str, sharpy: str, cited: str, citation: str):
        assert citation in (_REPO_ROOT / cited).read_text(encoding="utf-8"), (cited, citation)
        assert map_type(cs) == sharpy

    def test_every_generic_and_keyword_mapping_is_pinned(self):
        # Totality over the generator's own tables: every key of `_GENERIC_TYPE_MAP`,
        # `_SHARPY_TYPE_NAMES` and `_TYPE_MAP` is the outer name of some pinned row, or renders as
        # a row's rendering, so a new mapping cannot land without its identity row.
        pinned_cs = {re.sub(r"<.*$", "", cs).replace("System.", "", 1) for cs, *_ in _MAPPING_IDENTITY}
        pinned_out = {sharpy for _, sharpy, *_ in _MAPPING_IDENTITY}
        tables = (generator._GENERIC_TYPE_MAP, generator._SHARPY_TYPE_NAMES, generator._TYPE_MAP)
        unpinned = [
            key
            for table in tables
            for key, value in table.items()
            if key not in pinned_cs and value not in pinned_out and not any(o.startswith(value + "[") for o in pinned_out)
        ]
        assert unpinned == []


# ---------------------------------------------------------------------------
# Page ownership: every docs/stdlib page is generated, hand-authored, or named prose (#2163)
# ---------------------------------------------------------------------------


def _orphan_pages(docs_dir: Path, generated: set[str]) -> list[str]:
    owned = generated | {f"{name}.md" for name in HAND_AUTHORED_MODULES} | set(generator.PROSE_PAGES)
    return sorted(p.name for p in docs_dir.glob("*.md") if p.name not in owned)


class TestPageOwnership:
    def test_every_stdlib_page_has_an_owner(self, tmp_path: Path):
        generated = {p.name for p in _render_real_stdlib(tmp_path / "stdlib")}
        assert "str.md" in generated  # generated from Core's StringExtensions*.cs since #2163
        assert _orphan_pages(_STDLIB_DOCS, generated) == []
        # Every named prose page exists: a stale name would let a deleted page's slot be reused.
        assert all((_STDLIB_DOCS / name).exists() for name in generator.PROSE_PAGES)

    def test_a_fabricated_orphan_page_is_flagged(self, tmp_path: Path):
        generated = {p.name for p in _render_real_stdlib(tmp_path / "stdlib")}
        docs = tmp_path / "docs"
        docs.mkdir()
        for name in sorted(generated)[:3] + ["orphan.md"]:
            (docs / name).write_text("# page\n", encoding="utf-8")
        assert _orphan_pages(docs, generated) == ["orphan.md"]
