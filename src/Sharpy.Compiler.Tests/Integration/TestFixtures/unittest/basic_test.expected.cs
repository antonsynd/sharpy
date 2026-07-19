#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;
using static BasicTest;

public static partial class BasicTest
{
    public static void Main()
    {
#line (7, 5) - (7, 13) 8 "basic_test.spy"
        global::Sharpy.Builtins.Print(2);
#line hidden
    }
}

public partial class BasicTestTests
{
    [Xunit.FactAttribute]
    public void TestAddition()
    {
#line (3, 5) - (3, 20) 8 "basic_test.spy"
        int x = 1 + 1;
#line (4, 5) - (4, 19) 8 "basic_test.spy"
        Xunit.Assert.Equal(2, x);
#line hidden
    }
}
#line default
