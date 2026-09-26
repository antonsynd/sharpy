#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace AsyncBasic
{
    public static partial class AsyncBasicModule
    {
        public static async System.Threading.Tasks.Task<string> Greet()
        {
#line (2, 5) - (2, 20) 12 "async_basic.spy"
            return "hello";
#line hidden
        }

        public static async System.Threading.Tasks.Task Main()
        {
#line (5, 5) - (5, 33) 12 "async_basic.spy"
            string result = await global::AsyncBasic.AsyncBasicModule.Greet();
#line (6, 5) - (6, 18) 12 "async_basic.spy"
            global::Sharpy.Builtins.Print(result);
#line hidden
        }
    }
}
#line default
