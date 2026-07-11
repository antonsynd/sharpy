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
using static Sharpy.Stdlib.Tests.Spy.Cpython.CpythonListTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Cpython
    {
        [global::Sharpy.SharpyModule("cpython.cpython_list_tests")]
        public static partial class CpythonListTests
        {
            internal static bool _PopRaises(Sharpy.List<int> a, int i)
            {
#line (70, 5) - (75, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                try
                {
#line (71, 9) - (71, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                    return a.Pop(i) < 0;
                }
                catch (IndexError)
                {
#line (73, 9) - (73, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                    return true;
                }
            }

            internal static bool _PopEmptyRaises(Sharpy.List<int> a)
            {
#line (76, 5) - (81, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                try
                {
#line (77, 9) - (77, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                    return a.Pop() < 0;
                }
                catch (IndexError)
                {
#line (79, 9) - (79, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                    return true;
                }
            }

            internal static bool _RemoveRaises(Sharpy.List<int> a, int v)
            {
#line (96, 5) - (102, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                try
                {
#line (97, 9) - (97, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                    a.Remove(v);
#line (98, 9) - (98, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                    return false;
                }
                catch (ValueError)
                {
#line (100, 9) - (100, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                    return true;
                }
            }

            internal static bool _IndexRaises(Sharpy.List<int> a, int v)
            {
#line (117, 5) - (122, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                try
                {
#line (118, 9) - (118, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                    return a.Index(v) < 0;
                }
                catch (ValueError)
                {
#line (120, 9) - (120, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                    return true;
                }
            }

            internal static bool _GetitemRaises(Sharpy.List<int> a, int i)
            {
#line (237, 5) - (242, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                try
                {
#line (238, 9) - (238, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                    return a[i] < 0;
                }
                catch (IndexError)
                {
#line (240, 9) - (240, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                    return true;
                }
            }

            internal static bool _SetitemRaises(Sharpy.List<int> a, int i)
            {
#line (267, 5) - (273, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                try
                {
#line (268, 9) - (268, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                    a[i] = 200;
#line (269, 9) - (269, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                    return false;
                }
                catch (IndexError)
                {
#line (271, 9) - (271, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                    return true;
                }
            }
        }
    }

    public static partial class Cpython
    {
        public partial class CpythonListTestsTests
        {
            [Xunit.FactAttribute]
            public void TestAppend()
            {
#line (33, 5) - (33, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
                {
                };
#line (34, 5) - (34, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                a.Append(0);
#line (35, 5) - (35, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                a.Append(1);
#line (36, 5) - (36, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                a.Append(2);
#line (37, 5) - (37, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0, 1, 2 }, a);
            }

            [Xunit.FactAttribute]
            public void TestExtend()
            {
#line (43, 5) - (43, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
                {
                    0
                };
#line (44, 5) - (44, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                a.Extend(new Sharpy.List<int>() { 0, 1 });
#line (45, 5) - (45, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0, 0, 1 }, a);
#line (46, 5) - (46, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                a.Extend(new Sharpy.List<int>() { });
#line (47, 5) - (47, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0, 0, 1 }, a);
#line (48, 5) - (48, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                a.Extend(a);
#line (49, 5) - (49, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0, 0, 1, 0, 0, 1 }, a);
            }

            [Xunit.FactAttribute]
            public void TestInsert()
            {
#line (55, 5) - (55, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
                {
                    0,
                    1,
                    2
                };
#line (56, 5) - (56, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                a.Insert(0, -2);
#line (57, 5) - (57, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                a.Insert(1, -1);
#line (58, 5) - (58, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                a.Insert(2, 0);
#line (59, 5) - (59, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { -2, -1, 0, 0, 1, 2 }, a);
#line (61, 5) - (61, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> b = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (62, 5) - (62, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                b.Insert(-200, -1);
#line (63, 5) - (63, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { -1, 1, 2, 3 }, b);
#line (64, 5) - (64, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                b.Insert(200, 9);
#line (65, 5) - (65, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { -1, 1, 2, 3, 9 }, b);
            }

            [Xunit.FactAttribute]
            public void TestPop()
            {
#line (83, 5) - (83, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
                {
                    -1,
                    0,
                    1
                };
#line (84, 5) - (84, 12) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                a.Pop();
#line (85, 5) - (85, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { -1, 0 }, a);
#line (86, 5) - (86, 13) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                a.Pop(0);
#line (87, 5) - (87, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0 }, a);
#line (88, 5) - (88, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.True(_PopRaises(new Sharpy.List<int>() { 0 }, 5));
#line (89, 5) - (89, 13) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                a.Pop(0);
#line (90, 5) - (90, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(a));
#line (91, 5) - (91, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.True(_PopEmptyRaises(a));
            }

            [Xunit.FactAttribute]
            public void TestRemove()
            {
#line (104, 5) - (104, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
                {
                    0,
                    0,
                    1
                };
#line (105, 5) - (105, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                a.Remove(1);
#line (106, 5) - (106, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0, 0 }, a);
#line (107, 5) - (107, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                a.Remove(0);
#line (108, 5) - (108, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0 }, a);
#line (109, 5) - (109, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                a.Remove(0);
#line (110, 5) - (110, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(a));
#line (112, 5) - (112, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.True(_RemoveRaises(a, 0));
            }

            [Xunit.FactAttribute]
            public void TestIndex()
            {
#line (124, 5) - (124, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> u = new Sharpy.List<int>()
                {
                    0,
                    1
                };
#line (125, 5) - (125, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, u.Index(0));
#line (130, 5) - (130, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.True(_IndexRaises(u, 2));
#line (132, 5) - (132, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
                {
                    -2,
                    -1,
                    0,
                    0,
                    1,
                    2
                };
#line (133, 5) - (133, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(2, a.Count(0));
#line (134, 5) - (134, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(2, a.Index(0));
#line (135, 5) - (135, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(2, a.Index(0, 2));
#line (136, 5) - (136, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(3, a.Index(0, 3));
#line (137, 5) - (137, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(3, a.Index(0, 3, 4));
            }

            [Xunit.FactAttribute]
            public void TestCount()
            {
#line (143, 5) - (143, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
                {
                    0,
                    1,
                    2,
                    0,
                    1,
                    2,
                    0,
                    1,
                    2
                };
#line (144, 5) - (144, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(3, a.Count(0));
#line (145, 5) - (145, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(3, a.Count(1));
#line (146, 5) - (146, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, a.Count(3));
            }

            [Xunit.FactAttribute]
            public void TestReverse()
            {
#line (152, 5) - (152, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> u = new Sharpy.List<int>()
                {
                    -2,
                    -1,
                    0,
                    1,
                    2
                };
#line (153, 5) - (153, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                u.Reverse();
#line (154, 5) - (154, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 2, 1, 0, -1, -2 }, u);
#line (155, 5) - (155, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                u.Reverse();
#line (156, 5) - (156, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { -2, -1, 0, 1, 2 }, u);
            }

            [Xunit.FactAttribute]
            public void TestClear()
            {
#line (162, 5) - (162, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> u = new Sharpy.List<int>()
                {
                    2,
                    3,
                    4
                };
#line (163, 5) - (163, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                u.Clear();
#line (164, 5) - (164, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(u));
#line (166, 5) - (166, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> e = new Sharpy.List<int>()
                {
                };
#line (167, 5) - (167, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                e.Clear();
#line (168, 5) - (168, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(e));
#line (170, 5) - (170, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> v = new Sharpy.List<int>()
                {
                };
#line (171, 5) - (171, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                v.Append(1);
#line (172, 5) - (172, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                v.Clear();
#line (173, 5) - (173, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                v.Append(2);
#line (174, 5) - (174, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 2 }, v);
            }

            [Xunit.FactAttribute]
            public void TestCopy()
            {
#line (180, 5) - (180, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> u = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (181, 5) - (181, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> v = u.Copy();
#line (182, 5) - (182, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 3 }, v);
#line (184, 5) - (184, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> e = new Sharpy.List<int>()
                {
                };
#line (185, 5) - (185, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(e.Copy()));
#line (188, 5) - (188, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> w = new Sharpy.List<int>()
                {
                    1,
                    2
                };
#line (189, 5) - (189, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> x = w.Copy();
#line (190, 5) - (190, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                x.Append(9);
#line (191, 5) - (191, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2 }, w);
#line (192, 5) - (192, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 9 }, x);
            }

            [Xunit.FactAttribute]
            public void TestSort()
            {
#line (198, 5) - (198, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> u = new Sharpy.List<int>()
                {
                    1,
                    0
                };
#line (199, 5) - (199, 13) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                u.Sort();
#line (200, 5) - (200, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0, 1 }, u);
#line (202, 5) - (202, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> v = new Sharpy.List<int>()
                {
                    2,
                    1,
                    0,
                    -1,
                    -2
                };
#line (203, 5) - (203, 13) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                v.Sort();
#line (204, 5) - (204, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { -2, -1, 0, 1, 2 }, v);
            }

            [Xunit.FactAttribute]
            public void TestLen()
            {
#line (210, 5) - (210, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
                {
                };
#line (211, 5) - (211, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(empty));
#line (212, 5) - (212, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(new Sharpy.List<int>() { 0 }));
#line (213, 5) - (213, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(new Sharpy.List<int>() { 0, 1, 2 }));
            }

            [Xunit.FactAttribute]
            public void TestTruth()
            {
#line (219, 5) - (219, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
                {
                };
#line (220, 5) - (220, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(empty));
#line (221, 5) - (221, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(new Sharpy.List<int>() { 42 }));
            }

            [Xunit.FactAttribute]
            public void TestContains()
            {
#line (227, 5) - (227, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> u = new Sharpy.List<int>()
                {
                    0,
                    1,
                    2
                };
#line (228, 5) - (228, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(1, u.Count(0));
#line (229, 5) - (229, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(1, u.Count(1));
#line (230, 5) - (230, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(1, u.Count(2));
#line (231, 5) - (231, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, u.Count(-1));
#line (232, 5) - (232, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, u.Count(3));
            }

            [Xunit.FactAttribute]
            public void TestGetitem()
            {
#line (244, 5) - (244, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> u = new Sharpy.List<int>()
                {
                    0,
                    1,
                    2,
                    3,
                    4
                };
#line (245, 5) - (245, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, u[0]);
#line (246, 5) - (246, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(4, u[4]);
#line (247, 5) - (247, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(4, u[-1]);
#line (248, 5) - (248, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, u[-5]);
#line (249, 5) - (249, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.True(_GetitemRaises(u, 5));
#line (250, 5) - (250, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.True(_GetitemRaises(u, -6));
#line (252, 5) - (252, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
                {
                };
#line (253, 5) - (253, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.True(_GetitemRaises(empty, 0));
#line (254, 5) - (254, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.True(_GetitemRaises(empty, -1));
#line (256, 5) - (256, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
                {
                    10,
                    11
                };
#line (257, 5) - (257, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(10, a[0]);
#line (258, 5) - (258, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(11, a[1]);
#line (259, 5) - (259, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(10, a[-2]);
#line (260, 5) - (260, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(11, a[-1]);
#line (261, 5) - (261, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.True(_GetitemRaises(a, -3));
#line (262, 5) - (262, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.True(_GetitemRaises(a, 3));
            }

            [Xunit.FactAttribute]
            public void TestSetitem()
            {
#line (275, 5) - (275, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
                {
                    0,
                    1
                };
#line (276, 5) - (276, 13) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                a[0] = 0;
#line (277, 5) - (277, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                a[1] = 100;
#line (278, 5) - (278, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0, 100 }, a);
#line (279, 5) - (279, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                a[-1] = 200;
#line (280, 5) - (280, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0, 200 }, a);
#line (281, 5) - (281, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                a[-2] = 100;
#line (282, 5) - (282, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 100, 200 }, a);
#line (283, 5) - (283, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.True(_SetitemRaises(a, -3));
#line (284, 5) - (284, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.True(_SetitemRaises(a, 2));
#line (286, 5) - (286, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
                {
                };
#line (287, 5) - (287, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.True(_SetitemRaises(empty, 0));
#line (288, 5) - (288, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.True(_SetitemRaises(empty, -1));
            }

            [Xunit.FactAttribute]
            public void TestGetslice()
            {
#line (294, 5) - (294, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> u = new Sharpy.List<int>()
                {
                    0,
                    1,
                    2,
                    3,
                    4
                };
#line (295, 5) - (295, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(global::Sharpy.Slice.GetSlice(u, 0, 0, null)));
#line (296, 5) - (296, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1 }, global::Sharpy.Slice.GetSlice(u, 1, 2, null));
#line (297, 5) - (297, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 3 }, global::Sharpy.Slice.GetSlice(u, -2, -1, null));
#line (298, 5) - (298, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(u, global::Sharpy.Slice.GetSlice(u, -1000, 1000, null));
#line (299, 5) - (299, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(global::Sharpy.Slice.GetSlice(u, 1000, -1000, null)));
#line (300, 5) - (300, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(u, global::Sharpy.Slice.GetSlice(u, null, null, null));
#line (302, 5) - (302, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(u, global::Sharpy.Slice.GetSlice(u, null, null, null));
#line (303, 5) - (303, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0, 2, 4 }, global::Sharpy.Slice.GetSlice(u, null, null, 2));
#line (304, 5) - (304, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 3 }, global::Sharpy.Slice.GetSlice(u, 1, null, 2));
#line (305, 5) - (305, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 4, 3, 2, 1, 0 }, global::Sharpy.Slice.GetSlice(u, null, null, -1));
#line (306, 5) - (306, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 4, 2, 0 }, global::Sharpy.Slice.GetSlice(u, null, null, -2));
#line (307, 5) - (307, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 3, 1 }, global::Sharpy.Slice.GetSlice(u, 3, null, -2));
            }

            [Xunit.FactAttribute]
            public void TestAdd()
            {
#line (313, 5) - (313, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> u1 = new Sharpy.List<int>()
                {
                    0
                };
#line (314, 5) - (314, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> u2 = new Sharpy.List<int>()
                {
                    0,
                    1
                };
#line (315, 5) - (315, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
                {
                };
#line (316, 5) - (316, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(u1, u1 + empty);
#line (317, 5) - (317, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(u1, empty + u1);
#line (318, 5) - (318, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(u2, u1 + new Sharpy.List<int>() { 1 });
#line (319, 5) - (319, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { -1, 0 }, new Sharpy.List<int>() { -1 } + u1);
#line (320, 5) - (320, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0, 1, 0, 1 }, u2 + u2);
            }

            [Xunit.FactAttribute]
            public void TestIadd()
            {
#line (326, 5) - (326, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> u = new Sharpy.List<int>()
                {
                    0,
                    1
                };
#line (327, 5) - (327, 12) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                u = u + new Sharpy.List<int>()
                {
                };
#line (328, 5) - (328, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0, 1 }, u);
#line (329, 5) - (329, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                u = u + new Sharpy.List<int>()
                {
                    2,
                    3
                };
#line (330, 5) - (330, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0, 1, 2, 3 }, u);
#line (331, 5) - (331, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                u = u + new Sharpy.List<int>()
                {
                    4,
                    5
                };
#line (332, 5) - (332, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0, 1, 2, 3, 4, 5 }, u);
            }

            [Xunit.FactAttribute]
            public void TestMinmax()
            {
#line (338, 5) - (338, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> u = new Sharpy.List<int>()
                {
                    0,
                    1,
                    2
                };
#line (339, 5) - (339, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Min(u));
#line (340, 5) - (340, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Max(u));
            }
        }
    }
}
