#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.Source
{
    public static class Program
    {
        public interface IProcessor
        {
            int Process(int start, int end);
        }

        public abstract class BaseCalculator
        {
            public string Name;
            public abstract int Calculate(int start, int end);
            public virtual void DisplayName()
            {
                global::Sharpy.Core.Exports.Print(this.Name);
            }

            public BaseCalculator(string name)
            {
                this.Name = name;
            }
        }

        public class SumCalculator : BaseCalculator, IProcessor
        {
            public int Multiplier;
            public override int Calculate(int start, int end)
            {
                int total = 0;
                foreach (var __loopVar_0 in global::Sharpy.Core.Exports.Range(start, end))
                {
                    var i = __loopVar_0;
                    total = total + i * this.Multiplier;
                }

                return total;
            }

            public int Process(int start, int end)
            {
                return this.Calculate(start, end);
            }

            public SumCalculator(string name, int multiplier) : base(name)
            {
                this.Multiplier = multiplier;
            }
        }

        public class EvenSumCalculator : BaseCalculator, IProcessor
        {
            public override int Calculate(int start, int end)
            {
                int total = 0;
                foreach (var __loopVar_1 in global::Sharpy.Core.Exports.Range(start, end))
                {
                    var i = __loopVar_1;
                    if (i % 2 == 0)
                    {
                        total = total + i;
                    }
                }

                return total;
            }

            public int Process(int start, int end)
            {
                return this.Calculate(start, end);
            }

            public override void DisplayName()
            {
                global::Sharpy.Core.Exports.Print("EvenSum");
            }

            public EvenSumCalculator(string name) : base(name)
            {
            }
        }

        public static int RunProcessor(IProcessor proc, int start, int end)
        {
            return proc.Process(start, end);
        }

        public static int Count = 0;
        public static void Main()
        {
            var sumCalc = new SumCalculator("BasicSum", 2);
            sumCalc.DisplayName();
            int result1 = sumCalc.Calculate(1, 6);
            global::Sharpy.Core.Exports.Print(result1);
            var evenCalc = new EvenSumCalculator("EvenOnly");
            evenCalc.DisplayName();
            int result2 = evenCalc.Calculate(0, 10);
            global::Sharpy.Core.Exports.Print(result2);
            int result3 = RunProcessor(sumCalc, 5, 10);
            global::Sharpy.Core.Exports.Print(result3);
            int result4 = RunProcessor(evenCalc, 1, 8);
            global::Sharpy.Core.Exports.Print(result4);
            foreach (var __loopVar_2 in global::Sharpy.Core.Exports.Range(10, 15))
            {
                var j = __loopVar_2;
                if (j % 3 == 0)
                {
                    Count = Count + 1;
                }
            }

            global::Sharpy.Core.Exports.Print(Count);
        }
    }
}