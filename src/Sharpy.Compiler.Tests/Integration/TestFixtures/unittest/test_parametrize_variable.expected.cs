#nullable enable

#pragma warning disable xUnit2009, xUnit2017

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
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static Sharpy.List<global::System.ValueTuple<int, int, int>> TEST_DATA__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte TEST_DATA__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void TEST_DATA__init()
        {
            TEST_DATA__field = new Sharpy.List<global::System.ValueTuple<int, int, int>>()
            {
                (1, 2, 3),
                (4, 5, 9),
                (10, 20, 30)
            };
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void TEST_DATA__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref TEST_DATA__state, "TEST_DATA", TEST_DATA__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref readonly Sharpy.List<global::System.ValueTuple<int, int, int>> TEST_DATA
        {
            get
            {
                if (!__Swept.Done)
                    TEST_DATA__slow();
                return ref TEST_DATA__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void TEST_DATA__ensure()
        {
            if (!__Swept.Done)
                TEST_DATA__slow();
        }

        public static void Main()
        {
            __Load();
#line (8, 5) - (8, 16) 12 "test_parametrize_variable.spy"
            global::Sharpy.Builtins.Print("ok");
#line hidden
        }

        public static global::System.Collections.Generic.IEnumerable<object[]> TestDataMemberData => global::System.Linq.Enumerable.Select(TEST_DATA, row => new object[] { row.Item1, row.Item2, row.Item3 });

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static global::Sharpy.StaticInitHost __host;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void __Sweep()
        {
            global::Sharpy.StaticInit.Run(ref TEST_DATA__state, "TEST_DATA", TEST_DATA__init);
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static class __Swept
        {
            internal static readonly bool Done;
            static __Swept()
            {
                Done = global::Sharpy.StaticInit.TryLoad(ref __host, __Sweep);
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        [global::Sharpy.SharpyLoadEntry(true)]
        public static void __Load()
        {
            if (!__Swept.Done)
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
        }
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
