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
#line (44, 5) - (44, 94) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<SimpleRecord>("{\"name\": \"Alice\", \"age\": 30, \"active\": true}");
#line (45, 5) - (45, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (46, 5) - (46, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var record = result.Unwrap();
#line (47, 5) - (47, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("Alice", record.Name);
#line (48, 5) - (48, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(30, record.Age);
#line (49, 5) - (49, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(record.Active);
            }

            [Xunit.FactAttribute]
            public void TestLoadsTSnakeCaseMappingDeserializes()
            {
#line (53, 5) - (53, 106) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<RecordWithOptional>("{\"name\": \"Bob\", \"nickname\": \"Bobby\", \"count\": 5}");
#line (54, 5) - (54, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (55, 5) - (55, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var record = result.Unwrap();
#line (56, 5) - (56, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("Bob", record.Name);
#line (57, 5) - (57, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("Bobby", record.Nickname);
#line (58, 5) - (58, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(5, record.Count);
            }

            [Xunit.FactAttribute]
            public void TestLoadsTNestedRecordDeserializes()
            {
#line (62, 5) - (62, 130) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<NestedRecord>("{\"label\": \"outer\", \"inner\": {\"name\": \"inner\", \"age\": 10, \"active\": false}}");
#line (63, 5) - (63, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (64, 5) - (64, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var record = result.Unwrap();
#line (65, 5) - (65, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("outer", record.Label);
#line (66, 5) - (66, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("inner", record.Inner.Name);
#line (67, 5) - (67, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(10, record.Inner.Age);
#line (68, 5) - (68, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.False(record.Inner.Active);
            }

            [Xunit.FactAttribute]
            public void TestLoadsTRecordWithListDeserializes()
            {
#line (72, 5) - (72, 94) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<RecordWithList>("{\"name\": \"Charlie\", \"scores\": [100, 95, 87]}");
#line (73, 5) - (73, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (74, 5) - (74, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var record = result.Unwrap();
#line (75, 5) - (75, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("Charlie", record.Name);
#line (76, 5) - (76, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Sharpy.List<int> scores = record.Scores;
#line (77, 5) - (77, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(scores));
#line (78, 5) - (78, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(100, scores[0]);
#line (79, 5) - (79, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(95, scores[1]);
#line (80, 5) - (80, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(87, scores[2]);
            }

            [Xunit.FactAttribute]
            public void TestLoadsTRecordWithDictDeserializes()
            {
#line (84, 5) - (84, 100) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<RecordWithDict>("{\"name\": \"Dave\", \"metadata\": {\"x\": 1, \"y\": 2}}");
#line (85, 5) - (85, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (86, 5) - (86, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var record = result.Unwrap();
#line (87, 5) - (87, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("Dave", record.Name);
#line (88, 5) - (88, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Sharpy.Dict<string, int> metadata = record.Metadata;
#line (89, 5) - (89, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(1, metadata["x"]);
#line (90, 5) - (90, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(2, metadata["y"]);
            }

            [Xunit.FactAttribute]
            public void TestLoadsTOptionalFieldMissingDefaultsToNull()
            {
#line (94, 5) - (94, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<RecordWithOptional>("{\"name\": \"Eve\", \"count\": 3}");
#line (95, 5) - (95, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (96, 5) - (96, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var record = result.Unwrap();
#line (97, 5) - (97, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("Eve", record.Name);
#line (98, 5) - (98, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(record.Nickname.IsNone);
#line (99, 5) - (99, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(3, record.Count);
            }

            [Xunit.FactAttribute]
            public void TestLoadsTCaseInsensitiveDeserializes()
            {
#line (103, 5) - (103, 94) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<SimpleRecord>("{\"Name\": \"Frank\", \"Age\": 25, \"Active\": true}");
#line (104, 5) - (104, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (105, 5) - (105, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("Frank", result.Unwrap().Name);
            }

            [Xunit.FactAttribute]
            public void TestLoadsTInvalidJsonReturnsErr()
            {
#line (111, 5) - (111, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<SimpleRecord>("{invalid}");
#line (112, 5) - (112, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsErr);
#line (113, 5) - (113, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var err = result.UnwrapErr();
#line (114, 5) - (114, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("{invalid}", err.Doc);
            }

            [Xunit.FactAttribute]
            public void TestLoadsTTypeMismatchReturnsErr()
            {
#line (118, 5) - (118, 108) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<SimpleRecord>("{\"name\": \"Alice\", \"age\": \"not a number\", \"active\": true}");
#line (119, 5) - (119, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsErr);
            }

            [Xunit.FactAttribute]
            public void TestLoadsTEmptyStringReturnsErr()
            {
#line (123, 5) - (123, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<SimpleRecord>("");
#line (124, 5) - (124, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsErr);
            }

            [Xunit.FactAttribute]
            public void TestLoadsTOkResultUnwrapReturnsValue()
            {
#line (130, 5) - (130, 93) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<SimpleRecord>("{\"name\": \"Test\", \"age\": 1, \"active\": false}");
#line (131, 5) - (131, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (132, 5) - (132, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.False(result.IsErr);
#line (133, 5) - (133, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var record = result.Unwrap();
#line (134, 5) - (134, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("Test", record.Name);
            }

            [Xunit.FactAttribute]
            public void TestLoadsTErrResultUnwrapThrows()
            {
#line (141, 5) - (141, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<SimpleRecord>("not json");
#line (142, 5) - (142, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsErr);
#line (143, 5) - (143, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.False(result.IsOk);
            }

            [Xunit.FactAttribute]
            public void TestLoadsTErrResultErrorHasMessage()
            {
#line (147, 5) - (147, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var result = json.Loads<SimpleRecord>("{bad}");
#line (148, 5) - (148, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsErr);
#line (149, 5) - (149, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var err = result.UnwrapErr();
#line (150, 5) - (150, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Len(err.Msg) > 0);
#line (151, 5) - (151, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("{bad}", err.Doc);
            }

            [Xunit.FactAttribute]
            public void TestLoadTFileRoundTripDeserializes()
            {
                string tmpPath = _tmpPathFixture.Value;
#line (157, 5) - (157, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                var path = tmpPath + "/typed.json";
#line (158, 5) - (160, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                using (var fp = global::Sharpy.Builtins.Open(path, "w"))
                {
#line (159, 9) - (159, 76) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                    fp.Write("{\"name\": \"FileTest\", \"age\": 42, \"active\": true}");
                }

#line (160, 5) - (160, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                string name = "";
#line (161, 5) - (161, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                int age = 0;
#line (162, 5) - (162, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                bool ok = false;
#line (163, 5) - (170, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                using (var fp2 = global::Sharpy.Builtins.Open(path, "r"))
                {
#line (164, 9) - (164, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                    var result = json.Load<SimpleRecord>(fp2);
#line (165, 9) - (165, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                    ok = result.IsOk;
#line (166, 9) - (170, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                    if (ok)
                    {
#line (167, 13) - (167, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                        var record = result.Unwrap();
#line (168, 13) - (168, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                        name = record.Name;
#line (169, 13) - (169, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                        age = record.Age;
                    }
                }

#line (170, 5) - (170, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.True(ok);
#line (171, 5) - (171, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("FileTest", name);
#line (172, 5) - (172, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/json/json_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(42, age);
            }

            public void Dispose()
            {
                _tmpPathFixture.Dispose();
            }
        }
    }
}
