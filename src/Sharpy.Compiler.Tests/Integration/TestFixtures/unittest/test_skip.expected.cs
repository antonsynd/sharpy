#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;

public static partial class TestSkip
{
    public static void Main()
    {
#line (17, 5) - (17, 16) 1 "test_skip.spy"
        global::Sharpy.Builtins.Print("ok");
    }
}

public partial class TestSkipTests
{
    [Xunit.FactAttribute(Skip = "work in progress")]
    public void TestSkipped()
    {
#line (4, 5) - (4, 18) 1 "test_skip.spy"
        Xunit.Assert.True(false);
    }

    [Xunit.FactAttribute(Skip = "always skipped")]
    public void TestSkippedIfTrue()
    {
#line (9, 5) - (9, 18) 1 "test_skip.spy"
        Xunit.Assert.True(false);
    }

    [Xunit.FactAttribute]
    public void TestRunsWhenSkipIfFalse()
    {
#line (14, 5) - (14, 17) 1 "test_skip.spy"
        Xunit.Assert.True(true);
    }
}
