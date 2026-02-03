#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.UnusedVariableInTryElse
{
    public static class Program
    {
        public static void Main()
        {
#line 3 "unused_variable_in_try_else.spy"
            {
                bool __trySucceeded_0 = false;
                try
                {
#line 4 "unused_variable_in_try_else.spy"
                    global::Sharpy.Core.Exports.Print("trying");
                    __trySucceeded_0 = true;
                }
                catch (Exception e)
                {
#line 6 "unused_variable_in_try_else.spy"
                    global::Sharpy.Core.Exports.Print("caught");
                }

                if (__trySucceeded_0)
                {
#line 8 "unused_variable_in_try_else.spy"
                    int cleanup = 42;
                }
            }

#line 9 "unused_variable_in_try_else.spy"
            global::Sharpy.Core.Exports.Print("done");
        }
    }
}
