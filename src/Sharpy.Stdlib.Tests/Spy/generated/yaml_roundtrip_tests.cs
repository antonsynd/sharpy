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
using yaml = global::Sharpy.Yaml;
using @operator = global::Sharpy.Operator;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Yaml.YamlRoundtripTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Yaml
    {
        [global::Sharpy.SharpyModule("yaml.yaml_roundtrip_tests")]
        public static partial class YamlRoundtripTests
        {
        }
    }

    public static partial class Yaml
    {
        public partial class YamlRoundtripTestsTests
        {
            [Xunit.FactAttribute]
            public void TestRoundtripLoadMappingReturnsCommentedMap()
            {
#line (24, 5) - (24, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                object result = yaml.RoundtripLoad("name: test\ncount: 42\n");
#line (25, 5) - (32, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                switch (result)
                {
                    case global::Sharpy.CommentedMap m:
#line (27, 13) - (27, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(m["name"], "test"));
#line (28, 13) - (28, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(m["count"], 42));
                        break;
                    default:
#line (30, 13) - (30, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundtripLoadSequenceReturnsCommentedSeq()
            {
#line (34, 5) - (34, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                object result = yaml.RoundtripLoad("- 1\n- 2\n- 3\n");
#line (35, 5) - (43, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                switch (result)
                {
                    case global::Sharpy.CommentedSeq s:
#line (37, 13) - (37, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.Equal(3, s.Count);
#line (38, 13) - (38, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(s[0], 1));
#line (39, 13) - (39, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(s[2], 3));
                        break;
                    default:
#line (41, 13) - (41, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundtripLoadScalarTypesResolved()
            {
#line (45, 5) - (45, 88) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                object result = yaml.RoundtripLoad("i: 5\nf: 2.5\nb: true\nn: null\ns: hello\n");
#line (46, 5) - (56, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                switch (result)
                {
                    case global::Sharpy.CommentedMap m:
#line (48, 13) - (48, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(m["i"], 5));
#line (49, 13) - (49, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(m["f"], 2.5d));
#line (50, 13) - (50, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(m["b"], true));
#line (51, 13) - (51, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.Null(m["n"]);
#line (52, 13) - (52, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(m["s"], "hello"));
                        break;
                    default:
#line (54, 13) - (54, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundtripLoadQuotedNumberStaysString()
            {
#line (58, 5) - (58, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                object result = yaml.RoundtripLoad("version: \"3\"\n");
#line (59, 5) - (66, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                switch (result)
                {
                    case global::Sharpy.CommentedMap m:
#line (61, 13) - (61, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(m["version"]);
#line (62, 13) - (62, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(m["version"], "3"));
                        break;
                    default:
#line (64, 13) - (64, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundtripLoadPreservesKeyOrder()
            {
#line (68, 5) - (68, 76) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                object result = yaml.RoundtripLoad("zebra: 1\napple: 2\nmango: 3\n");
#line (69, 5) - (80, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                switch (result)
                {
                    case global::Sharpy.CommentedMap m:
#line (71, 13) - (71, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        var keys = m.Keys;
#line (72, 13) - (72, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.Equal("zebra", keys[0]);
#line (73, 13) - (73, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.Equal("apple", keys[1]);
#line (74, 13) - (74, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.Equal("mango", keys[2]);
                        break;
                    default:
#line (76, 13) - (76, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundtripLoadBeforeCommentPreserved()
            {
#line (82, 5) - (82, 79) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                object result = yaml.RoundtripLoad("# this is a comment\nkey: value\n");
#line (83, 5) - (93, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                switch (result)
                {
                    case global::Sharpy.CommentedMap m:
#line (85, 13) - (85, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        var comment = m.GetComment("key");
#line (86, 13) - (86, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.NotNull(comment);
#line (87, 13) - (87, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        var before = comment.BeforeComment;
#line (88, 13) - (88, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.NotNull(before);
#line (89, 13) - (89, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.Contains("this is a comment", before);
                        break;
                    default:
#line (91, 13) - (91, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundtripLoadInlineCommentPreserved()
            {
#line (95, 5) - (95, 74) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                object result = yaml.RoundtripLoad("key: value # trailing note\n");
#line (96, 5) - (106, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                switch (result)
                {
                    case global::Sharpy.CommentedMap m:
#line (98, 13) - (98, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        var comment = m.GetComment("key");
#line (99, 13) - (99, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.NotNull(comment);
#line (100, 13) - (100, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        var inline = comment.InlineComment;
#line (101, 13) - (101, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.NotNull(inline);
#line (102, 13) - (102, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.Contains("trailing note", inline);
                        break;
                    default:
#line (104, 13) - (104, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundtripDumpPreservesComments()
            {
#line (108, 5) - (108, 86) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                object loaded = yaml.RoundtripLoad("# leading comment\nname: test # inline\n");
#line (109, 5) - (109, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                string dumped = yaml.RoundtripDump(loaded);
#line (110, 5) - (110, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Contains("leading comment", dumped);
#line (111, 5) - (111, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Contains("inline", dumped);
#line (112, 5) - (112, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Contains("name:", dumped);
            }

            [Xunit.FactAttribute]
            public void TestRoundtripCommentsSurviveReload()
            {
#line (116, 5) - (116, 103) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                object loaded = yaml.RoundtripLoad("# config header\nhost: localhost # the host\nport: 8080\n");
#line (117, 5) - (117, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                string dumped = yaml.RoundtripDump(loaded);
#line (118, 5) - (132, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                switch (yaml.RoundtripLoad(dumped))
                {
                    case global::Sharpy.CommentedMap reloaded:
#line (120, 13) - (120, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(reloaded["host"], "localhost"));
#line (121, 13) - (121, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(reloaded["port"], 8080));
#line (122, 13) - (122, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        var hostComment = reloaded.GetComment("host");
#line (123, 13) - (123, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.NotNull(hostComment);
#line (124, 13) - (124, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        var inline = hostComment.InlineComment;
#line (125, 13) - (125, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.NotNull(inline);
#line (126, 13) - (126, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.Contains("the host", inline);
                        break;
                    default:
#line (128, 13) - (128, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundtripDumpPlainDictRoundTrips()
            {
#line (134, 5) - (134, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
                {
                };
#line (135, 5) - (135, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                data["a"] = 1;
#line (136, 5) - (136, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                data["b"] = "two";
#line (137, 5) - (137, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                string dumped = yaml.RoundtripDump(data);
#line (138, 5) - (145, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                switch (yaml.RoundtripLoad(dumped))
                {
                    case global::Sharpy.CommentedMap reloaded:
#line (140, 13) - (140, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(reloaded["a"], 1));
#line (141, 13) - (141, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(reloaded["b"], "two"));
                        break;
                    default:
#line (143, 13) - (143, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundtripDumpPlainListRoundTrips()
            {
#line (147, 5) - (147, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Sharpy.List<object> data = new Sharpy.List<object>()
                {
                };
#line (148, 5) - (148, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                data.Append(1);
#line (149, 5) - (149, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                data.Append(2);
#line (150, 5) - (150, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                data.Append(3);
#line (151, 5) - (151, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                string dumped = yaml.RoundtripDump(data);
#line (152, 5) - (159, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                switch (yaml.RoundtripLoad(dumped))
                {
                    case global::Sharpy.CommentedSeq reloaded:
#line (154, 13) - (154, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.Equal(3, reloaded.Count);
#line (155, 13) - (155, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(reloaded[0], 1));
                        break;
                    default:
#line (157, 13) - (157, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundtripDumpNestedCommentedMapRoundTrips()
            {
#line (161, 5) - (161, 101) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                object loaded = yaml.RoundtripLoad("server:\n  host: localhost\n  port: 9000\ndebug: true\n");
#line (162, 5) - (162, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                string dumped = yaml.RoundtripDump(loaded);
#line (163, 5) - (177, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                switch (yaml.RoundtripLoad(dumped))
                {
                    case global::Sharpy.CommentedMap reloaded:
#line (165, 13) - (171, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        switch (reloaded["server"])
                        {
                            case global::Sharpy.CommentedMap server:
#line (167, 21) - (167, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                                Xunit.Assert.True(@operator.Eq(server["host"], "localhost"));
#line (168, 21) - (168, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                                Xunit.Assert.True(@operator.Eq(server["port"], 9000));
                                break;
                            default:
#line (170, 21) - (170, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                                Xunit.Assert.True(false);
                                break;
                        }

#line (171, 13) - (171, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(reloaded["debug"], true));
                        break;
                    default:
#line (173, 13) - (173, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestCommentedMapAddAndIndexer()
            {
#line (179, 5) - (179, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var m = new global::Sharpy.CommentedMap();
#line (180, 5) - (180, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                m.Add("a", 1);
#line (181, 5) - (181, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                m["b"] = 2;
#line (182, 5) - (182, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.True(@operator.Eq(m["a"], 1));
#line (183, 5) - (183, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.True(@operator.Eq(m["b"], 2));
#line (184, 5) - (184, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Equal(2, m.Count);
            }

            [Xunit.FactAttribute]
            public void TestCommentedMapContainsKey()
            {
#line (188, 5) - (188, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var m = new global::Sharpy.CommentedMap();
#line (189, 5) - (189, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                m.Add("present", 1);
#line (190, 5) - (190, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.True(m.ContainsKey("present"));
#line (191, 5) - (191, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.False(m.ContainsKey("absent"));
            }

            [Xunit.FactAttribute]
            public void TestCommentedMapRemoveRemovesKeyAndComment()
            {
#line (195, 5) - (195, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var m = new global::Sharpy.CommentedMap();
#line (196, 5) - (196, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                m.Add("a", 1);
#line (197, 5) - (197, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                m.Add("b", 2);
#line (198, 5) - (198, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                m.GetOrAddComment("a").InlineComment = "note";
#line (199, 5) - (199, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.True(m.Remove("a"));
#line (200, 5) - (200, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.False(m.ContainsKey("a"));
#line (201, 5) - (201, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Null(m.GetComment("a"));
#line (202, 5) - (202, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Equal(1, m.Count);
            }

            [Xunit.FactAttribute]
            public void TestCommentedMapRemoveMissingKeyReturnsFalse()
            {
#line (206, 5) - (206, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var m = new global::Sharpy.CommentedMap();
#line (207, 5) - (207, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.False(m.Remove("missing"));
            }

            [Xunit.FactAttribute]
            public void TestCommentedMapOverwriteExistingKeyDoesNotDuplicateOrder()
            {
#line (211, 5) - (211, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var m = new global::Sharpy.CommentedMap();
#line (212, 5) - (212, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                m.Add("a", 1);
#line (213, 5) - (213, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                m["a"] = 99;
#line (214, 5) - (214, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Equal(1, m.Count);
#line (215, 5) - (215, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.True(@operator.Eq(m["a"], 99));
            }

            [Xunit.FactAttribute]
            public void TestCommentedMapPreservesInsertionOrder()
            {
#line (219, 5) - (219, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var m = new global::Sharpy.CommentedMap();
#line (220, 5) - (220, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                m.Add("z", 1);
#line (221, 5) - (221, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                m.Add("a", 2);
#line (222, 5) - (222, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                m.Add("m", 3);
#line (223, 5) - (223, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var keys = m.Keys;
#line (224, 5) - (224, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Equal("z", keys[0]);
#line (225, 5) - (225, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Equal("a", keys[1]);
#line (226, 5) - (226, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Equal("m", keys[2]);
            }

            [Xunit.FactAttribute]
            public void TestCommentedSeqAddAndIndexer()
            {
#line (232, 5) - (232, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var s = new global::Sharpy.CommentedSeq();
#line (233, 5) - (233, 13) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s.Add(1);
#line (234, 5) - (234, 13) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s.Add(2);
#line (235, 5) - (235, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Equal(2, s.Count);
#line (236, 5) - (236, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.True(@operator.Eq(s[0], 1));
#line (237, 5) - (237, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s[0] = 99;
#line (238, 5) - (238, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.True(@operator.Eq(s[0], 99));
            }

            [Xunit.FactAttribute]
            public void TestCommentedSeqInsertShiftsItems()
            {
#line (242, 5) - (242, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var s = new global::Sharpy.CommentedSeq();
#line (243, 5) - (243, 13) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s.Add(1);
#line (244, 5) - (244, 13) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s.Add(3);
#line (245, 5) - (245, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s.Insert(1, 2);
#line (246, 5) - (246, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Equal(3, s.Count);
#line (247, 5) - (247, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.True(@operator.Eq(s[0], 1));
#line (248, 5) - (248, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.True(@operator.Eq(s[1], 2));
#line (249, 5) - (249, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.True(@operator.Eq(s[2], 3));
            }

            [Xunit.FactAttribute]
            public void TestCommentedSeqRemoveAt()
            {
#line (253, 5) - (253, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var s = new global::Sharpy.CommentedSeq();
#line (254, 5) - (254, 13) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s.Add(1);
#line (255, 5) - (255, 13) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s.Add(2);
#line (256, 5) - (256, 13) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s.Add(3);
#line (257, 5) - (257, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s.RemoveAt(1);
#line (258, 5) - (258, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Equal(2, s.Count);
#line (259, 5) - (259, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.True(@operator.Eq(s[0], 1));
#line (260, 5) - (260, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.True(@operator.Eq(s[1], 3));
            }

            [Xunit.FactAttribute]
            public void TestCommentedSeqInsertShiftsComments()
            {
#line (264, 5) - (264, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var s = new global::Sharpy.CommentedSeq();
#line (265, 5) - (265, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s.Add("a");
#line (266, 5) - (266, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s.Add("b");
#line (267, 5) - (267, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s.GetOrAddComment(1).InlineComment = "b-comment";
#line (268, 5) - (268, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s.Insert(0, "z");
#line (269, 5) - (269, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.True(@operator.Eq(s[2], "b"));
#line (270, 5) - (270, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var comment = s.GetComment(2);
#line (271, 5) - (271, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.NotNull(comment);
#line (272, 5) - (272, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Equal("b-comment", comment.InlineComment);
            }

            [Xunit.FactAttribute]
            public void TestCommentedMapGetOrAddCommentReturnsMutableInstance()
            {
#line (278, 5) - (278, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var m = new global::Sharpy.CommentedMap();
#line (279, 5) - (279, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                m.Add("k", 1);
#line (280, 5) - (280, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var comment = m.GetOrAddComment("k");
#line (281, 5) - (281, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                comment.BeforeComment = "hi";
#line (283, 5) - (283, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Same(m.GetOrAddComment("k"), comment);
#line (284, 5) - (284, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var stored = m.GetComment("k");
#line (285, 5) - (285, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.NotNull(stored);
#line (286, 5) - (286, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Equal("hi", stored.BeforeComment);
            }
        }
    }
}
