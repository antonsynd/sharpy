#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

public static partial class ClassStaticMethods
{
    public class MathHelper
    {
        public static int Add(int a, int b)
        {
#line 4 "class_static_methods.spy"
            return a + b;
        }

        public static int Square(int x)
        {
#line 7 "class_static_methods.spy"
            return x * x;
        }
    }

    public static void Main()
    {
#line 10 "class_static_methods.spy"
        var result1 = MathHelper.Add(3, 7);
#line 11 "class_static_methods.spy"
        global::Sharpy.Builtins.Print(result1);
#line 13 "class_static_methods.spy"
        var result2 = MathHelper.Square(5);
#line 14 "class_static_methods.spy"
        global::Sharpy.Builtins.Print(result2);
#line 16 "class_static_methods.spy"
        var result3 = MathHelper.Add(result2, 10);
#line 17 "class_static_methods.spy"
        global::Sharpy.Builtins.Print(result3);
    }
}
