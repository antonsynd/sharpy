#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;

namespace TestParametrizeVariableSingle
{
    public static partial class TestParametrizeVariableSingleModule
    {
        public static readonly Sharpy.List<bool> FLAGS = new Sharpy.List<bool>()
        {
            true,
            false,
            true
        };
        public static void Main()
        {
#line (8, 5) - (8, 16) 12 "test_parametrize_variable_single.spy"
            global::Sharpy.Builtins.Print("ok");
#line hidden
        }

        public static global::System.Collections.Generic.IEnumerable<object[]> FLAGSMemberData => global::System.Linq.Enumerable.Select(FLAGS, row => new object[] { row });
    }

    public partial class TestParametrizeVariableSingleModuleTests
    {
        [Xunit.TheoryAttribute]
        [Xunit.MemberDataAttribute(nameof(global::TestParametrizeVariableSingle.TestParametrizeVariableSingleModule.FLAGSMemberData), MemberType = typeof(global::TestParametrizeVariableSingle.TestParametrizeVariableSingleModule))]
        public void TestBool(bool flag)
        {
#line (5, 5) - (5, 42) 12 "test_parametrize_variable_single.spy"
            Xunit.Assert.True(flag == true || flag == false);
#line hidden
        }
    }
}
#line default
