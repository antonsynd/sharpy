using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Xunit;

namespace Sharpy.Tests
{
    public class JsonModuleTests
    {
        #region Dumps - Primitives

        [Fact]
        public void Dumps_Null_ReturnsNullString()
        {
            Assert.Equal("null", Json.Dumps(null));
        }

        [Fact]
        public void Dumps_True_ReturnsTrueString()
        {
            Assert.Equal("true", Json.Dumps(true));
        }

        [Fact]
        public void Dumps_False_ReturnsFalseString()
        {
            Assert.Equal("false", Json.Dumps(false));
        }

        [Fact]
        public void Dumps_Int_ReturnsNumberString()
        {
            Assert.Equal("42", Json.Dumps(42));
        }

        [Fact]
        public void Dumps_NegativeInt_ReturnsNumberString()
        {
            Assert.Equal("-1", Json.Dumps(-1));
        }

        [Fact]
        public void Dumps_Long_ReturnsNumberString()
        {
            Assert.Equal("9999999999", Json.Dumps(9999999999L));
        }

        [Fact]
        public void Dumps_Double_ReturnsNumberString()
        {
            Assert.Equal("3.14", Json.Dumps(3.14));
        }

        [Fact]
        public void Dumps_DoubleZero_ReturnsNumberWithDecimal()
        {
            Assert.Equal("0.0", Json.Dumps(0.0));
        }

        [Fact]
        public void Dumps_String_ReturnsQuotedString()
        {
            Assert.Equal("\"hello\"", Json.Dumps("hello"));
        }

        [Fact]
        public void Dumps_EmptyString_ReturnsEmptyQuotes()
        {
            Assert.Equal("\"\"", Json.Dumps(""));
        }

        #endregion

        #region Dumps - String Escaping

        [Fact]
        public void Dumps_StringWithQuotes_EscapesQuotes()
        {
            Assert.Equal("\"say \\\"hi\\\"\"", Json.Dumps("say \"hi\""));
        }

        [Fact]
        public void Dumps_StringWithBackslash_EscapesBackslash()
        {
            Assert.Equal("\"a\\\\b\"", Json.Dumps("a\\b"));
        }

        [Fact]
        public void Dumps_StringWithNewline_EscapesNewline()
        {
            Assert.Equal("\"line1\\nline2\"", Json.Dumps("line1\nline2"));
        }

        [Fact]
        public void Dumps_StringWithTab_EscapesTab()
        {
            Assert.Equal("\"a\\tb\"", Json.Dumps("a\tb"));
        }

        [Fact]
        public void Dumps_StringWithUnicode_EscapesNonAscii()
        {
            // ensure_ascii=true by default
            Assert.Equal("\"caf\\u00e9\"", Json.Dumps("caf\u00e9"));
        }

        [Fact]
        public void Dumps_StringWithUnicode_EnsureAsciiFalse_PreservesUnicode()
        {
            Assert.Equal("\"caf\u00e9\"", Json.Dumps("caf\u00e9", ensureAscii: false));
        }

        #endregion

        #region Dumps - Collections

        [Fact]
        public void Dumps_EmptyDict_ReturnsEmptyObject()
        {
            Assert.Equal("{}", Json.Dumps(new Dict<string, object?>()));
        }

        [Fact]
        public void Dumps_Dict_ReturnsObject()
        {
            var d = new Dict<string, object?>();
            d["a"] = 1;
            string result = Json.Dumps(d);
            Assert.Equal("{\"a\":1}", result);
        }

        [Fact]
        public void Dumps_EmptyList_ReturnsEmptyArray()
        {
            Assert.Equal("[]", Json.Dumps(new List<object?>()));
        }

        [Fact]
        public void Dumps_List_ReturnsArray()
        {
            var l = new List<object?>();
            l.Append(1);
            l.Append("two");
            l.Append(true);
            string result = Json.Dumps(l);
            Assert.Equal("[1,\"two\",true]", result);
        }

        [Fact]
        public void Dumps_NestedStructure_Serializes()
        {
            var inner = new Dict<string, object?>();
            inner["x"] = 1;
            var list = new List<object?>();
            list.Append(inner);
            var outer = new Dict<string, object?>();
            outer["items"] = list;

            string result = Json.Dumps(outer);
            Assert.Equal("{\"items\":[{\"x\":1}]}", result);
        }

