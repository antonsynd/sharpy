#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ParameterNoWarn
{
    public static class Program
    {
        public static string Greet(string name, string prefix)
        {
#line 3 "parameter_no_warn.spy"
            return name;
        }

        public static void Main()
        {
#line 6 "parameter_no_warn.spy"
            global::Sharpy.Core.Exports.Print(Greet("world", "hello"));
        }
    }
}
