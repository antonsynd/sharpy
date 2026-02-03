#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.DictTypeParameter
{
    public static class Program
    {
        public static void Main()
        {
#line 4 "dict_type_parameter.spy"
            System.Collections.Generic.Dictionary<string, int> scores = new System.Collections.Generic.Dictionary<string, int>()
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
            global::Sharpy.Core.Exports.Print(scores["alice"]);
#line 6 "dict_type_parameter.spy"
            global::Sharpy.Core.Exports.Print(scores["bob"]);
        }
    }
}
