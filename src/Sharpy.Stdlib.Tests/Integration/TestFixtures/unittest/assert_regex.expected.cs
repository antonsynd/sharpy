#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;

namespace AssertRegex
{
    public static partial class AssertRegexModule
    {
        public static void Main()
        {
#line (12, 5) - (12, 16) 12 "assert_regex.spy"
            global::Sharpy.Builtins.Print("ok");
#line hidden
        }
    }

    public partial class AssertRegexModuleTests
    {
        [Xunit.FactAttribute]
        public void TestDateFormat()
        {
#line (5, 5) - (5, 53) 12 "assert_regex.spy"
            Xunit.Assert.Matches("\\d{4}-\\d{2}-\\d{2}", "2026-06-09");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSubstring()
        {
#line (9, 5) - (9, 41) 12 "assert_regex.spy"
            Xunit.Assert.Matches("world", "hello world");
#line hidden
        }
    }
}
#line default
