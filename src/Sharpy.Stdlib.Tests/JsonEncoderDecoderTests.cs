using System;
using FluentAssertions;
using Xunit;

namespace Sharpy.Tests
{
    public class JsonEncoderDecoderTests
    {
        #region JSONEncoder

        [Fact]
        public void Encoder_EncodeBasicTypes_Works()
        {
            var encoder = new JSONEncoder();
            encoder.Encode(42).Should().Be("42");
            encoder.Encode("hello").Should().Be("\"hello\"");
            encoder.Encode(true).Should().Be("true");
            encoder.Encode(null).Should().Be("null");
        }

        [Fact]
        public void Encoder_EncodeDict_Works()
        {
            var encoder = new JSONEncoder();
            var d = new Dict<string, object?> { { "a", 1 } };
            encoder.Encode(d).Should().Be("{\"a\": 1}");
        }

        [Fact]
        public void Encoder_WithIndent_PrettyPrints()
        {
            var encoder = new JSONEncoder(indent: 2);
            var d = new Dict<string, object?> { { "a", 1 } };
            var result = encoder.Encode(d);
            result.Should().Contain("\n");
            result.Should().Contain("  ");
        }

        [Fact]
        public void Encoder_WithSortKeys_SortsKeys()
        {
            var encoder = new JSONEncoder(sortKeys: true);
            var d = new Dict<string, object?> { { "b", 2 }, { "a", 1 } };
            var result = encoder.Encode(d);
            result.Should().Be("{\"a\": 1, \"b\": 2}");
        }

        [Fact]
        public void Encoder_WithSeparators_UsesCustomSeparators()
        {
            var encoder = new JSONEncoder(separators: (",", ":"));
            var d = new Dict<string, object?> { { "a", 1 }, { "b", 2 } };
            var result = encoder.Encode(d);
            result.Should().Be("{\"a\":1,\"b\":2}");
        }

        [Fact]
        public void Encoder_DefaultThrowsTypeError_ForUnknownTypes()
        {
            var encoder = new JSONEncoder();
            Action act = () => encoder.Default(new object());
            act.Should().Throw<TypeError>();
        }

        [Fact]
        public void Encoder_DefaultCalledForNonSerializable()
        {
            var encoder = new CustomEncoder();
            var result = encoder.Encode(new TestPoint(1, 2));
            result.Should().Be("[1, 2]");
        }

        [Fact]
        public void Encoder_ClsParameterOnDumps_Dispatches()
        {
            var encoder = new CustomEncoder();
            var result = Json.Dumps(new TestPoint(3, 4), cls: encoder);
            result.Should().Be("[3, 4]");
        }

        private class TestPoint
        {
            public int X { get; }
            public int Y { get; }
            public TestPoint(int x, int y) { X = x; Y = y; }
        }

        private class CustomEncoder : JSONEncoder
        {
            public override object? Default(object obj)
            {
                if (obj is TestPoint p)
                {
                    return new List<object?> { p.X, p.Y };
                }
                return base.Default(obj);
            }
        }

        #endregion

        #region JSONDecoder

        [Fact]
        public void Decoder_DecodeBasicTypes_Works()
        {
            var decoder = new JSONDecoder();
            decoder.Decode("42").Should().Be(42);
            decoder.Decode("\"hello\"").Should().Be("hello");
            decoder.Decode("true").Should().Be(true);
            decoder.Decode("null").Should().BeNull();
        }

        [Fact]
        public void Decoder_DecodeDict_Works()
        {
            var decoder = new JSONDecoder();
            var result = decoder.Decode("{\"a\": 1}");
            result.Should().BeOfType<Dict<string, object?>>();
            var dict = (Dict<string, object?>)result!;
            dict["a"].Should().Be(1);
        }

        [Fact]
        public void Decoder_WithObjectHook_AppliesHookToAllDicts()
        {
            int hookCallCount = 0;
            var decoder = new JSONDecoder(objectHook: d =>
            {
                hookCallCount++;
                d["_hooked"] = true;
                return d;
            });

            var result = decoder.Decode("{\"a\": {\"b\": 1}}");
            hookCallCount.Should().Be(2);
            var outer = result as Dict<string, object?>;
            outer!["_hooked"].Should().Be(true);
        }

        [Fact]
        public void Decoder_ClsParameterOnLoads_Dispatches()
        {
            var decoder = new JSONDecoder(objectHook: d =>
            {
                d["injected"] = "yes";
                return d;
            });
            var result = Json.Loads("{\"a\": 1}", cls: decoder) as Dict<string, object?>;
            result!["injected"].Should().Be("yes");
        }

        #endregion

        #region object_hook on Json.Loads directly

        [Fact]
        public void Loads_ObjectHookParameter_AppliesHook()
        {
            var result = Json.Loads(
                "{\"x\": 1}",
                objectHook: d =>
                {
                    d["transformed"] = true;
                    return d;
                }) as Dict<string, object?>;
            result!["transformed"].Should().Be(true);
        }

        [Fact]
        public void Loads_ObjectHookOnNestedDicts_AppliesRecursively()
        {
            int count = 0;
            Json.Loads(
                "{\"inner\": {\"deep\": {}}}",
                objectHook: d =>
                {
                    count++;
                    return d;
                });
            count.Should().Be(3);
        }

        [Fact]
        public void Loads_ObjectHookOnArray_AppliesOnlyToDicts()
        {
            int count = 0;
            Json.Loads(
                "[{\"a\": 1}, {\"b\": 2}]",
                objectHook: d =>
                {
                    count++;
                    return d;
                });
            count.Should().Be(2);
        }

        #endregion

        #region RawDecode

        [Fact]
        public void Decoder_RawDecode_ReturnsValueAndEndIndex()
        {
            var decoder = new JSONDecoder();
            var (value, endIdx) = decoder.RawDecode("{\"a\": 1}");
            value.Should().BeOfType<Dict<string, object?>>();
            endIdx.Should().Be(8);
        }

        #endregion
    }
}
