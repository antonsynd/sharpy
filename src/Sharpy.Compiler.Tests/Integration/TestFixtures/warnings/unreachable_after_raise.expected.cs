// Snapshot: Raise statement with unreachable code and try/except
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

public static partial class UnreachableAfterRaise
{
    public static int Foo()
    {
#line 2 "unreachable_after_raise.spy"
        throw new Exception("error");
#line 3 "unreachable_after_raise.spy"
        return 1;
    }

    public static void Main()
    {
#line 6 "unreachable_after_raise.spy"
        try
        {
#line 7 "unreachable_after_raise.spy"
            Foo();
        }
        catch (Exception e)
        {
#line 9 "unreachable_after_raise.spy"
            global::Sharpy.Builtins.Print("caught");
        }
    }
}
