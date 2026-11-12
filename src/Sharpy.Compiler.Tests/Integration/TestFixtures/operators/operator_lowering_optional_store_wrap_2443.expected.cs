// Snapshot: augmented operators into a narrowed Optional slot: one statement is a store wrap AND an operator site (plan-92249e T1, #2443)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace OperatorLoweringOptionalStoreWrap2443
{
    public static partial class OperatorLoweringOptionalStoreWrap2443Module
    {
        public static void Main()
        {
#line (8, 5) - (8, 24) 12 "operator_lowering_optional_store_wrap_2443.spy"
            Optional<int> x = Optional<int>.Some(-7);
#line (9, 5) - (9, 27) 12 "operator_lowering_optional_store_wrap_2443.spy"
            Optional<double> y = Optional<double>.Some(1.0d);
#line (10, 5) - (17, 17) 12 "operator_lowering_optional_store_wrap_2443.spy"
            if (x.IsSome && y.IsSome)
#line hidden
            {
#line (11, 9) - (11, 16) 16 "operator_lowering_optional_store_wrap_2443.spy"
                x = Optional<int>.Some(global::Sharpy.Builtins.FloorDiv(x.Unwrap(), 2));
#line (12, 9) - (12, 17) 16 "operator_lowering_optional_store_wrap_2443.spy"
                global::Sharpy.Builtins.Print(x.Unwrap());
#line (13, 9) - (13, 15) 16 "operator_lowering_optional_store_wrap_2443.spy"
                x = Optional<int>.Some(global::Sharpy.Builtins.FloorMod(x.Unwrap(), 3));
#line (14, 9) - (14, 17) 16 "operator_lowering_optional_store_wrap_2443.spy"
                global::Sharpy.Builtins.Print(x.Unwrap());
#line (15, 9) - (15, 14) 16 "operator_lowering_optional_store_wrap_2443.spy"
                x = Optional<int>.Some(5);
#line (16, 9) - (16, 18) 16 "operator_lowering_optional_store_wrap_2443.spy"
                y = Optional<double>.Some((double)(x.Unwrap()) / 2);
#line (17, 9) - (17, 17) 16 "operator_lowering_optional_store_wrap_2443.spy"
                global::Sharpy.Builtins.Print(y.Unwrap());
#line hidden
            }
        }
    }
}
#line default
