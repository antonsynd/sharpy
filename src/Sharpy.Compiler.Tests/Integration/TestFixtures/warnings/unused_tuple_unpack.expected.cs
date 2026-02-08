// Snapshot: Tuple unpacking from function return value
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

public static partial class UnusedTupleUnpack
{
    public static System.ValueTuple<int, int> GetPair()
    {
#line 2 "unused_tuple_unpack.spy"
        return (10, 20);
    }

    public static void Main()
    {
#line 5 "unused_tuple_unpack.spy"
        var (a, b) = GetPair();
#line 6 "unused_tuple_unpack.spy"
        global::Sharpy.Builtins.Print(a);
    }
}
