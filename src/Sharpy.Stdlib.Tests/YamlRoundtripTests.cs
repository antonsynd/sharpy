using System;
using System.Collections.Generic;
using Xunit;

namespace Sharpy.Tests
{
    // See YamlModuleTests for the note on namespace-relative type resolution of
    // unqualified `Dict`/`List`/`Yaml`.
    public class YamlRoundtripTests
    {
        #region RoundtripLoad - node types

        [Fact]
        public void RoundtripLoad_Mapping_ReturnsCommentedMap()
        {
            object? result = Yaml.RoundtripLoad("name: test\ncount: 42\n");
            var map = Assert.IsType<CommentedMap>(result);
            Assert.Equal("test", map["name"]);
            Assert.Equal(42, map["count"]);
        }

        [Fact]
        public void RoundtripLoad_Sequence_ReturnsCommentedSeq()
        {
            object? result = Yaml.RoundtripLoad("- 1\n- 2\n- 3\n");
            var seq = Assert.IsType<CommentedSeq>(result);
            Assert.Equal(3, seq.Count);
            Assert.Equal(1, seq[0]);
            Assert.Equal(3, seq[2]);
        }

        [Fact]
        public void RoundtripLoad_PreservesKeyOrder()
        {
            object? result = Yaml.RoundtripLoad("zebra: 1\napple: 2\nmango: 3\n");
            var map = Assert.IsType<CommentedMap>(result);
            Assert.Equal(new[] { "zebra", "apple", "mango" }, new List<string>(map.Keys));
        }

        [Fact]
        public void RoundtripLoad_ScalarTypes_Resolved()
        {
            object? result = Yaml.RoundtripLoad(
                "i: 5\nf: 2.5\nb: true\nn: null\ns: hello\n");
            var map = Assert.IsType<CommentedMap>(result);
            Assert.Equal(5, map["i"]);
            Assert.Equal(2.5, map["f"]);
            Assert.Equal(true, map["b"]);
            Assert.Null(map["n"]);
            Assert.Equal("hello", map["s"]);
        }

        [Fact]
        public void RoundtripLoad_QuotedNumber_StaysString()
        {
            object? result = Yaml.RoundtripLoad("version: \"3\"\n");
            var map = Assert.IsType<CommentedMap>(result);
            Assert.IsType<string>(map["version"]);
            Assert.Equal("3", map["version"]);
        }

        [Fact]
        public void RoundtripLoad_Null_ThrowsTypeError()
        {
            Assert.Throws<TypeError>(() => Yaml.RoundtripLoad(null!));
        }

        #endregion

        #region RoundtripLoad - comment preservation

        [Fact]
        public void RoundtripLoad_BeforeComment_Preserved()
        {
            string yaml = "# this is a comment\nkey: value\n";
            object? result = Yaml.RoundtripLoad(yaml);
            var map = Assert.IsType<CommentedMap>(result);
            var comment = map.GetComment("key");
            Assert.NotNull(comment);
            Assert.NotNull(comment!.BeforeComment);
            Assert.Contains("this is a comment", comment.BeforeComment!, StringComparison.Ordinal);
        }

        [Fact]
        public void RoundtripLoad_InlineComment_Preserved()
        {
            string yaml = "key: value # trailing note\n";
            object? result = Yaml.RoundtripLoad(yaml);
            var map = Assert.IsType<CommentedMap>(result);
            var comment = map.GetComment("key");
            Assert.NotNull(comment);
            Assert.NotNull(comment!.InlineComment);
            Assert.Contains("trailing note", comment.InlineComment!, StringComparison.Ordinal);
        }

        [Fact]
        public void RoundtripDump_PreservesComments()
        {
            string yaml = "# leading comment\nname: test # inline\n";
            object? loaded = Yaml.RoundtripLoad(yaml);
            string dumped = Yaml.RoundtripDump(loaded);

            Assert.Contains("leading comment", dumped, StringComparison.Ordinal);
            Assert.Contains("inline", dumped, StringComparison.Ordinal);
            Assert.Contains("name:", dumped, StringComparison.Ordinal);
        }

        [Fact]
        public void Roundtrip_CommentsSurviveReload()
        {
            string yaml = "# config header\nhost: localhost # the host\nport: 8080\n";
            object? loaded = Yaml.RoundtripLoad(yaml);
            string dumped = Yaml.RoundtripDump(loaded);

            var reloaded = Assert.IsType<CommentedMap>(Yaml.RoundtripLoad(dumped));
            Assert.Equal("localhost", reloaded["host"]);
            Assert.Equal(8080, reloaded["port"]);

            var hostComment = reloaded.GetComment("host");
            Assert.NotNull(hostComment);
            Assert.Contains("the host", hostComment!.InlineComment!, StringComparison.Ordinal);
        }

        #endregion

        #region RoundtripDump - non-commented data

        [Fact]
        public void RoundtripDump_PlainDict_RoundTrips()
        {
            var data = new Dict<string, object?>();
            data["a"] = 1;
            data["b"] = "two";

            string dumped = Yaml.RoundtripDump(data);
            var reloaded = Assert.IsType<CommentedMap>(Yaml.RoundtripLoad(dumped));
            Assert.Equal(1, reloaded["a"]);
            Assert.Equal("two", reloaded["b"]);
        }

        [Fact]
        public void RoundtripDump_PlainList_RoundTrips()
        {
            var data = new List<object?>();
            data.Append(1);
            data.Append(2);
            data.Append(3);

            string dumped = Yaml.RoundtripDump(data);
            var reloaded = Assert.IsType<CommentedSeq>(Yaml.RoundtripLoad(dumped));
            Assert.Equal(3, reloaded.Count);
            Assert.Equal(1, reloaded[0]);
        }

