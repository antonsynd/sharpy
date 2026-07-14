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
#line (236, 5) - (241, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                try
                {
#line (237, 9) - (237, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                    return a[i] < 0;
                }
                catch (IndexError)
                {
#line (239, 9) - (239, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                    return true;
                }
            }

            internal static bool _SetitemRaises(Sharpy.List<int> a, int i)
            {
#line (266, 5) - (272, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                try
                {
#line (267, 9) - (267, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                    a[i] = 200;
#line (268, 9) - (268, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                    return false;
                }
                catch (IndexError)
                {
#line (270, 9) - (270, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
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
#line (128, 5) - (128, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(1, u.Index(1));
#line (129, 5) - (129, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.True(_IndexRaises(u, 2));
#line (131, 5) - (131, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
                {
                    -2,
                    -1,
                    0,
                    0,
                    1,
                    2
                };
#line (132, 5) - (132, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(2, a.Count(0));
#line (133, 5) - (133, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(2, a.Index(0));
#line (134, 5) - (134, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(2, a.Index(0, 2));
#line (135, 5) - (135, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(3, a.Index(0, 3));
#line (136, 5) - (136, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(3, a.Index(0, 3, 4));
            }

            [Xunit.FactAttribute]
            public void TestCount()
            {
#line (142, 5) - (142, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
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
#line (143, 5) - (143, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(3, a.Count(0));
#line (144, 5) - (144, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(3, a.Count(1));
#line (145, 5) - (145, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, a.Count(3));
            }

            [Xunit.FactAttribute]
            public void TestReverse()
            {
#line (151, 5) - (151, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> u = new Sharpy.List<int>()
                {
                    -2,
                    -1,
                    0,
                    1,
                    2
                };
#line (152, 5) - (152, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                u.Reverse();
#line (153, 5) - (153, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 2, 1, 0, -1, -2 }, u);
#line (154, 5) - (154, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                u.Reverse();
#line (155, 5) - (155, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { -2, -1, 0, 1, 2 }, u);
            }

            [Xunit.FactAttribute]
            public void TestClear()
            {
#line (161, 5) - (161, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> u = new Sharpy.List<int>()
                {
                    2,
                    3,
                    4
                };
#line (162, 5) - (162, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                u.Clear();
#line (163, 5) - (163, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(u));
#line (165, 5) - (165, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> e = new Sharpy.List<int>()
                {
                };
#line (166, 5) - (166, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                e.Clear();
#line (167, 5) - (167, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(e));
#line (169, 5) - (169, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> v = new Sharpy.List<int>()
                {
                };
#line (170, 5) - (170, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                v.Append(1);
#line (171, 5) - (171, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                v.Clear();
#line (172, 5) - (172, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                v.Append(2);
#line (173, 5) - (173, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 2 }, v);
            }

            [Xunit.FactAttribute]
            public void TestCopy()
            {
#line (179, 5) - (179, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> u = new Sharpy.List<int>()
                {
                    1,
                    2,
                    3
                };
#line (180, 5) - (180, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> v = u.Copy();
#line (181, 5) - (181, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 3 }, v);
#line (183, 5) - (183, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> e = new Sharpy.List<int>()
                {
                };
#line (184, 5) - (184, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(e.Copy()));
#line (187, 5) - (187, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> w = new Sharpy.List<int>()
                {
                    1,
                    2
                };
#line (188, 5) - (188, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> x = w.Copy();
#line (189, 5) - (189, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                x.Append(9);
#line (190, 5) - (190, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2 }, w);
#line (191, 5) - (191, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 9 }, x);
            }

            [Xunit.FactAttribute]
            public void TestSort()
            {
#line (197, 5) - (197, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> u = new Sharpy.List<int>()
                {
                    1,
                    0
                };
#line (198, 5) - (198, 13) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                u.Sort();
#line (199, 5) - (199, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0, 1 }, u);
#line (201, 5) - (201, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> v = new Sharpy.List<int>()
                {
                    2,
                    1,
                    0,
                    -1,
                    -2
                };
#line (202, 5) - (202, 13) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                v.Sort();
#line (203, 5) - (203, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { -2, -1, 0, 1, 2 }, v);
            }

            [Xunit.FactAttribute]
            public void TestLen()
            {
#line (209, 5) - (209, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
                {
                };
#line (210, 5) - (210, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(empty));
#line (211, 5) - (211, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(new Sharpy.List<int>() { 0 }));
#line (212, 5) - (212, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(new Sharpy.List<int>() { 0, 1, 2 }));
            }

            [Xunit.FactAttribute]
            public void TestTruth()
            {
#line (218, 5) - (218, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
                {
                };
#line (219, 5) - (219, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(empty));
#line (220, 5) - (220, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(new Sharpy.List<int>() { 42 }));
            }

            [Xunit.FactAttribute]
            public void TestContains()
            {
#line (226, 5) - (226, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> u = new Sharpy.List<int>()
                {
                    0,
                    1,
                    2
                };
#line (227, 5) - (227, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(1, u.Count(0));
#line (228, 5) - (228, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(1, u.Count(1));
#line (229, 5) - (229, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(1, u.Count(2));
#line (230, 5) - (230, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, u.Count(-1));
#line (231, 5) - (231, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, u.Count(3));
            }

            [Xunit.FactAttribute]
            public void TestGetitem()
            {
#line (243, 5) - (243, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> u = new Sharpy.List<int>()
                {
                    0,
                    1,
                    2,
                    3,
                    4
                };
#line (244, 5) - (244, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, u.GetItemUnchecked(0));
#line (245, 5) - (245, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(4, u.GetItemUnchecked(4));
#line (246, 5) - (246, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(4, u[-1]);
#line (247, 5) - (247, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, u[-5]);
#line (248, 5) - (248, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.True(_GetitemRaises(u, 5));
#line (249, 5) - (249, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.True(_GetitemRaises(u, -6));
#line (251, 5) - (251, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
                {
                };
#line (252, 5) - (252, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.True(_GetitemRaises(empty, 0));
#line (253, 5) - (253, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.True(_GetitemRaises(empty, -1));
#line (255, 5) - (255, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
                {
                    10,
                    11
                };
#line (256, 5) - (256, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(10, a.GetItemUnchecked(0));
#line (257, 5) - (257, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(11, a.GetItemUnchecked(1));
#line (258, 5) - (258, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(10, a[-2]);
#line (259, 5) - (259, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(11, a[-1]);
#line (260, 5) - (260, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.True(_GetitemRaises(a, -3));
#line (261, 5) - (261, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.True(_GetitemRaises(a, 3));
            }

            [Xunit.FactAttribute]
            public void TestSetitem()
            {
#line (274, 5) - (274, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> a = new Sharpy.List<int>()
                {
                    0,
                    1
                };
#line (275, 5) - (275, 13) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                a[0] = 0;
#line (276, 5) - (276, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                a[1] = 100;
#line (277, 5) - (277, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0, 100 }, a);
#line (278, 5) - (278, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                a[-1] = 200;
#line (279, 5) - (279, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0, 200 }, a);
#line (280, 5) - (280, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                a[-2] = 100;
#line (281, 5) - (281, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 100, 200 }, a);
#line (282, 5) - (282, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.True(_SetitemRaises(a, -3));
#line (283, 5) - (283, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.True(_SetitemRaises(a, 2));
#line (285, 5) - (285, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
                {
                };
#line (286, 5) - (286, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.True(_SetitemRaises(empty, 0));
#line (287, 5) - (287, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.True(_SetitemRaises(empty, -1));
            }

            [Xunit.FactAttribute]
            public void TestGetslice()
            {
#line (293, 5) - (293, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> u = new Sharpy.List<int>()
                {
                    0,
                    1,
                    2,
                    3,
                    4
                };
#line (294, 5) - (294, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(global::Sharpy.Slice.GetSlice(u, 0, 0, null)));
#line (295, 5) - (295, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1 }, global::Sharpy.Slice.GetSlice(u, 1, 2, null));
#line (296, 5) - (296, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 3 }, global::Sharpy.Slice.GetSlice(u, -2, -1, null));
#line (297, 5) - (297, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(u, global::Sharpy.Slice.GetSlice(u, -1000, 1000, null));
#line (298, 5) - (298, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(global::Sharpy.Slice.GetSlice(u, 1000, -1000, null)));
#line (299, 5) - (299, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(u, global::Sharpy.Slice.GetSlice(u, null, null, null));
#line (301, 5) - (301, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(u, global::Sharpy.Slice.GetSlice(u, null, null, null));
#line (302, 5) - (302, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0, 2, 4 }, global::Sharpy.Slice.GetSlice(u, null, null, 2));
#line (303, 5) - (303, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 3 }, global::Sharpy.Slice.GetSlice(u, 1, null, 2));
#line (304, 5) - (304, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 4, 3, 2, 1, 0 }, global::Sharpy.Slice.GetSlice(u, null, null, -1));
#line (305, 5) - (305, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 4, 2, 0 }, global::Sharpy.Slice.GetSlice(u, null, null, -2));
#line (306, 5) - (306, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 3, 1 }, global::Sharpy.Slice.GetSlice(u, 3, null, -2));
            }

            [Xunit.FactAttribute]
            public void TestAdd()
            {
#line (312, 5) - (312, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> u1 = new Sharpy.List<int>()
                {
                    0
                };
#line (313, 5) - (313, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> u2 = new Sharpy.List<int>()
                {
                    0,
                    1
                };
#line (314, 5) - (314, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
                {
                };
#line (315, 5) - (315, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(u1, u1 + empty);
#line (316, 5) - (316, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(u1, empty + u1);
#line (317, 5) - (317, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(u2, u1 + new Sharpy.List<int>() { 1 });
#line (318, 5) - (318, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { -1, 0 }, new Sharpy.List<int>() { -1 } + u1);
#line (319, 5) - (319, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0, 1, 0, 1 }, u2 + u2);
            }

            [Xunit.FactAttribute]
            public void TestIadd()
            {
#line (325, 5) - (325, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> u = new Sharpy.List<int>()
                {
                    0,
                    1
                };
#line (326, 5) - (326, 12) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                u = u + new Sharpy.List<int>()
                {
                };
#line (327, 5) - (327, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0, 1 }, u);
#line (328, 5) - (328, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                u = u + new Sharpy.List<int>()
                {
                    2,
                    3
                };
#line (329, 5) - (329, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0, 1, 2, 3 }, u);
#line (330, 5) - (330, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                u = u + new Sharpy.List<int>()
                {
                    4,
                    5
                };
#line (331, 5) - (331, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0, 1, 2, 3, 4, 5 }, u);
            }

            [Xunit.FactAttribute]
            public void TestMinmax()
            {
#line (337, 5) - (337, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Sharpy.List<int> u = new Sharpy.List<int>()
                {
                    0,
                    1,
                    2
                };
#line (338, 5) - (338, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Min(u));
#line (339, 5) - (339, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_list_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Max(u));
            }
        }
    }
}
