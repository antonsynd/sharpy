#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class NoneElementMixed
{
    public static void Main()
    {
#line (2, 5) - (2, 25) 8 "none_element_mixed.spy"
        string? x = "a";
#line hidden
        global::System.ValueTuple<string?, int> __t0 = (null, 1);
        x = __t0.Item1;
#line (3, 5) - (3, 19) 8 "none_element_mixed.spy"
        var m = __t0.Item2;
#line (4, 5) - (4, 24) 8 "none_element_mixed.spy"
        global::Sharpy.Builtins.Print(x == null, m);
#line hidden
    }
}
#line default
