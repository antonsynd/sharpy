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
#line (7, 5) - (7, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Sharpy.List<int> result = new Sharpy.List<int>()
                {
                };
#line (8, 5) - (12, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                foreach (var __loopVar_0 in itertools.Count())
                {
                    var n = __loopVar_0;
#line (9, 9) - (9, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    result.Append(n);
#line (10, 9) - (12, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    if (global::Sharpy.Builtins.Len(result) == 5)
                    {
#line (11, 13) - (11, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                        break;
                    }
                }

#line (12, 5) - (12, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0, 1, 2, 3, 4 }, result);
            }

            [Xunit.FactAttribute]
            public void TestCountCustomStartStartsAtTen()
            {
#line (16, 5) - (16, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Sharpy.List<int> result = new Sharpy.List<int>()
                {
                };
#line (17, 5) - (21, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                foreach (var __loopVar_1 in itertools.Count(10))
                {
                    var n = __loopVar_1;
#line (18, 9) - (18, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    result.Append(n);
#line (19, 9) - (21, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    if (global::Sharpy.Builtins.Len(result) == 3)
                    {
#line (20, 13) - (20, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                        break;
                    }
                }

#line (21, 5) - (21, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 10, 11, 12 }, result);
            }

            [Xunit.FactAttribute]
            public void TestCountStepTwoYieldsEvenNumbers()
            {
#line (25, 5) - (25, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Sharpy.List<int> result = new Sharpy.List<int>()
                {
                };
#line (26, 5) - (30, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                foreach (var __loopVar_2 in itertools.Count(0, 2))
                {
                    var n = __loopVar_2;
#line (27, 9) - (27, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    result.Append(n);
#line (28, 9) - (30, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    if (global::Sharpy.Builtins.Len(result) == 4)
                    {
#line (29, 13) - (29, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                        break;
                    }
                }

#line (30, 5) - (30, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 0, 2, 4, 6 }, result);
            }

            [Xunit.FactAttribute]
            public void TestCountNegativeStepCountsDown()
            {
#line (34, 5) - (34, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Sharpy.List<int> result = new Sharpy.List<int>()
                {
                };
#line (35, 5) - (39, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                foreach (var __loopVar_3 in itertools.Count(10, -1))
                {
                    var n = __loopVar_3;
#line (36, 9) - (36, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    result.Append(n);
#line (37, 9) - (39, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    if (global::Sharpy.Builtins.Len(result) == 3)
                    {
#line (38, 13) - (38, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                        break;
                    }
                }

#line (39, 5) - (39, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 10, 9, 8 }, result);
            }

            [Xunit.FactAttribute]
            public void TestCycleMultipleElementsCyclesCorrectly()
            {
#line (45, 5) - (45, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Sharpy.List<int> result = new Sharpy.List<int>()
                {
                };
#line (46, 5) - (50, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                foreach (var __loopVar_4 in itertools.Cycle(new Sharpy.List<int>() { 1, 2, 3 }))
                {
                    var n = __loopVar_4;
#line (47, 9) - (47, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    result.Append(n);
#line (48, 9) - (50, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    if (global::Sharpy.Builtins.Len(result) == 7)
                    {
#line (49, 13) - (49, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                        break;
                    }
                }

#line (50, 5) - (50, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 1, 2, 3, 1, 2, 3, 1 }, result);
            }

            [Xunit.FactAttribute]
            public void TestCycleSingleElementRepeatsSingleElement()
            {
#line (54, 5) - (54, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Sharpy.List<int> result = new Sharpy.List<int>()
                {
                };
#line (55, 5) - (59, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                foreach (var __loopVar_5 in itertools.Cycle(new Sharpy.List<int>() { 42 }))
                {
                    var n = __loopVar_5;
#line (56, 9) - (56, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    result.Append(n);
#line (57, 9) - (59, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    if (global::Sharpy.Builtins.Len(result) == 5)
                    {
#line (58, 13) - (58, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                        break;
                    }
                }

#line (59, 5) - (59, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 42, 42, 42, 42, 42 }, result);
            }

            [Xunit.FactAttribute]
            public void TestCycleEmptyIterableProducesNoElements()
            {
#line (63, 5) - (63, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Sharpy.List<int> empty = new Sharpy.List<int>()
                {
                };
#line (64, 5) - (64, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Sharpy.List<int> result = new Sharpy.List<int>()
                {
                };
#line (65, 5) - (69, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                foreach (var __loopVar_6 in itertools.Cycle(empty))
                {
                    var n = __loopVar_6;
#line (66, 9) - (66, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    result.Append(n);
#line (67, 9) - (69, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    if (global::Sharpy.Builtins.Len(result) == 10)
                    {
#line (68, 13) - (68, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                        break;
                    }
                }

#line (69, 5) - (69, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
            }

            [Xunit.FactAttribute]
            public void TestRepeatInfiniteModeRepeatsElementIndefinitely()
            {
#line (75, 5) - (75, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Sharpy.List<string> result = new Sharpy.List<string>()
                {
                };
#line (76, 5) - (80, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                foreach (var __loopVar_7 in itertools.Repeat("hello"))
                {
                    var s = __loopVar_7;
#line (77, 9) - (77, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    result.Append(s);
#line (78, 9) - (80, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                    if (global::Sharpy.Builtins.Len(result) == 3)
                    {
#line (79, 13) - (79, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                        break;
                    }
                }

#line (80, 5) - (80, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "hello", "hello", "hello" }, result);
            }

            [Xunit.FactAttribute]
            public void TestRepeatCountedModeRepeatsExactNumberOfTimes()
            {
#line (84, 5) - (84, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Repeat(7, 3));
#line (85, 5) - (85, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<int>() { 7, 7, 7 }, result);
            }

            [Xunit.FactAttribute]
            public void TestRepeatCountZeroProducesNoElements()
            {
#line (89, 5) - (89, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Sharpy.List<int> result = new global::Sharpy.List<int>(itertools.Repeat(99, 0));
#line (90, 5) - (90, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/itertools/itertools_infinite_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
            }
        }
    }
}
