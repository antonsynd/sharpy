// Snapshot: Basic print statement and program entry point
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class HelloWorld
{
    public static void Main()
    {
#line (2, 5) - (2, 27) 1 "hello_world.spy"
        global::Sharpy.Builtins.Print("Hello, World!");
    }
}
