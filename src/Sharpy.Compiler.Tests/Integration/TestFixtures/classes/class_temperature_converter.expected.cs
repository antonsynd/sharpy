#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ClassTemperatureConverter
{
    public static class Program
    {
        public static int Temp1 = 0;
        public static int Temp2 = 100;
        public static int Temp3 = 25;
        public static int Temp4 = -10;
        public static void Main()
        {
#line 30 "class_temperature_converter.spy"
            global::Sharpy.Core.Exports.Print(TemperatureConverter.CelsiusToFahrenheit(Temp1));
#line 31 "class_temperature_converter.spy"
            global::Sharpy.Core.Exports.Print(TemperatureConverter.CelsiusToFahrenheit(Temp2));
#line 32 "class_temperature_converter.spy"
            global::Sharpy.Core.Exports.Print(TemperatureConverter.FahrenheitToCelsius(77));
#line 33 "class_temperature_converter.spy"
            global::Sharpy.Core.Exports.Print(TemperatureConverter.IsFreezing(Temp3));
#line 34 "class_temperature_converter.spy"
            global::Sharpy.Core.Exports.Print(TemperatureConverter.IsBoiling(Temp4));
        }
    }

    public class TemperatureConverter
    {
        public int FreezingPoint;
        public int BoilingPoint;
        public static int CelsiusToFahrenheit(int celsius)
        {
#line 12 "class_temperature_converter.spy"
            return ((int)System.Math.Floor((double)((double)(celsius * 9) / 5))) + 32;
        }

        public static int FahrenheitToCelsius(int fahrenheit)
        {
#line 15 "class_temperature_converter.spy"
            return (int)System.Math.Floor((double)((double)((fahrenheit - 32) * 5) / 9));
        }

        public static bool IsFreezing(int celsius)
        {
#line 18 "class_temperature_converter.spy"
            return celsius <= 0;
        }

        public static bool IsBoiling(int celsius)
        {
#line 21 "class_temperature_converter.spy"
            return celsius >= 100;
        }

        public TemperatureConverter()
        {
#line 8 "class_temperature_converter.spy"
            this.FreezingPoint = 0;
#line 9 "class_temperature_converter.spy"
            this.BoilingPoint = 100;
        }
    }
}
