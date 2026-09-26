// Snapshot: Tuple unpacking from function return value
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace UnusedTupleUnpack
{
    public static partial class UnusedTupleUnpackModule
    {
        public static global::System.ValueTuple<int, int> GetPair()
        {
#line (2, 5) - (2, 21) 12 "unused_tuple_unpack.spy"
            return (10, 20);
#line hidden
        }

        public static void Main()
        {
#line (5, 5) - (5, 22) 12 "unused_tuple_unpack.spy"
            var (a, b) = global::UnusedTupleUnpack.UnusedTupleUnpackModule.GetPair();
#line (6, 5) - (6, 13) 12 "unused_tuple_unpack.spy"
            global::Sharpy.Builtins.Print(a);
#line hidden
        }
    }
}
#line default
