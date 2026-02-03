#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.SetTypeParameter
{
    public static class Program
    {
        public static void Main()
        {
#line 4 "set_type_parameter.spy"
            System.Collections.Generic.HashSet<int> uniqueNums = new System.Collections.Generic.HashSet<int>()
            {
                1,
                2,
                2,
                3,
                3,
                3
            };
#line 6 "set_type_parameter.spy"
            foreach (var __loopVar_0 in uniqueNums)
            {
                var n = __loopVar_0;
#line 7 "set_type_parameter.spy"
                global::Sharpy.Core.Exports.Print(n);
            }
        }
    }
}
