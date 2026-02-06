#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

namespace Sharpy
{
    public static partial class Program
    {
        public static void Main()
        {
            global::Sharpy.Builtins.Print("Hello, World!");
            global::Sharpy.Builtins.Print("This is Sharpy!");
            global::Sharpy.Builtins.Print("Compiled to C# successfully!");
        }
    }
}