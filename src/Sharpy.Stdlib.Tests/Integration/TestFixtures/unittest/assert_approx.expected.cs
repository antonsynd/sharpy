#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using static global::Sharpy.Unittest;
using Xunit;
using static AssertApprox;

public static partial class AssertApprox
{
    public static void Main()
    {
#line (20, 5) - (20, 16) 1 "assert_approx.spy"
        global::Sharpy.Builtins.Print("ok");
    }
}

public partial class AssertApproxTests
{
    [Xunit.FactAttribute]
    public void TestApproxDefault()
    {
#line (5, 5) - (5, 37) 1 "assert_approx.spy"
        Xunit.Assert.Equal(0.3d, 0.1d + 0.2d, 7);
    }

    [Xunit.FactAttribute]
    public void TestApproxPlaces()
    {
#line (9, 5) - (9, 48) 1 "assert_approx.spy"
        Xunit.Assert.Equal(0.3d, 0.1d + 0.2d, 10);
    }

    [Xunit.FactAttribute]
    public void TestApproxAbs()
    {
#line (13, 5) - (13, 47) 1 "assert_approx.spy"
        Xunit.Assert.Equal(0.3d, 0.1d + 0.2d, 1e-9d);
    }

    [Xunit.FactAttribute]
    public void TestApproxLeft()
    {
#line (17, 5) - (17, 37) 1 "assert_approx.spy"
        Xunit.Assert.Equal(0.3d, 0.1d + 0.2d, 7);
    }
}
