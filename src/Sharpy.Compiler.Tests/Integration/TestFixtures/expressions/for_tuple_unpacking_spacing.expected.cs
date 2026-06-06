// Snapshot: Foreach tuple deconstruction spacing (issue 846)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class ForTupleUnpackingSpacing
{
    public static void Main()
    {
#line (2, 5) - (2, 57) 1 "for_tuple_unpacking_spacing.spy"
        Sharpy.List<global::System.ValueTuple<int, string>> pairs = new Sharpy.List<global::System.ValueTuple<int, string>>()
        {
            (1, "a"),
            (2, "b")
        };
#line (3, 5) - (6, 1) 1 "for_tuple_unpacking_spacing.spy"
        foreach (var (num, label) in pairs)
        {
#line (4, 9) - (4, 19) 1 "for_tuple_unpacking_spacing.spy"
            global::Sharpy.Builtins.Print(num);
#line (5, 9) - (5, 21) 1 "for_tuple_unpacking_spacing.spy"
            global::Sharpy.Builtins.Print(label);
        }
    }
}
