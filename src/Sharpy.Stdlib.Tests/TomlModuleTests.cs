using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace Sharpy.Tests
{
    public class TomlModuleTests
    {
        #region Loads - Basic Types

        [Fact]
        public void Loads_String_ReturnsDict()
        {
            var result = Toml.Loads("key = \"hello\"");
            result["key"].Should().Be("hello");
        }

        [Fact]
        public void Loads_Integer_ReturnsLong()
        {
            var result = Toml.Loads("count = 42");
            result["count"].Should().Be(42L);
        }

        [Fact]
        public void Loads_NegativeInteger_ReturnsLong()
        {
            var result = Toml.Loads("val = -10");
            result["val"].Should().Be(-10L);
        }

        [Fact]
        public void Loads_Float_ReturnsDouble()
        {
            var result = Toml.Loads("pi = 3.14");
            result["pi"].Should().Be(3.14);
        }

        [Fact]
        public void Loads_Boolean_ReturnsBool()
        {
            var result = Toml.Loads("flag = true");
            result["flag"].Should().Be(true);
        }

        [Fact]
        public void Loads_FalseBoolean_ReturnsBool()
        {
            var result = Toml.Loads("flag = false");
            result["flag"].Should().Be(false);
        }

        #endregion

        #region Loads - DateTime Types

        [Fact]
        public void Loads_OffsetDateTime_ReturnsDateTimeOffset()
        {
            var result = Toml.Loads("dt = 2024-01-15T10:30:00Z");
            result["dt"].Should().BeOfType<DateTimeOffset>();
            var dto = (DateTimeOffset)result["dt"]!;
            dto.Year.Should().Be(2024);
            dto.Month.Should().Be(1);
            dto.Day.Should().Be(15);
            dto.Hour.Should().Be(10);
            dto.Minute.Should().Be(30);
        }

        [Fact]
        public void Loads_LocalDateTime_ReturnsDateTime()
        {
            var result = Toml.Loads("dt = 2024-01-15T10:30:00");
            result["dt"].Should().BeOfType<System.DateTime>();
        }

        [Fact]
        public void Loads_LocalDate_ReturnsDateTime()
        {
            var result = Toml.Loads("d = 2024-01-15");
            result["d"].Should().BeOfType<System.DateTime>();
            var dt = (System.DateTime)result["d"]!;
            dt.Year.Should().Be(2024);
            dt.Month.Should().Be(1);
            dt.Day.Should().Be(15);
        }

        [Fact]
        public void Loads_LocalTime_ReturnsTimeSpan()
        {
            var result = Toml.Loads("t = 10:30:00");
            result["t"].Should().BeOfType<TimeSpan>();
            var ts = (TimeSpan)result["t"]!;
            ts.Hours.Should().Be(10);
            ts.Minutes.Should().Be(30);
        }

        #endregion

        #region Loads - Collections

        [Fact]
        public void Loads_Array_ReturnsList()
        {
            var result = Toml.Loads("arr = [1, 2, 3]");
            result["arr"].Should().BeOfType<List<object?>>();
            var list = (List<object?>)result["arr"]!;
            list.Should().HaveCount(3);
            list[0].Should().Be(1L);
            list[1].Should().Be(2L);
            list[2].Should().Be(3L);
        }

        [Fact]
        public void Loads_Table_ReturnsDict()
        {
            var toml = "[server]\nhost = \"localhost\"\nport = 8080";
            var result = Toml.Loads(toml);
            result["server"].Should().BeOfType<Dict<string, object?>>();
            var server = (Dict<string, object?>)result["server"]!;
            server["host"].Should().Be("localhost");
            server["port"].Should().Be(8080L);
        }

        [Fact]
        public void Loads_NestedTables_ReturnsNestedDicts()
        {
            var toml = "[a]\n[a.b]\n[a.b.c]\nval = 1";
            var result = Toml.Loads(toml);
            var a = (Dict<string, object?>)result["a"]!;
            var b = (Dict<string, object?>)a["b"]!;
            var c = (Dict<string, object?>)b["c"]!;
            c["val"].Should().Be(1L);
        }

        [Fact]
        public void Loads_DottedKeys()
        {
            var result = Toml.Loads("a.b.c = 42");
            var a = (Dict<string, object?>)result["a"]!;
            var b = (Dict<string, object?>)a["b"]!;
            b["c"].Should().Be(42L);
        }

        [Fact]
        public void Loads_InlineTable()
        {
            var result = Toml.Loads("point = {x = 1, y = 2}");
            var point = (Dict<string, object?>)result["point"]!;
            point["x"].Should().Be(1L);
            point["y"].Should().Be(2L);
        }

        [Fact]
        public void Loads_ArrayOfTables()
        {
            var toml = "[[products]]\nname = \"Hammer\"\n\n[[products]]\nname = \"Nail\"";
            var result = Toml.Loads(toml);
            var products = (List<object?>)result["products"]!;
            products.Should().HaveCount(2);
            var p0 = (Dict<string, object?>)products[0]!;
            p0["name"].Should().Be("Hammer");
            var p1 = (Dict<string, object?>)products[1]!;
            p1["name"].Should().Be("Nail");
        }

        #endregion

        #region Loads - Strings

        [Fact]
        public void Loads_MultilineBasicString()
        {
            var toml = "s = \"\"\"\nhello\nworld\"\"\"";
            var result = Toml.Loads(toml);
            ((string)result["s"]!).Should().Contain("hello");
            ((string)result["s"]!).Should().Contain("world");
        }

        [Fact]
        public void Loads_LiteralString()
        {
            var toml = "path = 'C:\\Users\\foo'";
            var result = Toml.Loads(toml);
            result["path"].Should().Be("C:\\Users\\foo");
        }

        #endregion

        #region Loads - Integers

        [Fact]
        public void Loads_HexInteger()
        {
            var result = Toml.Loads("val = 0xff");
            result["val"].Should().Be(255L);
        }

        [Fact]
        public void Loads_OctalInteger()
        {
            var result = Toml.Loads("val = 0o77");
            result["val"].Should().Be(63L);
        }

        [Fact]
        public void Loads_BinaryInteger()
        {
            var result = Toml.Loads("val = 0b1010");
            result["val"].Should().Be(10L);
        }

        [Fact]
        public void Loads_UnderscoreInteger()
        {
            var result = Toml.Loads("val = 1_000_000");
            result["val"].Should().Be(1_000_000L);
        }

        #endregion

        #region Loads - Special Floats

        [Fact]
        public void Loads_PositiveInfinity()
        {
            var result = Toml.Loads("val = inf");
            result["val"].Should().Be(double.PositiveInfinity);
        }

        [Fact]
        public void Loads_NegativeInfinity()
        {
            var result = Toml.Loads("val = -inf");
            result["val"].Should().Be(double.NegativeInfinity);
        }

        [Fact]
        public void Loads_NaN()
        {
            var result = Toml.Loads("val = nan");
            ((double)result["val"]!).Should().Be(double.NaN);
        }

        #endregion

        #region Loads - Edge Cases

        [Fact]
        public void Loads_EmptyDocument_ReturnsEmptyDict()
        {
            var result = Toml.Loads("");
            result.Should().BeEmpty();
        }

        [Fact]
        public void Loads_CommentOnly_ReturnsEmptyDict()
        {
            var result = Toml.Loads("# just a comment");
            result.Should().BeEmpty();
        }

        [Fact]
        public void Loads_NullInput_ThrowsTypeError()
        {
            var act = () => Toml.Loads(null!);
            act.Should().Throw<TypeError>();
        }

        #endregion

        #region Dumps

        [Fact]
        public void Dumps_SimpleDict_ReturnsToml()
        {
            var dict = new Dict<string, object?>();
            dict["name"] = "test";
            dict["count"] = 42L;

            var result = Toml.Dumps(dict);
            result.Should().Contain("name = \"test\"");
            result.Should().Contain("count = 42");
        }

        [Fact]
        public void Dumps_NestedDict_ReturnsToml()
        {
            var inner = new Dict<string, object?>();
            inner["key"] = "val";
            var dict = new Dict<string, object?>();
            dict["section"] = inner;

            var result = Toml.Dumps(dict);
            result.Should().Contain("[section]");
            result.Should().Contain("key = \"val\"");
        }

        [Fact]
        public void Dumps_WithArray_ReturnsToml()
        {
            var dict = new Dict<string, object?>();
            var list = new List<object?>();
            list.Append("a");
            list.Append("b");
            dict["tags"] = list;

            var result = Toml.Dumps(dict);
            result.Should().Contain("tags");
            result.Should().Contain("\"a\"");
            result.Should().Contain("\"b\"");
        }

        [Fact]
        public void Dumps_SortKeys_SortsAlphabetically()
        {
            var dict = new Dict<string, object?>();
            dict["zebra"] = 1L;
            dict["alpha"] = 2L;

            var result = Toml.Dumps(dict, sortKeys: true);
            var alphaIdx = result.IndexOf("alpha", StringComparison.Ordinal);
            var zebraIdx = result.IndexOf("zebra", StringComparison.Ordinal);
            alphaIdx.Should().BeLessThan(zebraIdx);
        }

        [Fact]
        public void Dumps_NonDict_ThrowsTypeError()
        {
            var act = () => Toml.Dumps("not a dict");
            act.Should().Throw<TypeError>();
        }

        [Fact]
        public void Dumps_Null_ThrowsTypeError()
        {
            var act = () => Toml.Dumps(null);
            act.Should().Throw<TypeError>();
        }

        #endregion

        #region Roundtrip

        [Fact]
        public void Roundtrip_SimpleDict()
        {
            var dict = new Dict<string, object?>();
            dict["name"] = "test";
            dict["count"] = 42L;
            dict["active"] = true;
            dict["ratio"] = 3.14;

            var toml = Toml.Dumps(dict);
            var result = Toml.Loads(toml);

            result["name"].Should().Be("test");
            result["count"].Should().Be(42L);
            result["active"].Should().Be(true);
            result["ratio"].Should().Be(3.14);
        }

        [Fact]
        public void Roundtrip_NestedDict()
        {
            var inner = new Dict<string, object?>();
            inner["host"] = "localhost";
            inner["port"] = 8080L;

            var dict = new Dict<string, object?>();
            dict["server"] = inner;

            var toml = Toml.Dumps(dict);
            var result = Toml.Loads(toml);

            var server = (Dict<string, object?>)result["server"]!;
            server["host"].Should().Be("localhost");
            server["port"].Should().Be(8080L);
        }

        #endregion

        #region Error Handling

        [Fact]
        public void Loads_MalformedToml_ThrowsTOMLDecodeError()
        {
            var act = () => Toml.Loads("invalid = [");
            act.Should().Throw<TOMLDecodeError>();
        }

        [Fact]
        public void Loads_MalformedToml_ErrorIsValueError()
        {
            var act = () => Toml.Loads("invalid = [");
            act.Should().Throw<ValueError>();
        }

        #endregion

        #region File I/O

        [Fact]
        public void Load_NullFp_ThrowsTypeError()
        {
            var act = () => Toml.Load(null!);
            act.Should().Throw<TypeError>();
        }

        [Fact]
        public void Dump_NullFp_ThrowsTypeError()
        {
            var dict = new Dict<string, object?>();
            dict["key"] = "val";
            var act = () => Toml.Dump(dict, null!);
            act.Should().Throw<TypeError>();
        }

        [Fact]
        public void LoadFile_Roundtrip()
        {
            var path = System.IO.Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "name = \"test\"\ncount = 42");
                var result = Toml.LoadFile(path);
                result["name"].Should().Be("test");
                result["count"].Should().Be(42L);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void DumpFile_Roundtrip()
        {
            var path = System.IO.Path.GetTempFileName();
            try
            {
                var dict = new Dict<string, object?>();
                dict["name"] = "test";
                dict["count"] = 42L;
                Toml.DumpFile(dict, path);

                var result = Toml.LoadFile(path);
                result["name"].Should().Be("test");
                result["count"].Should().Be(42L);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void LoadFile_Nonexistent_ThrowsFileNotFoundError()
        {
            var act = () => Toml.LoadFile("/nonexistent/path/file.toml");
            act.Should().Throw<FileNotFoundError>();
        }

        [Fact]
        public void LoadFile_NullPath_ThrowsTypeError()
        {
            var act = () => Toml.LoadFile(null!);
            act.Should().Throw<TypeError>();
        }

        [Fact]
        public void DumpFile_NullPath_ThrowsTypeError()
        {
            var dict = new Dict<string, object?>();
            dict["key"] = "val";
            var act = () => Toml.DumpFile(dict, null!);
            act.Should().Throw<TypeError>();
        }

        #endregion
    }
}
