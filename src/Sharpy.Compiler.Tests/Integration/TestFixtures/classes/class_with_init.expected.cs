#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

namespace Sharpy.ClassWithInit
{
    public static class Program
    {
        public static void Main()
        {
#line 26 "class_with_init.spy"
            var converter = new TemperatureConverter(0, "Water freezing point");
#line 27 "class_with_init.spy"
            global::Sharpy.Builtins.Print(converter.GetCelsius());
#line 28 "class_with_init.spy"
            global::Sharpy.Builtins.Print(converter.ToFahrenheit());
#line 29 "class_with_init.spy"
            global::Sharpy.Builtins.Print(converter.ToKelvin());
#line 31 "class_with_init.spy"
            converter.Adjust(100);
#line 32 "class_with_init.spy"
            global::Sharpy.Builtins.Print(converter.GetCelsius());
#line 33 "class_with_init.spy"
            global::Sharpy.Builtins.Print(converter.ToFahrenheit());
#line 35 "class_with_init.spy"
            var second = new TemperatureConverter(25, "Room temperature");
#line 36 "class_with_init.spy"
            global::Sharpy.Builtins.Print(second.GetCelsius());
#line 37 "class_with_init.spy"
            global::Sharpy.Builtins.Print(second.ToKelvin());
        }
    }

    public class TemperatureConverter
    {
        public double Celsius;
        public string Name;
        public double ToFahrenheit()
        {
#line 13 "class_with_init.spy"
            return this.Celsius * 9 / 5 + 32;
        }

        public double ToKelvin()
        {
#line 16 "class_with_init.spy"
            return this.Celsius + 273.15;
        }

        public void Adjust(double delta)
        {
#line 19 "class_with_init.spy"
            this.Celsius = this.Celsius + delta;
        }

        public double GetCelsius()
        {
#line 22 "class_with_init.spy"
            return this.Celsius;
        }

        public TemperatureConverter(double initialCelsius, string scaleName)
        {
#line 8 "class_with_init.spy"
            this.Celsius = initialCelsius;
#line 9 "class_with_init.spy"
            this.Name = scaleName;
#line 10 "class_with_init.spy"
            global::Sharpy.Builtins.Print(this.Name);
        }
    }
}
