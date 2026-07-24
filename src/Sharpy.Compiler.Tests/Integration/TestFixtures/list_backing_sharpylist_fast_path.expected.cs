#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class ListBackingSharpylistFastPath
{
    public static void Main()
    {
#line (8, 5) - (8, 34) 8 "list_backing_sharpylist_fast_path.spy"
        Sharpy.List<int> xs = new Sharpy.List<int>()
#line hidden
        {
            10,
            20,
            30
        };
#line (9, 5) - (9, 17) 8 "list_backing_sharpylist_fast_path.spy"
        global::Sharpy.Builtins.Print(xs.GetItemUnchecked(0));
#line (12, 5) - (12, 24) 8 "list_backing_sharpylist_fast_path.spy"
        global::Sharpy.Builtins.Print(new Sharpy.List<int>() { 1, 2, 3 }.GetItemUnchecked(2));
#line hidden
        var __src_1 = global::Sharpy.Builtins.Range(4);
        var __comp_0 = new Sharpy.List<int>(((global::Sharpy.ISized)__src_1).Count);
        foreach (var __loopVar_2 in __src_1)
        {
            var n = __loopVar_2;
            __comp_0.Add(n * n);
        }

#line (15, 5) - (15, 40) 8 "list_backing_sharpylist_fast_path.spy"
        global::Sharpy.Builtins.Print(__comp_0.GetItemUnchecked(3));
#line (18, 5) - (18, 33) 8 "list_backing_sharpylist_fast_path.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.StringExtensions.Split("a,b,c", ",").GetItemUnchecked(0));
#line hidden
    }
}
#line default
