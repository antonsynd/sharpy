#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace GenericInstanceMethodExplicitTypeArgs1133
{
    public static partial class GenericInstanceMethodExplicitTypeArgs1133Module
    {
        public static void Main()
        {
#line (24, 5) - (24, 20) 12 "generic_instance_method_explicit_type_args_1133.spy"
            global::GenericInstanceMethodExplicitTypeArgs1133.Box b = new global::GenericInstanceMethodExplicitTypeArgs1133.Box();
#line (25, 5) - (25, 29) 12 "generic_instance_method_explicit_type_args_1133.spy"
            global::Sharpy.Builtins.Print(b.Convert<int>(5));
#line (26, 5) - (26, 33) 12 "generic_instance_method_explicit_type_args_1133.spy"
            global::Sharpy.Builtins.Print(b.Describe<string>("hi"));
#line (27, 5) - (27, 26) 12 "generic_instance_method_explicit_type_args_1133.spy"
            global::GenericInstanceMethodExplicitTypeArgs1133.Holder h = new global::GenericInstanceMethodExplicitTypeArgs1133.Holder();
#line (28, 5) - (28, 34) 12 "generic_instance_method_explicit_type_args_1133.spy"
            global::Sharpy.Builtins.Print(h.Box.Convert<int>(99));
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Box")]
    public class Box
    {
        public string Convert<T>(T value)
#line 9 "generic_instance_method_explicit_type_args_1133.spy"
        {
#line (10, 9) - (10, 38) 12 "generic_instance_method_explicit_type_args_1133.spy"
            return FormattableString.Invariant($"converted: {(global::Sharpy.PyFormat.Apply(value, ""))}");
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

    [global::Sharpy.SharpyModuleType("__main__", "Holder")]
    public class Holder
    {
        public global::GenericInstanceMethodExplicitTypeArgs1133.Box Box;
        public Holder()
#line 19 "generic_instance_method_explicit_type_args_1133.spy"
        {
#line (20, 9) - (20, 25) 12 "generic_instance_method_explicit_type_args_1133.spy"
            this.Box = new global::GenericInstanceMethodExplicitTypeArgs1133.Box();
#line hidden
        }
    }
}
#line default
