// Snapshot: an operator under a wrapped tuple element into a narrowed Optional slot (plan-92249e T1, #2443)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace OperatorLoweringWrappedElement2443
{
    public static partial class OperatorLoweringWrappedElement2443Module
    {
        public static void Main()
        {
#line (8, 5) - (8, 11) 12 "operator_lowering_wrapped_element_2443.spy"
            var a = -7;
#line (9, 5) - (9, 10) 12 "operator_lowering_wrapped_element_2443.spy"
            var b = 2;
#line (10, 5) - (10, 23) 12 "operator_lowering_wrapped_element_2443.spy"
            Optional<int> p = Optional<int>.Some(0);
#line (11, 5) - (11, 23) 12 "operator_lowering_wrapped_element_2443.spy"
            Optional<int> r = Optional<int>.Some(0);
#line (12, 5) - (12, 10) 12 "operator_lowering_wrapped_element_2443.spy"
            var q = 0;
#line (13, 5) - (19, 20) 12 "operator_lowering_wrapped_element_2443.spy"
            if (p.IsSome && r.IsSome)
#line hidden
            {
#line (14, 9) - (14, 25) 16 "operator_lowering_wrapped_element_2443.spy"
                (p, q) = (Optional<int>.Some(global::Sharpy.Builtins.FloorDiv(a, b)), 1);
#line (15, 9) - (15, 20) 16 "operator_lowering_wrapped_element_2443.spy"
                global::Sharpy.Builtins.Print(p.Unwrap(), q);
#line (16, 9) - (16, 24) 16 "operator_lowering_wrapped_element_2443.spy"
                (r, q) = (Optional<int>.Some(global::Sharpy.Builtins.FloorMod(a, b)), 2);
#line (17, 9) - (17, 20) 16 "operator_lowering_wrapped_element_2443.spy"
                global::Sharpy.Builtins.Print(r.Unwrap(), q);
#line (18, 9) - (18, 26) 16 "operator_lowering_wrapped_element_2443.spy"
                (p, q) = (Optional<int>.Some(global::Sharpy.Builtins.FloorDiv(-a, b)), 3);
#line (19, 9) - (19, 20) 16 "operator_lowering_wrapped_element_2443.spy"
                global::Sharpy.Builtins.Print(p.Unwrap(), q);
#line hidden
            }
        }
    }
}
#line default
