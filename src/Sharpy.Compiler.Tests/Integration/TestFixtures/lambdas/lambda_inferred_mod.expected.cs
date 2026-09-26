// Snapshot: Inferred lambda parameter types reach the floored remainder/floor-division lowerings (#1161)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace LambdaInferredMod
{
    public static partial class LambdaInferredModModule
    {
        public static Sharpy.List<T> ApplyAll<T>(global::System.Func<T, bool> f, Sharpy.List<T> items)
        {
#line (6, 5) - (6, 23) 12 "lambda_inferred_mod.spy"
            Sharpy.List<T> @out = new Sharpy.List<T>()
#line hidden
            {
            };
#line (7, 5) - (9, 29) 12 "lambda_inferred_mod.spy"
            foreach (var __loopVar_0 in items)
#line hidden
            {
                var item = __loopVar_0;
#line (8, 9) - (9, 29) 16 "lambda_inferred_mod.spy"
                if (f(item))
#line hidden
                {
#line (9, 13) - (9, 29) 20 "lambda_inferred_mod.spy"
                    @out.Append(item);
#line hidden
                }
            }

#line (10, 5) - (10, 16) 12 "lambda_inferred_mod.spy"
            return @out;
#line hidden
        }

        public static void Main()
        {
#line (13, 5) - (13, 49) 12 "lambda_inferred_mod.spy"
            Sharpy.List<int> nums = new Sharpy.List<int>()
#line hidden
            {
                -7,
                -6,
                -5,
                -4,
                0,
                5,
                7
            };
#line (16, 5) - (16, 52) 12 "lambda_inferred_mod.spy"
            global::Sharpy.Builtins.Print(new Sharpy.List<int>(global::Sharpy.Builtins.Filter<int>(x => global::Sharpy.Builtins.FloorMod(x, 3) == 1, nums)));
#line (19, 5) - (19, 44) 12 "lambda_inferred_mod.spy"
            global::Sharpy.Builtins.Print(new Sharpy.List<int>(global::Sharpy.Builtins.Map(x => global::Sharpy.Builtins.FloorMod(x, 3), nums)));
#line (22, 5) - (22, 45) 12 "lambda_inferred_mod.spy"
            global::Sharpy.Builtins.Print(new Sharpy.List<int>(global::Sharpy.Builtins.Map(x => global::Sharpy.Builtins.FloorDiv(x, 3), nums)));
#line (25, 5) - (25, 53) 12 "lambda_inferred_mod.spy"
            global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Sorted(new Sharpy.List<int>() { -7, -6, -5 }, key: x => global::Sharpy.Builtins.FloorMod(x, 3)));
#line (28, 5) - (28, 67) 12 "lambda_inferred_mod.spy"
            global::Sharpy.Builtins.Print(new Sharpy.List<int>(global::Sharpy.Builtins.Filter<int>(x => ((global::System.Func<int, int>)(y => global::Sharpy.Builtins.FloorMod(y, 3)))(x) == 1, nums)));
#line (31, 5) - (31, 49) 12 "lambda_inferred_mod.spy"
            global::Sharpy.Builtins.Print(global::LambdaInferredMod.LambdaInferredModModule.ApplyAll<int>(v => global::Sharpy.Builtins.FloorMod(v, 3) == 1, nums));
#line (34, 5) - (34, 57) 12 "lambda_inferred_mod.spy"
            global::Sharpy.Builtins.Print(new Sharpy.List<int>(global::Sharpy.Builtins.Filter<int>((int x) => global::Sharpy.Builtins.FloorMod(x, 3) == 1, nums)));
#line hidden
        }
    }
}
#line default
