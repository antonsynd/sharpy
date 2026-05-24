// Snapshot: Function with default parameter values
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class DefaultParams
{
    public static int CalculatePrice(int @base, double taxRate = 0.1d, int discount = 0)
    {
#line (6, 5) - (6, 34) 1 "default_params.spy"
        int baseCents = @base * 100;
#line (7, 5) - (7, 64) 1 "default_params.spy"
        int taxCents = (100 == 0 ? throw new global::Sharpy.ZeroDivisionError("integer division or modulo by zero") : (int)global::System.Math.Floor((double)((double)((baseCents * global::Sharpy.Builtins.Int(taxRate * 100))) / 100)));
#line (8, 5) - (8, 47) 1 "default_params.spy"
        int totalCents = baseCents + taxCents;
#line (9, 5) - (9, 42) 1 "default_params.spy"
        int discountCents = discount * 100;
#line (10, 5) - (10, 53) 1 "default_params.spy"
        int finalCents = totalCents - discountCents;
#line (11, 5) - (11, 31) 1 "default_params.spy"
        return (100 == 0 ? throw new global::Sharpy.ZeroDivisionError("integer division or modulo by zero") : (int)global::System.Math.Floor((double)((double)(finalCents) / 100)));
    }

    public static string Greet(string name, string greeting = "Hello", string punctuation = "!")
    {
#line (15, 5) - (15, 21) 1 "default_params.spy"
        return greeting;
    }

    public static int Power(int @base, int exponent = 2)
    {
#line (18, 5) - (18, 21) 1 "default_params.spy"
        int result = 1;
#line (19, 5) - (19, 16) 1 "default_params.spy"
        int i = 0;
#line (20, 5) - (23, 1) 1 "default_params.spy"
        while (i < exponent)
        {
#line (21, 9) - (21, 31) 1 "default_params.spy"
            result = result * @base;
#line (22, 9) - (22, 18) 1 "default_params.spy"
            i = i + 1;
        }

#line (23, 5) - (23, 19) 1 "default_params.spy"
        return result;
    }

    public static int Price1 = CalculatePrice(100);
    public static int Price2 = CalculatePrice(100, 0.2d);
    public static int Price3 = CalculatePrice(100, 0.15d, 10);
    public static int Squared = Power(5);
    public static int Cubed = Power(3, 3);
    public static int Price4 = CalculatePrice(50, 0.1d, 5);
    public static int Price5 = CalculatePrice(200, 0.1d, 20);
    public static void Main()
    {
#line (35, 5) - (35, 18) 1 "default_params.spy"
        global::Sharpy.Builtins.Print(Price1);
#line (38, 5) - (38, 18) 1 "default_params.spy"
        global::Sharpy.Builtins.Print(Price2);
#line (41, 5) - (41, 18) 1 "default_params.spy"
        global::Sharpy.Builtins.Print(Price3);
#line (44, 5) - (44, 19) 1 "default_params.spy"
        global::Sharpy.Builtins.Print(Squared);
#line (47, 5) - (47, 17) 1 "default_params.spy"
        global::Sharpy.Builtins.Print(Cubed);
#line (50, 5) - (50, 18) 1 "default_params.spy"
        global::Sharpy.Builtins.Print(Price4);
#line (53, 5) - (53, 18) 1 "default_params.spy"
        global::Sharpy.Builtins.Print(Price5);
    }
}
