#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class FunctionStyleGetterSetter
{
    public class Temperature
    {
        protected double _Celsius;
        public double Celsius
        {
            get
            {
#line (8, 9) - (8, 30) 1 "function_style_getter_setter.spy"
                return this._Celsius;
            }

            set
            {
#line (11, 9) - (11, 30) 1 "function_style_getter_setter.spy"
                this._Celsius = value;
            }
        }

        public Temperature(double celsius)
#line 4 "function_style_getter_setter.spy"
        {
#line (5, 9) - (5, 32) 1 "function_style_getter_setter.spy"
            this._Celsius = celsius;
        }
    }

    public static void Main()
    {
#line (14, 5) - (14, 27) 1 "function_style_getter_setter.spy"
        var t = new Temperature(100.0d);
#line (15, 5) - (15, 21) 1 "function_style_getter_setter.spy"
        global::Sharpy.Builtins.Print(t.Celsius);
#line (16, 5) - (16, 21) 1 "function_style_getter_setter.spy"
        t.Celsius = 37.5d;
#line (17, 5) - (17, 21) 1 "function_style_getter_setter.spy"
        global::Sharpy.Builtins.Print(t.Celsius);
    }
}
