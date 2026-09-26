#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace SortedKeyLambda
{
    public static partial class SortedKeyLambdaModule
    {
        public static void Main()
        {
#line (3, 5) - (3, 64) 12 "sorted_key_lambda.spy"
            Sharpy.List<string> data = new Sharpy.List<string>()
#line hidden
            {
                "3",
                "1",
                "4",
                "1",
                "5",
                "9",
                "2",
                "6"
            };
#line (4, 5) - (4, 60) 12 "sorted_key_lambda.spy"
            Sharpy.List<string> result = global::Sharpy.Builtins.Sorted(data, key: x => global::Sharpy.Builtins.Int(x));
#line (5, 5) - (5, 18) 12 "sorted_key_lambda.spy"
            global::Sharpy.Builtins.Print(result);
#line hidden
        }
    }
}
#line default
