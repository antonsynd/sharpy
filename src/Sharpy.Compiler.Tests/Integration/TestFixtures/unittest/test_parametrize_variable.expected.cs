#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;

namespace TestParametrizeVariable
{
    public static partial class TestParametrizeVariableModule
    {
        public static readonly Sharpy.List<global::System.ValueTuple<int, int, int>> TEST_DATA = new Sharpy.List<global::System.ValueTuple<int, int, int>>()
        {
            (1, 2, 3),
            (4, 5, 9),
            (10, 20, 30)
        };
        public static void Main()
        {
#line (8, 5) - (8, 16) 12 "test_parametrize_variable.spy"
            global::Sharpy.Builtins.Print("ok");
#line hidden
        }

        public static global::System.Collections.Generic.IEnumerable<object[]> TestDataMemberData => global::System.Linq.Enumerable.Select(TEST_DATA, row => new object[] { row.Item1, row.Item2, row.Item3 });
    }

    public partial class TestParametrizeVariableModuleTests
    {
        [Xunit.TheoryAttribute]
        [Xunit.MemberDataAttribute(nameof(global::TestParametrizeVariable.TestParametrizeVariableModule.TestDataMemberData), MemberType = typeof(global::TestParametrizeVariable.TestParametrizeVariableModule))]
        public void TestAdd(int a, int b, int expected)
        {
#line (5, 5) - (5, 30) 12 "test_parametrize_variable.spy"
            Xunit.Assert.Equal(expected, a + b);
#line hidden
        }
    }
}
#line default
