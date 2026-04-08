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
#line 4 "dict_type_parameter.spy"
        Sharpy.Dict<Sharpy.Str, int> scores = new Sharpy.Dict<Sharpy.Str, int>()
        {
            {
                ((Sharpy.Str)"alice"),
                100
            },
            {
                ((Sharpy.Str)"bob"),
                85
            }
        };
#line 5 "dict_type_parameter.spy"
        global::Sharpy.Builtins.Print(scores[((Sharpy.Str)"alice")]);
#line 6 "dict_type_parameter.spy"
        global::Sharpy.Builtins.Print(scores[((Sharpy.Str)"bob")]);
    }
}
