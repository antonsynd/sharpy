// Snapshot: Exception handling with try/except and variable binding
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class UnusedExceptVariable
{
    public static void Main()
    {
#line (3, 5) - (7, 1) 1 "unused_except_variable.spy"
        try
        {
#line (4, 9) - (4, 24) 1 "unused_except_variable.spy"
            global::Sharpy.Builtins.Print("trying");
        }
        catch (Exception e)
        {
#line (6, 9) - (6, 24) 1 "unused_except_variable.spy"
            global::Sharpy.Builtins.Print("caught");
        }
    }
}
