// Snapshot: While loop
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace WhileLoop
{
    public static partial class WhileLoopModule
    {
        public static void Countdown(int n)
        {
#line (2, 5) - (4, 18) 12 "while_loop.spy"
            while (n > 0)
#line hidden
            {
#line (3, 9) - (3, 17) 16 "while_loop.spy"
                global::Sharpy.Builtins.Print(n);
#line (4, 9) - (4, 18) 16 "while_loop.spy"
                n = n - 1;
#line hidden
            }
        }

        public static void Main()
        {
            __Load();
#line (7, 5) - (7, 17) 12 "while_loop.spy"
            global::WhileLoop.WhileLoopModule.Countdown(3);
#line hidden
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        [global::Sharpy.SharpyLoadEntry(false)]
        public static void __Load()
        {
        }
    }
}
#line default
