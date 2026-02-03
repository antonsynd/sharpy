#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.LoopVariableNoWarn
{
    public static class Program
    {
        public static void Main()
        {
#line 3 "loop_variable_no_warn.spy"
            foreach (var __loopVar_0 in global::Sharpy.Core.Exports.Range(3))
            {
                var i = __loopVar_0;
#line 4 "loop_variable_no_warn.spy"
                global::Sharpy.Core.Exports.Print("hello");
            }
        }
    }
}
