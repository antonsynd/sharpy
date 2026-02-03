#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.Arithmetic
{
    public static class Program
    {
        public static int X = 10;
        public static int Y = 5;
        public static void Main()
        {
#line 5 "arithmetic.spy"
            global::Sharpy.Core.Exports.Print(X + Y);
#line 6 "arithmetic.spy"
            global::Sharpy.Core.Exports.Print(X - Y);
#line 7 "arithmetic.spy"
            global::Sharpy.Core.Exports.Print(X * Y);
#line 8 "arithmetic.spy"
            global::Sharpy.Core.Exports.Print((int)System.Math.Floor((double)((double)(X) / Y)));
#line 9 "arithmetic.spy"
            global::Sharpy.Core.Exports.Print(X % Y);
        }
    }
}
