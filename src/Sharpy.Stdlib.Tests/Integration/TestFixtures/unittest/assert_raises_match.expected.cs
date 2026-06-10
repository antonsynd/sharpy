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
#line (14, 5) - (14, 16) 1 "assert_raises_match.spy"
        global::Sharpy.Builtins.Print("ok");
    }
}

public partial class AssertRaisesMatchTests
{
    [Xunit.FactAttribute]
    public void TestMatchBasic()
    {
#line (5, 5) - (8, 1) 1 "assert_raises_match.spy"
        var __ex_0 = Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
        {
#line (6, 9) - (6, 39) 1 "assert_raises_match.spy"
            throw new global::Sharpy.ValueError("bad input");
        }));
        Xunit.Assert.Matches("bad.*input", __ex_0.Message);
    }

    [Xunit.FactAttribute]
    public void TestMatchSubstring()
    {
#line (10, 5) - (13, 1) 1 "assert_raises_match.spy"
        var __ex_1 = Xunit.Assert.Throws<RuntimeError>((global::System.Action)(() =>
        {
#line (11, 9) - (11, 48) 1 "assert_raises_match.spy"
            throw new global::Sharpy.RuntimeError("operation failed");
        }));
        Xunit.Assert.Matches("fail", __ex_1.Message);
    }
}
