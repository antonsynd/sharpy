#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using static global::Sharpy.Unittest;
using Xunit;

public static partial class TestAssertRaisesCapture
{
    public static void Main()
    {
        global::Sharpy.Builtins.Print("ok");
    }
}

public partial class TestAssertRaisesCaptureTests
{
    [Xunit.FactAttribute]
    public void TestCaptureException()
    {
        var exc = Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
        {
            throw new global::Sharpy.ValueError("bad input");
        }));
        Xunit.Assert.Contains("bad input", global::Sharpy.Builtins.Str(exc));
    }
}
