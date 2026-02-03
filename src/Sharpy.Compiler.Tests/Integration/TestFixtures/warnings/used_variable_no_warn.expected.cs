#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.UsedVariableNoWarn
{
    public static class Program
    {
        public static void Main()
        {
#line 3 "used_variable_no_warn.spy"
            int x = 42;
#line 4 "used_variable_no_warn.spy"
            global::Sharpy.Core.Exports.Print(x);
        }
    }
}
