#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using static global::Sharpy.Unittest;
using Xunit;
using static AssertRegex;

public static partial class AssertRegex
{
    public static void Main()
    {
#line (12, 5) - (12, 16) 8 "assert_regex.spy"
        global::Sharpy.Builtins.Print("ok");
#line hidden
    }
}

public partial class AssertRegexTests
{
    [Xunit.FactAttribute]
    public void TestDateFormat()
    {
#line (5, 5) - (5, 53) 8 "assert_regex.spy"
        Xunit.Assert.Matches("\\d{4}-\\d{2}-\\d{2}", "2026-06-09");
#line hidden
    }

    [Xunit.FactAttribute]
    public void TestSubstring()
    {
#line (9, 5) - (9, 41) 8 "assert_regex.spy"
        Xunit.Assert.Matches("world", "hello world");
#line hidden
    }
}
#line default
