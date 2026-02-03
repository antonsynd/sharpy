#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.FunctionCallingFunctionComplex
{
    public static class Program
    {
        public static int ProcessCalculation(Calculator calc, int val1, int val2)
        {
#line 40 "function_calling_function_complex.spy"
            global::Sharpy.Core.Exports.Print(calc.GetName());
#line 41 "function_calling_function_complex.spy"
            int result = calc.Calculate(val1, val2);
#line 42 "function_calling_function_complex.spy"
            return result;
        }

        public static int ApplyOperation(int a, int b, bool useAdd)
        {
#line 45 "function_calling_function_complex.spy"
            if (useAdd)
            {
#line 46 "function_calling_function_complex.spy"
                Calculator adder = new Adder();
#line 47 "function_calling_function_complex.spy"
                return ProcessCalculation(adder, a, b);
            }
            else
            {
#line 49 "function_calling_function_complex.spy"
                Calculator multiplier = new Multiplier();
#line 50 "function_calling_function_complex.spy"
                return ProcessCalculation(multiplier, a, b);
            }
        }

        public static int ComputeWithModifier(int @base, int modifier)
        {
#line 53 "function_calling_function_complex.spy"
            int intermediate = ApplyOperation(@base, modifier, true);
#line 54 "function_calling_function_complex.spy"
            global::Sharpy.Core.Exports.Print(intermediate);
#line 55 "function_calling_function_complex.spy"
            int final = ApplyOperation(intermediate, 2, false);
#line 56 "function_calling_function_complex.spy"
            return final;
        }

        public static void RunDemo()
        {
#line 59 "function_calling_function_complex.spy"
            global::Sharpy.Core.Exports.Print(100);
#line 61 "function_calling_function_complex.spy"
            int result1 = ComputeWithModifier(5, 3);
#line 62 "function_calling_function_complex.spy"
            global::Sharpy.Core.Exports.Print(result1);
#line 64 "function_calling_function_complex.spy"
            global::Sharpy.Core.Exports.Print(200);
#line 66 "function_calling_function_complex.spy"
            int result2 = ComputeWithModifier(10, 7);
#line 67 "function_calling_function_complex.spy"
            global::Sharpy.Core.Exports.Print(result2);
#line 69 "function_calling_function_complex.spy"
            global::Sharpy.Core.Exports.Print(300);
        }

        public static void Main()
        {
#line 72 "function_calling_function_complex.spy"
            RunDemo();
        }
    }

    public abstract class Calculator
    {
        public string Name;
        public abstract int Calculate(int a, int b);
        public string GetName()
        {
#line 15 "function_calling_function_complex.spy"
            return this.Name;
        }

        public Calculator(string calcName)
        {
#line 8 "function_calling_function_complex.spy"
            this.Name = calcName;
        }
    }

    public class Adder : Calculator
    {
        public override int Calculate(int a, int b)
        {
#line 23 "function_calling_function_complex.spy"
            return this.AddValues(a, b);
        }

        public int AddValues(int x, int y)
        {
#line 26 "function_calling_function_complex.spy"
            return x + y;
        }

        public Adder() : base("Addition Calculator")
        {
        }
    }

    public class Multiplier : Calculator
    {
        public override int Calculate(int a, int b)
        {
#line 34 "function_calling_function_complex.spy"
            return this.MultiplyValues(a, b);
        }

        public int MultiplyValues(int x, int y)
        {
#line 37 "function_calling_function_complex.spy"
            return x * y;
        }

        public Multiplier() : base("Multiplication Calculator")
        {
        }
    }
}
