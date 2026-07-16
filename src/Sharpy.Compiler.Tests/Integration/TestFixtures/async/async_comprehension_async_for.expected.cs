// Snapshot: Async comprehension with filter [x async for x in gen() if cond]
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class AsyncComprehensionAsyncFor
{
    public static async System.Collections.Generic.IAsyncEnumerable<int> Gen()
    {
#line (2, 5) - (2, 13) 1 "async_comprehension_async_for.spy"
        yield return 1;
#line (3, 5) - (3, 13) 1 "async_comprehension_async_for.spy"
        yield return 2;
#line (4, 5) - (4, 13) 1 "async_comprehension_async_for.spy"
        yield return 3;
    }

    public static async System.Threading.Tasks.Task Main()
    {
        var __comp_0 = new Sharpy.List<int>();
        await foreach (var __loopVar_1 in Gen())
        {
            var x = __loopVar_1;
            if (x > 0)
            {
                __comp_0.Add(x * 2);
            }
        }

#line (7, 5) - (7, 51) 1 "async_comprehension_async_for.spy"
        var result = __comp_0;
#line (8, 5) - (8, 18) 1 "async_comprehension_async_for.spy"
        global::Sharpy.Builtins.Print(result);
    }
}
