#if NET10_0_OR_GREATER
using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace Sharpy.Tests
{
    public class JsonTypedDeserializationTests
    {
        private class SimpleRecord
        {
            public string Name { get; set; } = "";
            public int Age { get; set; }
            public bool Active { get; set; }
        }

        private class NestedRecord
        {
            public string Label { get; set; } = "";
            public SimpleRecord Inner { get; set; } = new SimpleRecord();
        }

        private class RecordWithList
        {
            public string Name { get; set; } = "";
            public System.Collections.Generic.List<int> Scores { get; set; } = new();
        }

        private class RecordWithDict
        {
            public string Name { get; set; } = "";
            public System.Collections.Generic.Dictionary<string, int> Metadata { get; set; } = new();
        }

        private class RecordWithOptional
        {
            public string Name { get; set; } = "";
            public string? Nickname { get; set; }
            public int Count { get; set; }
        }

        #region Loads<T> — success cases

        [Fact]
        public void LoadsT_SimpleRecord_Deserializes()
        {
            var result = Json.Loads<SimpleRecord>("{\"name\": \"Alice\", \"age\": 30, \"active\": true}");
            result.IsOk.Should().BeTrue();
            var record = result.Unwrap();
            record.Name.Should().Be("Alice");
            record.Age.Should().Be(30);
            record.Active.Should().BeTrue();
        }

        [Fact]
        public void LoadsT_SnakeCaseMapping_Deserializes()
        {
            var result = Json.Loads<RecordWithOptional>("{\"name\": \"Bob\", \"nickname\": \"Bobby\", \"count\": 5}");
            result.IsOk.Should().BeTrue();
            var record = result.Unwrap();
            record.Name.Should().Be("Bob");
            record.Nickname.Should().Be("Bobby");
            record.Count.Should().Be(5);
        }

        [Fact]
        public void LoadsT_NestedRecord_Deserializes()
        {
            string json = "{\"label\": \"outer\", \"inner\": {\"name\": \"inner\", \"age\": 10, \"active\": false}}";
            var result = Json.Loads<NestedRecord>(json);
            result.IsOk.Should().BeTrue();
            var record = result.Unwrap();
            record.Label.Should().Be("outer");
            record.Inner.Name.Should().Be("inner");
            record.Inner.Age.Should().Be(10);
            record.Inner.Active.Should().BeFalse();
        }

        [Fact]
        public void LoadsT_RecordWithList_Deserializes()
        {
            string json = "{\"name\": \"Charlie\", \"scores\": [100, 95, 87]}";
            var result = Json.Loads<RecordWithList>(json);
            result.IsOk.Should().BeTrue();
            var record = result.Unwrap();
            record.Name.Should().Be("Charlie");
            record.Scores.Should().Equal(100, 95, 87);
        }

        [Fact]
        public void LoadsT_RecordWithDict_Deserializes()
        {
            string json = "{\"name\": \"Dave\", \"metadata\": {\"x\": 1, \"y\": 2}}";
            var result = Json.Loads<RecordWithDict>(json);
            result.IsOk.Should().BeTrue();
            var record = result.Unwrap();
            record.Name.Should().Be("Dave");
            record.Metadata.Should().ContainKey("x").WhoseValue.Should().Be(1);
            record.Metadata.Should().ContainKey("y").WhoseValue.Should().Be(2);
        }

        [Fact]
        public void LoadsT_OptionalFieldMissing_DefaultsToNull()
        {
            var result = Json.Loads<RecordWithOptional>("{\"name\": \"Eve\", \"count\": 3}");
            result.IsOk.Should().BeTrue();
            var record = result.Unwrap();
            record.Name.Should().Be("Eve");
            record.Nickname.Should().BeNull();
            record.Count.Should().Be(3);
        }

        [Fact]
        public void LoadsT_CaseInsensitive_Deserializes()
        {
            var result = Json.Loads<SimpleRecord>("{\"Name\": \"Frank\", \"Age\": 25, \"Active\": true}");
            result.IsOk.Should().BeTrue();
            result.Unwrap().Name.Should().Be("Frank");
        }

        #endregion

        #region Loads<T> — error cases

        [Fact]
        public void LoadsT_InvalidJson_ReturnsErr()
        {
            var result = Json.Loads<SimpleRecord>("{invalid}");
            result.IsErr.Should().BeTrue();
            var err = result.UnwrapErr();
            err.Should().BeOfType<JSONDecodeError>();
        }

        [Fact]
        public void LoadsT_TypeMismatch_ReturnsErr()
        {
            var result = Json.Loads<SimpleRecord>("{\"name\": \"Alice\", \"age\": \"not a number\", \"active\": true}");
            result.IsErr.Should().BeTrue();
        }

        [Fact]
        public void LoadsT_NullInput_ThrowsTypeError()
        {
            Action act = () => Json.Loads<SimpleRecord>(null!);
            act.Should().Throw<TypeError>();
        }

        [Fact]
        public void LoadsT_EmptyString_ReturnsErr()
        {
            var result = Json.Loads<SimpleRecord>("");
            result.IsErr.Should().BeTrue();
        }

        #endregion

        #region Loads<T> — Result unwrapping

        [Fact]
        public void LoadsT_OkResult_UnwrapReturnsValue()
        {
            var result = Json.Loads<SimpleRecord>("{\"name\": \"Test\", \"age\": 1, \"active\": false}");
            result.IsOk.Should().BeTrue();
            result.IsErr.Should().BeFalse();
            result.Unwrap().Should().NotBeNull();
        }

        [Fact]
        public void LoadsT_ErrResult_UnwrapThrows()
        {
            var result = Json.Loads<SimpleRecord>("not json");
            result.IsErr.Should().BeTrue();
            Action act = () => result.Unwrap();
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void LoadsT_ErrResult_ErrorHasMessage()
        {
            var result = Json.Loads<SimpleRecord>("{bad}");
            result.IsErr.Should().BeTrue();
            var err = result.UnwrapErr();
            err.Msg.Should().NotBeNullOrEmpty();
            err.Doc.Should().Be("{bad}");
        }

        #endregion

        #region Load<T> — file round-trip

        [Fact]
        public void LoadT_FileRoundTrip_Deserializes()
        {
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sharpy_json_test_" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(tempPath, "{\"name\": \"FileTest\", \"age\": 42, \"active\": true}");
                using var fp = Builtins.Open(tempPath, "r");
                var result = Json.Load<SimpleRecord>(fp);
                result.IsOk.Should().BeTrue();
                var record = result.Unwrap();
                record.Name.Should().Be("FileTest");
                record.Age.Should().Be(42);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        [Fact]
        public void LoadT_NullFile_ThrowsTypeError()
        {
            Action act = () => Json.Load<SimpleRecord>(null!);
            act.Should().Throw<TypeError>();
        }

        #endregion
    }
}
#endif
