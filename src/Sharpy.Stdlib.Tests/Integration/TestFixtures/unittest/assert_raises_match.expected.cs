#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using static global::Sharpy.Unittest;
using Xunit;
using static AssertRaisesMatch;

public static partial class AssertRaisesMatch
{
    public static void Main()
    {
#line (14, 5) - (14, 16) 8 "assert_raises_match.spy"
        global::Sharpy.Builtins.Print("ok");
#line hidden
    }
}

public partial class AssertRaisesMatchTests
{
    [Xunit.FactAttribute]
    public void TestMatchBasic()
    {
#line (5, 5) - (8, 1) 8 "assert_raises_match.spy"
        ValueError __ex_0 = null!;
#line hidden
        bool __raised_1 = false;
        try
        {
#line (6, 9) - (6, 39) 12 "assert_raises_match.spy"
            throw new global::Sharpy.ValueError("bad input");
#line hidden
        }
        catch (ValueError __caught_2)
        {
            __raised_1 = true;
            __ex_0 = __caught_2;
        }

        if (!__raised_1)
            throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
        if (!(global::System.Text.RegularExpressions.Regex.IsMatch(__ex_0.Message, "bad.*input")))
            throw new global::Sharpy.AssertionError("Expected the raised ValueError's message to match " + "bad.*input" + ", but it was: " + __ex_0.Message);
    }

    [Xunit.FactAttribute]
    public void TestMatchSubstring()
    {
#line (10, 5) - (13, 1) 8 "assert_raises_match.spy"
        RuntimeError __ex_3 = null!;
#line hidden
        bool __raised_4 = false;
        try
        {
#line (11, 9) - (11, 48) 12 "assert_raises_match.spy"
            throw new global::Sharpy.RuntimeError("operation failed");
#line hidden
        }
        catch (RuntimeError __caught_5)
        {
            __raised_4 = true;
            __ex_3 = __caught_5;
        }

        if (!__raised_4)
            throw new global::Sharpy.AssertionError("Expected RuntimeError to be raised, but no exception was raised");
        if (!(global::System.Text.RegularExpressions.Regex.IsMatch(__ex_3.Message, "fail")))
            throw new global::Sharpy.AssertionError("Expected the raised RuntimeError's message to match " + "fail" + ", but it was: " + __ex_3.Message);
    }
}
#line default
