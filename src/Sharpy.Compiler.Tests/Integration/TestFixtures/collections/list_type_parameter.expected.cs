#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ListTypeParameter
{
    public static class Program
    {
        public static int SumNumbers(System.Collections.Generic.List<int> numbers)
        {
#line 4 "list_type_parameter.spy"
            int total = 0;
#line 5 "list_type_parameter.spy"
            foreach (var __loopVar_0 in numbers)
            {
                var n = __loopVar_0;
#line 6 "list_type_parameter.spy"
                total = total + n;
            }

#line 7 "list_type_parameter.spy"
            return total;
        }

        public static void Main()
        {
#line 10 "list_type_parameter.spy"
            System.Collections.Generic.List<int> nums = new System.Collections.Generic.List<int>()
            {
                1,
                2,
                3,
                4,
                5
            };
#line 11 "list_type_parameter.spy"
            int result = SumNumbers(nums);
#line 12 "list_type_parameter.spy"
            global::Sharpy.Core.Exports.Print(result);
        }
    }
}
