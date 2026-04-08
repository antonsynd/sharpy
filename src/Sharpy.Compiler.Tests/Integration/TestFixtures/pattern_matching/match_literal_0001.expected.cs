#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class MatchLiteral0001
{
    public static void Main()
    {
#line 2 "match_literal_0001.spy"
        int value = 42;
#line 3 "match_literal_0001.spy"
        switch (value)
        {
            case 1:
#line 5 "match_literal_0001.spy"
                global::Sharpy.Builtins.Print(((Sharpy.Str)"one"));
                break;
            case 42:
#line 7 "match_literal_0001.spy"
                global::Sharpy.Builtins.Print(((Sharpy.Str)"forty-two"));
                break;
            default:
#line 9 "match_literal_0001.spy"
                global::Sharpy.Builtins.Print(((Sharpy.Str)"other"));
                break;
        }
    }
}
