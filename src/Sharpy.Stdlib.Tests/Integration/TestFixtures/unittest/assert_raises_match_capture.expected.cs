#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using static global::Sharpy.Unittest;
using Xunit;

public static partial class AssertRaisesMatchCapture
{
    public static void Main()
    {
#line (10, 5) - (10, 16) 1 "assert_raises_match_capture.spy"
        global::Sharpy.Builtins.Print("ok");
    }
}

public partial class AssertRaisesMatchCaptureTests
{
    [Xunit.FactAttribute]
    public void TestMatchCapture()
    {
#line (5, 5) - (7, 1) 1 "assert_raises_match_capture.spy"
        var exc = Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
        {
#line (6, 9) - (6, 39) 1 "assert_raises_match_capture.spy"
            throw new global::Sharpy.ValueError("bad input");
        }));
        Xunit.Assert.Matches("bad", exc.Message);
#line (7, 5) - (7, 36) 1 "assert_raises_match_capture.spy"
        Xunit.Assert.Equal("bad input", global::Sharpy.Builtins.Str(exc));
    }
}
