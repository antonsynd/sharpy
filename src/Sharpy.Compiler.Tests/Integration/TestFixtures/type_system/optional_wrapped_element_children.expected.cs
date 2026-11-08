// Snapshot: wrapped tuple elements whose own IR node is read through IrOptionalWrappedElement — comprehension, tuple equality, tuple/dict index, generator (plan-1faae3 D5)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace OptionalWrappedElementChildren
{
    public static partial class OptionalWrappedElementChildrenModule
    {
        public static void Main()
        {
#line (8, 5) - (8, 19) 12 "optional_wrapped_element_children.spy"
            var xs = new Sharpy.List<int>()
#line hidden
            {
                1,
                2,
                3
            };
#line (9, 5) - (9, 35) 12 "optional_wrapped_element_children.spy"
            global::System.ValueTuple<int, string> t = (1, "a");
#line (10, 5) - (10, 35) 12 "optional_wrapped_element_children.spy"
            global::System.ValueTuple<int, string> w = (5, "b");
#line (11, 5) - (11, 34) 12 "optional_wrapped_element_children.spy"
            Sharpy.Dict<string, int> d = new Sharpy.Dict<string, int>()
#line hidden
            {
                {
                    "k",
                    6
                }
            };
#line (12, 5) - (12, 31) 12 "optional_wrapped_element_children.spy"
            Optional<Sharpy.List<int>> a = Optional<Sharpy.List<int>>.Some(new Sharpy.List<int>() { 0 });
#line (13, 5) - (13, 28) 12 "optional_wrapped_element_children.spy"
            Optional<bool> g = Optional<bool>.Some(false);
#line (14, 5) - (14, 23) 12 "optional_wrapped_element_children.spy"
            Optional<int> n = Optional<int>.Some(0);
#line (15, 5) - (15, 23) 12 "optional_wrapped_element_children.spy"
            Optional<int> p = Optional<int>.Some(0);
#line (16, 5) - (16, 23) 12 "optional_wrapped_element_children.spy"
            Optional<int> q = Optional<int>.Some(0);
#line (17, 5) - (17, 41) 12 "optional_wrapped_element_children.spy"
            Optional<global::Sharpy.Iterator<int>> e = Optional<global::Sharpy.Iterator<int>>.Some(global::Sharpy.Builtins.Iter<int>(new Sharpy.List<int>() { 0 }));
#line (18, 5) - (27, 30) 12 "optional_wrapped_element_children.spy"
            if (a.IsSome && g.IsSome && n.IsSome)
#line hidden
            {
#line (19, 9) - (27, 30) 16 "optional_wrapped_element_children.spy"
                if (p.IsSome && q.IsSome && e.IsSome)
#line hidden
                {
                    Sharpy.List<int> __src_1 = xs;
                    var __comp_0 = new Sharpy.List<int>(((global::Sharpy.ISized)__src_1).Count);
                    foreach (var __loopVar_2 in __src_1)
                    {
                        var x = __loopVar_2;
                        __comp_0.Add(x * 2);
                    }

#line (20, 13) - (20, 42) 20 "optional_wrapped_element_children.spy"
                    (a, n) = (Optional<Sharpy.List<int>>.Some(__comp_0), Optional<int>.Some(4));
#line (21, 13) - (21, 24) 20 "optional_wrapped_element_children.spy"
                    global::Sharpy.Builtins.Print(a.Unwrap(), n.Unwrap());
#line (22, 13) - (22, 36) 20 "optional_wrapped_element_children.spy"
                    (g, n) = (Optional<bool>.Some(t.Equals((1, "a"))), Optional<int>.Some(9));
#line (23, 13) - (23, 24) 20 "optional_wrapped_element_children.spy"
                    global::Sharpy.Builtins.Print(g.Unwrap(), n.Unwrap());
#line (24, 13) - (24, 32) 20 "optional_wrapped_element_children.spy"
                    (p, q) = (Optional<int>.Some(w.Item1), Optional<int>.Some(d["k"]));
#line (25, 13) - (25, 24) 20 "optional_wrapped_element_children.spy"
                    global::Sharpy.Builtins.Print(p.Unwrap(), q.Unwrap());
#line (26, 13) - (26, 42) 20 "optional_wrapped_element_children.spy"
                    (e, n) = (Optional<global::Sharpy.Iterator<int>>.Some(global::Sharpy.Builtins.Iter<int>(xs.Select(x_1 => x_1 * 3))), Optional<int>.Some(5));
#line (27, 13) - (27, 30) 20 "optional_wrapped_element_children.spy"
                    global::Sharpy.Builtins.Print(new Sharpy.List<int>(e.Unwrap()), n.Unwrap());
#line hidden
                }
            }
        }
    }
}
#line default
