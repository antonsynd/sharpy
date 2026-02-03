#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.AugmentedAssignmentDivideMod
{
    public static class Program
    {
        public static int Value = 100;
        public static void Main()
        {
#line 5 "augmented_assignment_divide_mod.spy"
            Value = (int)System.Math.Floor((double)((double)(Value) / 3));
#line 6 "augmented_assignment_divide_mod.spy"
            global::Sharpy.Core.Exports.Print(Value);
#line 8 "augmented_assignment_divide_mod.spy"
            Value = Value % 10;
#line 9 "augmented_assignment_divide_mod.spy"
            global::Sharpy.Core.Exports.Print(Value);
#line 11 "augmented_assignment_divide_mod.spy"
            Value = Value * 4;
#line 12 "augmented_assignment_divide_mod.spy"
            global::Sharpy.Core.Exports.Print(Value);
        }
    }
}
