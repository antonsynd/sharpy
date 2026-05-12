#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class MixedTupleUnpack
{
    public static void Main()
    {
#line (2, 5) - (2, 16) 1 "mixed_tuple_unpack.spy"
        int a = 1;
        var __t0 = (10, 20);
        a = __t0.Item1;
#line (3, 5) - (3, 18) 1 "mixed_tuple_unpack.spy"
        var b = __t0.Item2;
#line (4, 5) - (4, 13) 1 "mixed_tuple_unpack.spy"
        global::Sharpy.Builtins.Print(a);
#line (5, 5) - (5, 13) 1 "mixed_tuple_unpack.spy"
        global::Sharpy.Builtins.Print(b);
    }
}