        [Fact]
        public void RoundtripDump_NestedCommentedMap_RoundTrips()
        {
            string yaml =
                "server:\n" +
                "  host: localhost\n" +
                "  port: 9000\n" +
                "debug: true\n";
            object? loaded = Yaml.RoundtripLoad(yaml);
            string dumped = Yaml.RoundtripDump(loaded);

            var reloaded = Assert.IsType<CommentedMap>(Yaml.RoundtripLoad(dumped));
            var server = Assert.IsType<CommentedMap>(reloaded["server"]);
            Assert.Equal("localhost", server["host"]);
            Assert.Equal(9000, server["port"]);
            Assert.Equal(true, reloaded["debug"]);
        }

        #endregion

        #region CommentedMap operations

        [Fact]
        public void CommentedMap_AddAndIndexer()
        {
            var map = new CommentedMap();
            map.Add("a", 1);
            map["b"] = 2;
            Assert.Equal(1, map["a"]);
            Assert.Equal(2, map["b"]);
            Assert.Equal(2, map.Count);
        }

        [Fact]
        public void CommentedMap_PreservesInsertionOrder()
        {
            var map = new CommentedMap();
            map.Add("z", 1);
            map.Add("a", 2);
            map.Add("m", 3);
            Assert.Equal(new[] { "z", "a", "m" }, new List<string>(map.Keys));
        }

        [Fact]
        public void CommentedMap_ContainsKey()
        {
            var map = new CommentedMap();
            map.Add("present", 1);
            Assert.True(map.ContainsKey("present"));
            Assert.False(map.ContainsKey("absent"));
        }

        [Fact]
        public void CommentedMap_Remove_RemovesKeyAndComment()
        {
            var map = new CommentedMap();
            map.Add("a", 1);
            map.Add("b", 2);
            map.GetOrAddComment("a").InlineComment = "note";

            Assert.True(map.Remove("a"));
            Assert.False(map.ContainsKey("a"));
            Assert.Null(map.GetComment("a"));
            Assert.Equal(1, map.Count);
            Assert.DoesNotContain("a", new List<string>(map.Keys));
        }

        [Fact]
        public void CommentedMap_Remove_MissingKey_ReturnsFalse()
        {
            var map = new CommentedMap();
            Assert.False(map.Remove("missing"));
        }

        [Fact]
        public void CommentedMap_OverwriteExistingKey_DoesNotDuplicateOrder()
        {
            var map = new CommentedMap();
            map.Add("a", 1);
            map["a"] = 99;
            Assert.Equal(1, map.Count);
            Assert.Equal(99, map["a"]);
        }

        [Fact]
        public void CommentedMap_TryGetValue()
        {
            var map = new CommentedMap();
            map.Add("a", 42);
            Assert.True(map.TryGetValue("a", out object? value));
            Assert.Equal(42, value);
            Assert.False(map.TryGetValue("b", out _));
        }

        #endregion

        #region CommentedSeq operations

        [Fact]
        public void CommentedSeq_AddAndIndexer()
        {
            var seq = new CommentedSeq();
            seq.Add(1);
            seq.Add(2);
            Assert.Equal(2, seq.Count);
            Assert.Equal(1, seq[0]);
            seq[0] = 99;
            Assert.Equal(99, seq[0]);
        }

        [Fact]
        public void CommentedSeq_Insert_ShiftsItems()
        {
            var seq = new CommentedSeq();
            seq.Add(1);
            seq.Add(3);
            seq.Insert(1, 2);
            Assert.Equal(3, seq.Count);
            Assert.Equal(1, seq[0]);
            Assert.Equal(2, seq[1]);
            Assert.Equal(3, seq[2]);
        }

        [Fact]
        public void CommentedSeq_RemoveAt()
        {
            var seq = new CommentedSeq();
            seq.Add(1);
            seq.Add(2);
            seq.Add(3);
            seq.RemoveAt(1);
            Assert.Equal(2, seq.Count);
            Assert.Equal(1, seq[0]);
            Assert.Equal(3, seq[1]);
        }

        [Fact]
        public void CommentedSeq_Insert_ShiftsComments()
        {
            var seq = new CommentedSeq();
            seq.Add("a");
            seq.Add("b");
            seq.GetOrAddComment(1).InlineComment = "b-comment";

            seq.Insert(0, "z");
            // The comment that was on index 1 should now be on index 2.
            Assert.Equal("b", seq[2]);
            var comment = seq.GetComment(2);
            Assert.NotNull(comment);
            Assert.Equal("b-comment", comment!.InlineComment);
        }

        #endregion

        #region CommentInfo

        [Fact]
        public void CommentInfo_HasComments_FalseWhenEmpty()
        {
            var info = new CommentInfo();
            Assert.False(info.HasComments);
        }

        [Theory]
        [InlineData("before", null, null)]
        [InlineData(null, "inline", null)]
        [InlineData(null, null, "after")]
        public void CommentInfo_HasComments_TrueWhenAnySet(string? before, string? inline, string? after)
        {
            var info = new CommentInfo
            {
                BeforeComment = before,
                InlineComment = inline,
                AfterComment = after,
            };
            Assert.True(info.HasComments);
        }

        [Fact]
        public void CommentedMap_GetOrAddComment_ReturnsMutableInstance()
        {
            var map = new CommentedMap();
            map.Add("k", 1);
            var comment = map.GetOrAddComment("k");
            comment.BeforeComment = "hi";
            // Same instance is returned on subsequent calls.
            Assert.Same(comment, map.GetOrAddComment("k"));
            Assert.Equal("hi", map.GetComment("k")!.BeforeComment);
        }

        #endregion
    }
}
