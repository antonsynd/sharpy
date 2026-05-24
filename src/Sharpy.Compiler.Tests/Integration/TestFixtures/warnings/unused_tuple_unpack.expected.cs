// Snapshot: Tuple unpacking from function return value
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class UnusedTupleUnpack
{
    public static global::System.ValueTuple<int, int> GetPair()
    {
#line (2, 5) - (2, 21) 1 "unused_tuple_unpack.spy"
        return (10, 20);
    }

    public static void Main()
    {
#line (5, 5) - (5, 22) 1 "unused_tuple_unpack.spy"
        var (a, b) = GetPair();
#line (6, 5) - (6, 13) 1 "unused_tuple_unpack.spy"
        global::Sharpy.Builtins.Print(a);
    }
}
