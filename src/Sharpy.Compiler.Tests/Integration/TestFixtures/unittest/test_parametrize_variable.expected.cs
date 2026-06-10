#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;
using static TestParametrizeVariable;

public static partial class TestParametrizeVariable
{
    public static readonly Sharpy.List<global::System.ValueTuple<int, int, int>> TEST_DATA = new Sharpy.List<global::System.ValueTuple<int, int, int>>()
    {
        (1, 2, 3),
        (4, 5, 9),
        (10, 20, 30)
    };
    public static void Main()
    {
#line (8, 5) - (8, 16) 1 "test_parametrize_variable.spy"
        global::Sharpy.Builtins.Print("ok");
    }

    public static global::System.Collections.Generic.IEnumerable<object[]> TestDataMemberData => global::System.Linq.Enumerable.Select(TEST_DATA, row => new object[] { row.Item1, row.Item2, row.Item3 });
}

public partial class TestParametrizeVariableTests
{
    [Xunit.TheoryAttribute]
    [Xunit.MemberDataAttribute(nameof(TestParametrizeVariable.TestDataMemberData), MemberType = typeof(TestParametrizeVariable))]
    public void TestAdd(int a, int b, int expected)
    {
#line (5, 5) - (5, 30) 1 "test_parametrize_variable.spy"
        Xunit.Assert.Equal(expected, a + b);
    }
}
