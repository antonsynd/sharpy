// Snapshot: Augmented assignment operators (+=, -=, *=)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class AugmentedAssignment0001
{
    public static int CurrentTemp = 20;
    public static void Main()
    {
#line (5, 5) - (5, 22) 1 "augmented_assignment_0001.spy"
        CurrentTemp = CurrentTemp + 5;
#line (6, 5) - (6, 24) 1 "augmented_assignment_0001.spy"
        global::Sharpy.Builtins.Print(CurrentTemp);
#line (8, 5) - (8, 22) 1 "augmented_assignment_0001.spy"
        CurrentTemp = CurrentTemp - 3;
#line (9, 5) - (9, 24) 1 "augmented_assignment_0001.spy"
        global::Sharpy.Builtins.Print(CurrentTemp);
#line (11, 5) - (11, 22) 1 "augmented_assignment_0001.spy"
        CurrentTemp = CurrentTemp * 2;
#line (12, 5) - (12, 24) 1 "augmented_assignment_0001.spy"
        global::Sharpy.Builtins.Print(CurrentTemp);
#line (14, 5) - (14, 23) 1 "augmented_assignment_0001.spy"
        CurrentTemp = (4 == 0 ? throw new global::Sharpy.ZeroDivisionError("integer division or modulo by zero") : (int)System.Math.Floor((double)((double)(CurrentTemp) / 4)));
#line (15, 5) - (15, 24) 1 "augmented_assignment_0001.spy"
        global::Sharpy.Builtins.Print(CurrentTemp);
#line (17, 5) - (17, 22) 1 "augmented_assignment_0001.spy"
        CurrentTemp = CurrentTemp % 7;
#line (18, 5) - (18, 24) 1 "augmented_assignment_0001.spy"
        global::Sharpy.Builtins.Print(CurrentTemp);
    }
}
