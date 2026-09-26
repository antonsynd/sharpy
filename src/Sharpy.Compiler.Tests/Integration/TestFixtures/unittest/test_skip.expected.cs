#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;

namespace TestSkip
{
    public static partial class TestSkipModule
    {
        public static void Main()
        {
#line (17, 5) - (17, 16) 12 "test_skip.spy"
            global::Sharpy.Builtins.Print("ok");
#line hidden
        }
    }

    public partial class TestSkipModuleTests
    {
        [Xunit.FactAttribute(Skip = "work in progress")]
        public void TestSkipped()
        {
#line (4, 5) - (4, 18) 12 "test_skip.spy"
            Xunit.Assert.True(false);
#line hidden
        }

        [Xunit.FactAttribute(Skip = "always skipped")]
        public void TestSkippedIfTrue()
        {
#line (9, 5) - (9, 18) 12 "test_skip.spy"
            Xunit.Assert.True(false);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestRunsWhenSkipIfFalse()
        {
#line (14, 5) - (14, 17) 12 "test_skip.spy"
            Xunit.Assert.True(true);
#line hidden
        }
    }
}
#line default
