#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.FloatVariables0009
{
    public static class Program
    {
        public static double ComputeArea(double radius)
        {
#line 4 "float_variables_0009.spy"
            double pi = 3.14159;
#line 5 "float_variables_0009.spy"
            return pi * radius * radius;
        }

        public static double ConvertTemperature(double celsius)
        {
#line 9 "float_variables_0009.spy"
            double factor = 1.8;
#line 10 "float_variables_0009.spy"
            double offset = 32;
#line 11 "float_variables_0009.spy"
            return celsius * factor + offset;
        }

        public static double X = 10.5;
        public static double Y = 3.2;
        public static double SumVal = X + Y;
        public static double DiffVal = X - Y;
        public static double ProdVal = X * Y;
        public static double QuotVal = X / Y;
        public static double CircleArea = ComputeArea(2);
        public static double TempF = ConvertTemperature(25);
        public static double Threshold = 50;
        public static double Accumulator = 1;
        public static void Main()
        {
#line 26 "float_variables_0009.spy"
            global::Sharpy.Core.Exports.Print(X);
#line 27 "float_variables_0009.spy"
            global::Sharpy.Core.Exports.Print(Y);
#line 31 "float_variables_0009.spy"
            global::Sharpy.Core.Exports.Print(SumVal);
#line 32 "float_variables_0009.spy"
            global::Sharpy.Core.Exports.Print(DiffVal);
#line 33 "float_variables_0009.spy"
            global::Sharpy.Core.Exports.Print(ProdVal);
#line 34 "float_variables_0009.spy"
            global::Sharpy.Core.Exports.Print(QuotVal);
#line 37 "float_variables_0009.spy"
            global::Sharpy.Core.Exports.Print(CircleArea);
#line 39 "float_variables_0009.spy"
            global::Sharpy.Core.Exports.Print(TempF);
#line 42 "float_variables_0009.spy"
            if (TempF > Threshold)
            {
#line 43 "float_variables_0009.spy"
                global::Sharpy.Core.Exports.Print(true);
            }
            else
            {
#line 45 "float_variables_0009.spy"
                global::Sharpy.Core.Exports.Print(false);
            }

#line 48 "float_variables_0009.spy"
            Accumulator = Accumulator * 2.5;
#line 49 "float_variables_0009.spy"
            Accumulator = Accumulator + 0.5;
#line 50 "float_variables_0009.spy"
            global::Sharpy.Core.Exports.Print(Accumulator);
        }
    }
}
