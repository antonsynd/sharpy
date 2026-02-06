#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

namespace Sharpy.TypeInference
{
    public static class Program
    {
        public static void Main()
        {
#line 2 "type_inference.spy"
            var x = 42;
#line 3 "type_inference.spy"
            var y = 3.14;
#line 4 "type_inference.spy"
            var z = "hello";
#line 5 "type_inference.spy"
            var flag = true;
#line 7 "type_inference.spy"
            global::Sharpy.Builtins.Print(x);
#line 8 "type_inference.spy"
            global::Sharpy.Builtins.Print(y);
#line 9 "type_inference.spy"
            global::Sharpy.Builtins.Print(z);
#line 10 "type_inference.spy"
            global::Sharpy.Builtins.Print(flag);
        }
    }
}
