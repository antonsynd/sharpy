// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using np = global::Sharpy.Numpy;
using Xunit;

namespace Sharpy.Stdlib.Tests.Spy.Compiler.OperandOrderTests
{
    [global::Sharpy.SharpyModule("compiler.operand_order_tests")]
    public static partial class OperandOrderTestsModule
    {
        public static int Take(Sharpy.List<int> xs)
        {
#line (19, 5) - (19, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/compiler/operand_order_tests.spy"
            return xs.Pop(0);
#line hidden
        }

        public static int Take0(Sharpy.List<int> xs)
        {
#line (23, 5) - (23, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/compiler/operand_order_tests.spy"
            xs.Pop(0);
#line (24, 5) - (24, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/compiler/operand_order_tests.spy"
            return 0;
#line hidden
        }

        public static double Takef(Sharpy.List<double> xs)
        {
#line (28, 5) - (28, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/compiler/operand_order_tests.spy"
            return xs.Pop(0);
#line hidden
        }

        public static string TakeStr(Sharpy.List<int> state, string s)
        {
#line (32, 5) - (32, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/compiler/operand_order_tests.spy"
            state.Pop(0);
#line (33, 5) - (33, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/compiler/operand_order_tests.spy"
            return s;
#line hidden
        }
    }

    public partial class OperandOrderTestsModuleTests
    {
        [Xunit.FactAttribute]
        public void TestAssertEqualOperandOrder1853()
        {
#line (40, 5) - (40, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/compiler/operand_order_tests.spy"
            Sharpy.List<int> xs = new Sharpy.List<int>()
#line hidden
            {
                2,
                9,
                9
            };
            var __arg_3 = global::Sharpy.Stdlib.Tests.Spy.Compiler.OperandOrderTests.OperandOrderTestsModule.Take(xs);
            Sharpy.List<int> __src_1 = xs;
            var __comp_0 = new Sharpy.List<int>(((global::Sharpy.ISized)__src_1).Count);
            foreach (var __loopVar_2 in __src_1)
            {
                var v = __loopVar_2;
                __comp_0.Add(v);
            }

#line (41, 5) - (41, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/compiler/operand_order_tests.spy"
            Xunit.Assert.Equal(global::Sharpy.Builtins.Len(__comp_0), __arg_3);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAssertNotEqualOperandOrder1853()
        {
#line (47, 5) - (47, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/compiler/operand_order_tests.spy"
            Sharpy.List<int> xs = new Sharpy.List<int>()
#line hidden
            {
                3,
                9,
                9
            };
            var __arg_7 = global::Sharpy.Stdlib.Tests.Spy.Compiler.OperandOrderTests.OperandOrderTestsModule.Take(xs);
            Sharpy.List<int> __src_5 = xs;
            var __comp_4 = new Sharpy.List<int>(((global::Sharpy.ISized)__src_5).Count);
            foreach (var __loopVar_6 in __src_5)
            {
                var v = __loopVar_6;
                __comp_4.Add(v);
            }

#line (48, 5) - (48, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/compiler/operand_order_tests.spy"
            Xunit.Assert.NotEqual(global::Sharpy.Builtins.Len(__comp_4), __arg_7);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAssertNotInOperandOrder1853()
        {
#line (54, 5) - (54, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/compiler/operand_order_tests.spy"
            Sharpy.List<int> xs = new Sharpy.List<int>()
#line hidden
            {
                9,
                1,
                2
            };
            var __arg_11 = global::Sharpy.Stdlib.Tests.Spy.Compiler.OperandOrderTests.OperandOrderTestsModule.Take(xs);
            Sharpy.List<int> __src_9 = xs;
            var __comp_8 = new Sharpy.List<int>(((global::Sharpy.ISized)__src_9).Count);
            foreach (var __loopVar_10 in __src_9)
            {
                var v = __loopVar_10;
                __comp_8.Add(v);
            }

#line (55, 5) - (55, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/compiler/operand_order_tests.spy"
            Xunit.Assert.DoesNotContain(__arg_11, __comp_8);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAssertApproxOperandOrder1853()
        {
#line (62, 5) - (62, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/compiler/operand_order_tests.spy"
            Sharpy.List<double> xs = new Sharpy.List<double>()
#line hidden
            {
                2.0d,
                9.0d,
                9.0d
            };
            var __arg_15 = global::Sharpy.Stdlib.Tests.Spy.Compiler.OperandOrderTests.OperandOrderTestsModule.Takef(xs);
            Sharpy.List<double> __src_13 = xs;
            var __comp_12 = new Sharpy.List<double>(((global::Sharpy.ISized)__src_13).Count);
            foreach (var __loopVar_14 in __src_13)
            {
                var v = __loopVar_14;
                __comp_12.Add(v);
            }

#line (63, 5) - (63, 60) 12 "src/Sharpy.Stdlib.Tests/Spy/compiler/operand_order_tests.spy"
            Xunit.Assert.Equal(global::Sharpy.Builtins.Len(__comp_12) * 1.0d, __arg_15, 7);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAssertAlmostEqualOperandOrder1853()
        {
#line (70, 5) - (70, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/compiler/operand_order_tests.spy"
            Sharpy.List<double> xs = new Sharpy.List<double>()
#line hidden
            {
                2.0d,
                9.0d,
                9.0d
            };
            var __arg_19 = global::Sharpy.Stdlib.Tests.Spy.Compiler.OperandOrderTests.OperandOrderTestsModule.Takef(xs);
            Sharpy.List<double> __src_17 = xs;
            var __comp_16 = new Sharpy.List<double>(((global::Sharpy.ISized)__src_17).Count);
            foreach (var __loopVar_18 in __src_17)
            {
                var v = __loopVar_18;
                __comp_16.Add(v);
            }

#line (71, 5) - (71, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/compiler/operand_order_tests.spy"
            Xunit.Assert.Equal(global::Sharpy.Builtins.Len(__comp_16) * 1.0d, __arg_19, 7);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAssertGreaterOperandOrder1853()
        {
#line (78, 5) - (78, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/compiler/operand_order_tests.spy"
            Sharpy.List<int> xs = new Sharpy.List<int>()
#line hidden
            {
                3,
                9,
                9
            };
            var __arg_23 = global::Sharpy.Stdlib.Tests.Spy.Compiler.OperandOrderTests.OperandOrderTestsModule.Take(xs);
            Sharpy.List<int> __src_21 = xs;
            var __comp_20 = new Sharpy.List<int>(((global::Sharpy.ISized)__src_21).Count);
            foreach (var __loopVar_22 in __src_21)
            {
                var v = __loopVar_22;
                __comp_20.Add(v);
            }

#line (79, 5) - (79, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/compiler/operand_order_tests.spy"
            Xunit.Assert.True(__arg_23 > global::Sharpy.Builtins.Len(__comp_20), "Expected first argument > second argument");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAssertHelperNotInOperandOrder1853()
        {
#line (86, 5) - (86, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/compiler/operand_order_tests.spy"
            Sharpy.List<int> xs = new Sharpy.List<int>()
#line hidden
            {
                9,
                1,
                2
            };
            var __arg_27 = global::Sharpy.Stdlib.Tests.Spy.Compiler.OperandOrderTests.OperandOrderTestsModule.Take(xs);
            Sharpy.List<int> __src_25 = xs;
            var __comp_24 = new Sharpy.List<int>(((global::Sharpy.ISized)__src_25).Count);
            foreach (var __loopVar_26 in __src_25)
            {
                var v = __loopVar_26;
                __comp_24.Add(v);
            }

#line (87, 5) - (87, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/compiler/operand_order_tests.spy"
            Xunit.Assert.DoesNotContain(__arg_27, __comp_24);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAssertRegexOperandOrder1853()
        {
#line (95, 5) - (95, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/compiler/operand_order_tests.spy"
            Sharpy.List<int> state = new Sharpy.List<int>()
#line hidden
            {
                9,
                2,
                3
            };
            var __arg_31 = global::Sharpy.Stdlib.Tests.Spy.Compiler.OperandOrderTests.OperandOrderTestsModule.TakeStr(state, "23");
            Sharpy.List<int> __src_29 = state;
            var __comp_28 = new Sharpy.List<string>(((global::Sharpy.ISized)__src_29).Count);
            foreach (var __loopVar_30 in __src_29)
            {
                var v = __loopVar_30;
                __comp_28.Add(global::Sharpy.Builtins.Str(v));
            }

#line (96, 5) - (96, 74) 12 "src/Sharpy.Stdlib.Tests/Spy/compiler/operand_order_tests.spy"
            Xunit.Assert.Matches(global::Sharpy.StringExtensions.Join("", __comp_28), __arg_31);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestMultiaxisOperandOrder1853()
        {
#line (104, 5) - (104, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/compiler/operand_order_tests.spy"
            Sharpy.List<int> xs = new Sharpy.List<int>()
#line hidden
            {
                1,
                2,
                3
            };
#line (105, 5) - (105, 59) 12 "src/Sharpy.Stdlib.Tests/Spy/compiler/operand_order_tests.spy"
            var a = np.Array(new Sharpy.List<Sharpy.List<double>>() { new Sharpy.List<double>() { 10.0d, 20.0d, 30.0d }, new Sharpy.List<double>() { 40.0d, 50.0d, 60.0d } });
#line hidden
            var __arg_35 = global::Sharpy.Stdlib.Tests.Spy.Compiler.OperandOrderTests.OperandOrderTestsModule.Take0(xs);
            Sharpy.List<int> __src_33 = xs;
            var __comp_32 = new Sharpy.List<int>(((global::Sharpy.ISized)__src_33).Count);
            foreach (var __loopVar_34 in __src_33)
            {
                var v = __loopVar_34;
                __comp_32.Add(v);
            }

#line (106, 5) - (106, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/compiler/operand_order_tests.spy"
            var r = a.Slice(global::Sharpy.SliceSpec.At(__arg_35), new global::Sharpy.SliceSpec((int?)global::Sharpy.Builtins.Len(__comp_32), null));
#line (107, 5) - (107, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/compiler/operand_order_tests.spy"
            Xunit.Assert.Equal(1, r.Size);
#line (108, 5) - (108, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/compiler/operand_order_tests.spy"
            Xunit.Assert.Equal(30.0d, r[0]);
#line hidden
        }
    }
}
#line default
