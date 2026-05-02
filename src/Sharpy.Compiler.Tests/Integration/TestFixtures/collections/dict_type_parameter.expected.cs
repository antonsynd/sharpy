// Snapshot: Dictionary with type parameters
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class DictTypeParameter
{
    public static void Main()
    {
#line (4, 5) - (4, 56) 1 "dict_type_parameter.spy"
        Sharpy.Dict<string, int> scores = new Sharpy.Dict<string, int>()
        {
            {
                "alice",
                100
            },
            {
                "bob",
                85
            }
        };
#line (5, 5) - (5, 27) 1 "dict_type_parameter.spy"
        global::Sharpy.Builtins.Print(scores["alice"]);
#line (6, 5) - (6, 25) 1 "dict_type_parameter.spy"
        global::Sharpy.Builtins.Print(scores["bob"]);
    }
}
