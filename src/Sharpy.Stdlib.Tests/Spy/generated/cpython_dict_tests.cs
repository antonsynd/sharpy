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
#line (153, 5) - (158, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                try
#line hidden
                {
#line (154, 9) - (154, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                    return d[k] < 0;
#line hidden
                }
                catch (global::Sharpy.KeyError)
                {
#line (156, 9) - (156, 21) 20 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                    return true;
#line hidden
                }
            }

            internal static bool _PopRaises(Sharpy.Dict<string, int> d, string k)
            {
#line (181, 5) - (186, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                try
#line hidden
                {
#line (182, 9) - (182, 29) 20 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                    return d.Pop(k) < 0;
#line hidden
                }
                catch (global::Sharpy.KeyError)
                {
#line (184, 9) - (184, 21) 20 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                    return true;
#line hidden
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
#line (33, 5) - (33, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<int, int> empty = new Sharpy.Dict<int, int>()
#line hidden
                {
                };
#line (34, 5) - (34, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.False(global::Sharpy.Builtins.Bool(empty));
#line (35, 5) - (35, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Bool(new Sharpy.Dict<int, int>() { { 1, 2 } }));
#line (36, 5) - (36, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(empty));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLen()
            {
#line (42, 5) - (42, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> empty = new Sharpy.Dict<string, int>()
#line hidden
                {
                };
#line (43, 5) - (43, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(empty));
#line (44, 5) - (44, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
#line hidden
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
#line (45, 5) - (45, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestContains()
            {
#line (51, 5) - (51, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> empty = new Sharpy.Dict<string, int>()
#line hidden
                {
                };
#line (52, 5) - (52, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.True((!empty.Contains("a")));
#line (53, 5) - (53, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
#line hidden
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
#line (54, 5) - (54, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.True((d.Contains("a")));
#line (55, 5) - (55, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.True((d.Contains("b")));
#line (56, 5) - (56, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.True((!d.Contains("c")));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestKeys()
            {
#line (62, 5) - (62, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> empty = new Sharpy.Dict<string, int>()
#line hidden
                {
                };
#line (63, 5) - (63, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(empty));
#line (64, 5) - (64, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
#line hidden
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
#line (65, 5) - (65, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b" }, global::Sharpy.Builtins.Sorted<string>(d.Keys()));
#line (66, 5) - (66, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.True((d.Contains("a")));
#line (67, 5) - (67, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.True((d.Contains("b")));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestValues()
            {
#line (73, 5) - (73, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> empty = new Sharpy.Dict<string, int>()
#line hidden
                {
                };
#line (74, 5) - (74, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(empty));
#line (75, 5) - (75, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
#line hidden
                {
                    {
                        "a",
                        2
                    }
                };
#line (76, 5) - (76, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.List<int> collected = new Sharpy.List<int>()
#line hidden
                {
                };
#line (77, 5) - (79, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                foreach (var __loopVar_0 in d.Values())
#line hidden
                {
                    var v = __loopVar_0;
#line (78, 9) - (78, 28) 20 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                    collected.Append(v);
#line hidden
                }

#line (79, 5) - (79, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 2 }, collected);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestItems()
            {
#line (85, 5) - (85, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> empty = new Sharpy.Dict<string, int>()
#line hidden
                {
                };
#line (86, 5) - (86, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                int count = 0;
#line (87, 5) - (89, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                foreach (var __loopVar_1 in empty.Items())
#line hidden
                {
                    var _pair = __loopVar_1;
#line (88, 9) - (88, 19) 20 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                    count = count + 1;
#line hidden
                }

#line (89, 5) - (89, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(0, count);
#line (90, 5) - (90, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
#line hidden
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
#line (91, 5) - (91, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.List<string> keyacc = new Sharpy.List<string>()
#line hidden
                {
                };
#line (92, 5) - (92, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                int valsum = 0;
#line (93, 5) - (96, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                foreach (var __loopVar_2 in d.Items())
#line hidden
                {
                    var pair = __loopVar_2;
#line (94, 9) - (94, 31) 20 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                    keyacc.Append(pair.Item1);
#line (95, 9) - (95, 26) 20 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                    valsum = valsum + pair.Item2;
#line hidden
                }

#line (96, 5) - (96, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                keyacc.Sort();
#line (97, 5) - (97, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b" }, keyacc);
#line (98, 5) - (98, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(3, valsum);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestClear()
            {
#line (104, 5) - (104, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<int, int> d = new Sharpy.Dict<int, int>()
#line hidden
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
#line (105, 5) - (105, 14) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                d.Clear();
#line (106, 5) - (106, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestGet()
            {
#line (112, 5) - (112, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> empty = new Sharpy.Dict<string, int>()
#line hidden
                {
                };
#line (113, 5) - (113, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(-1, empty.Get("c", -1));
#line (114, 5) - (114, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(3, empty.Get("c", 3));
#line (115, 5) - (115, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
#line hidden
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
#line (116, 5) - (116, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(-1, d.Get("c", -1));
#line (117, 5) - (117, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(3, d.Get("c", 3));
#line (118, 5) - (118, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(1, d.Get("a", 3));
#line (119, 5) - (119, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(1, d.Get("a", -1));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCopy()
            {
#line (125, 5) - (125, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<int, int> d = new Sharpy.Dict<int, int>()
#line hidden
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
#line (126, 5) - (126, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<int, int> c = d.Copy();
#line (127, 5) - (127, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(d, c);
#line (128, 5) - (128, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(c));
#line (130, 5) - (130, 13) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                d[4] = 4;
#line (131, 5) - (131, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(c));
#line (132, 5) - (132, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(4, global::Sharpy.Builtins.Len(d));
#line (134, 5) - (134, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<int, int> empty = new Sharpy.Dict<int, int>()
#line hidden
                {
                };
#line (135, 5) - (135, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(empty.Copy()));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestUpdate()
            {
#line (141, 5) - (141, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
#line hidden
                {
                    {
                        "a",
                        1
                    }
                };
#line (142, 5) - (142, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                d.Update(new Sharpy.Dict<string, int>() { { "b", 2 }, { "c", 3 } });
#line (143, 5) - (143, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "c" }, global::Sharpy.Builtins.Sorted<string>(d.Keys()));
#line (144, 5) - (144, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(2, d["b"]);
#line (145, 5) - (145, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(3, d["c"]);
#line (147, 5) - (147, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                d.Update(new Sharpy.Dict<string, int>() { { "a", 10 } });
#line (148, 5) - (148, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(10, d["a"]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestKeyerrorOnMissing()
            {
#line (160, 5) - (160, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
#line hidden
                {
                    {
                        "a",
                        1
                    }
                };
#line (161, 5) - (161, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.True(_GetitemRaises(d, "z"));
#line (162, 5) - (162, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> empty = new Sharpy.Dict<string, int>()
#line hidden
                {
                };
#line (163, 5) - (163, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.True(_GetitemRaises(empty, "anything"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSetitemGetitem()
            {
#line (169, 5) - (169, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
#line hidden
                {
                };
#line (170, 5) - (170, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                d["a"] = 1;
#line (171, 5) - (171, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                d["b"] = 2;
#line (172, 5) - (172, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(1, d["a"]);
#line (173, 5) - (173, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(2, d["b"]);
#line (174, 5) - (174, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                d["a"] = 10;
#line (175, 5) - (175, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(10, d["a"]);
#line (176, 5) - (176, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestPop()
            {
#line (188, 5) - (188, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
#line hidden
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
#line (189, 5) - (189, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(1, d.Pop("a"));
#line (190, 5) - (190, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.True((!d.Contains("a")));
#line (191, 5) - (191, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(d));
#line (192, 5) - (192, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(2, d["b"]);
#line (193, 5) - (193, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.True(_PopRaises(d, "missing"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestConstructor()
            {
#line (199, 5) - (199, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> empty = new Sharpy.Dict<string, int>()
#line hidden
                {
                };
#line (200, 5) - (200, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(empty));
#line (201, 5) - (201, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
#line hidden
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
#line (202, 5) - (202, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(d));
#line (203, 5) - (203, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(1, d["a"]);
#line (204, 5) - (204, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(3, d["c"]);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestEquality()
            {
#line (210, 5) - (210, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.Dict<string, int>() { { "b", 2 }, { "a", 1 } }, new Sharpy.Dict<string, int>() { { "a", 1 }, { "b", 2 } });
#line (211, 5) - (211, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.NotEqual(new Sharpy.Dict<string, int>() { { "a", 2 } }, new Sharpy.Dict<string, int>() { { "a", 1 } });
#line (212, 5) - (212, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.NotEqual(new Sharpy.Dict<string, int>() { { "a", 1 }, { "b", 2 } }, new Sharpy.Dict<string, int>() { { "a", 1 } });
#line (213, 5) - (213, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> empty1 = new Sharpy.Dict<string, int>()
#line hidden
                {
                };
#line (214, 5) - (214, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> empty2 = new Sharpy.Dict<string, int>()
#line hidden
                {
                };
#line (215, 5) - (215, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(empty2, empty1);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMergeOperator()
            {
#line (221, 5) - (221, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<int, int> a = new Sharpy.Dict<int, int>()
#line hidden
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
#line (222, 5) - (222, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<int, int> b = new Sharpy.Dict<int, int>()
#line hidden
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
#line (225, 5) - (225, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.Dict<int, int>() { { 0, 0 }, { 1, 1 }, { 2, 2 }, { 3, 3 } }, (a | b));
#line (226, 5) - (226, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.Dict<int, int>() { { 1, 1 }, { 2, 1 }, { 3, 3 }, { 0, 0 } }, (b | a));
#line (227, 5) - (227, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<int, int> c = a.Copy();
#line (228, 5) - (228, 11) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                c = c | b;
#line (229, 5) - (229, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.Dict<int, int>() { { 0, 0 }, { 1, 1 }, { 2, 2 }, { 3, 3 } }, c);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestTupleKeyerror()
            {
#line (235, 5) - (235, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<global::System.ValueTuple<int, int>, string> d = new Sharpy.Dict<global::System.ValueTuple<int, int>, string>()
#line hidden
                {
                };
#line (236, 5) - (236, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                d[(1, 2)] = "x";
#line (237, 5) - (237, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal("x", d[(1, 2)]);
#line (238, 5) - (238, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                bool ke = false;
#line (239, 5) - (243, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                try
#line hidden
                {
#line (240, 9) - (240, 22) 20 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                    var _ = d[(3, 4)];
#line hidden
                }
                catch (global::Sharpy.KeyError)
                {
#line (242, 9) - (242, 18) 20 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                    ke = true;
#line hidden
                }

#line (243, 5) - (243, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.True(ke);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestMutatingIteration()
            {
#line (249, 5) - (249, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<int, int> d = new Sharpy.Dict<int, int>()
#line hidden
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
#line (250, 5) - (252, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                foreach (var __loopVar_3 in global::Sharpy.Builtins.Sorted<int>(d.Keys()))
#line hidden
                {
                    var k = __loopVar_3;
#line (251, 9) - (251, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                    d[k] = d[k] * 10;
#line hidden
                }

#line (252, 5) - (252, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(new Sharpy.Dict<int, int>() { { 1, 10 }, { 2, 20 }, { 3, 30 } }, d);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestStringKeysCanTrackValues()
            {
#line (258, 5) - (258, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
#line hidden
                {
                };
#line (259, 5) - (259, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                d["a"] = 1;
#line (260, 5) - (260, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                d["b"] = 2;
#line (261, 5) - (261, 15) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                d["a"] = 3;
#line (262, 5) - (262, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(3, d["a"]);
#line (263, 5) - (263, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(2, d["b"]);
#line (264, 5) - (264, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSetdefault()
            {
#line (273, 5) - (273, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
#line hidden
                {
                };
#line (274, 5) - (274, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(0, d.SetDefault("key", 0));
#line (275, 5) - (275, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(0, d["key"]);
#line (276, 5) - (276, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(0, d.SetDefault("key", 99));
#line (277, 5) - (277, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(0, d["key"]);
#line (278, 5) - (278, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(3, d.SetDefault("other", 3));
#line (279, 5) - (279, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestPopitem()
            {
#line (287, 5) - (287, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
#line hidden
                {
                    {
                        "a",
                        1
                    }
                };
#line (288, 5) - (288, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                global::System.ValueTuple<string, int> pair = d.PopItem();
#line (289, 5) - (289, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(("a", 1), pair);
#line (290, 5) - (290, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestPopitemLifoOrder()
            {
#line (297, 5) - (297, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
#line hidden
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
#line (298, 5) - (298, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(("c", 3), d.PopItem());
#line (299, 5) - (299, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(("b", 2), d.PopItem());
#line (300, 5) - (300, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(("a", 1), d.PopItem());
#line (301, 5) - (301, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_dict_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(d));
#line hidden
            }
        }
    }
}
#line default
