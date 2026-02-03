#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.AugmentedAssignment0011
{
    public static class Program
    {
        public static int X = 10;
        public static void Main()
        {
#line 6 "augmented_assignment_0011.spy"
            global::Sharpy.Core.Exports.Print(X);
#line 8 "augmented_assignment_0011.spy"
            X = X + 5;
#line 9 "augmented_assignment_0011.spy"
            global::Sharpy.Core.Exports.Print(X);
#line 11 "augmented_assignment_0011.spy"
            X = X - 3;
#line 12 "augmented_assignment_0011.spy"
            global::Sharpy.Core.Exports.Print(X);
#line 14 "augmented_assignment_0011.spy"
            X = X * 2;
#line 15 "augmented_assignment_0011.spy"
            global::Sharpy.Core.Exports.Print(X);
#line 17 "augmented_assignment_0011.spy"
            X = (int)System.Math.Floor((double)((double)(X) / 4));
#line 18 "augmented_assignment_0011.spy"
            global::Sharpy.Core.Exports.Print(X);
        }
    }
}
