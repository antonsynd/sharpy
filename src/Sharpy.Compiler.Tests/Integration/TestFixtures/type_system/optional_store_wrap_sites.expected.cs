// Snapshot: a plain value stored into a narrowed Optional slot, member, array element or tuple target is wrapped in Optional<T>.Some(...); Optional values and `int | None` slots are not
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace OptionalStoreWrapSites
{
    public static partial class OptionalStoreWrapSitesModule
    {
        public static void Main()
        {
#line (17, 5) - (17, 23) 12 "optional_store_wrap_sites.spy"
            Optional<int> d = Optional<int>.Some(1);
#line (18, 5) - (20, 21) 12 "optional_store_wrap_sites.spy"
            if (d.IsSome)
#line hidden
            {
#line (19, 9) - (19, 14) 16 "optional_store_wrap_sites.spy"
                d = Optional<int>.Some(5);
#line (20, 9) - (20, 21) 16 "optional_store_wrap_sites.spy"
                global::Sharpy.Builtins.Print(d.Unwrap() + 1);
#line hidden
            }

#line (21, 5) - (21, 24) 12 "optional_store_wrap_sites.spy"
            Optional<int> x = Optional<int>.Some(10);
#line (22, 5) - (24, 17) 12 "optional_store_wrap_sites.spy"
            if (x.IsSome)
#line hidden
            {
#line (23, 9) - (23, 15) 16 "optional_store_wrap_sites.spy"
                x = Optional<int>.Some(x.Unwrap() + 5);
#line (24, 9) - (24, 17) 16 "optional_store_wrap_sites.spy"
                global::Sharpy.Builtins.Print(x.Unwrap());
#line hidden
            }

#line (25, 5) - (25, 22) 12 "optional_store_wrap_sites.spy"
            Optional<int> y = Optional<int>.None;
#line (26, 5) - (26, 13) 12 "optional_store_wrap_sites.spy"
            if (!y.IsSome)
#line hidden
                y = Optional<int>.Some(42);
#line (27, 5) - (27, 22) 12 "optional_store_wrap_sites.spy"
            global::Sharpy.Builtins.Print(y.Unwrap());
#line (28, 5) - (28, 22) 12 "optional_store_wrap_sites.spy"
            Optional<int> z = Optional<int>.None;
#line (29, 5) - (29, 18) 12 "optional_store_wrap_sites.spy"
            if (!z.IsSome)
#line hidden
                z = Optional<int>.Some(7);
#line (30, 5) - (30, 22) 12 "optional_store_wrap_sites.spy"
            global::Sharpy.Builtins.Print(z.Unwrap());
#line (31, 5) - (31, 14) 12 "optional_store_wrap_sites.spy"
            var b = new global::OptionalStoreWrapSites.Box();
#line (32, 5) - (32, 14) 12 "optional_store_wrap_sites.spy"
            if (!b.O.IsSome)
#line hidden
                b.O = Optional<int>.Some(3);
#line (33, 5) - (33, 24) 12 "optional_store_wrap_sites.spy"
            global::Sharpy.Builtins.Print(b.O.Unwrap());
#line (34, 5) - (34, 35) 12 "optional_store_wrap_sites.spy"
            Optional<int>[] arr = new Optional<int>[2];
#line (35, 5) - (35, 21) 12 "optional_store_wrap_sites.spy"
            global::Sharpy.ArrayHelpers.SetItem(arr, 1, Optional<int>.Some(2));
#line (36, 5) - (36, 17) 12 "optional_store_wrap_sites.spy"
            global::Sharpy.ArrayHelpers.SetItem(arr, 0, global::Sharpy.ArrayHelpers.GetItem(arr, 0).IsSome ? global::Sharpy.ArrayHelpers.GetItem(arr, 0) : Optional<int>.Some(9));
#line (37, 5) - (37, 17) 12 "optional_store_wrap_sites.spy"
            global::Sharpy.ArrayHelpers.SetItem(arr, 1, global::Sharpy.ArrayHelpers.GetItem(arr, 1).IsSome ? global::Sharpy.ArrayHelpers.GetItem(arr, 1) : Optional<int>.Some(8));
#line (38, 5) - (38, 27) 12 "optional_store_wrap_sites.spy"
            global::Sharpy.Builtins.Print(global::Sharpy.ArrayHelpers.GetItem(arr, 0).Unwrap());
#line (39, 5) - (39, 27) 12 "optional_store_wrap_sites.spy"
            global::Sharpy.Builtins.Print(global::Sharpy.ArrayHelpers.GetItem(arr, 1).Unwrap());
#line (40, 5) - (40, 23) 12 "optional_store_wrap_sites.spy"
            Optional<int> p = Optional<int>.Some(1);
#line (41, 5) - (41, 23) 12 "optional_store_wrap_sites.spy"
            Optional<int> q = Optional<int>.Some(2);
#line (42, 5) - (44, 21) 12 "optional_store_wrap_sites.spy"
            if (p.IsSome && q.IsSome)
#line hidden
            {
#line (43, 9) - (43, 22) 16 "optional_store_wrap_sites.spy"
                (p, q) = (Optional<int>.Some(30), Optional<int>.Some(40));
#line (44, 9) - (44, 21) 16 "optional_store_wrap_sites.spy"
                global::Sharpy.Builtins.Print(p.Unwrap() + q.Unwrap());
#line hidden
            }

#line (45, 5) - (45, 22) 12 "optional_store_wrap_sites.spy"
            Optional<int> g = Optional<int>.None;
#line hidden
            global::System.ValueTuple<Optional<int>, int> __t0 = (Optional<int>.Some(5), 1);
            g = __t0.Item1;
#line (46, 5) - (46, 22) 12 "optional_store_wrap_sites.spy"
            var k = __t0.Item2;
#line (47, 5) - (47, 26) 12 "optional_store_wrap_sites.spy"
            global::Sharpy.Builtins.Print(g.Unwrap() + k);
#line (48, 5) - (48, 23) 12 "optional_store_wrap_sites.spy"
            Optional<int> a = Optional<int>.Some(3);
#line (49, 5) - (49, 22) 12 "optional_store_wrap_sites.spy"
            Optional<int> w = Optional<int>.None;
#line (50, 5) - (51, 16) 12 "optional_store_wrap_sites.spy"
            if (a.IsSome)
#line hidden
            {
#line (51, 9) - (51, 16) 16 "optional_store_wrap_sites.spy"
                if (!w.IsSome)
#line hidden
                    w = Optional<int>.Some(a.Unwrap());
            }

#line (52, 5) - (52, 22) 12 "optional_store_wrap_sites.spy"
            global::Sharpy.Builtins.Print(w.Unwrap());
#line (53, 5) - (53, 23) 12 "optional_store_wrap_sites.spy"
            int? n = 4;
#line (54, 5) - (57, 17) 12 "optional_store_wrap_sites.spy"
            if (n is not null)
#line hidden
            {
#line (55, 9) - (55, 15) 16 "optional_store_wrap_sites.spy"
                n = n.Value + 1;
#line (56, 9) - (56, 14) 16 "optional_store_wrap_sites.spy"
                n = 7;
#line (57, 9) - (57, 17) 16 "optional_store_wrap_sites.spy"
                global::Sharpy.Builtins.Print(n.Value);
#line hidden
            }

#line (58, 5) - (58, 22) 12 "optional_store_wrap_sites.spy"
            Optional<int> v = Optional<int>.None;
#line (59, 5) - (59, 16) 12 "optional_store_wrap_sites.spy"
            v = Optional<int>.Some(4);
#line (60, 5) - (60, 22) 12 "optional_store_wrap_sites.spy"
            global::Sharpy.Builtins.Print(v.Unwrap());
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Box")]
    public class Box
    {
        public Optional<int> O;
        public Box()
#line 12 "optional_store_wrap_sites.spy"
        {
#line (13, 9) - (13, 24) 12 "optional_store_wrap_sites.spy"
            this.O = Optional<int>.None;
#line hidden
        }
    }
}
#line default
