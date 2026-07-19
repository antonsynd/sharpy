// Snapshot: Try/except/else block with variable scoping
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class UnusedVariableInTryElse
{
    public static void Main()
    {
#line (3, 5) - (9, 1) 8 "unused_variable_in_try_else.spy"
        {
#line hidden
            bool __trySucceeded_0 = false;
            try
            {
#line (4, 9) - (4, 24) 16 "unused_variable_in_try_else.spy"
                global::Sharpy.Builtins.Print("trying");
#line hidden
                __trySucceeded_0 = true;
            }
            catch (Exception e)
            {
#line (6, 9) - (6, 24) 16 "unused_variable_in_try_else.spy"
                global::Sharpy.Builtins.Print("caught");
#line hidden
            }

            if (__trySucceeded_0)
            {
#line (8, 9) - (8, 27) 16 "unused_variable_in_try_else.spy"
                int cleanup = 42;
#line hidden
            }
        }

#line (9, 5) - (9, 18) 8 "unused_variable_in_try_else.spy"
        global::Sharpy.Builtins.Print("done");
#line hidden
    }
}
#line default
