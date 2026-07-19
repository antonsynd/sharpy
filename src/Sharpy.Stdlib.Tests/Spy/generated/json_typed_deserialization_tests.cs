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
#line (44, 5) - (44, 94) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<SimpleRecord>("{\"name\": \"Alice\", \"age\": 30, \"active\": true}");
#line (45, 5) - (45, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (46, 5) - (46, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var record = result.Unwrap();
#line (47, 5) - (47, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("Alice", record.Name);
#line (48, 5) - (48, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(30, record.Age);
#line (49, 5) - (49, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(record.Active);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTSnakeCaseMappingDeserializes()
            {
#line (53, 5) - (53, 106) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<RecordWithOptional>("{\"name\": \"Bob\", \"nickname\": \"Bobby\", \"count\": 5}");
#line (54, 5) - (54, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (55, 5) - (55, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var record = result.Unwrap();
#line (56, 5) - (56, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("Bob", record.Name);
#line (57, 5) - (57, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("Bobby", record.Nickname);
#line (58, 5) - (58, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(5, record.Count);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTNestedRecordDeserializes()
            {
#line (62, 5) - (62, 130) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<NestedRecord>("{\"label\": \"outer\", \"inner\": {\"name\": \"inner\", \"age\": 10, \"active\": false}}");
#line (63, 5) - (63, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (64, 5) - (64, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var record = result.Unwrap();
#line (65, 5) - (65, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("outer", record.Label);
#line (66, 5) - (66, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("inner", record.Inner.Name);
#line (67, 5) - (67, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(10, record.Inner.Age);
#line (68, 5) - (68, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.False(record.Inner.Active);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTRecordWithListDeserializes()
            {
#line (72, 5) - (72, 94) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<RecordWithList>("{\"name\": \"Charlie\", \"scores\": [100, 95, 87]}");
#line (73, 5) - (73, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (74, 5) - (74, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var record = result.Unwrap();
#line (75, 5) - (75, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("Charlie", record.Name);
#line (76, 5) - (76, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Sharpy.List<int> scores = record.Scores;
#line (77, 5) - (77, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(scores));
#line (78, 5) - (78, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(100, scores.GetItemUnchecked(0));
#line (79, 5) - (79, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(95, scores.GetItemUnchecked(1));
#line (80, 5) - (80, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(87, scores.GetItemUnchecked(2));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTRecordWithDictDeserializes()
            {
#line (84, 5) - (84, 100) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<RecordWithDict>("{\"name\": \"Dave\", \"metadata\": {\"x\": 1, \"y\": 2}}");
#line (85, 5) - (85, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (86, 5) - (86, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var record = result.Unwrap();
#line (87, 5) - (87, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("Dave", record.Name);
#line (88, 5) - (88, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Sharpy.Dict<string, int> metadata = record.Metadata;
#line (89, 5) - (89, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(1, metadata["x"]);
#line (90, 5) - (90, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(2, metadata["y"]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTOptionalFieldMissingDefaultsToNull()
            {
#line (94, 5) - (94, 81) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<RecordWithOptional>("{\"name\": \"Eve\", \"count\": 3}");
#line (95, 5) - (95, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (96, 5) - (96, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var record = result.Unwrap();
#line (97, 5) - (97, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("Eve", record.Name);
#line (98, 5) - (98, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(record.Nickname.IsNone);
#line (99, 5) - (99, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(3, record.Count);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTCaseInsensitiveDeserializes()
            {
#line (103, 5) - (103, 94) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<SimpleRecord>("{\"Name\": \"Frank\", \"Age\": 25, \"Active\": true}");
#line (104, 5) - (104, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (105, 5) - (105, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("Frank", result.Unwrap().Name);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTInvalidJsonReturnsErr()
            {
#line (111, 5) - (111, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<SimpleRecord>("{invalid}");
#line (112, 5) - (112, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsErr);
#line (113, 5) - (113, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var err = result.UnwrapErr();
#line (114, 5) - (114, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("{invalid}", err.Doc);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTTypeMismatchReturnsErr()
            {
#line (118, 5) - (118, 108) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<SimpleRecord>("{\"name\": \"Alice\", \"age\": \"not a number\", \"active\": true}");
#line (119, 5) - (119, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsErr);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTEmptyStringReturnsErr()
            {
#line (123, 5) - (123, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<SimpleRecord>("");
#line (124, 5) - (124, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsErr);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTOkResultUnwrapReturnsValue()
            {
#line (130, 5) - (130, 93) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<SimpleRecord>("{\"name\": \"Test\", \"age\": 1, \"active\": false}");
#line (131, 5) - (131, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (132, 5) - (132, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.False(result.IsErr);
#line (133, 5) - (133, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var record = result.Unwrap();
#line (134, 5) - (134, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("Test", record.Name);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTErrResultUnwrapThrows()
            {
#line (141, 5) - (141, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<SimpleRecord>("not json");
#line (142, 5) - (142, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsErr);
#line (143, 5) - (143, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.False(result.IsOk);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadsTErrResultErrorHasMessage()
            {
#line (147, 5) - (147, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<SimpleRecord>("{bad}");
#line (148, 5) - (148, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsErr);
#line (149, 5) - (149, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var err = result.UnwrapErr();
#line (150, 5) - (150, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Len(err.Msg) > 0);
#line (151, 5) - (151, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("{bad}", err.Doc);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLoadTFileRoundTripDeserializes()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (157, 5) - (157, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var path = tmpPath + "/typed.json";
#line (158, 5) - (160, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                using (var fp = global::Sharpy.Builtins.Open(path, "w"))
#line hidden
                {
#line (159, 9) - (159, 76) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                    fp.Write("{\"name\": \"FileTest\", \"age\": 42, \"active\": true}");
#line hidden
                }

#line (160, 5) - (160, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                string name = "";
#line (161, 5) - (161, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                int age = 0;
#line (162, 5) - (162, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                bool ok = false;
#line (163, 5) - (170, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                using (var fp2 = global::Sharpy.Builtins.Open(path, "r"))
#line hidden
                {
#line (164, 9) - (164, 46) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                    var result = json.Load<SimpleRecord>(fp2);
#line (165, 9) - (165, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                    ok = result.IsOk;
#line (166, 9) - (170, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                    if (ok)
#line hidden
                    {
#line (167, 13) - (167, 37) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                        var record = result.Unwrap();
#line (168, 13) - (168, 31) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                        name = record.Name;
#line (169, 13) - (169, 29) 24 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                        age = record.Age;
#line hidden
                    }
                }

#line (170, 5) - (170, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(ok);
#line (171, 5) - (171, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("FileTest", name);
#line (172, 5) - (172, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(42, age);
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
