#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using static global::Sharpy.Unittest;
using Xunit;
using static AssertRaisesMatchCapture;

public static partial class AssertRaisesMatchCapture
{
    public static void Main()
    {
#line (10, 5) - (10, 16) 8 "assert_raises_match_capture.spy"
        global::Sharpy.Builtins.Print("ok");
#line hidden
    }
}

public partial class AssertRaisesMatchCaptureTests
{
    [Xunit.FactAttribute]
    public void TestMatchCapture()
    {
#line (5, 5) - (7, 1) 8 "assert_raises_match_capture.spy"
        var exc = Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
#line hidden
        {
#line (6, 9) - (6, 39) 12 "assert_raises_match_capture.spy"
            throw new global::Sharpy.ValueError("bad input");
#line hidden
        }));
        Xunit.Assert.Matches("bad", exc.Message);
#line (7, 5) - (7, 36) 8 "assert_raises_match_capture.spy"
        Xunit.Assert.Equal("bad input", global::Sharpy.Builtins.Str(exc));
#line hidden
    }
}
#line default
