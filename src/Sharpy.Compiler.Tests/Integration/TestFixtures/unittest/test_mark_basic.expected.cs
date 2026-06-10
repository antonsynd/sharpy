#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;
using static TestMarkBasic;

public static partial class TestMarkBasic
{
    public static void Main()
    {
#line (14, 5) - (14, 16) 1 "test_mark_basic.spy"
        global::Sharpy.Builtins.Print("ok");
    }
}

public partial class TestMarkBasicTests
{
    [Xunit.FactAttribute]
    [Xunit.TraitAttribute("Category", "slow")]
    public void TestMarkedSlow()
    {
#line (4, 5) - (4, 20) 1 "test_mark_basic.spy"
        int x = 1 + 1;
#line (5, 5) - (5, 19) 1 "test_mark_basic.spy"
        Xunit.Assert.Equal(2, x);
    }

    [Xunit.FactAttribute]
    [Xunit.TraitAttribute("Category", "integration")]
    public void TestMarkedIntegration()
    {
#line (10, 5) - (10, 26) 1 "test_mark_basic.spy"
        string name = "sharpy";
#line (11, 5) - (11, 26) 1 "test_mark_basic.spy"
        Xunit.Assert.True(name.Length > 0);
    }
}
