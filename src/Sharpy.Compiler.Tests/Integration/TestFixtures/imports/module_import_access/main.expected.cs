// main.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;
using calculator = Sharpy.Test.Calculator.Exports;

namespace Sharpy.Test.Main
{
    public static class Program
    {
        public static void Main()
        {
#line 7 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/module_import_access/main.spy"
            int result1 = calculator.Add(5, 3);
#line 8 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/module_import_access/main.spy"
            global::Sharpy.Core.Exports.Print(result1);
#line 10 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/module_import_access/main.spy"
            int result2 = calculator.Subtract(10, 4);
#line 11 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/module_import_access/main.spy"
            global::Sharpy.Core.Exports.Print(result2);
#line 13 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/module_import_access/main.spy"
            int result3 = calculator.Multiply(7, 6);
#line 14 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/module_import_access/main.spy"
            global::Sharpy.Core.Exports.Print(result3);
#line 17 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/module_import_access/main.spy"
            int @base = calculator.@BASE;
#line 18 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/module_import_access/main.spy"
            global::Sharpy.Core.Exports.Print(@base);
#line 21 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/module_import_access/main.spy"
            int step1 = calculator.Add(@base, 5);
#line 22 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/module_import_access/main.spy"
            int step2 = calculator.Multiply(step1, 2);
#line 23 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/module_import_access/main.spy"
            global::Sharpy.Core.Exports.Print(step2);
        }
    }
}

// calculator.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.Test.Calculator
{
    public static class Exports
    {
        public static int Add(int x, int y)
        {
#line 4 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/module_import_access/calculator.spy"
            return x + y;
        }

        public static int Subtract(int x, int y)
        {
#line 7 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/module_import_access/calculator.spy"
            return x - y;
        }

        public static int Multiply(int x, int y)
        {
#line 10 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/module_import_access/calculator.spy"
            return x * y;
        }

        public static int @BASE = 10;
    }
}
