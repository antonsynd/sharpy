// Snapshot: a walrus rebinding a narrowed Optional slot wraps at its own site, (c = Optional<int>.Some(42)).Unwrap(); a walrus tuple element is its payload, and an unpacking element into a narrowed Optional target wraps around it
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace OptionalWalrusTupleElement
{
    public static partial class OptionalWalrusTupleElementModule
    {
        public static void Main()
        {
#line (8, 5) - (8, 23) 12 "optional_walrus_tuple_element.spy"
            Optional<int> c = Optional<int>.Some(1);
#line (9, 5) - (9, 23) 12 "optional_walrus_tuple_element.spy"
            Optional<int> p = Optional<int>.Some(2);
#line (10, 5) - (10, 23) 12 "optional_walrus_tuple_element.spy"
            Optional<int> q = Optional<int>.Some(3);
#line (11, 5) - (23, 20) 12 "optional_walrus_tuple_element.spy"
            if (c.IsSome && p.IsSome && q.IsSome)
#line hidden
            {
#line (12, 9) - (12, 28) 16 "optional_walrus_tuple_element.spy"
                global::Sharpy.Builtins.Print(((c = Optional<int>.Some(42)).Unwrap(), 1));
#line (13, 9) - (13, 25) 16 "optional_walrus_tuple_element.spy"
                global::System.ValueTuple<int, int> t = ((c = Optional<int>.Some(43)).Unwrap(), 2);
#line (14, 9) - (14, 17) 16 "optional_walrus_tuple_element.spy"
                global::Sharpy.Builtins.Print(t);
#line (15, 9) - (15, 30) 16 "optional_walrus_tuple_element.spy"
                global::Sharpy.Builtins.Print((((c = Optional<int>.Some(44)).Unwrap()), 3));
#line (16, 9) - (16, 28) 16 "optional_walrus_tuple_element.spy"
                (p, q) = (Optional<int>.Some((c = Optional<int>.Some(45)).Unwrap()), Optional<int>.Some(4));
#line (17, 9) - (17, 23) 16 "optional_walrus_tuple_element.spy"
                global::Sharpy.Builtins.Print(p.Unwrap(), q.Unwrap(), c.Unwrap());
#line (18, 9) - (19, 21) 16 "optional_walrus_tuple_element.spy"
                if (((c = Optional<int>.Some(46)).Unwrap()) > 0)
#line hidden
                {
#line (19, 13) - (19, 21) 20 "optional_walrus_tuple_element.spy"
                    global::Sharpy.Builtins.Print(c.Unwrap());
#line hidden
                }

#line (21, 9) - (21, 29) 16 "optional_walrus_tuple_element.spy"
                Sharpy.List<int> xs = new Sharpy.List<int>()
#line hidden
                {
                    7
                };
#line (22, 9) - (22, 30) 16 "optional_walrus_tuple_element.spy"
                (p, q) = (Optional<int>.Some(8), Optional<int>.Some(xs[-1]));
#line (23, 9) - (23, 20) 16 "optional_walrus_tuple_element.spy"
                global::Sharpy.Builtins.Print(p.Unwrap(), q.Unwrap());
#line hidden
            }
        }
    }
}
#line default
