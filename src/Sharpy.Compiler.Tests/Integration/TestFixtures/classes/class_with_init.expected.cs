// Snapshot: Class with __init__ constructor
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class ClassWithInit
{
    public class TemperatureConverter
    {
        public double Celsius;
        public string Name;
        public double ToFahrenheit()
#line 12 "class_with_init.spy"
        {
#line (13, 9) - (13, 48) 12 "class_with_init.spy"
            return this.Celsius * 9.0d / 5.0d + 32.0d;
#line hidden
        }

        public double ToKelvin()
#line 15 "class_with_init.spy"
        {
#line (16, 9) - (16, 38) 12 "class_with_init.spy"
            return this.Celsius + 273.15d;
#line hidden
        }

        public void Adjust(double delta)
#line 18 "class_with_init.spy"
        {
#line (19, 9) - (19, 30) 12 "class_with_init.spy"
            this.Celsius = this.Celsius + delta;
#line hidden
        }

        public double GetCelsius()
#line 21 "class_with_init.spy"
        {
#line (22, 9) - (22, 29) 12 "class_with_init.spy"
            return this.Celsius;
#line hidden
        }

        public TemperatureConverter(double initialCelsius, string scaleName)
#line 7 "class_with_init.spy"
        {
#line (8, 9) - (8, 39) 12 "class_with_init.spy"
            this.Celsius = initialCelsius;
#line (9, 9) - (9, 31) 12 "class_with_init.spy"
            this.Name = scaleName;
#line (10, 9) - (10, 25) 12 "class_with_init.spy"
            global::Sharpy.Builtins.Print(this.Name);
#line hidden
        }
    }

    public static void Main()
    {
#line (26, 5) - (26, 66) 8 "class_with_init.spy"
        var converter = new TemperatureConverter(0.0d, "Water freezing point");
#line (27, 5) - (27, 35) 8 "class_with_init.spy"
        global::Sharpy.Builtins.Print(converter.GetCelsius());
#line (28, 5) - (28, 37) 8 "class_with_init.spy"
        global::Sharpy.Builtins.Print(converter.ToFahrenheit());
#line (29, 5) - (29, 33) 8 "class_with_init.spy"
        global::Sharpy.Builtins.Print(converter.ToKelvin());
#line (31, 5) - (31, 28) 8 "class_with_init.spy"
        converter.Adjust(100.0d);
#line (32, 5) - (32, 35) 8 "class_with_init.spy"
        global::Sharpy.Builtins.Print(converter.GetCelsius());
#line (33, 5) - (33, 37) 8 "class_with_init.spy"
        global::Sharpy.Builtins.Print(converter.ToFahrenheit());
#line (35, 5) - (35, 60) 8 "class_with_init.spy"
        var second = new TemperatureConverter(25.0d, "Room temperature");
#line (36, 5) - (36, 32) 8 "class_with_init.spy"
        global::Sharpy.Builtins.Print(second.GetCelsius());
#line (37, 5) - (37, 30) 8 "class_with_init.spy"
        global::Sharpy.Builtins.Print(second.ToKelvin());
#line hidden
    }
}
#line default
