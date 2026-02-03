#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.UnderscorePrefixNoWarn
{
    public static class Program
    {
        public static void Main()
        {
#line 3 "underscore_prefix_no_warn.spy"
            int _unused = 42;
#line 4 "underscore_prefix_no_warn.spy"
            global::Sharpy.Core.Exports.Print("ok");
        }
    }
}
