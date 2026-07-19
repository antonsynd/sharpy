// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using itertools = global::Sharpy.Itertools;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Itertools.ItertoolsInfiniteTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Itertools
    {
        [global::Sharpy.SharpyModule("itertools.itertools_infinite_tests")]
        public static partial class ItertoolsInfiniteTests
        {
        }
    }

    public static partial class Itertools
    {
        public partial class ItertoolsInfiniteTestsTests
        {
            [Xunit.FactAttribute]
            public void TestCountDefaultStartStepStartsAtZeroStepOne()
            {
#line (7, 5) - (7, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Sharpy.List<int> result = new Sharpy.List<int>()
#line hidden
                {
                };
#line (8, 5) - (12, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                foreach (var __loopVar_0 in itertools.Count())
#line hidden
                {
                    var n = __loopVar_0;
#line (9, 9) - (9, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    result.Append(n);
#line (10, 9) - (12, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    if (global::Sharpy.Builtins.Len(result) == 5)
#line hidden
                    {
#line (11, 13) - (11, 19) 24 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                        break;
#line hidden
                    }
                }

#line (12, 5) - (12, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0, 1, 2, 3, 4 }, result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCountCustomStartStartsAtTen()
            {
#line (16, 5) - (16, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Sharpy.List<int> result = new Sharpy.List<int>()
#line hidden
                {
                };
#line (17, 5) - (21, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                foreach (var __loopVar_1 in itertools.Count(10))
#line hidden
                {
                    var n = __loopVar_1;
#line (18, 9) - (18, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    result.Append(n);
#line (19, 9) - (21, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    if (global::Sharpy.Builtins.Len(result) == 3)
#line hidden
                    {
#line (20, 13) - (20, 19) 24 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                        break;
#line hidden
                    }
                }

#line (21, 5) - (21, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 10, 11, 12 }, result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCountStepTwoYieldsEvenNumbers()
            {
#line (25, 5) - (25, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Sharpy.List<int> result = new Sharpy.List<int>()
#line hidden
                {
                };
#line (26, 5) - (30, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                foreach (var __loopVar_2 in itertools.Count(0, 2))
#line hidden
                {
                    var n = __loopVar_2;
#line (27, 9) - (27, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    result.Append(n);
#line (28, 9) - (30, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    if (global::Sharpy.Builtins.Len(result) == 4)
#line hidden
                    {
#line (29, 13) - (29, 19) 24 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                        break;
#line hidden
                    }
                }

#line (30, 5) - (30, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0, 2, 4, 6 }, result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCountNegativeStepCountsDown()
            {
#line (34, 5) - (34, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Sharpy.List<int> result = new Sharpy.List<int>()
#line hidden
                {
                };
#line (35, 5) - (39, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                foreach (var __loopVar_3 in itertools.Count(10, -1))
#line hidden
                {
                    var n = __loopVar_3;
#line (36, 9) - (36, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    result.Append(n);
#line (37, 9) - (39, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    if (global::Sharpy.Builtins.Len(result) == 3)
#line hidden
                    {
#line (38, 13) - (38, 19) 24 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                        break;
#line hidden
                    }
                }

#line (39, 5) - (39, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 10, 9, 8 }, result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCycleMultipleElementsCyclesCorrectly()
            {
#line (45, 5) - (45, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Sharpy.List<int> result = new Sharpy.List<int>()
#line hidden
                {
                };
#line (46, 5) - (50, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                foreach (var __loopVar_4 in itertools.Cycle(new Sharpy.List<int>() { 1, 2, 3 }))
#line hidden
                {
                    var n = __loopVar_4;
#line (47, 9) - (47, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    result.Append(n);
#line (48, 9) - (50, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    if (global::Sharpy.Builtins.Len(result) == 7)
#line hidden
                    {
#line (49, 13) - (49, 19) 24 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                        break;
#line hidden
                    }
                }

#line (50, 5) - (50, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 3, 1, 2, 3, 1 }, result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCycleSingleElementRepeatsSingleElement()
            {
#line (54, 5) - (54, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Sharpy.List<int> result = new Sharpy.List<int>()
#line hidden
                {
                };
#line (55, 5) - (59, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                foreach (var __loopVar_5 in itertools.Cycle(new Sharpy.List<int>() { 42 }))
#line hidden
                {
                    var n = __loopVar_5;
#line (56, 9) - (56, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    result.Append(n);
#line (57, 9) - (59, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    if (global::Sharpy.Builtins.Len(result) == 5)
#line hidden
                    {
#line (58, 13) - (58, 19) 24 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                        break;
#line hidden
                    }
                }

#line (59, 5) - (59, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 42, 42, 42, 42, 42 }, result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCycleEmptyIterableProducesNoElements()
            {
#line (63, 5) - (63, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
#line hidden
                {
                };
#line (64, 5) - (64, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Sharpy.List<int> result = new Sharpy.List<int>()
#line hidden
                {
                };
#line (65, 5) - (69, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                foreach (var __loopVar_6 in itertools.Cycle(empty))
#line hidden
                {
                    var n = __loopVar_6;
#line (66, 9) - (66, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    result.Append(n);
#line (67, 9) - (69, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    if (global::Sharpy.Builtins.Len(result) == 10)
#line hidden
                    {
#line (68, 13) - (68, 19) 24 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                        break;
#line hidden
                    }
                }

#line (69, 5) - (69, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRepeatInfiniteModeRepeatsElementIndefinitely()
            {
#line (75, 5) - (75, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Sharpy.List<string> result = new Sharpy.List<string>()
#line hidden
                {
                };
#line (76, 5) - (80, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                foreach (var __loopVar_7 in itertools.Repeat("hello"))
#line hidden
                {
                    var s = __loopVar_7;
#line (77, 9) - (77, 25) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    result.Append(s);
#line (78, 9) - (80, 1) 20 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    if (global::Sharpy.Builtins.Len(result) == 3)
#line hidden
                    {
#line (79, 13) - (79, 19) 24 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                        break;
#line hidden
                    }
                }

#line (80, 5) - (80, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "hello", "hello", "hello" }, result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRepeatCountedModeRepeatsExactNumberOfTimes()
            {
#line (84, 5) - (84, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Repeat(7, 3));
#line (85, 5) - (85, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 7, 7, 7 }, result);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRepeatCountZeroProducesNoElements()
            {
#line (89, 5) - (89, 55) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Repeat(99, 0));
#line (90, 5) - (90, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
#line hidden
            }
        }
    }
}
#line default
