#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class AsyncBasic
{
    public static async System.Threading.Tasks.Task<string> Greet()
    {
#line (2, 5) - (2, 20) 8 "async_basic.spy"
        return "hello";
#line hidden
    }

    public static async System.Threading.Tasks.Task Main()
    {
#line (5, 5) - (5, 33) 8 "async_basic.spy"
        string result = await Greet();
#line (6, 5) - (6, 18) 8 "async_basic.spy"
        global::Sharpy.Builtins.Print(result);
#line hidden
    }
}
#line default
