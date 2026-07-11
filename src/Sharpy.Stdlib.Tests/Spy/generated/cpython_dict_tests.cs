// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Cpython.CpythonDictTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Cpython
    {
        [global::Sharpy.SharpyModule("cpython.cpython_dict_tests")]
        public static partial class CpythonDictTests
        {
            internal static bool _GetitemRaises(Sharpy.Dict<string, int> d, string k)
            {
#line (149, 5) - (154, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                try
                {
#line (150, 9) - (150, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                    return d[k] < 0;
                }
                catch (KeyError)
                {
#line (152, 9) - (152, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                    return true;
                }
            }

            internal static bool _PopRaises(Sharpy.Dict<string, int> d, string k)
            {
#line (177, 5) - (182, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                try
                {
#line (178, 9) - (178, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                    return d.Pop(k) < 0;
                }
                catch (KeyError)
                {
#line (180, 9) - (180, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                    return true;
                }
            }
        }
    }

    public static partial class Cpython
    {
        public partial class CpythonDictTestsTests
        {
            [Xunit.FactAttribute]
            public void TestBool()
            {
#line (29, 5) - (29, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<int, int> empty = new Sharpy.Dict<int, int>()
                {
                };
#line (30, 5) - (30, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.False(global::Sharpy.Builtins.Bool(empty));
#line (31, 5) - (31, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Bool(new Sharpy.Dict<int, int>() { { 1, 2 } }));
#line (32, 5) - (32, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(empty));
            }

            [Xunit.FactAttribute]
            public void TestLen()
            {
#line (38, 5) - (38, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> empty = new Sharpy.Dict<string, int>()
                {
                };
#line (39, 5) - (39, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(empty));
#line (40, 5) - (40, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
                {
                    {
                        "a",
                        1
                    },
                    {
                        "b",
                        2
                    }
                };
#line (41, 5) - (41, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(d));
            }

            [Xunit.FactAttribute]
            public void TestContains()
            {
#line (47, 5) - (47, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> empty = new Sharpy.Dict<string, int>()
                {
                };
#line (48, 5) - (48, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.True((!empty.Contains("a")));
#line (49, 5) - (49, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
                {
                    {
                        "a",
                        1
                    },
                    {
                        "b",
                        2
                    }
                };
#line (50, 5) - (50, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.True((d.Contains("a")));
#line (51, 5) - (51, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.True((d.Contains("b")));
#line (52, 5) - (52, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.True((!d.Contains("c")));
            }

            [Xunit.FactAttribute]
            public void TestKeys()
            {
#line (58, 5) - (58, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> empty = new Sharpy.Dict<string, int>()
                {
                };
#line (59, 5) - (59, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(empty));
#line (60, 5) - (60, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
                {
                    {
                        "a",
                        1
                    },
                    {
                        "b",
                        2
                    }
                };
#line (61, 5) - (61, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b" }, global::Sharpy.Builtins.Sorted<string>(d.Keys()));
#line (62, 5) - (62, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.True((d.Contains("a")));
#line (63, 5) - (63, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.True((d.Contains("b")));
            }

            [Xunit.FactAttribute]
            public void TestValues()
            {
#line (69, 5) - (69, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> empty = new Sharpy.Dict<string, int>()
                {
                };
#line (70, 5) - (70, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(empty));
#line (71, 5) - (71, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
                {
                    {
                        "a",
                        2
                    }
                };
#line (72, 5) - (72, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.List<int> collected = new Sharpy.List<int>()
                {
                };
#line (73, 5) - (75, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                foreach (var __loopVar_0 in d.Values())
                {
                    var v = __loopVar_0;
#line (74, 9) - (74, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                    collected.Append(v);
                }

#line (75, 5) - (75, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 2 }, collected);
            }

            [Xunit.FactAttribute]
            public void TestItems()
            {
#line (81, 5) - (81, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> empty = new Sharpy.Dict<string, int>()
                {
                };
#line (82, 5) - (82, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                int count = 0;
#line (83, 5) - (85, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                foreach (var __loopVar_1 in empty.Items())
                {
                    var _pair = __loopVar_1;
#line (84, 9) - (84, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                    count = count + 1;
                }

#line (85, 5) - (85, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(0, count);
#line (86, 5) - (86, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
                {
                    {
                        "a",
                        1
                    },
                    {
                        "b",
                        2
                    }
                };
#line (87, 5) - (87, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.List<string> keyacc = new Sharpy.List<string>()
                {
                };
#line (88, 5) - (88, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                int valsum = 0;
#line (89, 5) - (92, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                foreach (var __loopVar_2 in d.Items())
                {
                    var pair = __loopVar_2;
#line (90, 9) - (90, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                    keyacc.Append(pair.Item1);
#line (91, 9) - (91, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                    valsum = valsum + pair.Item2;
                }

#line (92, 5) - (92, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                keyacc.Sort();
#line (93, 5) - (93, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b" }, keyacc);
#line (94, 5) - (94, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(3, valsum);
            }

            [Xunit.FactAttribute]
            public void TestClear()
            {
#line (100, 5) - (100, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<int, int> d = new Sharpy.Dict<int, int>()
                {
                    {
                        1,
                        1
                    },
                    {
                        2,
                        2
                    },
                    {
                        3,
                        3
                    }
                };
#line (101, 5) - (101, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                d.Clear();
#line (102, 5) - (102, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(d));
            }

            [Xunit.FactAttribute]
            public void TestGet()
            {
#line (108, 5) - (108, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> empty = new Sharpy.Dict<string, int>()
                {
                };
#line (109, 5) - (109, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(-1, empty.Get("c", -1));
#line (110, 5) - (110, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(3, empty.Get("c", 3));
#line (111, 5) - (111, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
                {
                    {
                        "a",
                        1
                    },
                    {
                        "b",
                        2
                    }
                };
#line (112, 5) - (112, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(-1, d.Get("c", -1));
#line (113, 5) - (113, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(3, d.Get("c", 3));
#line (114, 5) - (114, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(1, d.Get("a", 3));
#line (115, 5) - (115, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(1, d.Get("a", -1));
            }

            [Xunit.FactAttribute]
            public void TestCopy()
            {
#line (121, 5) - (121, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<int, int> d = new Sharpy.Dict<int, int>()
                {
                    {
                        1,
                        1
                    },
                    {
                        2,
                        2
                    },
                    {
                        3,
                        3
                    }
                };
#line (122, 5) - (122, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<int, int> c = d.Copy();
#line (123, 5) - (123, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(d, c);
#line (124, 5) - (124, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(c));
#line (126, 5) - (126, 13) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                d[4] = 4;
#line (127, 5) - (127, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(c));
#line (128, 5) - (128, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(4, global::Sharpy.Builtins.Len(d));
#line (130, 5) - (130, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<int, int> empty = new Sharpy.Dict<int, int>()
                {
                };
#line (131, 5) - (131, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(empty.Copy()));
            }

            [Xunit.FactAttribute]
            public void TestUpdate()
            {
#line (137, 5) - (137, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
                {
                    {
                        "a",
                        1
                    }
                };
#line (138, 5) - (138, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                d.Update(new Sharpy.Dict<string, int>() { { "b", 2 }, { "c", 3 } });
#line (139, 5) - (139, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "c" }, global::Sharpy.Builtins.Sorted<string>(d.Keys()));
#line (140, 5) - (140, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(2, d["b"]);
#line (141, 5) - (141, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(3, d["c"]);
#line (143, 5) - (143, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                d.Update(new Sharpy.Dict<string, int>() { { "a", 10 } });
#line (144, 5) - (144, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(10, d["a"]);
            }

            [Xunit.FactAttribute]
            public void TestKeyerrorOnMissing()
            {
#line (156, 5) - (156, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
                {
                    {
                        "a",
                        1
                    }
                };
#line (157, 5) - (157, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.True(_GetitemRaises(d, "z"));
#line (158, 5) - (158, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> empty = new Sharpy.Dict<string, int>()
                {
                };
#line (159, 5) - (159, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.True(_GetitemRaises(empty, "anything"));
            }

            [Xunit.FactAttribute]
            public void TestSetitemGetitem()
            {
#line (165, 5) - (165, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
                {
                };
#line (166, 5) - (166, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                d["a"] = 1;
#line (167, 5) - (167, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                d["b"] = 2;
#line (168, 5) - (168, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(1, d["a"]);
#line (169, 5) - (169, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(2, d["b"]);
#line (170, 5) - (170, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                d["a"] = 10;
#line (171, 5) - (171, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(10, d["a"]);
#line (172, 5) - (172, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(d));
            }

            [Xunit.FactAttribute]
            public void TestPop()
            {
#line (184, 5) - (184, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
                {
                    {
                        "a",
                        1
                    },
                    {
                        "b",
                        2
                    }
                };
#line (185, 5) - (185, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(1, d.Pop("a"));
#line (186, 5) - (186, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.True((!d.Contains("a")));
#line (187, 5) - (187, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(d));
#line (188, 5) - (188, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(2, d["b"]);
#line (189, 5) - (189, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.True(_PopRaises(d, "missing"));
            }

            [Xunit.FactAttribute]
            public void TestConstructor()
            {
#line (195, 5) - (195, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> empty = new Sharpy.Dict<string, int>()
                {
                };
#line (196, 5) - (196, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(empty));
#line (197, 5) - (197, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
                {
                    {
                        "a",
                        1
                    },
                    {
                        "b",
                        2
                    },
                    {
                        "c",
                        3
                    }
                };
#line (198, 5) - (198, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(d));
#line (199, 5) - (199, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(1, d["a"]);
#line (200, 5) - (200, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(3, d["c"]);
            }

            [Xunit.FactAttribute]
            public void TestEquality()
            {
#line (206, 5) - (206, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.Dict<string, int>() { { "b", 2 }, { "a", 1 } }, new Sharpy.Dict<string, int>() { { "a", 1 }, { "b", 2 } });
#line (207, 5) - (207, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.NotEqual(new Sharpy.Dict<string, int>() { { "a", 2 } }, new Sharpy.Dict<string, int>() { { "a", 1 } });
#line (208, 5) - (208, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.NotEqual(new Sharpy.Dict<string, int>() { { "a", 1 }, { "b", 2 } }, new Sharpy.Dict<string, int>() { { "a", 1 } });
#line (209, 5) - (209, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> empty1 = new Sharpy.Dict<string, int>()
                {
                };
#line (210, 5) - (210, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> empty2 = new Sharpy.Dict<string, int>()
                {
                };
#line (211, 5) - (211, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(empty2, empty1);
            }

            [Xunit.FactAttribute]
            public void TestMergeOperator()
            {
#line (217, 5) - (217, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<int, int> a = new Sharpy.Dict<int, int>()
                {
                    {
                        0,
                        0
                    },
                    {
                        1,
                        1
                    },
                    {
                        2,
                        1
                    }
                };
#line (218, 5) - (218, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<int, int> b = new Sharpy.Dict<int, int>()
                {
                    {
                        1,
                        1
                    },
                    {
                        2,
                        2
                    },
                    {
                        3,
                        3
                    }
                };
#line (221, 5) - (221, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.Dict<int, int>() { { 0, 0 }, { 1, 1 }, { 2, 2 }, { 3, 3 } }, (a | b));
#line (222, 5) - (222, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.Dict<int, int>() { { 1, 1 }, { 2, 1 }, { 3, 3 }, { 0, 0 } }, (b | a));
#line (223, 5) - (223, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<int, int> c = a.Copy();
#line (224, 5) - (224, 11) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                c = c | b;
#line (225, 5) - (225, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.Dict<int, int>() { { 0, 0 }, { 1, 1 }, { 2, 2 }, { 3, 3 } }, c);
            }

            [Xunit.FactAttribute]
            public void TestTupleKeyerror()
            {
#line (231, 5) - (231, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<global::System.ValueTuple<int, int>, string> d = new Sharpy.Dict<global::System.ValueTuple<int, int>, string>()
                {
                };
#line (232, 5) - (232, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                d[(1, 2)] = "x";
#line (233, 5) - (233, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal("x", d[(1, 2)]);
#line (234, 5) - (234, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                bool ke = false;
#line (235, 5) - (239, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                try
                {
#line (236, 9) - (236, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                    var _ = d[(3, 4)];
                }
                catch (KeyError)
                {
#line (238, 9) - (238, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                    ke = true;
                }

#line (239, 5) - (239, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.True(ke);
            }

            [Xunit.FactAttribute]
            public void TestMutatingIteration()
            {
#line (245, 5) - (245, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<int, int> d = new Sharpy.Dict<int, int>()
                {
                    {
                        1,
                        1
                    },
                    {
                        2,
                        2
                    },
                    {
                        3,
                        3
                    }
                };
#line (246, 5) - (248, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                foreach (var __loopVar_3 in global::Sharpy.Builtins.Sorted<int>(d.Keys()))
                {
                    var k = __loopVar_3;
#line (247, 9) - (247, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                    d[k] = d[k] * 10;
                }

#line (248, 5) - (248, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.Dict<int, int>() { { 1, 10 }, { 2, 20 }, { 3, 30 } }, d);
            }

            [Xunit.FactAttribute]
            public void TestStringKeysCanTrackValues()
            {
#line (254, 5) - (254, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
                {
                };
#line (255, 5) - (255, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                d["a"] = 1;
#line (256, 5) - (256, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                d["b"] = 2;
#line (257, 5) - (257, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                d["a"] = 3;
#line (258, 5) - (258, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(3, d["a"]);
#line (259, 5) - (259, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(2, d["b"]);
#line (260, 5) - (260, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(d));
            }
        }
    }
}
