// Snapshot: Numeric safe casts (#1110) — range-checked helper for narrowing, Optional.Some for widening
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class SafeCastNumeric
{
    public static void Main()
    {
#line (6, 5) - (6, 20) 8 "safe_cast_numeric.spy"
        double a = 3.9d;
#line (7, 5) - (7, 29) 8 "safe_cast_numeric.spy"
        global::Sharpy.Builtins.Print(((global::Sharpy.NumericSafeCast.ToIntOrNone(a))).UnwrapOr(-1));
#line (10, 5) - (10, 21) 8 "safe_cast_numeric.spy"
        double b = -3.9d;
#line (11, 5) - (11, 29) 8 "safe_cast_numeric.spy"
        global::Sharpy.Builtins.Print(((global::Sharpy.NumericSafeCast.ToIntOrNone(b))).UnwrapOr(-1));
#line (14, 5) - (14, 23) 8 "safe_cast_numeric.spy"
        double big = 1e30d;
#line (15, 5) - (15, 31) 8 "safe_cast_numeric.spy"
        global::Sharpy.Builtins.Print(((global::Sharpy.NumericSafeCast.ToIntOrNone(big))).UnwrapOr(-1));
#line (18, 5) - (18, 32) 8 "safe_cast_numeric.spy"
        double inf = 1e300d * 1e300d;
#line (19, 5) - (19, 28) 8 "safe_cast_numeric.spy"
        double nan = inf - inf;
#line (20, 5) - (20, 31) 8 "safe_cast_numeric.spy"
        global::Sharpy.Builtins.Print(((global::Sharpy.NumericSafeCast.ToIntOrNone(nan))).UnwrapOr(-1));
#line (21, 5) - (21, 31) 8 "safe_cast_numeric.spy"
        global::Sharpy.Builtins.Print(((global::Sharpy.NumericSafeCast.ToIntOrNone(inf))).UnwrapOr(-1));
#line (24, 5) - (24, 23) 8 "safe_cast_numeric.spy"
        long small = 100;
#line (25, 5) - (25, 33) 8 "safe_cast_numeric.spy"
        global::Sharpy.Builtins.Print(((global::Sharpy.NumericSafeCast.ToIntOrNone(small))).UnwrapOr(-1));
#line (26, 5) - (26, 29) 8 "safe_cast_numeric.spy"
        long huge = 5000000000L;
#line (27, 5) - (27, 32) 8 "safe_cast_numeric.spy"
        global::Sharpy.Builtins.Print(((global::Sharpy.NumericSafeCast.ToIntOrNone(huge))).UnwrapOr(-1));
#line (30, 5) - (30, 16) 8 "safe_cast_numeric.spy"
        int n = 7;
#line (31, 5) - (31, 32) 8 "safe_cast_numeric.spy"
        global::Sharpy.Builtins.Print(((Optional<double>.Some((double)n))).UnwrapOr(0.0d));
#line (34, 5) - (34, 17) 8 "safe_cast_numeric.spy"
        int m = 42;
#line (35, 5) - (35, 29) 8 "safe_cast_numeric.spy"
        global::Sharpy.Builtins.Print(((Optional<int>.Some((int)m))).UnwrapOr(-1));
#line (38, 5) - (38, 29) 8 "safe_cast_numeric.spy"
        Optional<int> oob = global::Sharpy.NumericSafeCast.ToIntOrNone(big);
#line (39, 5) - (39, 23) 8 "safe_cast_numeric.spy"
        global::Sharpy.Builtins.Print(oob.IsNone);
#line (40, 5) - (40, 23) 8 "safe_cast_numeric.spy"
        global::Sharpy.Builtins.Print((oob).UnwrapOr(-999));
#line hidden
    }
}
#line default