        #endregion

        #region Dumps - Formatting

        [Fact]
        public void Dumps_WithIndent_PrettyPrints()
        {
            var d = new Dict<string, object?>();
            d["a"] = 1;
            d["b"] = 2;

            string result = Json.Dumps(d, indent: 2);
            string expected = "{\n  \"a\": 1,\n  \"b\": 2\n}";
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Dumps_WithSortKeys_SortsKeys()
        {
            var d = new Dict<string, object?>();
            d["c"] = 3;
            d["a"] = 1;
            d["b"] = 2;

            string result = Json.Dumps(d, sortKeys: true);
            Assert.Equal("{\"a\":1,\"b\":2,\"c\":3}", result);
        }

        [Fact]
        public void Dumps_IndentAndSortKeys_Combined()
        {
            var d = new Dict<string, object?>();
            d["z"] = 26;
            d["a"] = 1;

            string result = Json.Dumps(d, indent: 4, sortKeys: true);
            string expected = "{\n    \"a\": 1,\n    \"z\": 26\n}";
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Dumps_NestedIndent_IndentsCorrectly()
        {
            var inner = new Dict<string, object?>();
            inner["x"] = 1;
            var outer = new Dict<string, object?>();
            outer["inner"] = inner;

            string result = Json.Dumps(outer, indent: 2);
            string expected = "{\n  \"inner\": {\n    \"x\": 1\n  }\n}";
            Assert.Equal(expected, result);
        }

        #endregion

        #region Dumps - Error Cases

        [Fact]
        public void Dumps_Infinity_ThrowsValueError()
        {
            Assert.Throws<ValueError>(() => Json.Dumps(double.PositiveInfinity));
        }

        [Fact]
        public void Dumps_NaN_ThrowsValueError()
        {
            Assert.Throws<ValueError>(() => Json.Dumps(double.NaN));
        }

        [Fact]
        public void Dumps_NonSerializableType_ThrowsTypeError()
        {
            Assert.Throws<TypeError>(() => Json.Dumps(new object()));
        }

        #endregion

        #region Loads - Primitives

        [Fact]
        public void Loads_Null_ReturnsNull()
        {
            Assert.Null(Json.Loads("null"));
        }

        [Fact]
        public void Loads_True_ReturnsTrue()
        {
            Assert.Equal(true, Json.Loads("true"));
        }

        [Fact]
        public void Loads_False_ReturnsFalse()
        {
            Assert.Equal(false, Json.Loads("false"));
        }

        [Fact]
        public void Loads_Int_ReturnsInt()
        {
            object? result = Json.Loads("42");
            Assert.IsType<int>(result);
            Assert.Equal(42, result);
        }

        [Fact]
        public void Loads_NegativeInt_ReturnsInt()
        {
            object? result = Json.Loads("-7");
            Assert.IsType<int>(result);
            Assert.Equal(-7, result);
        }

        [Fact]
        public void Loads_LargeInt_ReturnsLong()
        {
            object? result = Json.Loads("9999999999");
            Assert.IsType<long>(result);
            Assert.Equal(9999999999L, result);
        }

        [Fact]
        public void Loads_Float_ReturnsDouble()
        {
            object? result = Json.Loads("3.14");
            Assert.IsType<double>(result);
            Assert.Equal(3.14, result);
        }

        [Fact]
        public void Loads_Scientific_ReturnsDouble()
        {
            object? result = Json.Loads("1.5e10");
            Assert.IsType<double>(result);
            Assert.Equal(1.5e10, result);
        }

        [Fact]
        public void Loads_Zero_ReturnsInt()
        {
            object? result = Json.Loads("0");
            Assert.IsType<int>(result);
            Assert.Equal(0, result);
        }

        [Fact]
        public void Loads_String_ReturnsString()
        {
            Assert.Equal("hello", Json.Loads("\"hello\""));
        }

        [Fact]
        public void Loads_EmptyString_ReturnsEmptyString()
        {
            Assert.Equal("", Json.Loads("\"\""));
        }

        #endregion

        #region Loads - String Escapes

        [Fact]
        public void Loads_EscapedQuote_ParsesCorrectly()
        {
            Assert.Equal("say \"hi\"", Json.Loads("\"say \\\"hi\\\"\""));
        }

        [Fact]
        public void Loads_EscapedBackslash_ParsesCorrectly()
        {
            Assert.Equal("a\\b", Json.Loads("\"a\\\\b\""));
        }

        [Fact]
        public void Loads_EscapedNewline_ParsesCorrectly()
        {
            Assert.Equal("a\nb", Json.Loads("\"a\\nb\""));
        }

        [Fact]
        public void Loads_UnicodeEscape_ParsesCorrectly()
        {
            Assert.Equal("caf\u00e9", Json.Loads("\"caf\\u00e9\""));
        }

        [Fact]
        public void Loads_AllEscapes_ParsesCorrectly()
        {
            // Test all standard JSON escape sequences
            Assert.Equal("/", Json.Loads("\"\\/\""));
            Assert.Equal("\b", Json.Loads("\"\\b\""));
            Assert.Equal("\f", Json.Loads("\"\\f\""));
            Assert.Equal("\r", Json.Loads("\"\\r\""));
            Assert.Equal("\t", Json.Loads("\"\\t\""));
        }

        #endregion

        #region Loads - Objects

        [Fact]
        public void Loads_EmptyObject_ReturnsEmptyDict()
        {
            object? result = Json.Loads("{}");
            var dict = Assert.IsType<Dict<string, object?>>(result);
            Assert.Empty(dict);
        }

        [Fact]
        public void Loads_SimpleObject_ReturnsDict()
        {
            object? result = Json.Loads("{\"a\": 1, \"b\": \"two\"}");
            var dict = Assert.IsType<Dict<string, object?>>(result);
            Assert.Equal(1, dict["a"]);
            Assert.Equal("two", dict["b"]);
        }

        [Fact]
        public void Loads_NestedObject_ReturnsNestedDict()
        {
            object? result = Json.Loads("{\"outer\": {\"inner\": 42}}");
            var dict = Assert.IsType<Dict<string, object?>>(result);
            var inner = Assert.IsType<Dict<string, object?>>(dict["outer"]);
            Assert.Equal(42, inner["inner"]);
        }

        #endregion

        #region Loads - Arrays

        [Fact]
        public void Loads_EmptyArray_ReturnsEmptyList()
        {
            object? result = Json.Loads("[]");
            var list = Assert.IsType<List<object?>>(result);
            Assert.Empty(list);
        }

        [Fact]
        public void Loads_SimpleArray_ReturnsList()
        {
            object? result = Json.Loads("[1, 2, 3]");
            var list = Assert.IsType<List<object?>>(result);
            Assert.Equal(3, ((ICollection<object?>)list).Count);
            Assert.Equal(1, list[0]);
            Assert.Equal(2, list[1]);
            Assert.Equal(3, list[2]);
        }

        [Fact]
        public void Loads_MixedArray_ReturnsList()
        {
            object? result = Json.Loads("[1, \"two\", true, null]");
            var list = Assert.IsType<List<object?>>(result);
            Assert.Equal(1, list[0]);
            Assert.Equal("two", list[1]);
            Assert.Equal(true, list[2]);
            Assert.Null(list[3]);
        }

        [Fact]
        public void Loads_NestedArray_ReturnsList()
        {
            object? result = Json.Loads("[[1, 2], [3, 4]]");
            var list = Assert.IsType<List<object?>>(result);
            var inner1 = Assert.IsType<List<object?>>(list[0]);
            Assert.Equal(1, inner1[0]);
            Assert.Equal(2, inner1[1]);
        }

        #endregion

        #region Loads - Whitespace

        [Fact]
        public void Loads_WithLeadingWhitespace_Parses()
        {
            Assert.Equal(42, Json.Loads("  42"));
        }

        [Fact]
        public void Loads_WithTrailingWhitespace_Parses()
        {
            Assert.Equal(42, Json.Loads("42  "));
        }

        [Fact]
        public void Loads_PrettyPrintedJson_Parses()
        {
            string json = "{\n  \"a\": 1,\n  \"b\": [\n    2,\n    3\n  ]\n}";
            object? result = Json.Loads(json);
            var dict = Assert.IsType<Dict<string, object?>>(result);
            Assert.Equal(1, dict["a"]);
        }

        #endregion

        #region Loads - Error Cases

        [Fact]
        public void Loads_EmptyString_ThrowsJSONDecodeError()
        {
            Assert.Throws<JSONDecodeError>(() => Json.Loads(""));
        }

        [Fact]
        public void Loads_InvalidJson_ThrowsJSONDecodeError()
        {
            Assert.Throws<JSONDecodeError>(() => Json.Loads("invalid"));
        }

        [Fact]
        public void Loads_TrailingCommaInObject_ThrowsJSONDecodeError()
        {
            Assert.Throws<JSONDecodeError>(() => Json.Loads("{\"a\": 1,}"));
        }

        [Fact]
        public void Loads_TrailingCommaInArray_ThrowsJSONDecodeError()
        {
            Assert.Throws<JSONDecodeError>(() => Json.Loads("[1, 2,]"));
        }

        [Fact]
        public void Loads_ExtraData_ThrowsJSONDecodeError()
        {
            Assert.Throws<JSONDecodeError>(() => Json.Loads("1 2"));
        }

        [Fact]
        public void Loads_UnclosedString_ThrowsJSONDecodeError()
        {
            Assert.Throws<JSONDecodeError>(() => Json.Loads("\"unclosed"));
        }

        [Fact]
        public void Loads_UnclosedObject_ThrowsJSONDecodeError()
        {
            Assert.Throws<JSONDecodeError>(() => Json.Loads("{\"a\": 1"));
        }

        [Fact]
        public void Loads_UnclosedArray_ThrowsJSONDecodeError()
        {
            Assert.Throws<JSONDecodeError>(() => Json.Loads("[1, 2"));
        }

        [Fact]
        public void Loads_Null_ThrowsTypeError()
        {
            Assert.Throws<TypeError>(() => Json.Loads(null!));
        }

        [Fact]
        public void JSONDecodeError_IsValueError()
        {
            var ex = Assert.Throws<JSONDecodeError>(() => Json.Loads("invalid"));
            Assert.IsAssignableFrom<ValueError>(ex);
        }

        [Fact]
        public void JSONDecodeError_HasPositionInfo()
        {
            var ex = Assert.Throws<JSONDecodeError>(() => Json.Loads("invalid"));
            Assert.Equal("invalid", ex.Doc);
            Assert.Equal(0, ex.Pos);
            Assert.Contains("line 1", ex.Message);
            Assert.Contains("column 1", ex.Message);
        }

        #endregion

        #region Round-Trip Tests

        [Fact]
        public void RoundTrip_Dict_PreservesData()
        {
            var d = new Dict<string, object?>();
            d["name"] = "test";
            d["value"] = 42;
            d["active"] = true;
            d["nothing"] = null;

            string json = Json.Dumps(d);
            object? parsed = Json.Loads(json);
            var result = Assert.IsType<Dict<string, object?>>(parsed);

            Assert.Equal("test", result["name"]);
            Assert.Equal(42, result["value"]);
            Assert.Equal(true, result["active"]);
            Assert.Null(result["nothing"]);
        }

        [Fact]
        public void RoundTrip_List_PreservesData()
        {
            var l = new List<object?>();
            l.Append(1);
            l.Append("two");
            l.Append(3.0);
            l.Append(false);
            l.Append(null);

            string json = Json.Dumps(l);
            object? parsed = Json.Loads(json);
            var result = Assert.IsType<List<object?>>(parsed);

            Assert.Equal(1, result[0]);
            Assert.Equal("two", result[1]);
            Assert.Equal(3.0, result[2]);
            Assert.Equal(false, result[3]);
            Assert.Null(result[4]);
        }

        [Fact]
        public void RoundTrip_NestedComplex_PreservesData()
        {
            var items = new List<object?>();
            var item1 = new Dict<string, object?>();
            item1["id"] = 1;
            item1["name"] = "alpha";
            items.Append(item1);
            var item2 = new Dict<string, object?>();
            item2["id"] = 2;
            item2["name"] = "beta";
            items.Append(item2);

            var root = new Dict<string, object?>();
            root["items"] = items;
            root["count"] = 2;

            string json = Json.Dumps(root);
            object? parsed = Json.Loads(json);
            var result = Assert.IsType<Dict<string, object?>>(parsed);
            Assert.Equal(2, result["count"]);

            var resultItems = Assert.IsType<List<object?>>(result["items"]);
            var first = Assert.IsType<Dict<string, object?>>(resultItems[0]);
            Assert.Equal(1, first["id"]);
            Assert.Equal("alpha", first["name"]);
        }

        [Fact]
        public void RoundTrip_StringWithEscapes_PreservesData()
        {
            string original = "line1\nline2\ttab \"quoted\" back\\slash";
            string json = Json.Dumps(original);
            Assert.Equal(original, Json.Loads(json));
        }

        [Fact]
        public void RoundTrip_UnicodeString_PreservesData()
        {
            string original = "caf\u00e9 \u00fc\u00f1\u00ee\u00e7\u00f6\u00f0\u00e9";
            string json = Json.Dumps(original);
            Assert.Equal(original, Json.Loads(json));
        }

        #endregion

        #region Dump/Load File I/O

        [Fact]
        public void Dump_WritesJsonToFile()
        {
            string tempPath = System.IO.Path.GetTempFileName();
            try
            {
                var data = new Dict<string, object?>();
                data["key"] = "value";

                using (var fp = Builtins.Open(tempPath, "w"))
                {
                    Json.Dump(data, fp);
                }

                string content = System.IO.File.ReadAllText(tempPath);
                Assert.Equal("{\"key\":\"value\"}", content);
            }
            finally
            {
                System.IO.File.Delete(tempPath);
            }
        }

        [Fact]
        public void Load_ReadsJsonFromFile()
        {
            string tempPath = System.IO.Path.GetTempFileName();
            try
            {
                System.IO.File.WriteAllText(tempPath, "{\"key\": \"value\"}");

                using (var fp = Builtins.Open(tempPath, "r"))
                {
                    object? result = Json.Load(fp);
                    var dict = Assert.IsType<Dict<string, object?>>(result);
                    Assert.Equal("value", dict["key"]);
                }
            }
            finally
            {
                System.IO.File.Delete(tempPath);
            }
        }

        [Fact]
        public void DumpLoad_RoundTrip_ThroughFile()
        {
            string tempPath = System.IO.Path.GetTempFileName();
            try
            {
                var data = new Dict<string, object?>();
                data["name"] = "test";
                data["values"] = new List<object?>(new object?[] { 1, 2, 3 });

                using (var fp = Builtins.Open(tempPath, "w"))
                {
                    Json.Dump(data, fp, indent: 2);
                }

                using (var fp = Builtins.Open(tempPath, "r"))
                {
                    object? result = Json.Load(fp);
                    var dict = Assert.IsType<Dict<string, object?>>(result);
                    Assert.Equal("test", dict["name"]);
                    var values = Assert.IsType<List<object?>>(dict["values"]);
                    Assert.Equal(3, ((ICollection<object?>)values).Count);
                }
            }
            finally
            {
                System.IO.File.Delete(tempPath);
            }
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void Loads_DeeplyNested_HandlesRecursion()
        {
            // 20 levels of nesting
            string json = new string('[', 20) + "1" + new string(']', 20);
            object? result = Json.Loads(json);

            // Unwrap 20 levels
            for (int i = 0; i < 20; i++)
            {
                var list = Assert.IsType<List<object?>>(result);
                result = list[0];
            }

            Assert.Equal(1, result);
        }

        [Fact]
        public void Loads_ObjectWithDuplicateKeys_LastWins()
        {
            // Python json.loads also uses last-wins semantics
            object? result = Json.Loads("{\"a\": 1, \"a\": 2}");
            var dict = Assert.IsType<Dict<string, object?>>(result);
            Assert.Equal(2, dict["a"]);
        }

        [Fact]
        public void Loads_EmptyObjectAndArray_InArray()
        {
            object? result = Json.Loads("[{}, []]");
            var list = Assert.IsType<List<object?>>(result);
            Assert.IsType<Dict<string, object?>>(list[0]);
            Assert.IsType<List<object?>>(list[1]);
        }

        [Fact]
        public void Dumps_NullValueInDict_SerializesAsNull()
        {
            var d = new Dict<string, object?>();
            d["key"] = null;
            Assert.Equal("{\"key\":null}", Json.Dumps(d));
        }

        [Fact]
        public void Dumps_NullInList_SerializesAsNull()
        {
            var l = new List<object?>();
            l.Append(null);
            Assert.Equal("[null]", Json.Dumps(l));
        }

        #endregion
    }
}
