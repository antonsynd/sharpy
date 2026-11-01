#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace StaticInitSourceOrder0001
{
    public static partial class StaticInitSourceOrder0001Module
    {
        public static void Main()
        {
#line (15, 5) - (15, 18) 12 "static_init_source_order_0001.spy"
            global::Sharpy.Builtins.Print(global::StaticInitSourceOrder0001.Host.G);
#line (16, 5) - (16, 18) 12 "static_init_source_order_0001.spy"
            global::Sharpy.Builtins.Print(global::StaticInitSourceOrder0001.Host.C);
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Host")]
    public class Host
    {
        public static int A = 1;
        public static int Sp { get; set; } = 4;

        public static int G = global::StaticInitSourceOrder0001.Host.Sp;
        public static readonly int C = global::StaticInitSourceOrder0001.Host.Sp;
    }
}
#line default
