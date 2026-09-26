#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace NarrowingIsinstancePlainRead
{
    public static partial class NarrowingIsinstancePlainReadModule
    {
        public static object GetValue()
        {
#line (6, 5) - (6, 15) 12 "narrowing_isinstance_plain_read.spy"
            return 42;
#line hidden
        }

        public static void Main()
        {
#line (9, 5) - (9, 29) 12 "narrowing_isinstance_plain_read.spy"
            object r = global::NarrowingIsinstancePlainRead.NarrowingIsinstancePlainReadModule.GetValue();
#line (10, 5) - (10, 31) 12 "narrowing_isinstance_plain_read.spy"
            if (!((object?)r is int))
#line hidden
            {
                throw new global::Sharpy.AssertionError();
            }

#line (11, 5) - (12, 21) 12 "narrowing_isinstance_plain_read.spy"
            if ((object?)r is int)
#line hidden
            {
#line (12, 9) - (12, 21) 16 "narrowing_isinstance_plain_read.spy"
                global::Sharpy.Builtins.Print(((int)r!) + 1);
#line hidden
            }

#line (13, 5) - (13, 13) 12 "narrowing_isinstance_plain_read.spy"
            global::Sharpy.Builtins.Print(((int)r!));
#line hidden
        }
    }
}
#line default
