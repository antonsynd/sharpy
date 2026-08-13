#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using static global::Sharpy.Unittest;
using Xunit;
using static AssertRaisesTest;

public static partial class AssertRaisesTest
{
    public static void Main()
    {
#line (14, 5) - (14, 16) 8 "assert_raises_test.spy"
        global::Sharpy.Builtins.Print("ok");
#line hidden
    }
}

public partial class AssertRaisesTestTests
{
    [Xunit.FactAttribute]
    public void TestRaisesValueError()
    {
#line (5, 5) - (8, 1) 8 "assert_raises_test.spy"
        bool __raised_0 = false;
#line hidden
        try
        {
#line (6, 9) - (6, 34) 12 "assert_raises_test.spy"
            throw new global::Sharpy.ValueError("oops");
#line hidden
        }
        catch (ValueError)
        {
            __raised_0 = true;
        }

        if (!__raised_0)
            throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
    }

    [Xunit.FactAttribute]
    public void TestRaisesRuntimeError()
    {
#line (10, 5) - (13, 1) 8 "assert_raises_test.spy"
        bool __raised_1 = false;
#line hidden
        try
        {
#line (11, 9) - (11, 36) 12 "assert_raises_test.spy"
            throw new global::Sharpy.RuntimeError("boom");
#line hidden
        }
        catch (RuntimeError)
        {
            __raised_1 = true;
        }

        if (!__raised_1)
            throw new global::Sharpy.AssertionError("Expected RuntimeError to be raised, but no exception was raised");
    }
}
#line default
