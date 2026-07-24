#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class GenericInstanceMethodExplicitTypeArgs1133
{
    public class Box
    {
        public string Convert<T>(T value)
#line 9 "generic_instance_method_explicit_type_args_1133.spy"
        {
#line (10, 9) - (10, 38) 12 "generic_instance_method_explicit_type_args_1133.spy"
            return FormattableString.Invariant($"converted: {(value)}");
#line hidden
        }

        public string Describe<U>(U value)
#line 12 "generic_instance_method_explicit_type_args_1133.spy"
        {
#line (13, 9) - (13, 39) 12 "generic_instance_method_explicit_type_args_1133.spy"
            return this.Convert<U>(value);
#line hidden
        }

        public Box()
#line 6 "generic_instance_method_explicit_type_args_1133.spy"
        {
#line (7, 9) - (7, 14) 12 "generic_instance_method_explicit_type_args_1133.spy"
            ;
#line hidden
        }
    }

    public class Holder
    {
        public Box Box;
        public Holder()
#line 19 "generic_instance_method_explicit_type_args_1133.spy"
        {
#line (20, 9) - (20, 25) 12 "generic_instance_method_explicit_type_args_1133.spy"
            this.Box = new Box();
#line hidden
        }
    }

    public static void Main()
    {
#line (24, 5) - (24, 20) 8 "generic_instance_method_explicit_type_args_1133.spy"
        Box b = new Box();
#line (25, 5) - (25, 29) 8 "generic_instance_method_explicit_type_args_1133.spy"
        global::Sharpy.Builtins.Print(b.Convert<int>(5));
#line (26, 5) - (26, 33) 8 "generic_instance_method_explicit_type_args_1133.spy"
        global::Sharpy.Builtins.Print(b.Describe<string>("hi"));
#line (27, 5) - (27, 26) 8 "generic_instance_method_explicit_type_args_1133.spy"
        Holder h = new Holder();
#line (28, 5) - (28, 34) 8 "generic_instance_method_explicit_type_args_1133.spy"
        global::Sharpy.Builtins.Print(h.Box.Convert<int>(99));
#line hidden
    }
}
#line default
