// Snapshot: Variable type inference
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class TypeInference
{
    public static void Main()
    {
#line (2, 5) - (2, 11) 1 "type_inference.spy"
        var x = 42;
#line (3, 5) - (3, 13) 1 "type_inference.spy"
        var y = 3.14d;
#line (4, 5) - (4, 16) 1 "type_inference.spy"
        var z = "hello";
#line (5, 5) - (5, 16) 1 "type_inference.spy"
        var flag = true;
#line (7, 5) - (7, 13) 1 "type_inference.spy"
        global::Sharpy.Builtins.Print(x);
#line (8, 5) - (8, 13) 1 "type_inference.spy"
        global::Sharpy.Builtins.Print(y);
#line (9, 5) - (9, 13) 1 "type_inference.spy"
        global::Sharpy.Builtins.Print(z);
#line (10, 5) - (10, 16) 1 "type_inference.spy"
        global::Sharpy.Builtins.Print(flag);
    }
}
