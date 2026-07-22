#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class NarrowingRetestAfterAssert
{
    public static int CheckNullable(int? x)
    {
#line (6, 5) - (6, 26) 8 "narrowing_retest_after_assert.spy"
        if (!(x != null))
#line hidden
        {
            throw new global::Sharpy.AssertionError();
        }

#line (7, 5) - (9, 1) 8 "narrowing_retest_after_assert.spy"
        if (x != null)
#line hidden
        {
#line (8, 9) - (8, 22) 12 "narrowing_retest_after_assert.spy"
            return x.Value + 1;
#line hidden
        }

#line (9, 5) - (9, 14) 8 "narrowing_retest_after_assert.spy"
        return 0;
#line hidden
    }

    public static int CheckOptional(Optional<int> x)
    {
#line (12, 5) - (12, 26) 8 "narrowing_retest_after_assert.spy"
        if (!(x.IsSome))
#line hidden
        {
            throw new global::Sharpy.AssertionError();
        }

#line (13, 5) - (15, 1) 8 "narrowing_retest_after_assert.spy"
        if (x.IsSome)
#line hidden
        {
#line (14, 9) - (14, 22) 12 "narrowing_retest_after_assert.spy"
            return x.Unwrap() + 1;
#line hidden
        }

#line (15, 5) - (15, 14) 8 "narrowing_retest_after_assert.spy"
        return 0;
#line hidden
    }

    public static void Main()
    {
#line (18, 5) - (18, 29) 8 "narrowing_retest_after_assert.spy"
        global::Sharpy.Builtins.Print(CheckNullable(5));
#line (19, 5) - (19, 35) 8 "narrowing_retest_after_assert.spy"
        global::Sharpy.Builtins.Print(CheckOptional(Optional<int>.Some(7)));
#line hidden
    }
}
#line default
