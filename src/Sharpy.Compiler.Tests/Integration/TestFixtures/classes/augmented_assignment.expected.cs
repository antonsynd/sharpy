#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.AugmentedAssignment
{
    public static class Program
    {
        public static int Counter = 0;
        public static void Main()
        {
#line 29 "augmented_assignment.spy"
            var acc = new Accumulator(100);
#line 30 "augmented_assignment.spy"
            global::Sharpy.Core.Exports.Print(acc.GetTotal());
#line 32 "augmented_assignment.spy"
            acc.Add(25);
#line 33 "augmented_assignment.spy"
            global::Sharpy.Core.Exports.Print(acc.GetTotal());
#line 35 "augmented_assignment.spy"
            acc.Subtract(10);
#line 36 "augmented_assignment.spy"
            global::Sharpy.Core.Exports.Print(acc.GetTotal());
#line 38 "augmented_assignment.spy"
            acc.Scale(2);
#line 39 "augmented_assignment.spy"
            acc.Scale(1.5);
#line 40 "augmented_assignment.spy"
            global::Sharpy.Core.Exports.Print(acc.GetMultiplier());
#line 42 "augmented_assignment.spy"
            foreach (var __loopVar_0 in global::Sharpy.Core.Exports.Range(5))
            {
                var i = __loopVar_0;
#line 43 "augmented_assignment.spy"
                Counter = Counter + i * 2;
            }

#line 45 "augmented_assignment.spy"
            global::Sharpy.Core.Exports.Print(Counter);
        }
    }

    public class Accumulator
    {
        public int Total;
        public double Multiplier;
        public void Add(int value)
        {
#line 12 "augmented_assignment.spy"
            this.Total = this.Total + value;
        }

        public void Subtract(int value)
        {
#line 15 "augmented_assignment.spy"
            this.Total = this.Total - value;
        }

        public void Scale(double factor)
        {
#line 18 "augmented_assignment.spy"
            this.Multiplier = this.Multiplier * factor;
        }

        public int GetTotal()
        {
#line 21 "augmented_assignment.spy"
            return this.Total;
        }

        public double GetMultiplier()
        {
#line 24 "augmented_assignment.spy"
            return this.Multiplier;
        }

        public Accumulator(int start)
        {
#line 8 "augmented_assignment.spy"
            this.Total = start;
#line 9 "augmented_assignment.spy"
            this.Multiplier = 1;
        }
    }
}
