// Snapshot: Raise statement with unreachable code and try/except
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class UnreachableAfterRaise
{
    public static int Foo()
    {
#line (2, 5) - (2, 30) 8 "unreachable_after_raise.spy"
        throw new Exception("error");
#line (3, 5) - (3, 14) 8 "unreachable_after_raise.spy"
        return 1;
#line hidden
    }

    public static void Main()
    {
#line (6, 5) - (10, 1) 8 "unreachable_after_raise.spy"
        try
#line hidden
        {
#line (7, 9) - (7, 14) 12 "unreachable_after_raise.spy"
            Foo();
#line hidden
        }
        catch (Exception e)
        {
#line (9, 9) - (9, 24) 12 "unreachable_after_raise.spy"
            global::Sharpy.Builtins.Print("caught");
#line hidden
        }
    }
}
#line default
