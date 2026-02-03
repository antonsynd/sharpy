#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.EnumToIntCoercion
{
    public static class Program
    {
        public static int GetColorValue(Color c)
        {
#line 8 "enum_to_int_coercion.spy"
            return (int)c;
        }

        public static Color Primary = Color.Red;
        public static Color Secondary = Color.Blue;
        public static void Main()
        {
#line 15 "enum_to_int_coercion.spy"
            global::Sharpy.Core.Exports.Print(GetColorValue(Primary));
#line 16 "enum_to_int_coercion.spy"
            global::Sharpy.Core.Exports.Print(GetColorValue(Secondary));
#line 18 "enum_to_int_coercion.spy"
            if (Primary == Color.Red)
            {
#line 19 "enum_to_int_coercion.spy"
                global::Sharpy.Core.Exports.Print(1);
            }
            else
            {
#line 21 "enum_to_int_coercion.spy"
                global::Sharpy.Core.Exports.Print(0);
            }
        }
    }

    public enum Color
    {
        Red = 1,
        Green = 2,
        Blue = 3
    }
}
