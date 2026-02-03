#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.UnusedVariable
{
    public static class Program
    {
        public static void Main()
        {
#line 2 "unused_variable.spy"
            int x = 42;
#line 3 "unused_variable.spy"
            global::Sharpy.Core.Exports.Print("hello");
        }
    }
}
