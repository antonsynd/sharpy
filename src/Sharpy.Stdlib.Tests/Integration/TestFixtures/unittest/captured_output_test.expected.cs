#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;

namespace CapturedOutputTest
{
    public static partial class CapturedOutputTestModule
    {
        public static void Main()
        {
#line (12, 5) - (12, 16) 12 "captured_output_test.spy"
            global::Sharpy.Builtins.Print("ok");
#line hidden
        }
    }

    public partial class CapturedOutputTestModuleTests
    {
        [Xunit.FactAttribute]
        public void TestPrint()
        {
#line (7, 5) - (9, 47) 12 "captured_output_test.spy"
            using (var output = global::Sharpy.Unittest.CapturedOutput())
#line hidden
            {
#line (8, 9) - (8, 23) 16 "captured_output_test.spy"
                global::Sharpy.Builtins.Print("hello");
#line (9, 9) - (9, 47) 16 "captured_output_test.spy"
                Xunit.Assert.Equal("hello\n", output.Getvalue());
#line hidden
            }
        }
    }
}
#line default
