#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class MatchTuple0001
{
    public static void Main()
    {
#line 2 "match_tuple_0001.spy"
        var point = (10, 20);
#line 3 "match_tuple_0001.spy"
        switch (point)
        {
            case (var x, var y):
#line 5 "match_tuple_0001.spy"
                global::Sharpy.Builtins.Print(x);
#line 6 "match_tuple_0001.spy"
                global::Sharpy.Builtins.Print(y);
                break;
        }
    }
}
