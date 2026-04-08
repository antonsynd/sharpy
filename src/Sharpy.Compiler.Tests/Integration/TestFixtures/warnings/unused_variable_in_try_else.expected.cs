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
#line 3 "unused_variable_in_try_else.spy"
        {
            bool __trySucceeded_0 = false;
            try
            {
#line 4 "unused_variable_in_try_else.spy"
                global::Sharpy.Builtins.Print(((Sharpy.Str)"trying"));
                __trySucceeded_0 = true;
            }
            catch (Exception e)
            {
#line 6 "unused_variable_in_try_else.spy"
                global::Sharpy.Builtins.Print(((Sharpy.Str)"caught"));
            }

            if (__trySucceeded_0)
            {
#line 8 "unused_variable_in_try_else.spy"
                int cleanup = 42;
            }
        }

#line 9 "unused_variable_in_try_else.spy"
        global::Sharpy.Builtins.Print(((Sharpy.Str)"done"));
    }
}
