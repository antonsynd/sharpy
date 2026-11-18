// Snapshot: Comparison operators (==, !=, <, >, <=, >=)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace ComparisonOperators
{
    public static partial class ComparisonOperatorsModule
    {
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int X__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte X__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void X__init()
        {
            X__field = 10;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void X__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref X__state, "x", X__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int X
        {
            get
            {
                if (!__Swept.Done)
                    X__slow();
                return ref X__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void X__ensure()
        {
            if (!__Swept.Done)
                X__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void X__storeSlow(int value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref X__state, ref X__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int X__set(int value)
        {
            if (__Swept.Done)
                X__field = value;
            else
                X__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? X__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref X__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int X__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = X__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref X__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R X__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                X__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int Y__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Y__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Y__init()
        {
            Y__field = 20;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Y__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Y__state, "y", Y__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int Y
        {
            get
            {
                if (!__Swept.Done)
                    Y__slow();
                return ref Y__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Y__ensure()
        {
            if (!__Swept.Done)
                Y__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Y__storeSlow(int value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Y__state, ref Y__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int Y__set(int value)
        {
            if (__Swept.Done)
                Y__field = value;
            else
                Y__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? Y__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Y__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int Y__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = Y__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Y__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Y__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                Y__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int Z__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Z__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Z__init()
        {
            Z__field = 10;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Z__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Z__state, "z", Z__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int Z
        {
            get
            {
                if (!__Swept.Done)
                    Z__slow();
                return ref Z__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Z__ensure()
        {
            if (!__Swept.Done)
                Z__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Z__storeSlow(int value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Z__state, ref Z__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int Z__set(int value)
        {
            if (__Swept.Done)
                Z__field = value;
            else
                Z__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? Z__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Z__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int Z__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = Z__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Z__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Z__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                Z__set(spill.Value!);
            return result;
        }

        public static void Main()
        {
            __Load();
#line (9, 5) - (9, 18) 12 "comparison_operators.spy"
            global::Sharpy.Builtins.Print(global::ComparisonOperators.ComparisonOperatorsModule.X == global::ComparisonOperators.ComparisonOperatorsModule.Z);
#line (10, 5) - (10, 18) 12 "comparison_operators.spy"
            global::Sharpy.Builtins.Print(global::ComparisonOperators.ComparisonOperatorsModule.X == global::ComparisonOperators.ComparisonOperatorsModule.Y);
#line (11, 5) - (11, 18) 12 "comparison_operators.spy"
            global::Sharpy.Builtins.Print(global::ComparisonOperators.ComparisonOperatorsModule.X != global::ComparisonOperators.ComparisonOperatorsModule.Y);
#line (14, 5) - (14, 17) 12 "comparison_operators.spy"
            global::Sharpy.Builtins.Print(global::ComparisonOperators.ComparisonOperatorsModule.X < global::ComparisonOperators.ComparisonOperatorsModule.Y);
#line (15, 5) - (15, 17) 12 "comparison_operators.spy"
            global::Sharpy.Builtins.Print(global::ComparisonOperators.ComparisonOperatorsModule.Y > global::ComparisonOperators.ComparisonOperatorsModule.X);
#line (16, 5) - (16, 17) 12 "comparison_operators.spy"
            global::Sharpy.Builtins.Print(global::ComparisonOperators.ComparisonOperatorsModule.X > global::ComparisonOperators.ComparisonOperatorsModule.Y);
#line (19, 5) - (19, 18) 12 "comparison_operators.spy"
            global::Sharpy.Builtins.Print(global::ComparisonOperators.ComparisonOperatorsModule.X <= global::ComparisonOperators.ComparisonOperatorsModule.Z);
#line (20, 5) - (20, 18) 12 "comparison_operators.spy"
            global::Sharpy.Builtins.Print(global::ComparisonOperators.ComparisonOperatorsModule.X >= global::ComparisonOperators.ComparisonOperatorsModule.Z);
#line (21, 5) - (21, 18) 12 "comparison_operators.spy"
            global::Sharpy.Builtins.Print(global::ComparisonOperators.ComparisonOperatorsModule.Y <= global::ComparisonOperators.ComparisonOperatorsModule.X);
#line hidden
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static global::Sharpy.StaticInitHost __host;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void __Sweep()
        {
            global::Sharpy.StaticInit.Run(ref X__state, "x", X__init);
            global::Sharpy.StaticInit.Run(ref Y__state, "y", Y__init);
            global::Sharpy.StaticInit.Run(ref Z__state, "z", Z__init);
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
}
#line default
