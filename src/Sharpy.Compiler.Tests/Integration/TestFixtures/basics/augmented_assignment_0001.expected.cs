#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.AugmentedAssignment0001
{
    public static class Program
    {
        public static int CurrentTemp = 20;
        public static void Main()
        {
#line 5 "augmented_assignment_0001.spy"
            CurrentTemp = CurrentTemp + 5;
#line 6 "augmented_assignment_0001.spy"
            global::Sharpy.Core.Exports.Print(CurrentTemp);
#line 8 "augmented_assignment_0001.spy"
            CurrentTemp = CurrentTemp - 3;
#line 9 "augmented_assignment_0001.spy"
            global::Sharpy.Core.Exports.Print(CurrentTemp);
#line 11 "augmented_assignment_0001.spy"
            CurrentTemp = CurrentTemp * 2;
#line 12 "augmented_assignment_0001.spy"
            global::Sharpy.Core.Exports.Print(CurrentTemp);
#line 14 "augmented_assignment_0001.spy"
            CurrentTemp = (int)System.Math.Floor((double)((double)(CurrentTemp) / 4));
#line 15 "augmented_assignment_0001.spy"
            global::Sharpy.Core.Exports.Print(CurrentTemp);
#line 17 "augmented_assignment_0001.spy"
            CurrentTemp = CurrentTemp % 7;
#line 18 "augmented_assignment_0001.spy"
            global::Sharpy.Core.Exports.Print(CurrentTemp);
        }
    }
}
