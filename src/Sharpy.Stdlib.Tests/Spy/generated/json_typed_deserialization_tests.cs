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
using math = global::Sharpy.MathModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.JSON.JsonTypedDeserializationTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class JSON
    {
        [global::Sharpy.SharpyModule("json.json_typed_deserialization_tests")]
        public static partial class JsonTypedDeserializationTests
        {
            public class SimpleRecord
            {
                public string Name = "";
                public int Age = 0;
                public bool Active = false;
            }

            public class NestedRecord
            {
                public string Label = "";
                public SimpleRecord Inner = new SimpleRecord();
            }

            public class RecordWithList
            {
                public string Name = "";
                public Sharpy.List<int> Scores = new Sharpy.List<int>()
                {
                };
            }

            public class RecordWithDict
            {
                public string Name = "";
                public Sharpy.Dict<string, int> Metadata = new Sharpy.Dict<string, int>()
                {
                };
            }

            public class RecordWithOptional
            {
                public string Name = "";
                public Optional<string> Nickname = Optional<string>.None;
                public int Count = 0;
            }
        }
    }

    public static partial class JSON
    {
        public partial class JsonTypedDeserializationTestsTests : global::System.IDisposable
        {
            private readonly global::Sharpy.TmpPathFixture _tmpPathFixture = new global::Sharpy.TmpPathFixture();
            [Xunit.FactAttribute]
            public void TestLoadsTSimpleRecordDeserializes()
            {
#line (45, 5) - (45, 94) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<SimpleRecord>("{\"name\": \"Alice\", \"age\": 30, \"active\": true}");
#line (46, 5) - (46, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (47, 5) - (47, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var record = result.Unwrap();
#line (48, 5) - (48, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("Alice", record.Name);
#line (49, 5) - (49, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(30, record.Age);
#line (50, 5) - (50, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(record.Active);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTSnakeCaseMappingDeserializes()
            {
#line (54, 5) - (54, 106) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<RecordWithOptional>("{\"name\": \"Bob\", \"nickname\": \"Bobby\", \"count\": 5}");
#line (55, 5) - (55, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (56, 5) - (56, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var record = result.Unwrap();
#line (57, 5) - (57, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("Bob", record.Name);
#line (58, 5) - (58, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("Bobby", record.Nickname);
#line (59, 5) - (59, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(5, record.Count);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTNestedRecordDeserializes()
            {
#line (63, 5) - (63, 130) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<NestedRecord>("{\"label\": \"outer\", \"inner\": {\"name\": \"inner\", \"age\": 10, \"active\": false}}");
#line (64, 5) - (64, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (65, 5) - (65, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var record = result.Unwrap();
#line (66, 5) - (66, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("outer", record.Label);
#line (67, 5) - (67, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("inner", record.Inner.Name);
#line (68, 5) - (68, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(10, record.Inner.Age);
#line (69, 5) - (69, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.False(record.Inner.Active);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTRecordWithListDeserializes()
            {
#line (73, 5) - (73, 94) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<RecordWithList>("{\"name\": \"Charlie\", \"scores\": [100, 95, 87]}");
#line (74, 5) - (74, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (75, 5) - (75, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var record = result.Unwrap();
#line (76, 5) - (76, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("Charlie", record.Name);
#line (77, 5) - (77, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Sharpy.List<int> scores = record.Scores;
#line (78, 5) - (78, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(scores));
#line (79, 5) - (79, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(100, scores.GetItemUnchecked(0));
#line (80, 5) - (80, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(95, scores.GetItemUnchecked(1));
#line (81, 5) - (81, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(87, scores.GetItemUnchecked(2));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTRecordWithDictDeserializes()
            {
#line (85, 5) - (85, 100) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<RecordWithDict>("{\"name\": \"Dave\", \"metadata\": {\"x\": 1, \"y\": 2}}");
#line (86, 5) - (86, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (87, 5) - (87, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var record = result.Unwrap();
#line (88, 5) - (88, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("Dave", record.Name);
#line (89, 5) - (89, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Sharpy.Dict<string, int> metadata = record.Metadata;
#line (90, 5) - (90, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(1, metadata["x"]);
#line (91, 5) - (91, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(2, metadata["y"]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTOptionalFieldMissingDefaultsToNull()
            {
#line (95, 5) - (95, 81) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<RecordWithOptional>("{\"name\": \"Eve\", \"count\": 3}");
#line (96, 5) - (96, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (97, 5) - (97, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var record = result.Unwrap();
#line (98, 5) - (98, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("Eve", record.Name);
#line (99, 5) - (99, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(record.Nickname.IsNone);
#line (100, 5) - (100, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(3, record.Count);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTCaseInsensitiveDeserializes()
            {
#line (104, 5) - (104, 94) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<SimpleRecord>("{\"Name\": \"Frank\", \"Age\": 25, \"Active\": true}");
#line (105, 5) - (105, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (106, 5) - (106, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("Frank", result.Unwrap().Name);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTInvalidJsonReturnsErr()
            {
#line (112, 5) - (112, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<SimpleRecord>("{invalid}");
#line (113, 5) - (113, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsErr);
#line (114, 5) - (114, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var err = result.UnwrapErr();
#line (115, 5) - (115, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("{invalid}", err.Doc);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTTypeMismatchReturnsErr()
            {
#line (119, 5) - (119, 108) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<SimpleRecord>("{\"name\": \"Alice\", \"age\": \"not a number\", \"active\": true}");
#line (120, 5) - (120, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsErr);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTEmptyStringReturnsErr()
            {
#line (124, 5) - (124, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<SimpleRecord>("");
#line (125, 5) - (125, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsErr);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTOkResultUnwrapReturnsValue()
            {
#line (131, 5) - (131, 93) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<SimpleRecord>("{\"name\": \"Test\", \"age\": 1, \"active\": false}");
#line (132, 5) - (132, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (133, 5) - (133, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.False(result.IsErr);
#line (134, 5) - (134, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var record = result.Unwrap();
#line (135, 5) - (135, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("Test", record.Name);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTErrResultUnwrapThrows()
            {
#line (142, 5) - (142, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<SimpleRecord>("not json");
#line (143, 5) - (143, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsErr);
#line (144, 5) - (144, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.False(result.IsOk);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTErrResultErrorHasMessage()
            {
#line (148, 5) - (148, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<SimpleRecord>("{bad}");
#line (149, 5) - (149, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsErr);
#line (150, 5) - (150, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var err = result.UnwrapErr();
#line (151, 5) - (151, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Len(err.Msg) > 0);
#line (152, 5) - (152, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("{bad}", err.Doc);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadTFileRoundTripDeserializes()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (158, 5) - (158, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var path = tmpPath + "/typed.json";
#line (159, 5) - (161, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                using (var fp = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
                {
#line (160, 9) - (160, 76) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                    fp.Write("{\"name\": \"FileTest\", \"age\": 42, \"active\": true}");
#line hidden
                }

#line (161, 5) - (161, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                string name = "";
#line (162, 5) - (162, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                int age = 0;
#line (163, 5) - (163, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                bool ok = false;
#line (164, 5) - (171, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                using (var fp2 = global::Sharpy.Builtins.Open(path, "r"))
#line hidden
                {
#line (165, 9) - (165, 46) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                    var result = json.Load<SimpleRecord>(fp2);
#line (166, 9) - (166, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                    ok = result.IsOk;
#line (167, 9) - (171, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                    if (ok)
#line hidden
                    {
#line (168, 13) - (168, 37) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                        var record = result.Unwrap();
#line (169, 13) - (169, 31) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                        name = record.Name;
#line (170, 13) - (170, 29) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                        age = record.Age;
#line hidden
                    }
                }

#line (171, 5) - (171, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(ok);
#line (172, 5) - (172, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("FileTest", name);
#line (173, 5) - (173, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(42, age);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTReadsTheThreeNonFiniteTokens()
            {
#line (187, 5) - (187, 72) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<Sharpy.List<double>>("[1.0, Infinity, NaN, -Infinity]");
#line (188, 5) - (188, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (189, 5) - (189, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Sharpy.List<double> xs = result.Unwrap();
#line (190, 5) - (190, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(4, global::Sharpy.Builtins.Len(xs));
#line (191, 5) - (191, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(1.0d, xs.GetItemUnchecked(0));
#line (192, 5) - (192, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(math.Isinf(xs.GetItemUnchecked(1)));
#line (193, 5) - (193, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(xs.GetItemUnchecked(1) > 0.0d);
#line (194, 5) - (194, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(math.Isnan(xs.GetItemUnchecked(2)));
#line (195, 5) - (195, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(math.Isinf(xs.GetItemUnchecked(3)));
#line (196, 5) - (196, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(xs.GetItemUnchecked(3) < 0.0d);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDumpsThenLoadsTRoundTripsNonFinite()
            {
#line (201, 5) - (201, 60) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var text = json.Dumps(new Sharpy.List<double>() { 1.0d, math.Inf, math.Nan, -math.Inf });
#line (202, 5) - (202, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("[1.0, Infinity, NaN, -Infinity]", text);
#line (203, 5) - (203, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<Sharpy.List<double>>(text);
#line (204, 5) - (204, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (205, 5) - (205, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Sharpy.List<double> xs = result.Unwrap();
#line (206, 5) - (206, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(1.0d, xs.GetItemUnchecked(0));
#line (207, 5) - (207, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(math.Isinf(xs.GetItemUnchecked(1)) && xs.GetItemUnchecked(1) > 0.0d);
#line (208, 5) - (208, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(math.Isnan(xs.GetItemUnchecked(2)));
#line (209, 5) - (209, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(math.Isinf(xs.GetItemUnchecked(3)) && xs.GetItemUnchecked(3) < 0.0d);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTRejectsLowercaseNonFiniteTokens()
            {
#line (216, 5) - (216, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var lower = json.Loads<Sharpy.List<double>>("[infinity]");
#line (217, 5) - (217, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(lower.IsErr);
#line (218, 5) - (218, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var lowerNan = json.Loads<Sharpy.List<double>>("[nan]");
#line (219, 5) - (219, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(lowerNan.IsErr);
#line (220, 5) - (220, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var shout = json.Loads<Sharpy.List<double>>("[NAN]");
#line (221, 5) - (221, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(shout.IsErr);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTLeavesNonFiniteSpellingsInsideStringsAlone()
            {
#line (230, 5) - (230, 74) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<Sharpy.List<string>>("[\"Infinity is data\", \"NaN too\"]");
#line (231, 5) - (231, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (232, 5) - (232, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Sharpy.List<string> xs = result.Unwrap();
#line (233, 5) - (233, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("Infinity is data", xs.GetItemUnchecked(0));
#line (234, 5) - (234, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("NaN too", xs.GetItemUnchecked(1));
#line hidden
            }

            public void Dispose()
            {
                _tmpPathFixture.Dispose();
            }
        }
    }
}
#line default
