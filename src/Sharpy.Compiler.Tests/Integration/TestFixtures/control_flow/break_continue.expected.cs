#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.BreakContinue
{
    public static class Program
    {
        public static void Main()
        {
#line 4 "break_continue.spy"
            var i = 0;
#line 5 "break_continue.spy"
            while (i < 10)
            {
#line 6 "break_continue.spy"
                i = i + 1;
#line 7 "break_continue.spy"
                if (i == 3)
                {
#line 8 "break_continue.spy"
                    continue;
                }

#line 9 "break_continue.spy"
                if (i == 6)
                {
#line 10 "break_continue.spy"
                    break;
                }

#line 11 "break_continue.spy"
                global::Sharpy.Core.Exports.Print(i);
            }

#line 13 "break_continue.spy"
            global::Sharpy.Core.Exports.Print(100);
        }
    }
}
