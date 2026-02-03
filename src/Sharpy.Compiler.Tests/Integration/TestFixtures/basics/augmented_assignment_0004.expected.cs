#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.AugmentedAssignment0004
{
    public static class Program
    {
        public static int X = 10;
        public static void Main()
        {
#line 5 "augmented_assignment_0004.spy"
            global::Sharpy.Core.Exports.Print(X);
#line 7 "augmented_assignment_0004.spy"
            X = X + 5;
#line 8 "augmented_assignment_0004.spy"
            global::Sharpy.Core.Exports.Print(X);
#line 10 "augmented_assignment_0004.spy"
            X = X - 3;
#line 11 "augmented_assignment_0004.spy"
            global::Sharpy.Core.Exports.Print(X);
#line 13 "augmented_assignment_0004.spy"
            X = X * 2;
#line 14 "augmented_assignment_0004.spy"
            global::Sharpy.Core.Exports.Print(X);
#line 16 "augmented_assignment_0004.spy"
            X = (int)System.Math.Floor((double)((double)(X) / 4));
#line 17 "augmented_assignment_0004.spy"
            global::Sharpy.Core.Exports.Print(X);
        }
    }
}
