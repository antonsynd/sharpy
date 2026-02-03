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
#line 4 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/warnings/all_imports_used/main.spy"
            global::Sharpy.Core.Exports.Print(Add(2, 3));
#line 5 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/warnings/all_imports_used/main.spy"
            global::Sharpy.Core.Exports.Print(Multiply(4, 5));
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
        public static int Add(int a, int b)
        {
#line 2 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/warnings/all_imports_used/helpers.spy"
            return a + b;
        }

        public static int Multiply(int a, int b)
        {
#line 5 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/warnings/all_imports_used/helpers.spy"
            return a * b;
        }
    }
}
