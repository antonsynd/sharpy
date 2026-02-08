// Snapshot: Function with default parameter values
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

public static partial class DefaultParams
{
    public static int CalculatePrice(int @base, double taxRate = 0.1, int discount = 0)
    {
#line 6 "default_params.spy"
        int baseCents = @base * 100;
#line 7 "default_params.spy"
        int taxCents = (int)System.Math.Floor((double)((double)((baseCents * global::Sharpy.Builtins.Int(taxRate * 100))) / 100));
#line 8 "default_params.spy"
        int totalCents = baseCents + taxCents;
#line 9 "default_params.spy"
        int discountCents = discount * 100;
#line 10 "default_params.spy"
        int finalCents = totalCents - discountCents;
#line 11 "default_params.spy"
        return (int)System.Math.Floor((double)((double)(finalCents) / 100));
    }

    public static string Greet(string name, string greeting = "Hello", string punctuation = "!")
    {
#line 15 "default_params.spy"
        return greeting;
    }

    public static int Power(int @base, int exponent = 2)
    {
#line 18 "default_params.spy"
        int result = 1;
#line 19 "default_params.spy"
        int i = 0;
#line 20 "default_params.spy"
        while (i < exponent)
        {
#line 21 "default_params.spy"
            result = result * @base;
#line 22 "default_params.spy"
            i = i + 1;
        }

#line 23 "default_params.spy"
        return result;
    }

    public static int Price1 = CalculatePrice(100);
    public static int Price2 = CalculatePrice(100, 0.2);
    public static int Price3 = CalculatePrice(100, 0.15, 10);
    public static int Squared = Power(5);
    public static int Cubed = Power(3, 3);
    public static int Price4 = CalculatePrice(50, 0.1, 5);
    public static int Price5 = CalculatePrice(200, 0.1, 20);
    public static void Main()
    {
#line 35 "default_params.spy"
        global::Sharpy.Builtins.Print(Price1);
#line 38 "default_params.spy"
        global::Sharpy.Builtins.Print(Price2);
#line 41 "default_params.spy"
        global::Sharpy.Builtins.Print(Price3);
#line 44 "default_params.spy"
        global::Sharpy.Builtins.Print(Squared);
#line 47 "default_params.spy"
        global::Sharpy.Builtins.Print(Cubed);
#line 50 "default_params.spy"
        global::Sharpy.Builtins.Print(Price4);
#line 53 "default_params.spy"
        global::Sharpy.Builtins.Print(Price5);
    }
}
