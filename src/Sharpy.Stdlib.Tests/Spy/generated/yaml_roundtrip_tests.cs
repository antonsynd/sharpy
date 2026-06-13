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
#line (28, 5) - (28, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                object result = yaml.RoundtripLoad("name: test\ncount: 42\n");
#line (29, 5) - (36, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                switch (result)
                {
                    case global::Sharpy.CommentedMap m:
#line (31, 13) - (31, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(m["name"], "test"));
#line (32, 13) - (32, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(m["count"], 42));
                        break;
                    default:
#line (34, 13) - (34, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundtripLoadSequenceReturnsCommentedSeq()
            {
#line (38, 5) - (38, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                object result = yaml.RoundtripLoad("- 1\n- 2\n- 3\n");
#line (39, 5) - (47, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                switch (result)
                {
                    case global::Sharpy.CommentedSeq s:
#line (41, 13) - (41, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.Equal(3, s.Count);
#line (42, 13) - (42, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(s[0], 1));
#line (43, 13) - (43, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(s[2], 3));
                        break;
                    default:
#line (45, 13) - (45, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundtripLoadScalarTypesResolved()
            {
#line (49, 5) - (49, 88) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                object result = yaml.RoundtripLoad("i: 5\nf: 2.5\nb: true\nn: null\ns: hello\n");
#line (50, 5) - (60, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                switch (result)
                {
                    case global::Sharpy.CommentedMap m:
#line (52, 13) - (52, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(m["i"], 5));
#line (53, 13) - (53, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(m["f"], 2.5d));
#line (54, 13) - (54, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(m["b"], true));
#line (55, 13) - (55, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.Null(m["n"]);
#line (56, 13) - (56, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(m["s"], "hello"));
                        break;
                    default:
#line (58, 13) - (58, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundtripLoadQuotedNumberStaysString()
            {
#line (62, 5) - (62, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                object result = yaml.RoundtripLoad("version: \"3\"\n");
#line (63, 5) - (72, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                switch (result)
                {
                    case global::Sharpy.CommentedMap m:
#line (65, 13) - (65, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.IsAssignableFrom<string>(m["version"]);
#line (66, 13) - (66, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(m["version"], "3"));
                        break;
                    default:
#line (68, 13) - (68, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundtripLoadBeforeCommentPreserved()
            {
#line (74, 5) - (74, 79) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                object result = yaml.RoundtripLoad("# this is a comment\nkey: value\n");
#line (75, 5) - (85, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                switch (result)
                {
                    case global::Sharpy.CommentedMap m:
#line (77, 13) - (77, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        var comment = m.GetComment("key");
#line (78, 13) - (78, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.NotNull(comment);
#line (79, 13) - (79, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        var before = comment.BeforeComment;
#line (80, 13) - (80, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.NotNull(before);
#line (81, 13) - (81, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.Contains("this is a comment", before);
                        break;
                    default:
#line (83, 13) - (83, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundtripLoadInlineCommentPreserved()
            {
#line (87, 5) - (87, 74) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                object result = yaml.RoundtripLoad("key: value # trailing note\n");
#line (88, 5) - (98, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                switch (result)
                {
                    case global::Sharpy.CommentedMap m:
#line (90, 13) - (90, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        var comment = m.GetComment("key");
#line (91, 13) - (91, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.NotNull(comment);
#line (92, 13) - (92, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        var inline = comment.InlineComment;
#line (93, 13) - (93, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.NotNull(inline);
#line (94, 13) - (94, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.Contains("trailing note", inline);
                        break;
                    default:
#line (96, 13) - (96, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundtripDumpPreservesComments()
            {
#line (100, 5) - (100, 86) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                object loaded = yaml.RoundtripLoad("# leading comment\nname: test # inline\n");
#line (101, 5) - (101, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                string dumped = yaml.RoundtripDump(loaded);
#line (102, 5) - (102, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Contains("leading comment", dumped);
#line (103, 5) - (103, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Contains("inline", dumped);
#line (104, 5) - (104, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Contains("name:", dumped);
            }

            [Xunit.FactAttribute]
            public void TestRoundtripCommentsSurviveReload()
            {
#line (108, 5) - (108, 103) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                object loaded = yaml.RoundtripLoad("# config header\nhost: localhost # the host\nport: 8080\n");
#line (109, 5) - (109, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                string dumped = yaml.RoundtripDump(loaded);
#line (110, 5) - (124, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                switch (yaml.RoundtripLoad(dumped))
                {
                    case global::Sharpy.CommentedMap reloaded:
#line (112, 13) - (112, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(reloaded["host"], "localhost"));
#line (113, 13) - (113, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(reloaded["port"], 8080));
#line (114, 13) - (114, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        var hostComment = reloaded.GetComment("host");
#line (115, 13) - (115, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.NotNull(hostComment);
#line (116, 13) - (116, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        var inline = hostComment.InlineComment;
#line (117, 13) - (117, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.NotNull(inline);
#line (118, 13) - (118, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.Contains("the host", inline);
                        break;
                    default:
#line (120, 13) - (120, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundtripDumpPlainDictRoundTrips()
            {
#line (126, 5) - (126, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Sharpy.Dict<string, object> data = new Sharpy.Dict<string, object>()
                {
                };
#line (127, 5) - (127, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                data["a"] = 1;
#line (128, 5) - (128, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                data["b"] = "two";
#line (129, 5) - (129, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                string dumped = yaml.RoundtripDump(data);
#line (130, 5) - (137, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                switch (yaml.RoundtripLoad(dumped))
                {
                    case global::Sharpy.CommentedMap reloaded:
#line (132, 13) - (132, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(reloaded["a"], 1));
#line (133, 13) - (133, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(reloaded["b"], "two"));
                        break;
                    default:
#line (135, 13) - (135, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundtripDumpPlainListRoundTrips()
            {
#line (139, 5) - (139, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Sharpy.List<object> data = new Sharpy.List<object>()
                {
                };
#line (140, 5) - (140, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                data.Append(1);
#line (141, 5) - (141, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                data.Append(2);
#line (142, 5) - (142, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                data.Append(3);
#line (143, 5) - (143, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                string dumped = yaml.RoundtripDump(data);
#line (144, 5) - (151, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                switch (yaml.RoundtripLoad(dumped))
                {
                    case global::Sharpy.CommentedSeq reloaded:
#line (146, 13) - (146, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.Equal(3, reloaded.Count);
#line (147, 13) - (147, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(reloaded[0], 1));
                        break;
                    default:
#line (149, 13) - (149, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestRoundtripDumpNestedCommentedMapRoundTrips()
            {
#line (153, 5) - (153, 101) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                object loaded = yaml.RoundtripLoad("server:\n  host: localhost\n  port: 9000\ndebug: true\n");
#line (154, 5) - (154, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                string dumped = yaml.RoundtripDump(loaded);
#line (155, 5) - (169, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                switch (yaml.RoundtripLoad(dumped))
                {
                    case global::Sharpy.CommentedMap reloaded:
#line (157, 13) - (163, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        switch (reloaded["server"])
                        {
                            case global::Sharpy.CommentedMap server:
#line (159, 21) - (159, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                                Xunit.Assert.True(@operator.Eq(server["host"], "localhost"));
#line (160, 21) - (160, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                                Xunit.Assert.True(@operator.Eq(server["port"], 9000));
                                break;
                            default:
#line (162, 21) - (162, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                                Xunit.Assert.True(false);
                                break;
                        }

#line (163, 13) - (163, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(@operator.Eq(reloaded["debug"], true));
                        break;
                    default:
#line (165, 13) - (165, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                        Xunit.Assert.True(false);
                        break;
                }
            }

            [Xunit.FactAttribute]
            public void TestCommentedMapAddAndIndexer()
            {
#line (171, 5) - (171, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var m = new global::Sharpy.CommentedMap();
#line (172, 5) - (172, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                m.Add("a", 1);
#line (173, 5) - (173, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                m["b"] = 2;
#line (174, 5) - (174, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.True(@operator.Eq(m["a"], 1));
#line (175, 5) - (175, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.True(@operator.Eq(m["b"], 2));
#line (176, 5) - (176, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Equal(2, m.Count);
            }

            [Xunit.FactAttribute]
            public void TestCommentedMapContainsKey()
            {
#line (180, 5) - (180, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var m = new global::Sharpy.CommentedMap();
#line (181, 5) - (181, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                m.Add("present", 1);
#line (182, 5) - (182, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.True(m.ContainsKey("present"));
#line (183, 5) - (183, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.False(m.ContainsKey("absent"));
            }

            [Xunit.FactAttribute]
            public void TestCommentedMapRemoveRemovesKeyAndComment()
            {
#line (187, 5) - (187, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var m = new global::Sharpy.CommentedMap();
#line (188, 5) - (188, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                m.Add("a", 1);
#line (189, 5) - (189, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                m.Add("b", 2);
#line (190, 5) - (190, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                m.GetOrAddComment("a").InlineComment = "note";
#line (191, 5) - (191, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.True(m.Remove("a"));
#line (192, 5) - (192, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.False(m.ContainsKey("a"));
#line (193, 5) - (193, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Null(m.GetComment("a"));
#line (194, 5) - (194, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Equal(1, m.Count);
            }

            [Xunit.FactAttribute]
            public void TestCommentedMapRemoveMissingKeyReturnsFalse()
            {
#line (198, 5) - (198, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var m = new global::Sharpy.CommentedMap();
#line (199, 5) - (199, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.False(m.Remove("missing"));
            }

            [Xunit.FactAttribute]
            public void TestCommentedMapOverwriteExistingKeyDoesNotDuplicateOrder()
            {
#line (203, 5) - (203, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var m = new global::Sharpy.CommentedMap();
#line (204, 5) - (204, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                m.Add("a", 1);
#line (205, 5) - (205, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                m["a"] = 99;
#line (206, 5) - (206, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Equal(1, m.Count);
#line (207, 5) - (207, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.True(@operator.Eq(m["a"], 99));
            }

            [Xunit.FactAttribute]
            public void TestCommentedSeqAddAndIndexer()
            {
#line (213, 5) - (213, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var s = new global::Sharpy.CommentedSeq();
#line (214, 5) - (214, 13) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s.Add(1);
#line (215, 5) - (215, 13) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s.Add(2);
#line (216, 5) - (216, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Equal(2, s.Count);
#line (217, 5) - (217, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.True(@operator.Eq(s[0], 1));
#line (218, 5) - (218, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s[0] = 99;
#line (219, 5) - (219, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.True(@operator.Eq(s[0], 99));
            }

            [Xunit.FactAttribute]
            public void TestCommentedSeqInsertShiftsItems()
            {
#line (223, 5) - (223, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var s = new global::Sharpy.CommentedSeq();
#line (224, 5) - (224, 13) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s.Add(1);
#line (225, 5) - (225, 13) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s.Add(3);
#line (226, 5) - (226, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s.Insert(1, 2);
#line (227, 5) - (227, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Equal(3, s.Count);
#line (228, 5) - (228, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.True(@operator.Eq(s[0], 1));
#line (229, 5) - (229, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.True(@operator.Eq(s[1], 2));
#line (230, 5) - (230, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.True(@operator.Eq(s[2], 3));
            }

            [Xunit.FactAttribute]
            public void TestCommentedSeqRemoveAt()
            {
#line (234, 5) - (234, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var s = new global::Sharpy.CommentedSeq();
#line (235, 5) - (235, 13) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s.Add(1);
#line (236, 5) - (236, 13) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s.Add(2);
#line (237, 5) - (237, 13) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s.Add(3);
#line (238, 5) - (238, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s.RemoveAt(1);
#line (239, 5) - (239, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Equal(2, s.Count);
#line (240, 5) - (240, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.True(@operator.Eq(s[0], 1));
#line (241, 5) - (241, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.True(@operator.Eq(s[1], 3));
            }

            [Xunit.FactAttribute]
            public void TestCommentedSeqInsertShiftsComments()
            {
#line (245, 5) - (245, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var s = new global::Sharpy.CommentedSeq();
#line (246, 5) - (246, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s.Add("a");
#line (247, 5) - (247, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s.Add("b");
#line (248, 5) - (248, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s.GetOrAddComment(1).InlineComment = "b-comment";
#line (249, 5) - (249, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                s.Insert(0, "z");
#line (250, 5) - (250, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.True(@operator.Eq(s[2], "b"));
#line (251, 5) - (251, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var comment = s.GetComment(2);
#line (252, 5) - (252, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.NotNull(comment);
#line (253, 5) - (253, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Equal("b-comment", comment.InlineComment);
            }

            [Xunit.FactAttribute]
            public void TestCommentedMapGetOrAddCommentReturnsMutableInstance()
            {
#line (259, 5) - (259, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var m = new global::Sharpy.CommentedMap();
#line (260, 5) - (260, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                m.Add("k", 1);
#line (261, 5) - (261, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var comment = m.GetOrAddComment("k");
#line (262, 5) - (262, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                comment.BeforeComment = "hi";
#line (264, 5) - (264, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Same(m.GetOrAddComment("k"), comment);
#line (265, 5) - (265, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                var stored = m.GetComment("k");
#line (266, 5) - (266, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.NotNull(stored);
#line (267, 5) - (267, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_roundtrip_tests.spy"
                Xunit.Assert.Equal("hi", stored.BeforeComment);
            }
        }
    }
}
