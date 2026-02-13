// Snapshot: Dictionary with type parameters
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

public static partial class DictTypeParameter
{
    public static void Main()
    {
#line 4 "dict_type_parameter.spy"
        Dict<string, int> scores = new Dict<string, int>()
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
#line 5 "dict_type_parameter.spy"
        global::Sharpy.Builtins.Print(scores["alice"]);
#line 6 "dict_type_parameter.spy"
        global::Sharpy.Builtins.Print(scores["bob"]);
    }
}
