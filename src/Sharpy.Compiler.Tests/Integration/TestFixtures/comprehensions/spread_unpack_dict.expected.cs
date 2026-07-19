// Snapshot: Dict-spread comprehension {**d for d in dicts}
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class SpreadUnpackDict
{
    public static void Main()
    {
#line (2, 5) - (2, 64) 8 "spread_unpack_dict.spy"
        Sharpy.List<Sharpy.Dict<string, int>> dicts = new Sharpy.List<Sharpy.Dict<string, int>>()
#line hidden
        {
            new Sharpy.Dict<string, int>()
            {
                {
                    "a",
                    1
                },
                {
                    "b",
                    2
                }
            },
            new Sharpy.Dict<string, int>()
            {
                {
                    "c",
                    3
                }
            }
        };
        var __comp_0 = new Sharpy.Dict<string, int>();
        foreach (var __loopVar_1 in dicts)
        {
            var d = __loopVar_1;
            __comp_0.Update(d);
        }

#line (3, 5) - (3, 51) 8 "spread_unpack_dict.spy"
        Sharpy.Dict<string, int> result = __comp_0;
#line (4, 5) - (4, 45) 8 "spread_unpack_dict.spy"
        Sharpy.List<string> keys = global::Sharpy.Builtins.Sorted<string>(result.Keys());
#line (5, 5) - (7, 1) 8 "spread_unpack_dict.spy"
        foreach (var __loopVar_2 in keys)
#line hidden
        {
            var k = __loopVar_2;
#line (6, 9) - (6, 28) 12 "spread_unpack_dict.spy"
            global::Sharpy.Builtins.Print(k, result[k]);
#line hidden
        }
    }
}
#line default
