#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class DelegateBasic0001
{
    public delegate string Greeter(string name);
    public static void Main()
    {
#line (5, 5) - (5, 52) 1 "delegate_basic_0001.spy"
        Greeter greet = name => "Hello, " + name;
#line (6, 5) - (6, 28) 1 "delegate_basic_0001.spy"
        var result = greet("World");
#line (7, 5) - (7, 18) 1 "delegate_basic_0001.spy"
        global::Sharpy.Builtins.Print(result);
    }
}
