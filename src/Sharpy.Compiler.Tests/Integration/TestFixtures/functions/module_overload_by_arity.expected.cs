// Snapshot: Module-level function overloading by arity
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class ModuleOverloadByArity
{
    public static string Greet(string name)
    {
#line (2, 5) - (2, 29) 1 "module_overload_by_arity.spy"
        return "Hello, " + name;
    }

    public static string Greet(string name, string greeting)
    {
#line (5, 5) - (5, 35) 1 "module_overload_by_arity.spy"
        return greeting + ", " + name;
    }

    public static string Greet(string name, string greeting, string punctuation)
    {
#line (8, 5) - (8, 49) 1 "module_overload_by_arity.spy"
        return greeting + ", " + name + punctuation;
    }

    public static void Main()
    {
#line (11, 5) - (11, 26) 1 "module_overload_by_arity.spy"
        global::Sharpy.Builtins.Print(Greet("World"));
#line (12, 5) - (12, 32) 1 "module_overload_by_arity.spy"
        global::Sharpy.Builtins.Print(Greet("World", "Hi"));
#line (13, 5) - (13, 38) 1 "module_overload_by_arity.spy"
        global::Sharpy.Builtins.Print(Greet("World", "Hey", "!"));
    }
}
