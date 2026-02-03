#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.InterfaceMethodReturnType0001
{
    public static class Program
    {
        public static int RunCalculator(ICalculator calculator, int value)
        {
#line 19 "interface_method_return_type_0001.spy"
            return calculator.Calculate(value);
        }

        public static void Main()
        {
#line 23 "interface_method_return_type_0001.spy"
            ICalculator calculator = new SimpleCalculator(2);
#line 26 "interface_method_return_type_0001.spy"
            int result1 = calculator.Calculate(5);
#line 27 "interface_method_return_type_0001.spy"
            global::Sharpy.Core.Exports.Print(result1);
#line 30 "interface_method_return_type_0001.spy"
            int total = 0;
#line 31 "interface_method_return_type_0001.spy"
            total = total + RunCalculator(calculator, 3);
#line 32 "interface_method_return_type_0001.spy"
            total = total + RunCalculator(calculator, 4);
#line 33 "interface_method_return_type_0001.spy"
            global::Sharpy.Core.Exports.Print(total);
        }
    }

    public interface ICalculator
    {
        int Calculate(int x);
    }

    public class SimpleCalculator : ICalculator
    {
        public int Multiplier;
        public int Calculate(int x)
        {
#line 16 "interface_method_return_type_0001.spy"
            return x * this.Multiplier;
        }

        public SimpleCalculator(int m)
        {
#line 13 "interface_method_return_type_0001.spy"
            this.Multiplier = m;
        }
    }
}
