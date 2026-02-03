#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ClassStaticMethods0002
{
    public static class Program
    {
        public static int Result = MathHelper.Multiply(6, 7);
        public static bool Check = MathHelper.IsEven(Result);
        public static bool Check2 = MathHelper.IsEven(10);
        public static void Main()
        {
#line 16 "class_static_methods_0002.spy"
            global::Sharpy.Core.Exports.Print(Result);
#line 18 "class_static_methods_0002.spy"
            global::Sharpy.Core.Exports.Print(Check);
#line 20 "class_static_methods_0002.spy"
            global::Sharpy.Core.Exports.Print(Check2);
        }
    }

    public class MathHelper
    {
        public static int Multiply(int x, int y)
        {
#line 5 "class_static_methods_0002.spy"
            return x * y;
        }

        public static bool IsEven(int n)
        {
#line 8 "class_static_methods_0002.spy"
            return n % 2 == 0;
        }
    }
}
