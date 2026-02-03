#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.FunctionSquareAddSquares
{
    public static class Program
    {
        public static int Square(int n)
        {
#line 3 "function_square_add_squares.spy"
            return n * n;
        }

        public static int AddSquares(int a, int b)
        {
#line 6 "function_square_add_squares.spy"
            return Square(a) + Square(b);
        }

        public static int Result1 = Square(7);
        public static int Result2 = AddSquares(3, 4);
        public static void Main()
        {
#line 12 "function_square_add_squares.spy"
            global::Sharpy.Core.Exports.Print(Result1);
#line 13 "function_square_add_squares.spy"
            global::Sharpy.Core.Exports.Print(Result2);
        }
    }
}
