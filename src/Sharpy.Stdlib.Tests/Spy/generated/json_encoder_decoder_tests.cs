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
            internal static Sharpy.Dict<string, object> _TagHook(Sharpy.Dict<string, object> d)
            {
#line (96, 5) - (96, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                d["_hooked"] = true;
#line (97, 5) - (97, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                return d;
            }

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
#line (15, 5) - (15, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                var encoder = new global::Sharpy.JSONEncoder();
#line (16, 5) - (16, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.Equal("42", encoder.Encode(42));
#line (17, 5) - (17, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.Equal("\"hello\"", encoder.Encode("hello"));
#line (18, 5) - (18, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.Equal("true", encoder.Encode(true));
#line (19, 5) - (19, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.Equal("null", encoder.Encode(null));
            }

            [Xunit.FactAttribute]
            public void TestEncoderEncodeDictWorks()
            {
#line (23, 5) - (23, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                var encoder = new global::Sharpy.JSONEncoder();
#line (24, 5) - (24, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (25, 5) - (25, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                d["a"] = 1;
#line (26, 5) - (26, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.Equal("{\"a\": 1}", encoder.Encode(d));
            }

            [Xunit.FactAttribute]
            public void TestEncoderWithIndentPrettyPrints()
            {
#line (30, 5) - (30, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                var encoder = new global::Sharpy.JSONEncoder(indent: 2);
#line (31, 5) - (31, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (32, 5) - (32, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                d["a"] = 1;
#line (33, 5) - (33, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                string result = encoder.Encode(d);
#line (34, 5) - (34, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.Contains("\n", result);
#line (35, 5) - (35, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.Contains("  ", result);
            }

            [Xunit.FactAttribute]
            public void TestEncoderWithSortKeysSortsKeys()
            {
#line (39, 5) - (39, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                var encoder = new global::Sharpy.JSONEncoder(sortKeys: true);
#line (40, 5) - (40, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (41, 5) - (41, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                d["b"] = 2;
#line (42, 5) - (42, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                d["a"] = 1;
#line (43, 5) - (43, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.Equal("{\"a\": 1, \"b\": 2}", encoder.Encode(d));
            }

            [Xunit.FactAttribute]
            public void TestEncoderWithSeparatorsUsesCustomSeparators()
            {
#line (47, 5) - (47, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                var encoder = new global::Sharpy.JSONEncoder(separators: (",", ":"));
#line (48, 5) - (48, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Sharpy.Dict<string, object> d = new Sharpy.Dict<string, object>()
                {
                };
#line (49, 5) - (49, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                d["a"] = 1;
#line (50, 5) - (50, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                d["b"] = 2;
#line (51, 5) - (51, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.Equal("{\"a\":1,\"b\":2}", encoder.Encode(d));
            }

            [Xunit.FactAttribute]
            public void TestEncoderDefaultThrowsTypeErrorForUnknownTypes()
            {
#line (55, 5) - (55, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                var encoder = new global::Sharpy.JSONEncoder();
#line (56, 5) - (61, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.Throws<TypeError>((global::System.Action)(() =>
                {
#line (57, 9) - (57, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                    encoder.Default(new Unserializable());
                }));
            }

            [Xunit.FactAttribute]
            public void TestDecoderDecodeBasicTypesWorks()
            {
#line (63, 5) - (63, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                var decoder = new global::Sharpy.JSONDecoder();
#line (64, 5) - (64, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.True(@operator.Eq(decoder.Decode("42"), 42));
#line (65, 5) - (65, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.True(@operator.Eq(decoder.Decode("\"hello\""), "hello"));
#line (66, 5) - (66, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.True(@operator.Eq(decoder.Decode("true"), true));
#line (67, 5) - (67, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.Null(decoder.Decode("null"));
            }

            [Xunit.FactAttribute]
            public void TestDecoderDecodeDictWorks()
            {
#line (71, 5) - (71, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                var decoder = new global::Sharpy.JSONDecoder();
#line (72, 5) - (72, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                object result = decoder.Decode("{\"a\": 1}");
#line (73, 5) - (81, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (75, 13) - (75, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["a"], 1));
                        break;
                    default:
#line (77, 13) - (77, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestDecoderRawDecodeReturnsValueAndEndIndex()
            {
#line (83, 5) - (83, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                var decoder = new global::Sharpy.JSONDecoder();
#line (84, 5) - (84, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                var (value, endIdx) = decoder.RawDecode("{\"a\": 1}");
#line (85, 5) - (90, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                switch (value)
                {
                    case global::Sharpy.IDict _:
#line (87, 13) - (87, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                        ;
                        break;
                    default:
#line (89, 13) - (89, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }

#line (90, 5) - (90, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.Equal(8, endIdx);
            }

            [Xunit.FactAttribute]
            public void TestDecoderWithObjectHookAppliesHookToAllDicts()
            {
#line (101, 5) - (101, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                var decoder = new global::Sharpy.JSONDecoder(objectHook: _TagHook!);
#line (102, 5) - (102, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                object result = decoder.Decode("{\"a\": {\"b\": 1}}");
#line (103, 5) - (109, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict outer:
#line (105, 13) - (105, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                        Xunit.Assert.True(@operator.Eq(outer["_hooked"], true));
                        break;
                    default:
#line (107, 13) - (107, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestDecoderClsParameterOnLoadsDispatches()
            {
#line (111, 5) - (114, 5) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Sharpy.Dict<string, object> InjectHook(Sharpy.Dict<string, object> d)
#line 111 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                {
#line (112, 9) - (112, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                    d["injected"] = "yes";
#line (113, 9) - (113, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                    return d;
                }

#line (114, 5) - (114, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                var decoder = new global::Sharpy.JSONDecoder(objectHook: InjectHook!);
#line (115, 5) - (115, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                object result = json.Loads("{\"a\": 1}", cls: decoder);
#line (116, 5) - (122, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (118, 13) - (118, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["injected"], "yes"));
                        break;
                    default:
#line (120, 13) - (120, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsObjectHookParameterAppliesHook()
            {
#line (124, 5) - (124, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                object result = json.Loads("{\"x\": 1}", objectHook: _TagHook!);
#line (125, 5) - (131, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                switch (result)
                {
                    case global::Sharpy.IDict d:
#line (127, 13) - (127, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                        Xunit.Assert.True(@operator.Eq(d["_hooked"], true));
                        break;
                    default:
#line (129, 13) - (129, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestLoadsObjectHookOnNestedDictsAppliesRecursively()
            {
#line (133, 5) - (133, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Sharpy.List<int> count = new Sharpy.List<int>()
                {
                    0
                };
#line (134, 5) - (137, 5) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Sharpy.Dict<string, object> CountingHook(Sharpy.Dict<string, object> d)
#line 134 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                {
#line (135, 9) - (135, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                    count[0] = count[0] + 1;
#line (136, 9) - (136, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                    return d;
                }

#line (137, 5) - (137, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                json.Loads("{\"inner\": {\"deep\": {}}}", objectHook: CountingHook!);
#line (138, 5) - (138, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.Equal(3, count[0]);
            }

            [Xunit.FactAttribute]
            public void TestLoadsObjectHookOnArrayAppliesOnlyToDicts()
            {
#line (142, 5) - (142, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Sharpy.List<int> count = new Sharpy.List<int>()
                {
                    0
                };
#line (143, 5) - (146, 5) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Sharpy.Dict<string, object> CountingHook(Sharpy.Dict<string, object> d)
#line 143 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                {
#line (144, 9) - (144, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                    count[0] = count[0] + 1;
#line (145, 9) - (145, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                    return d;
                }

#line (146, 5) - (146, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                json.Loads("[{\"a\": 1}, {\"b\": 2}]", objectHook: CountingHook!);
#line (147, 5) - (147, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_encoder_decoder_tests.spy"
                Xunit.Assert.Equal(2, count[0]);
            }
        }
    }
}
