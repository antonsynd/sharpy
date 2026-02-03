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
#line 4 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/simple_import_test/math_utils.spy"
            return n * n;
        }

        public static int MultiplyByTwo(int n)
        {
#line 7 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/simple_import_test/math_utils.spy"
            return n * 2;
        }
    }
}

// main.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;
using static Sharpy.Test.MathUtils.Exports;
using Sharpy.Test.MathUtils;

namespace Sharpy.Test.Main
{
    public static class Program
    {
        public static void Main()
        {
#line 5 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/simple_import_test/main.spy"
            int x = 5;
#line 6 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/simple_import_test/main.spy"
            global::Sharpy.Core.Exports.Print(Square(x));
#line 7 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/simple_import_test/main.spy"
            global::Sharpy.Core.Exports.Print(MultiplyByTwo(x));
        }
    }
}
