// Snapshot: Inferred lambda parameter types reach the floored remainder/floor-division lowerings (#1161)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class LambdaInferredMod
{
    public static Sharpy.List<T> ApplyAll<T>(global::System.Func<T, bool> f, Sharpy.List<T> items)
    {
#line (6, 5) - (6, 23) 8 "lambda_inferred_mod.spy"
        Sharpy.List<T> @out = new Sharpy.List<T>()
#line hidden
        {
        };
#line (7, 5) - (10, 1) 8 "lambda_inferred_mod.spy"
        foreach (var __loopVar_0 in items)
#line hidden
        {
            var item = __loopVar_0;
#line (8, 9) - (10, 1) 12 "lambda_inferred_mod.spy"
            if (f(item))
#line hidden
            {
#line (9, 13) - (9, 29) 16 "lambda_inferred_mod.spy"
                @out.Append(item);
#line hidden
            }
        }

#line (10, 5) - (10, 16) 8 "lambda_inferred_mod.spy"
        return @out;
#line hidden
    }

    public static void Main()
    {
#line (13, 5) - (13, 49) 8 "lambda_inferred_mod.spy"
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
#line (16, 5) - (16, 52) 8 "lambda_inferred_mod.spy"
        global::Sharpy.Builtins.Print(new Sharpy.List<int>(global::Sharpy.Builtins.Filter(x => global::Sharpy.Builtins.FloorMod(x, 3) == 1, nums)));
#line (19, 5) - (19, 44) 8 "lambda_inferred_mod.spy"
        global::Sharpy.Builtins.Print(new Sharpy.List<int>(global::Sharpy.Builtins.Map(x => global::Sharpy.Builtins.FloorMod(x, 3), nums)));
#line (22, 5) - (22, 45) 8 "lambda_inferred_mod.spy"
        global::Sharpy.Builtins.Print(new Sharpy.List<int>(global::Sharpy.Builtins.Map(x => (3 == 0 ? throw new global::Sharpy.ZeroDivisionError("integer division or modulo by zero") : (int)global::System.Math.Floor((double)((double)(x) / 3))), nums)));
#line (25, 5) - (25, 53) 8 "lambda_inferred_mod.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Sorted(new Sharpy.List<int>() { -7, -6, -5 }, key: x => global::Sharpy.Builtins.FloorMod(x, 3)));
#line (28, 5) - (28, 67) 8 "lambda_inferred_mod.spy"
        global::Sharpy.Builtins.Print(new Sharpy.List<int>(global::Sharpy.Builtins.Filter(x => ((global::System.Func<int, int>)(y => global::Sharpy.Builtins.FloorMod(y, 3)))(x) == 1, nums)));
#line (31, 5) - (31, 49) 8 "lambda_inferred_mod.spy"
        global::Sharpy.Builtins.Print(ApplyAll(v => global::Sharpy.Builtins.FloorMod(v, 3) == 1, nums));
#line (34, 5) - (34, 57) 8 "lambda_inferred_mod.spy"
        global::Sharpy.Builtins.Print(new Sharpy.List<int>(global::Sharpy.Builtins.Filter((int x) => global::Sharpy.Builtins.FloorMod(x, 3) == 1, nums)));
#line hidden
    }
}
#line default
