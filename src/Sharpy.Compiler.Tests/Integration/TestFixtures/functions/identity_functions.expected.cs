#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.IdentityFunctions
{
    public static class Program
    {
        public static int IdentityInt(int value)
        {
#line 5 "identity_functions.spy"
            return value;
        }

        public static string IdentityStr(string value)
        {
#line 8 "identity_functions.spy"
            return value;
        }

        public static bool IdentityBool(bool value)
        {
#line 11 "identity_functions.spy"
            return value;
        }

        public static void Main()
        {
#line 14 "identity_functions.spy"
            int x = IdentityInt(42);
#line 15 "identity_functions.spy"
            string y = IdentityStr("hello");
#line 16 "identity_functions.spy"
            bool z = IdentityBool(true);
#line 18 "identity_functions.spy"
            global::Sharpy.Core.Exports.Print(x);
#line 19 "identity_functions.spy"
            global::Sharpy.Core.Exports.Print(y);
#line 20 "identity_functions.spy"
            global::Sharpy.Core.Exports.Print(z);
        }
    }
}
