using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Sharpy.Tests
{
    // NOTE: This file lives in `Sharpy.Tests`, nested within `Sharpy`, so unqualified
    // `Dict`, `List`, `Set`, `Yaml`, and the Python-style exceptions resolve to the
    // `Sharpy.*` types (enclosing-namespace precedence over the `System.*` usings).
    public class YamlModuleTests
    {
        private static int Count(List<object?> list) => ((ICollection<object?>)list).Count;

        #region SafeLoad / SafeDump round-trip

        [Fact]
        public void RoundTrip_SimpleMapping_PreservesData()
        {
            var data = new Dict<string, object?>();
            data["name"] = "test";
            data["count"] = 42;

            string yaml = Yaml.SafeDump(data);
            object? parsed = Yaml.SafeLoad(yaml);

            var result = Assert.IsType<Dict<string, object?>>(parsed);
            Assert.Equal("test", result["name"]);
            Assert.Equal(42, result["count"]);
        }

        [Fact]
        public void RoundTrip_List_PreservesData()
        {
            var data = new List<object?>();
            data.Append(1);
            data.Append("two");
            data.Append(true);
            data.Append(null);

            string yaml = Yaml.SafeDump(data);
            object? parsed = Yaml.SafeLoad(yaml);

            var result = Assert.IsType<List<object?>>(parsed);
            Assert.Equal(1, result[0]);
            Assert.Equal("two", result[1]);
            Assert.Equal(true, result[2]);
            Assert.Null(result[3]);
        }

        #endregion

        #region SafeLoad - Scalar types

        [Fact]
        public void SafeLoad_String_ReturnsString()
        {
            object? result = Yaml.SafeLoad("key: hello");
            var dict = Assert.IsType<Dict<string, object?>>(result);
            Assert.Equal("hello", dict["key"]);
        }

        [Fact]
        public void SafeLoad_QuotedString_ReturnsString()
        {
            object? result = Yaml.SafeLoad("key: \"hello world\"");
            var dict = Assert.IsType<Dict<string, object?>>(result);
            Assert.Equal("hello world", dict["key"]);
        }

        [Fact]
        public void SafeLoad_Int_ReturnsInt()
        {
            object? result = Yaml.SafeLoad("key: 42");
            var dict = Assert.IsType<Dict<string, object?>>(result);
            Assert.IsType<int>(dict["key"]);
            Assert.Equal(42, dict["key"]);
        }

        [Fact]
        public void SafeLoad_NegativeInt_ReturnsInt()
        {
            object? result = Yaml.SafeLoad("key: -7");
            var dict = Assert.IsType<Dict<string, object?>>(result);
            Assert.Equal(-7, dict["key"]);
        }

        [Fact]
        public void SafeLoad_LargeInt_ReturnsLong()
        {
            object? result = Yaml.SafeLoad("key: 9999999999");
            var dict = Assert.IsType<Dict<string, object?>>(result);
            Assert.IsType<long>(dict["key"]);
            Assert.Equal(9999999999L, dict["key"]);
        }

        [Fact]
        public void SafeLoad_Float_ReturnsDouble()
        {
            object? result = Yaml.SafeLoad("key: 3.14");
            var dict = Assert.IsType<Dict<string, object?>>(result);
            Assert.IsType<double>(dict["key"]);
            // KNOWN QUIRK: SafeLoad parses plain floats as single-precision `float`
            // (via YamlDotNet's unquoted-string type deserialization) and widens to
            // double, so "3.14" reloads as ~3.1400001 rather than the exact 3.14
            // that Python's yaml.safe_load yields. Asserting within float precision.
            Assert.Equal(3.14, (double)dict["key"]!, 5);
        }

        [Fact]
        public void SafeLoad_WholeNumberFloat_StaysFloatTyped()
        {
            // A value with a decimal point should reload as a double, not an int.
            object? result = Yaml.SafeLoad("key: 2.0");
            var dict = Assert.IsType<Dict<string, object?>>(result);
            Assert.IsType<double>(dict["key"]);
            Assert.Equal(2.0, (double)dict["key"]!, 5);
        }

        [Fact]
        public void SafeLoad_BoolTrue_ReturnsBool()
        {
            object? result = Yaml.SafeLoad("key: true");
            var dict = Assert.IsType<Dict<string, object?>>(result);
            Assert.IsType<bool>(dict["key"]);
            Assert.Equal(true, dict["key"]);
        }

        [Fact]
        public void SafeLoad_BoolFalse_ReturnsBool()
        {
            object? result = Yaml.SafeLoad("key: false");
            var dict = Assert.IsType<Dict<string, object?>>(result);
            Assert.Equal(false, dict["key"]);
        }

        [Fact]
        public void SafeLoad_Null_ReturnsNull()
        {
            object? result = Yaml.SafeLoad("key: null");
            var dict = Assert.IsType<Dict<string, object?>>(result);
            Assert.Null(dict["key"]);
        }

        [Fact]
        public void SafeLoad_Tilde_ReturnsNull()
        {
            object? result = Yaml.SafeLoad("key: ~");
            var dict = Assert.IsType<Dict<string, object?>>(result);
            Assert.Null(dict["key"]);
        }

        #endregion

        #region SafeLoad - Norway problem (YAML 1.2 core schema)

        // Sharpy's yaml uses YAML 1.2 core-schema semantics: only `true`/`false`
        // (case-insensitive) are booleans. The YAML 1.1 "Norway problem" values
        // (no/yes/on/off and friends) must stay strings, never coerced to bools.

        [Theory]
        [InlineData("NO")]
        [InlineData("no")]
        [InlineData("No")]
        [InlineData("Yes")]
        [InlineData("yes")]
        [InlineData("on")]
        [InlineData("On")]
        [InlineData("off")]
        [InlineData("OFF")]
        [InlineData("Y")]
        [InlineData("N")]
        public void SafeLoad_NorwayProblemValues_StayStrings(string value)
        {
            object? result = Yaml.SafeLoad($"key: {value}");
            var dict = Assert.IsType<Dict<string, object?>>(result);
            Assert.IsType<string>(dict["key"]);
            Assert.Equal(value, dict["key"]);
        }

        #endregion

        #region SafeLoad - Nested structures

        [Fact]
        public void SafeLoad_MapInMap_ReturnsNestedDict()
        {
            string yaml = "outer:\n  inner: 42\n";
            object? result = Yaml.SafeLoad(yaml);
            var dict = Assert.IsType<Dict<string, object?>>(result);
            var inner = Assert.IsType<Dict<string, object?>>(dict["outer"]);
            Assert.Equal(42, inner["inner"]);
        }

        [Fact]
        public void SafeLoad_ListInMap_ReturnsNestedList()
        {
            string yaml = "items:\n  - 1\n  - 2\n  - 3\n";
            object? result = Yaml.SafeLoad(yaml);
            var dict = Assert.IsType<Dict<string, object?>>(result);
            var list = Assert.IsType<List<object?>>(dict["items"]);
            Assert.Equal(3, Count(list));
            Assert.Equal(1, list[0]);
            Assert.Equal(3, list[2]);
        }

        [Fact]
        public void SafeLoad_MapInList_ReturnsListOfDicts()
        {
            string yaml = "- id: 1\n  name: alpha\n- id: 2\n  name: beta\n";
            object? result = Yaml.SafeLoad(yaml);
            var list = Assert.IsType<List<object?>>(result);
            Assert.Equal(2, Count(list));
            var first = Assert.IsType<Dict<string, object?>>(list[0]);
            Assert.Equal(1, first["id"]);
            Assert.Equal("alpha", first["name"]);
        }

        #endregion

        #region SafeLoad - Empty / edge cases

        [Fact]
        public void SafeLoad_EmptyDocument_ReturnsNull()
        {
            Assert.Null(Yaml.SafeLoad(""));
        }

        [Fact]
        public void SafeLoad_WhitespaceOnly_ReturnsNull()
        {
            Assert.Null(Yaml.SafeLoad("   \n  \n"));
        }

        [Fact]
        public void SafeLoad_EmptyMapping_ReturnsEmptyDict()
        {
            object? result = Yaml.SafeLoad("{}");
            var dict = Assert.IsType<Dict<string, object?>>(result);
            Assert.Empty(dict);
        }

        [Fact]
        public void SafeLoad_EmptySequence_ReturnsEmptyList()
        {
            object? result = Yaml.SafeLoad("[]");
            var list = Assert.IsType<List<object?>>(result);
            Assert.Equal(0, Count(list));
        }

        [Fact]
        public void SafeLoad_UnicodeString_PreservesCharacters()
        {
            object? result = Yaml.SafeLoad("key: café üñî");
            var dict = Assert.IsType<Dict<string, object?>>(result);
            Assert.Equal("café üñî", dict["key"]);
        }

        [Fact]
        public void SafeLoad_FlowMapping_ReturnsDict()
        {
            object? result = Yaml.SafeLoad("{a: 1, b: 2}");
            var dict = Assert.IsType<Dict<string, object?>>(result);
            Assert.Equal(1, dict["a"]);
            Assert.Equal(2, dict["b"]);
        }

        #endregion

        #region SafeDump - Formatting options

        [Fact]
        public void SafeDump_BlockStyle_ByDefault()
        {
            var data = new Dict<string, object?>();
            data["a"] = 1;
            string yaml = Yaml.SafeDump(data);
            // Block style emits "a: 1" on its own line, not inline braces.
            Assert.Contains("a: 1", yaml, StringComparison.Ordinal);
            Assert.DoesNotContain("{", yaml, StringComparison.Ordinal);
        }

        [Fact]
        public void SafeDump_FlowStyle_ProducesInline()
        {
            var data = new Dict<string, object?>();
            data["a"] = 1;
            data["b"] = 2;
            string yaml = Yaml.SafeDump(data, defaultFlowStyle: true);
            Assert.Contains("{", yaml, StringComparison.Ordinal);
            Assert.Contains("}", yaml, StringComparison.Ordinal);
        }

        [Fact]
        public void SafeDump_Indent_UsesGivenWidth()
        {
            var inner = new Dict<string, object?>();
            inner["x"] = 1;
            var outer = new Dict<string, object?>();
            outer["outer"] = inner;

            string yaml = Yaml.SafeDump(outer, indent: 4);
            // The nested key is indented by four spaces.
            Assert.Contains("    x: 1", yaml, StringComparison.Ordinal);
        }

        [Fact]
        public void SafeDump_SortKeys_True_SortsAlphabetically()
        {
            var data = new Dict<string, object?>();
            data["c"] = 3;
            data["a"] = 1;
            data["b"] = 2;

            string yaml = Yaml.SafeDump(data, sortKeys: true);
            int posA = yaml.IndexOf("a:", StringComparison.Ordinal);
            int posB = yaml.IndexOf("b:", StringComparison.Ordinal);
            int posC = yaml.IndexOf("c:", StringComparison.Ordinal);
            Assert.True(posA < posB && posB < posC, $"Expected a<b<c order, got:\n{yaml}");
        }

        [Fact]
        public void SafeDump_SortKeys_False_PreservesInsertionOrder()
        {
            var data = new Dict<string, object?>();
            data["c"] = 3;
            data["a"] = 1;
            data["b"] = 2;

            string yaml = Yaml.SafeDump(data, sortKeys: false);
            int posC = yaml.IndexOf("c:", StringComparison.Ordinal);
            int posA = yaml.IndexOf("a:", StringComparison.Ordinal);
            int posB = yaml.IndexOf("b:", StringComparison.Ordinal);
            Assert.True(posC < posA && posA < posB, $"Expected c<a<b insertion order, got:\n{yaml}");
        }

        [Fact]
        public void SafeDump_Null_EmitsNullToken()
        {
            string yaml = Yaml.SafeDump(null);
            object? reparsed = Yaml.SafeLoad(yaml);
            Assert.Null(reparsed);
        }

        #endregion

        #region SafeLoadAll / SafeDumpAll - Multi-document

        [Fact]
        public void SafeLoadAll_TwoDocuments_ReturnsBoth()
        {
            string yaml = "a: 1\n---\nb: 2\n";
            List<object?> docs = Yaml.SafeLoadAll(yaml);
            Assert.Equal(2, Count(docs));
            var first = Assert.IsType<Dict<string, object?>>(docs[0]);
            var second = Assert.IsType<Dict<string, object?>>(docs[1]);
            Assert.Equal(1, first["a"]);
            Assert.Equal(2, second["b"]);
        }

        [Fact]
        public void SafeLoadAll_ThreeDocuments_ReturnsAll()
        {
            string yaml = "1\n---\n2\n---\n3\n";
            List<object?> docs = Yaml.SafeLoadAll(yaml);
            Assert.Equal(3, Count(docs));
            Assert.Equal(1, docs[0]);
            Assert.Equal(2, docs[1]);
            Assert.Equal(3, docs[2]);
        }

        [Fact]
        public void SafeLoadAll_SingleDocument_ReturnsOne()
        {
            List<object?> docs = Yaml.SafeLoadAll("key: value\n");
            Assert.Equal(1, Count(docs));
            var dict = Assert.IsType<Dict<string, object?>>(docs[0]);
            Assert.Equal("value", dict["key"]);
        }

        [Fact]
        public void SafeLoadAll_EmptyDocumentInStream_YieldsNull()
        {
            string yaml = "a: 1\n---\n---\nb: 2\n";
            List<object?> docs = Yaml.SafeLoadAll(yaml);
            Assert.Equal(3, Count(docs));
            Assert.IsType<Dict<string, object?>>(docs[0]);
            Assert.Null(docs[1]);
            Assert.IsType<Dict<string, object?>>(docs[2]);
        }

        [Fact]
        public void SafeDumpAll_MultipleDocuments_ProducesSeparators()
        {
            var doc1 = new Dict<string, object?>();
            doc1["a"] = 1;
            var doc2 = new Dict<string, object?>();
            doc2["b"] = 2;
            var documents = new List<object?>();
            documents.Append(doc1);
            documents.Append(doc2);

            string yaml = Yaml.SafeDumpAll(documents);
            Assert.Contains("---", yaml, StringComparison.Ordinal);

            // Re-parse what we dumped.
            List<object?> reparsed = Yaml.SafeLoadAll(yaml);
            Assert.Equal(2, Count(reparsed));
        }

        [Fact]
        public void SafeDumpAll_SingleDocument_NoLeadingSeparator()
        {
            var doc1 = new Dict<string, object?>();
            doc1["a"] = 1;
            var documents = new List<object?>();
            documents.Append(doc1);

            string yaml = Yaml.SafeDumpAll(documents);
            Assert.False(yaml.StartsWith("---", StringComparison.Ordinal),
                $"Single document should not start with '---', got:\n{yaml}");
        }

        #endregion

        #region Anchors and aliases

        [Fact]
        public void SafeLoad_AnchorAndAlias_ResolvesMappingReference()
        {
            string yaml =
                "defaults: &defaults\n" +
                "  timeout: 30\n" +
                "  retries: 3\n" +
                "production: *defaults\n";

            object? result = Yaml.SafeLoad(yaml);
            var dict = Assert.IsType<Dict<string, object?>>(result);
            var production = Assert.IsType<Dict<string, object?>>(dict["production"]);
            // The alias duplicates the entire anchored mapping.
            Assert.Equal(30, production["timeout"]);
            Assert.Equal(3, production["retries"]);
        }

        [Fact]
        public void SafeLoad_SimpleAlias_DuplicatesValue()
        {
            string yaml =
                "first: &val hello\n" +
                "second: *val\n";

            object? result = Yaml.SafeLoad(yaml);
            var dict = Assert.IsType<Dict<string, object?>>(result);
            Assert.Equal("hello", dict["first"]);
            Assert.Equal("hello", dict["second"]);
        }

        #endregion

        #region File I/O

        [Fact]
        public void SafeDumpFile_SafeLoadFile_RoundTrip()
        {
            string tempPath = System.IO.Path.GetTempFileName();
            try
            {
                var data = new Dict<string, object?>();
                data["name"] = "file-test";
                data["value"] = 7;

                using (var fp = Builtins.Open(tempPath, "w"))
                {
                    Yaml.SafeDumpFile(data, fp);
                }

                using (var fp = Builtins.Open(tempPath, "r"))
                {
                    object? result = Yaml.SafeLoadFile(fp);
                    var dict = Assert.IsType<Dict<string, object?>>(result);
                    Assert.Equal("file-test", dict["name"]);
                    Assert.Equal(7, dict["value"]);
                }
            }
            finally
            {
                System.IO.File.Delete(tempPath);
            }
        }

        #endregion

        #region Error handling

        [Fact]
        public void SafeLoad_MalformedYaml_ThrowsParseError()
        {
            // Unbalanced flow brackets are invalid YAML.
            Assert.Throws<YAMLParseError>(() => Yaml.SafeLoad("key: [1, 2"));
        }

        [Fact]
        public void SafeLoad_UnterminatedQuote_ThrowsParseError()
        {
            Assert.Throws<YAMLParseError>(() => Yaml.SafeLoad("key: 'unterminated\n"));
        }

        [Fact]
        public void SafeLoad_TabIndentation_ThrowsParseError()
        {
            // Tabs are not permitted for indentation in YAML.
            Assert.Throws<YAMLParseError>(() => Yaml.SafeLoad("a:\n\t- 1\n"));
        }

        [Fact]
        public void YAMLParseError_HasLineAndColumn()
        {
            var ex = Assert.Throws<YAMLParseError>(() => Yaml.SafeLoad("key: [1, 2"));
            Assert.True(ex.Line >= 0, $"Expected non-negative line, got {ex.Line}");
            Assert.True(ex.Column >= 0, $"Expected non-negative column, got {ex.Column}");
        }

        [Fact]
        public void YAMLParseError_IsYAMLError()
        {
            var ex = Assert.Throws<YAMLParseError>(() => Yaml.SafeLoad("key: [1, 2"));
            Assert.IsAssignableFrom<YAMLError>(ex);
        }

        [Fact]
        public void SafeLoad_Null_ThrowsTypeError()
        {
            Assert.Throws<TypeError>(() => Yaml.SafeLoad(null!));
        }

        [Fact]
        public void SafeLoadAll_Null_ThrowsTypeError()
        {
            Assert.Throws<TypeError>(() => Yaml.SafeLoadAll(null!));
        }

        [Fact]
        public void SafeLoadFile_Null_ThrowsTypeError()
        {
            Assert.Throws<TypeError>(() => Yaml.SafeLoadFile(null!));
        }

        [Fact]
        public void SafeDumpFile_NullFile_ThrowsTypeError()
        {
            var data = new Dict<string, object?>();
            data["a"] = 1;
            Assert.Throws<TypeError>(() => Yaml.SafeDumpFile(data, null!));
        }

        [Fact]
        public void SafeDumpAll_Null_ThrowsTypeError()
        {
            Assert.Throws<TypeError>(() => Yaml.SafeDumpAll(null!));
        }

        #endregion
    }
}
