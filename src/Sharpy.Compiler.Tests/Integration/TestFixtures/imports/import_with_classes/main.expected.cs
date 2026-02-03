// math_utils.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.Test.MathUtils
{
    public static class Exports
    {
        public static int Square(int n)
        {
#line 4 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/import_with_classes/math_utils.spy"
            return n * n;
        }

        public static int Cube(int n)
        {
#line 7 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/import_with_classes/math_utils.spy"
            return n * n * n;
        }

        public static bool IsEven(int n)
        {
#line 10 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/import_with_classes/math_utils.spy"
            return n % 2 == 0;
        }

        public static int PI_APPROX = 3;
    }
}

// main.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;
using math_utils = Sharpy.Test.MathUtils.Exports;
using static Sharpy.Test.MathUtils.Exports;
using Sharpy.Test.MathUtils;

namespace Sharpy.Test.Main
{
    public static class Program
    {
        public static void ProcessNumbers(int limit)
        {
#line 7 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/import_with_classes/main.spy"
            global::Sharpy.Core.Exports.Print(limit);
#line 8 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/import_with_classes/main.spy"
            int count = 0;
#line 10 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/import_with_classes/main.spy"
            foreach (var __loopVar_0 in global::Sharpy.Core.Exports.Range(1, limit))
            {
                var i = __loopVar_0;
#line 11 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/import_with_classes/main.spy"
                if (IsEven(i))
                {
#line 12 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/import_with_classes/main.spy"
                    int result = Square(i);
#line 13 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/import_with_classes/main.spy"
                    global::Sharpy.Core.Exports.Print(result);
#line 14 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/import_with_classes/main.spy"
                    count = count + 1;
                }
            }

#line 16 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/import_with_classes/main.spy"
            global::Sharpy.Core.Exports.Print(count);
        }

        public static void DemonstrateModuleAccess()
        {
#line 19 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/import_with_classes/main.spy"
            int val = 5;
#line 20 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/import_with_classes/main.spy"
            int cubed = math_utils.Cube(val);
#line 21 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/import_with_classes/main.spy"
            global::Sharpy.Core.Exports.Print(cubed);
#line 23 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/import_with_classes/main.spy"
            int pi = math_utils.PI_APPROX;
#line 24 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/import_with_classes/main.spy"
            global::Sharpy.Core.Exports.Print(pi);
        }

        public static void Main()
        {
#line 27 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/import_with_classes/main.spy"
            global::Sharpy.Core.Exports.Print(1);
#line 28 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/import_with_classes/main.spy"
            ProcessNumbers(6);
#line 29 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/import_with_classes/main.spy"
            global::Sharpy.Core.Exports.Print(2);
#line 30 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/import_with_classes/main.spy"
            DemonstrateModuleAccess();
#line 31 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/import_with_classes/main.spy"
            global::Sharpy.Core.Exports.Print(3);
        }
    }
}
