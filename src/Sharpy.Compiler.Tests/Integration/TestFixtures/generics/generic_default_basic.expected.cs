#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace GenericDefaultBasic
{
    public static partial class GenericDefaultBasicModule
    {
        public static void Main()
        {
#line (11, 5) - (11, 21) 12 "generic_default_basic.spy"
            var b = new global::GenericDefaultBasic.Box<int>(42);
#line (12, 5) - (12, 19) 12 "generic_default_basic.spy"
            global::Sharpy.Builtins.Print(b.Get());
#line (13, 5) - (13, 26) 12 "generic_default_basic.spy"
            var s = new global::GenericDefaultBasic.Box<string>("hello");
#line (14, 5) - (14, 19) 12 "generic_default_basic.spy"
            global::Sharpy.Builtins.Print(s.Get());
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Box")]
    public class Box<T>
    {
        public T Value;
        public T Get()
#line 7 "generic_default_basic.spy"
        {
#line (8, 9) - (8, 27) 12 "generic_default_basic.spy"
            return this.Value;
#line hidden
        }

        public Box(T value)
#line 4 "generic_default_basic.spy"
        {
#line (5, 9) - (5, 27) 12 "generic_default_basic.spy"
            this.Value = value;
#line hidden
        }
    }
}
#line default
