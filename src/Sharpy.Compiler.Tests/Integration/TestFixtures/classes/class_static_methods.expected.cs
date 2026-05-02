// Snapshot: Class with @static methods
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class ClassStaticMethods
{
    public class MathHelper
    {
        public static int Add(int a, int b)
#line 3 "class_static_methods.spy"
        {
#line (4, 9) - (4, 22) 1 "class_static_methods.spy"
            return a + b;
        }

        public static int Square(int x)
#line 6 "class_static_methods.spy"
        {
#line (7, 9) - (7, 22) 1 "class_static_methods.spy"
            return x * x;
        }
    }

    public static void Main()
    {
#line (10, 5) - (10, 35) 1 "class_static_methods.spy"
        var result1 = MathHelper.Add(3, 7);
#line (11, 5) - (11, 19) 1 "class_static_methods.spy"
        global::Sharpy.Builtins.Print(result1);
#line (13, 5) - (13, 35) 1 "class_static_methods.spy"
        var result2 = MathHelper.Square(5);
#line (14, 5) - (14, 19) 1 "class_static_methods.spy"
        global::Sharpy.Builtins.Print(result2);
#line (16, 5) - (16, 42) 1 "class_static_methods.spy"
        var result3 = MathHelper.Add(result2, 10);
#line (17, 5) - (17, 19) 1 "class_static_methods.spy"
        global::Sharpy.Builtins.Print(result3);
    }
}
