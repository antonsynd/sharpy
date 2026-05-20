#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;

public static partial class AsyncTestParametrize
{
    public static void Main()
    {
#line (7, 5) - (7, 16) 1 "async_test_parametrize.spy"
        global::Sharpy.Builtins.Print("ok");
    }
}

public partial class AsyncTestParametrizeTests
{
    [Xunit.TheoryAttribute]
    [Xunit.InlineDataAttribute(1, 1)]
    [Xunit.InlineDataAttribute(2, 4)]
    [Xunit.InlineDataAttribute(3, 9)]
    public async System.Threading.Tasks.Task TestAsyncSquare(int value, int expected)
    {
#line (3, 5) - (3, 33) 1 "async_test_parametrize.spy"
        int result = value * value;
#line (4, 5) - (4, 31) 1 "async_test_parametrize.spy"
        Xunit.Assert.Equal(expected, result);
    }
}
