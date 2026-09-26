// Snapshot: Decorator with string argument emits C# attribute
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace DecoratorArgsBasic0001
{
    public static partial class DecoratorArgsBasic0001Module
    {
        [Obsolete("Use new_greet instead")]
        public static string Greet()
        {
#line (3, 5) - (3, 20) 12 "decorator_args_basic_0001.spy"
            return "hello";
#line hidden
        }

        public static void Main()
        {
#line (6, 5) - (6, 19) 12 "decorator_args_basic_0001.spy"
            global::Sharpy.Builtins.Print(global::DecoratorArgsBasic0001.DecoratorArgsBasic0001Module.Greet());
#line hidden
        }
    }
}
#line default
