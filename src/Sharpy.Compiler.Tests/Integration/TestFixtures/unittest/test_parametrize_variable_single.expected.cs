#nullable enable

#pragma warning disable xUnit2009, xUnit2017

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
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static Sharpy.List<bool> FLAGS__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte FLAGS__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void FLAGS__init()
        {
            FLAGS__field = new Sharpy.List<bool>()
            {
                true,
                false,
                true
            };
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void FLAGS__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref FLAGS__state, "FLAGS", FLAGS__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref readonly Sharpy.List<bool> FLAGS
        {
            get
            {
                if (!__Swept.Done)
                    FLAGS__slow();
                return ref FLAGS__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void FLAGS__ensure()
        {
            if (!__Swept.Done)
                FLAGS__slow();
        }

        public static void Main()
        {
            __Load();
#line (8, 5) - (8, 16) 12 "test_parametrize_variable_single.spy"
            global::Sharpy.Builtins.Print("ok");
#line hidden
        }

        public static global::System.Collections.Generic.IEnumerable<object[]> FLAGSMemberData => global::System.Linq.Enumerable.Select(FLAGS, row => new object[] { row });

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static global::Sharpy.StaticInitHost __host;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void __Sweep()
        {
            global::Sharpy.StaticInit.Run(ref FLAGS__state, "FLAGS", FLAGS__init);
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
