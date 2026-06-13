// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using static global::Sharpy.Unittest;
using json = global::Sharpy.Json;
using @operator = global::Sharpy.Operator;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.JSON.JsonEncoderDecoderTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class JSON
    {
        [global::Sharpy.SharpyModule("json.json_encoder_decoder_tests")]
        public static partial class JsonEncoderDecoderTests
        {
            public class Unserializable
            {
                public int Marker = 0;
            }
        }
    }

    public static partial class JSON
    {
        public partial class JsonEncoderDecoderTestsTests
        {
            [Xunit.FactAttribute]
            public void TestEncoderEncodeBasicTypesWorks()
            {
#line (18, 5) - (18, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                var encoder = new global::Sharpy.JSONEncoder();
#line (19, 5) - (19, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.Equal("42", encoder.Encode(42));
#line (20, 5) - (20, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.Equal("\"hello\"", encoder.Encode("hello"));
#line (21, 5) - (21, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.Equal("true", encoder.Encode(true));
#line (22, 5) - (22, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.Equal("null", encoder.Encode(null));
            }

            [Xunit.FactAttribute]
            public void TestEncoderEncodeDictWorks()
            {
#line (26, 5) - (26, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                var encoder = new global::Sharpy.JSONEncoder();
#line (27, 5) - (27, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (28, 5) - (28, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                d["a"] = 1;
#line (29, 5) - (29, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.Equal("{\"a\": 1}", encoder.Encode(d));
            }

            [Xunit.FactAttribute]
            public void TestEncoderWithIndentPrettyPrints()
            {
#line (33, 5) - (33, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                var encoder = new global::Sharpy.JSONEncoder(indent: 2);
#line (34, 5) - (34, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (35, 5) - (35, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                d["a"] = 1;
#line (36, 5) - (36, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                string result = encoder.Encode(d);
#line (37, 5) - (37, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.Contains("\n", result);
#line (38, 5) - (38, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.Contains("  ", result);
            }

            [Xunit.FactAttribute]
            public void TestEncoderWithSortKeysSortsKeys()
            {
#line (42, 5) - (42, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                var encoder = new global::Sharpy.JSONEncoder(sortKeys: true);
#line (43, 5) - (43, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (44, 5) - (44, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                d["b"] = 2;
#line (45, 5) - (45, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                d["a"] = 1;
#line (46, 5) - (46, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.Equal("{\"a\": 1, \"b\": 2}", encoder.Encode(d));
            }

            [Xunit.FactAttribute]
            public void TestEncoderWithSeparatorsUsesCustomSeparators()
            {
#line (50, 5) - (50, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                var encoder = new global::Sharpy.JSONEncoder(separators: (",", ":"));
#line (51, 5) - (51, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (52, 5) - (52, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                d["a"] = 1;
#line (53, 5) - (53, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                d["b"] = 2;
#line (54, 5) - (54, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.Equal("{\"a\":1,\"b\":2}", encoder.Encode(d));
            }

            [Xunit.FactAttribute]
            public void TestEncoderDefaultThrowsTypeErrorForUnknownTypes()
            {
#line (58, 5) - (58, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                var encoder = new global::Sharpy.JSONEncoder();
#line (59, 5) - (64, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.Throws<TypeError>((global::System.Action)(() =>
                {
#line (60, 9) - (60, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                    encoder.Default(new Unserializable());
                }));
            }

            [Xunit.FactAttribute]
            public void TestDecoderDecodeBasicTypesWorks()
            {
#line (66, 5) - (66, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                var decoder = new global::Sharpy.JSONDecoder();
#line (67, 5) - (67, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.True(@operator.Eq(decoder.Decode("42"), 42));
#line (68, 5) - (68, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.True(@operator.Eq(decoder.Decode("\"hello\""), "hello"));
#line (69, 5) - (69, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.True(@operator.Eq(decoder.Decode("true"), true));
#line (70, 5) - (70, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.Null(decoder.Decode("null"));
            }

            [Xunit.FactAttribute]
            public void TestDecoderDecodeDictWorks()
            {
#line (74, 5) - (74, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                var decoder = new global::Sharpy.JSONDecoder();
#line (75, 5) - (75, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                object result = decoder.Decode("{\"a\": 1}");
#line (76, 5) - (84, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (78, 13) - (78, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["a"], 1));
                        break;
                    default:
#line (80, 13) - (80, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestDecoderRawDecodeReturnsValueAndEndIndex()
            {
#line (86, 5) - (86, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                var decoder = new global::Sharpy.JSONDecoder();
#line (87, 5) - (87, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                var (value, endIdx) = decoder.RawDecode("{\"a\": 1}");
#line (88, 5) - (93, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                switch (value)
                {
                    case global::Sharpy.IDict _:
#line (90, 13) - (90, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                        ;
                        break;
                    default:
#line (92, 13) - (92, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (93, 5) - (93, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.Equal(8, endIdx);
            }
        }
    }
}
