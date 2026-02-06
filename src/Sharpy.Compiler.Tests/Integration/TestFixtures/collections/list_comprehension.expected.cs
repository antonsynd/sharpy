#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

namespace Sharpy
{
    public static partial class Program
    {
        public static void Main()
        {
#line 3 "list_comprehension.spy"
            System.Collections.Generic.List<int> result = global::Sharpy.Builtins.Range(5).Select(x => x * 2).ToList();
#line 4 "list_comprehension.spy"
            foreach (var __loopVar_0 in result)
            {
                var item = __loopVar_0;
#line 5 "list_comprehension.spy"
                global::Sharpy.Builtins.Print(item);
            }

#line 6 "list_comprehension.spy"
            global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Len(result));
        }
    }
}
