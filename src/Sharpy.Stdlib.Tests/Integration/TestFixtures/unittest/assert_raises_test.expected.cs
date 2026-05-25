#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using static global::Sharpy.Unittest;
using Xunit;

public static partial class AssertRaisesTest
{
    public static void Main()
    {
#line (14, 5) - (14, 16) 1 "assert_raises_test.spy"
        global::Sharpy.Builtins.Print("ok");
    }
}

public partial class AssertRaisesTestTests
{
    [Xunit.FactAttribute]
    public void TestRaisesValueError()
    {
#line (5, 5) - (8, 1) 1 "assert_raises_test.spy"
        Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
        {
#line (6, 9) - (6, 34) 1 "assert_raises_test.spy"
            throw new global::Sharpy.ValueError("oops");
        }));
    }

    [Xunit.FactAttribute]
    public void TestRaisesRuntimeError()
    {
#line (10, 5) - (13, 1) 1 "assert_raises_test.spy"
        Xunit.Assert.Throws<RuntimeError>((global::System.Action)(() =>
        {
#line (11, 9) - (11, 36) 1 "assert_raises_test.spy"
            throw new global::Sharpy.RuntimeError("boom");
        }));
    }
}
