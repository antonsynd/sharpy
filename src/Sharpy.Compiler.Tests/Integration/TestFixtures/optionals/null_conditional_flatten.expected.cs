#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace NullConditionalFlatten
{
    public static partial class NullConditionalFlattenModule
    {
        public static void Main()
        {
#line (25, 5) - (25, 61) 12 "null_conditional_flatten.spy"
            Optional<global::NullConditionalFlatten.Outer> o1 = Optional<global::NullConditionalFlatten.Outer>.Some(new global::NullConditionalFlatten.Outer(Optional<global::NullConditionalFlatten.Inner>.Some(new global::NullConditionalFlatten.Inner(1, Optional<string>.Some("hello")))));
#line (26, 5) - (26, 45) 12 "null_conditional_flatten.spy"
            Optional<string> r1 = ((o1).IsSome ? o1.Unwrap().GetInner() : Optional<global::NullConditionalFlatten.Inner>.None) is var __opt_0 && (__opt_0).IsSome ? __opt_0.Unwrap().GetLabel() : Optional<string>.None;
#line (27, 5) - (27, 14) 12 "null_conditional_flatten.spy"
            global::Sharpy.Builtins.Print(r1);
#line (30, 5) - (30, 25) 12 "null_conditional_flatten.spy"
            Optional<global::NullConditionalFlatten.Outer> o2 = Optional<global::NullConditionalFlatten.Outer>.None;
#line (31, 5) - (31, 45) 12 "null_conditional_flatten.spy"
            Optional<string> r2 = ((o2).IsSome ? o2.Unwrap().GetInner() : Optional<global::NullConditionalFlatten.Inner>.None) is var __opt_1 && (__opt_1).IsSome ? __opt_1.Unwrap().GetLabel() : Optional<string>.None;
#line (32, 5) - (32, 14) 12 "null_conditional_flatten.spy"
            global::Sharpy.Builtins.Print(r2);
#line (35, 5) - (35, 38) 12 "null_conditional_flatten.spy"
            Optional<global::NullConditionalFlatten.Outer> o3 = Optional<global::NullConditionalFlatten.Outer>.Some(new global::NullConditionalFlatten.Outer(Optional<global::NullConditionalFlatten.Inner>.None));
#line (36, 5) - (36, 45) 12 "null_conditional_flatten.spy"
            Optional<string> r3 = ((o3).IsSome ? o3.Unwrap().GetInner() : Optional<global::NullConditionalFlatten.Inner>.None) is var __opt_2 && (__opt_2).IsSome ? __opt_2.Unwrap().GetLabel() : Optional<string>.None;
#line (37, 5) - (37, 14) 12 "null_conditional_flatten.spy"
            global::Sharpy.Builtins.Print(r3);
#line (40, 5) - (40, 54) 12 "null_conditional_flatten.spy"
            Optional<global::NullConditionalFlatten.Outer> o4 = Optional<global::NullConditionalFlatten.Outer>.Some(new global::NullConditionalFlatten.Outer(Optional<global::NullConditionalFlatten.Inner>.Some(new global::NullConditionalFlatten.Inner(2, Optional<string>.None))));
#line (41, 5) - (41, 45) 12 "null_conditional_flatten.spy"
            Optional<string> r4 = ((o4).IsSome ? o4.Unwrap().GetInner() : Optional<global::NullConditionalFlatten.Inner>.None) is var __opt_3 && (__opt_3).IsSome ? __opt_3.Unwrap().GetLabel() : Optional<string>.None;
#line (42, 5) - (42, 14) 12 "null_conditional_flatten.spy"
            global::Sharpy.Builtins.Print(r4);
#line (47, 5) - (47, 61) 12 "null_conditional_flatten.spy"
            Optional<global::NullConditionalFlatten.Outer> o5 = Optional<global::NullConditionalFlatten.Outer>.Some(new global::NullConditionalFlatten.Outer(Optional<global::NullConditionalFlatten.Inner>.Some(new global::NullConditionalFlatten.Inner(3, Optional<string>.Some("world")))));
#line (48, 5) - (48, 39) 12 "null_conditional_flatten.spy"
            Optional<string> r5 = ((o5).IsSome ? o5.Unwrap().GetInner() : Optional<global::NullConditionalFlatten.Inner>.None) is var __opt_4 && (__opt_4).IsSome ? __opt_4.Unwrap().Label : Optional<string>.None;
#line (49, 5) - (49, 14) 12 "null_conditional_flatten.spy"
            global::Sharpy.Builtins.Print(r5);
#line (52, 5) - (52, 54) 12 "null_conditional_flatten.spy"
            Optional<global::NullConditionalFlatten.Outer> o6 = Optional<global::NullConditionalFlatten.Outer>.Some(new global::NullConditionalFlatten.Outer(Optional<global::NullConditionalFlatten.Inner>.Some(new global::NullConditionalFlatten.Inner(4, Optional<string>.None))));
#line (53, 5) - (53, 39) 12 "null_conditional_flatten.spy"
            Optional<string> r6 = ((o6).IsSome ? o6.Unwrap().GetInner() : Optional<global::NullConditionalFlatten.Inner>.None) is var __opt_5 && (__opt_5).IsSome ? __opt_5.Unwrap().Label : Optional<string>.None;
#line (54, 5) - (54, 14) 12 "null_conditional_flatten.spy"
            global::Sharpy.Builtins.Print(r6);
#line (59, 5) - (59, 55) 12 "null_conditional_flatten.spy"
            Optional<global::NullConditionalFlatten.Outer> o7 = Optional<global::NullConditionalFlatten.Outer>.Some(new global::NullConditionalFlatten.Outer(Optional<global::NullConditionalFlatten.Inner>.Some(new global::NullConditionalFlatten.Inner(42, Optional<string>.None))));
#line (60, 5) - (60, 39) 12 "null_conditional_flatten.spy"
            Optional<int> r7 = ((o7).IsSome ? o7.Unwrap().GetInner() : Optional<global::NullConditionalFlatten.Inner>.None) is var __opt_6 && (__opt_6).IsSome ? Optional<int>.Some(__opt_6.Unwrap().Value) : Optional<int>.None;
#line (61, 5) - (61, 14) 12 "null_conditional_flatten.spy"
            global::Sharpy.Builtins.Print(r7);
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Inner")]
    public class Inner
    {
        public int Value;
        public Optional<string> Label;
        public Optional<string> GetLabel()
#line 9 "null_conditional_flatten.spy"
        {
#line (10, 9) - (10, 27) 12 "null_conditional_flatten.spy"
            return this.Label;
#line hidden
        }

        public Inner(int value, Optional<string> label)
#line 5 "null_conditional_flatten.spy"
        {
#line (6, 9) - (6, 27) 12 "null_conditional_flatten.spy"
            this.Value = value;
#line (7, 9) - (7, 27) 12 "null_conditional_flatten.spy"
            this.Label = label;
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Outer")]
    public class Outer
    {
        public Optional<global::NullConditionalFlatten.Inner> Inner;
        public Optional<global::NullConditionalFlatten.Inner> GetInner()
#line 18 "null_conditional_flatten.spy"
        {
#line (19, 9) - (19, 27) 12 "null_conditional_flatten.spy"
            return this.Inner;
#line hidden
        }

        public Outer(Optional<global::NullConditionalFlatten.Inner> inner)
#line 15 "null_conditional_flatten.spy"
        {
#line (16, 9) - (16, 27) 12 "null_conditional_flatten.spy"
            this.Inner = inner;
#line hidden
        }
    }
}
#line default
