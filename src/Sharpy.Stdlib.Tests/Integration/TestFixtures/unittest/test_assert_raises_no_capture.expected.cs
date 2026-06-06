#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using static global::Sharpy.Unittest;
using Xunit;

public static partial class TestAssertRaisesNoCapture
{
    public static void Main()
    {
#line (9, 5) - (9, 16) 1 "test_assert_raises_no_capture.spy"
        global::Sharpy.Builtins.Print("ok");
    }
}

public partial class TestAssertRaisesNoCaptureTests
{
    [Xunit.FactAttribute]
    public void TestNoCapture()
    {
#line (5, 5) - (8, 1) 1 "test_assert_raises_no_capture.spy"
        Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
        {
#line (6, 9) - (6, 34) 1 "test_assert_raises_no_capture.spy"
            throw new global::Sharpy.ValueError("oops");
        }));
    }
}
