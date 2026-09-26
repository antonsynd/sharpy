// Snapshot: Augmented assignment operators (+=, -=, *=)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace AugmentedAssignment0001
{
    public static partial class AugmentedAssignment0001Module
    {
        public static int CurrentTemp = 20;
        public static void Main()
        {
#line (5, 5) - (5, 22) 12 "augmented_assignment_0001.spy"
            CurrentTemp = CurrentTemp + 5;
#line (6, 5) - (6, 24) 12 "augmented_assignment_0001.spy"
            global::Sharpy.Builtins.Print(global::AugmentedAssignment0001.AugmentedAssignment0001Module.CurrentTemp);
#line (8, 5) - (8, 22) 12 "augmented_assignment_0001.spy"
            CurrentTemp = CurrentTemp - 3;
#line (9, 5) - (9, 24) 12 "augmented_assignment_0001.spy"
            global::Sharpy.Builtins.Print(global::AugmentedAssignment0001.AugmentedAssignment0001Module.CurrentTemp);
#line (11, 5) - (11, 22) 12 "augmented_assignment_0001.spy"
            CurrentTemp = CurrentTemp * 2;
#line (12, 5) - (12, 24) 12 "augmented_assignment_0001.spy"
            global::Sharpy.Builtins.Print(global::AugmentedAssignment0001.AugmentedAssignment0001Module.CurrentTemp);
#line (14, 5) - (14, 23) 12 "augmented_assignment_0001.spy"
            CurrentTemp = global::Sharpy.Builtins.FloorDiv(CurrentTemp, 4);
#line (15, 5) - (15, 24) 12 "augmented_assignment_0001.spy"
            global::Sharpy.Builtins.Print(global::AugmentedAssignment0001.AugmentedAssignment0001Module.CurrentTemp);
#line (17, 5) - (17, 22) 12 "augmented_assignment_0001.spy"
            CurrentTemp = global::Sharpy.Builtins.FloorMod(CurrentTemp, 7);
#line (18, 5) - (18, 24) 12 "augmented_assignment_0001.spy"
            global::Sharpy.Builtins.Print(global::AugmentedAssignment0001.AugmentedAssignment0001Module.CurrentTemp);
#line hidden
        }
    }
}
#line default
