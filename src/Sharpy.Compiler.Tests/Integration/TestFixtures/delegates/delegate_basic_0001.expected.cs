#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace DelegateBasic0001
{
    public static partial class DelegateBasic0001Module
    {
        public static void Main()
        {
#line (5, 5) - (5, 52) 12 "delegate_basic_0001.spy"
            global::DelegateBasic0001.Greeter greet = name => "Hello, " + name;
#line (6, 5) - (6, 28) 12 "delegate_basic_0001.spy"
            var result = greet("World");
#line (7, 5) - (7, 18) 12 "delegate_basic_0001.spy"
            global::Sharpy.Builtins.Print(result);
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Greeter")]
    public delegate string Greeter(string name);
}
#line default
