#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ClassMathUtils
{
    public static class Program
    {
        public static int Result1 = MathUtils.Square(7);
        public static int Result2 = MathUtils.AddThree(2, 5, 8);
        public static void Main()
        {
#line 13 "class_math_utils.spy"
            global::Sharpy.Core.Exports.Print(Result1);
#line 15 "class_math_utils.spy"
            global::Sharpy.Core.Exports.Print(Result2);
        }
    }

    public class MathUtils
    {
        public static int Square(int x)
        {
#line 4 "class_math_utils.spy"
            return x * x;
        }

        public static int AddThree(int a, int b, int c)
        {
#line 7 "class_math_utils.spy"
            return a + b + c;
        }
    }
}
