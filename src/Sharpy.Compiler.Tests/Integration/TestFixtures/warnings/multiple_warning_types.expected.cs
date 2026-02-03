#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.MultipleWarningTypes
{
    public static class Program
    {
        public static int Foo()
        {
#line 3 "multiple_warning_types.spy"
            int unused = 99;
#line 4 "multiple_warning_types.spy"
            return 42;
#line 5 "multiple_warning_types.spy"
            global::Sharpy.Core.Exports.Print("unreachable");
        }

        public static void Main()
        {
#line 8 "multiple_warning_types.spy"
            global::Sharpy.Core.Exports.Print(Foo());
        }
    }
}
