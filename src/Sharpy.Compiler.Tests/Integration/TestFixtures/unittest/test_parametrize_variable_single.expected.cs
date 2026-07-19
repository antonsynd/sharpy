#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;
using static TestParametrizeVariableSingle;

public static partial class TestParametrizeVariableSingle
{
    public static readonly Sharpy.List<bool> FLAGS = new Sharpy.List<bool>()
    {
        true,
        false,
        true
    };
    public static void Main()
    {
#line (8, 5) - (8, 16) 8 "test_parametrize_variable_single.spy"
        global::Sharpy.Builtins.Print("ok");
#line hidden
    }

    public static global::System.Collections.Generic.IEnumerable<object[]> FLAGSMemberData => global::System.Linq.Enumerable.Select(FLAGS, row => new object[] { row });
}

public partial class TestParametrizeVariableSingleTests
{
    [Xunit.TheoryAttribute]
    [Xunit.MemberDataAttribute(nameof(TestParametrizeVariableSingle.FLAGSMemberData), MemberType = typeof(TestParametrizeVariableSingle))]
    public void TestBool(bool flag)
    {
#line (5, 5) - (5, 42) 8 "test_parametrize_variable_single.spy"
        Xunit.Assert.True(flag == true || flag == false);
#line hidden
    }
}
#line default
