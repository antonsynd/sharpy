#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using static global::Sharpy.Unittest;
using Xunit;
using static CapturedOutputTest;

public static partial class CapturedOutputTest
{
    public static void Main()
    {
#line (12, 5) - (12, 16) 8 "captured_output_test.spy"
        global::Sharpy.Builtins.Print("ok");
#line hidden
    }
}

public partial class CapturedOutputTestTests
{
    [Xunit.FactAttribute]
    public void TestPrint()
    {
#line (7, 5) - (11, 1) 8 "captured_output_test.spy"
        using (var output = CapturedOutput())
#line hidden
        {
#line (8, 9) - (8, 23) 12 "captured_output_test.spy"
            global::Sharpy.Builtins.Print("hello");
#line (9, 9) - (9, 47) 12 "captured_output_test.spy"
            Xunit.Assert.Equal("hello\n", output.Getvalue());
#line hidden
        }
    }
}
#line default
