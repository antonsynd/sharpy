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
        public Sharpy.Str Name;
        public double ToFahrenheit()
        {
#line 13 "class_with_init.spy"
            return this.Celsius * 9.0d / 5.0d + 32.0d;
        }

        public double ToKelvin()
        {
#line 16 "class_with_init.spy"
            return this.Celsius + 273.15d;
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

        public TemperatureConverter(double initialCelsius, Sharpy.Str scaleName)
        {
#line 8 "class_with_init.spy"
            this.Celsius = initialCelsius;
#line 9 "class_with_init.spy"
            this.Name = scaleName;
#line 10 "class_with_init.spy"
            global::Sharpy.Builtins.Print(this.Name);
        }
    }

    public static void Main()
    {
#line 26 "class_with_init.spy"
        var converter = new TemperatureConverter(0.0d, ((Sharpy.Str)"Water freezing point"));
#line 27 "class_with_init.spy"
        global::Sharpy.Builtins.Print(converter.GetCelsius());
#line 28 "class_with_init.spy"
        global::Sharpy.Builtins.Print(converter.ToFahrenheit());
#line 29 "class_with_init.spy"
        global::Sharpy.Builtins.Print(converter.ToKelvin());
#line 31 "class_with_init.spy"
        converter.Adjust(100.0d);
#line 32 "class_with_init.spy"
        global::Sharpy.Builtins.Print(converter.GetCelsius());
#line 33 "class_with_init.spy"
        global::Sharpy.Builtins.Print(converter.ToFahrenheit());
#line 35 "class_with_init.spy"
        var second = new TemperatureConverter(25.0d, ((Sharpy.Str)"Room temperature"));
#line 36 "class_with_init.spy"
        global::Sharpy.Builtins.Print(second.GetCelsius());
#line 37 "class_with_init.spy"
        global::Sharpy.Builtins.Print(second.ToKelvin());
    }
}
