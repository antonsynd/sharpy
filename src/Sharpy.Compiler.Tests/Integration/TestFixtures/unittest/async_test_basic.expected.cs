#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;
using static AsyncTestBasic;

public static partial class AsyncTestBasic
{
    public static void Main()
    {
#line (12, 5) - (12, 16) 1 "async_test_basic.spy"
        global::Sharpy.Builtins.Print("ok");
    }
}

public partial class AsyncTestBasicTests
{
    [Xunit.FactAttribute]
    public async System.Threading.Tasks.Task TestAsyncBasic()
    {
#line (3, 5) - (3, 17) 1 "async_test_basic.spy"
        int x = 42;
#line (4, 5) - (4, 20) 1 "async_test_basic.spy"
        Xunit.Assert.Equal(42, x);
    }

    [Xunit.FactAttribute]
    public async System.Threading.Tasks.Task TestAsyncWithValue()
    {
#line (8, 5) - (8, 26) 1 "async_test_basic.spy"
        string name = "sharpy";
#line (9, 5) - (9, 27) 1 "async_test_basic.spy"
        Xunit.Assert.Equal(6, name.Length);
    }
}
