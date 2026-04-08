// Snapshot: Decorator with string argument emits C# attribute
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class DecoratorArgsBasic0001
{
    [Obsolete("Use new_greet instead")]
    public static Sharpy.Str Greet()
    {
#line 3 "decorator_args_basic_0001.spy"
        return ((Sharpy.Str)"hello");
    }

    public static void Main()
    {
#line 6 "decorator_args_basic_0001.spy"
        global::Sharpy.Builtins.Print(Greet());
    }
}
