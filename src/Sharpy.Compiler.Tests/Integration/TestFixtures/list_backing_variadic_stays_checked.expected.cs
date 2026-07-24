#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class ListBackingVariadicStaysChecked
{
    public static void Show(params Sharpy.List<int>[] rows)
    {
#line (5, 5) - (5, 19) 8 "list_backing_variadic_stays_checked.spy"
        global::Sharpy.Builtins.Print(rows[0]);
#line hidden
    }

    public static void Main()
    {
#line (8, 5) - (8, 25) 8 "list_backing_variadic_stays_checked.spy"
        Show(new Sharpy.List<int>() { 1, 2 }, new Sharpy.List<int>() { 3, 4 });
#line hidden
    }
}
#line default
