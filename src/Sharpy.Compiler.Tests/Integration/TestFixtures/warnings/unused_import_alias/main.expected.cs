// main.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;
using static Sharpy.Test.Helpers.Exports;
using Sharpy.Test.Helpers;

namespace Sharpy.Test.Main
{
    public static class Program
    {
        public static void Main()
        {
#line 4 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/warnings/unused_import_alias/main.spy"
            global::Sharpy.Core.Exports.Print(Greet("world"));
        }
    }
}

// helpers.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.Test.Helpers
{
    public static class Exports
    {
        public static string Greet(string name)
        {
#line 2 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/warnings/unused_import_alias/helpers.spy"
            return "Hello, " + name;
        }

        public static string Farewell(string name)
        {
#line 5 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/warnings/unused_import_alias/helpers.spy"
            return "Goodbye, " + name;
        }
    }
}
