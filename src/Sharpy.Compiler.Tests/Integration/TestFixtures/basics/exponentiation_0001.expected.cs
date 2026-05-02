// Snapshot: Exponentiation operator (**)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class Exponentiation0001
{
    public static void Main()
    {
#line (2, 5) - (2, 21) 1 "exponentiation_0001.spy"
        int x = (int)(System.Math.Pow(3, 2));
#line (3, 5) - (3, 13) 1 "exponentiation_0001.spy"
        global::Sharpy.Builtins.Print(x);
#line (4, 5) - (4, 22) 1 "exponentiation_0001.spy"
        int y = (int)(System.Math.Pow(2, 10));
#line (5, 5) - (5, 13) 1 "exponentiation_0001.spy"
        global::Sharpy.Builtins.Print(y);
#line (6, 5) - (6, 27) 1 "exponentiation_0001.spy"
        double z = System.Math.Pow(2.0d, 3.0d);
#line (7, 5) - (7, 13) 1 "exponentiation_0001.spy"
        global::Sharpy.Builtins.Print(z);
    }
}
