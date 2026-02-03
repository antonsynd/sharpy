#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.DictComprehension
{
    public static class Program
    {
        public static void Main()
        {
#line 3 "dict_comprehension.spy"
            System.Collections.Generic.Dictionary<int, int> result = global::Sharpy.Core.Exports.Range(5).ToDictionary(i => i, i => i * 2);
#line 4 "dict_comprehension.spy"
            global::Sharpy.Core.Exports.Print(global::Sharpy.Core.Exports.Len(result));
#line 5 "dict_comprehension.spy"
            global::Sharpy.Core.Exports.Print(result[0]);
#line 6 "dict_comprehension.spy"
            global::Sharpy.Core.Exports.Print(result[1]);
#line 7 "dict_comprehension.spy"
            global::Sharpy.Core.Exports.Print(result[2]);
#line 8 "dict_comprehension.spy"
            global::Sharpy.Core.Exports.Print(result[3]);
#line 9 "dict_comprehension.spy"
            global::Sharpy.Core.Exports.Print(result[4]);
        }
    }
}
