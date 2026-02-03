#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.Exponentiation0001
{
    public static class Program
    {
        public static void Main()
        {
#line 2 "exponentiation_0001.spy"
            int x = (int)(System.Math.Pow(3, 2));
#line 3 "exponentiation_0001.spy"
            global::Sharpy.Core.Exports.Print(x);
#line 4 "exponentiation_0001.spy"
            int y = (int)(System.Math.Pow(2, 10));
#line 5 "exponentiation_0001.spy"
            global::Sharpy.Core.Exports.Print(y);
#line 6 "exponentiation_0001.spy"
            double z = System.Math.Pow(2, 3);
#line 7 "exponentiation_0001.spy"
            global::Sharpy.Core.Exports.Print(z);
        }
    }
}
