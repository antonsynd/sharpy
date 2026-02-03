#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ClassStaticMethods0012
{
    public static class Program
    {
        public static void Main()
        {
#line 12 "class_static_methods_0012.spy"
            var result1 = MathHelper.Square(4);
#line 13 "class_static_methods_0012.spy"
            var result2 = MathHelper.Cube(3);
#line 14 "class_static_methods_0012.spy"
            global::Sharpy.Core.Exports.Print(result1);
#line 15 "class_static_methods_0012.spy"
            global::Sharpy.Core.Exports.Print(result2);
        }
    }

    public class MathHelper
    {
        public int PiApprox = 3;
        public static int Square(int n)
        {
#line 6 "class_static_methods_0012.spy"
            return n * n;
        }

        public static int Cube(int n)
        {
#line 9 "class_static_methods_0012.spy"
            return n * n * n;
        }
    }
}
