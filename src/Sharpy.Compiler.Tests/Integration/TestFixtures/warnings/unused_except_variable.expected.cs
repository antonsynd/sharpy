#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.UnusedExceptVariable
{
    public static class Program
    {
        public static void Main()
        {
#line 3 "unused_except_variable.spy"
            try
            {
#line 4 "unused_except_variable.spy"
                global::Sharpy.Core.Exports.Print("trying");
            }
            catch (Exception e)
            {
#line 6 "unused_except_variable.spy"
                global::Sharpy.Core.Exports.Print("caught");
            }
        }
    }
}
