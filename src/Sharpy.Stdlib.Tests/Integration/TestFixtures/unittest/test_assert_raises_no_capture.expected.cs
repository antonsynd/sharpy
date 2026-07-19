#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using static global::Sharpy.Unittest;
using Xunit;
using static TestAssertRaisesNoCapture;

public static partial class TestAssertRaisesNoCapture
{
    public static void Main()
    {
#line (9, 5) - (9, 16) 8 "test_assert_raises_no_capture.spy"
        global::Sharpy.Builtins.Print("ok");
#line hidden
    }
}

public partial class TestAssertRaisesNoCaptureTests
{
    [Xunit.FactAttribute]
    public void TestNoCapture()
    {
#line (5, 5) - (8, 1) 8 "test_assert_raises_no_capture.spy"
        Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
#line hidden
        {
#line (6, 9) - (6, 34) 12 "test_assert_raises_no_capture.spy"
            throw new global::Sharpy.ValueError("oops");
#line hidden
        }));
    }
}
#line default
