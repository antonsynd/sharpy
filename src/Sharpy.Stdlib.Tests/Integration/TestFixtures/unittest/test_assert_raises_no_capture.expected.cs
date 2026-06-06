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
        global::Sharpy.Builtins.Print("ok");
    }
}

public partial class TestAssertRaisesNoCaptureTests
{
    [Xunit.FactAttribute]
    public void TestNoCapture()
    {
        Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
        {
            throw new global::Sharpy.ValueError("oops");
        }));
    }
}
