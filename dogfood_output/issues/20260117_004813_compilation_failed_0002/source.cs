#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.Source
{
    public static class Program
    {
        public class FactorialCalculator
        {
            public int Limit;
            public int Calculate()
            {
                int result = 1;
                int i = 1;
                while (i <= this.Limit)
                {
                    result = result * i;
                    i = i + 1;
                }

                return result;
            }

            public void PrintSteps()
            {
                int product = 1;
                int step = 1;
                while (step <= this.Limit)
                {
                    product = product * step;
                    global::Sharpy.Core.Exports.Print(product);
                    step = step + 1;
                }
            }

            public FactorialCalculator(int n)
            {
                this.Limit = n;
                global::Sharpy.Core.Exports.Print("Calculator initialized");
            }
        }

        public static void Main()
        {
            var calc = new FactorialCalculator(5);
            global::Sharpy.Core.Exports.Print("Computing steps:");
            calc.PrintSteps();
            int finalResult = calc.Calculate();
            global::Sharpy.Core.Exports.Print("Final result:");
            global::Sharpy.Core.Exports.Print(finalResult);
        }
    }
}