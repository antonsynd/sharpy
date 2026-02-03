#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.FloorDivisionFloatFunction
{
    public static class Program
    {
        public static double GetFloat()
        {
#line 6 "floor_division_float_function.spy"
            return 7.5;
        }

        public static double GetNegFloat()
        {
#line 9 "floor_division_float_function.spy"
            return -7.5;
        }

        public static void Main()
        {
#line 13 "floor_division_float_function.spy"
            global::Sharpy.Core.Exports.Print(System.Math.Floor((double)(GetFloat() / 2)));
#line 14 "floor_division_float_function.spy"
            global::Sharpy.Core.Exports.Print(System.Math.Floor((double)(GetFloat() / 2)));
#line 17 "floor_division_float_function.spy"
            global::Sharpy.Core.Exports.Print(System.Math.Floor((double)(7.5 / 2)));
#line 20 "floor_division_float_function.spy"
            global::Sharpy.Core.Exports.Print(System.Math.Floor((double)(GetNegFloat() / 2)));
#line 21 "floor_division_float_function.spy"
            global::Sharpy.Core.Exports.Print(System.Math.Floor((double)(GetNegFloat() / 2)));
        }
    }
}
