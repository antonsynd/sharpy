#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;

namespace AsyncTestParametrize
{
    public static partial class AsyncTestParametrizeModule
    {
        public static void Main()
        {
#line (7, 5) - (7, 16) 12 "async_test_parametrize.spy"
            global::Sharpy.Builtins.Print("ok");
#line hidden
        }
    }

    public partial class AsyncTestParametrizeModuleTests
    {
        [Xunit.TheoryAttribute]
        [Xunit.InlineDataAttribute(1, 1)]
        [Xunit.InlineDataAttribute(2, 4)]
        [Xunit.InlineDataAttribute(3, 9)]
        public async System.Threading.Tasks.Task TestAsyncSquare(int value, int expected)
        {
#line (3, 5) - (3, 33) 12 "async_test_parametrize.spy"
            int result = value * value;
#line (4, 5) - (4, 31) 12 "async_test_parametrize.spy"
            Xunit.Assert.Equal(expected, result);
#line hidden
        }
    }
}
#line default
