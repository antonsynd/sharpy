#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ComprehensionWithCondition
{
    public static class Program
    {
        public static void Main()
        {
#line 3 "comprehension_with_condition.spy"
            System.Collections.Generic.List<int> result = global::Sharpy.Core.Exports.Range(10).Where(x => x > 5).Select(x => x).ToList();
#line 4 "comprehension_with_condition.spy"
            foreach (var __loopVar_0 in result)
            {
                var item = __loopVar_0;
#line 5 "comprehension_with_condition.spy"
                global::Sharpy.Core.Exports.Print(item);
            }

#line 6 "comprehension_with_condition.spy"
            global::Sharpy.Core.Exports.Print(global::Sharpy.Core.Exports.Len(result));
        }
    }
}
