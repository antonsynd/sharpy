#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using static global::Sharpy.Unittest;
using Xunit;
using static TestAssertRaisesCapture;

public static partial class TestAssertRaisesCapture
{
    public static void Main()
    {
#line (10, 5) - (10, 16) 8 "test_assert_raises_capture.spy"
        global::Sharpy.Builtins.Print("ok");
#line hidden
    }
}

public partial class TestAssertRaisesCaptureTests
{
    [Xunit.FactAttribute]
    public void TestCaptureException()
    {
#line (5, 5) - (7, 1) 8 "test_assert_raises_capture.spy"
        var exc = Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
#line hidden
        {
#line (6, 9) - (6, 39) 12 "test_assert_raises_capture.spy"
            throw new global::Sharpy.ValueError("bad input");
#line hidden
        }));
#line (7, 5) - (7, 36) 8 "test_assert_raises_capture.spy"
        Xunit.Assert.Contains("bad input", global::Sharpy.Builtins.Str(exc));
#line hidden
    }
}
#line default
