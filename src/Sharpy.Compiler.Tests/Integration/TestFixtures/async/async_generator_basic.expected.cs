#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class AsyncGeneratorBasic
{
    public static async System.Collections.Generic.IAsyncEnumerable<int> CountAsync(int n)
    {
#line (2, 5) - (2, 10) 1 "async_generator_basic.spy"
        var i = 0;
#line (3, 5) - (7, 1) 1 "async_generator_basic.spy"
        while (i < n)
        {
#line (4, 9) - (4, 17) 1 "async_generator_basic.spy"
            yield return i;
#line (5, 9) - (5, 15) 1 "async_generator_basic.spy"
            i = i + 1;
        }
    }

    public static async System.Threading.Tasks.Task Main()
    {
#line (8, 5) - (10, 1) 1 "async_generator_basic.spy"
        await foreach (var __loopVar_0 in CountAsync(3))
        {
            var x = __loopVar_0;
#line (9, 9) - (9, 17) 1 "async_generator_basic.spy"
            global::Sharpy.Builtins.Print(x);
        }
    }
}
