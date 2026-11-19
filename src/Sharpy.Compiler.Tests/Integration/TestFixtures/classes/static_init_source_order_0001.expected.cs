#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace StaticInitSourceOrder0001
{
    public static partial class StaticInitSourceOrder0001Module
    {
        public static void Main()
        {
            __Load();
#line (15, 5) - (15, 18) 12 "static_init_source_order_0001.spy"
            global::Sharpy.Builtins.Print(global::StaticInitSourceOrder0001.Host.G);
#line (16, 5) - (16, 18) 12 "static_init_source_order_0001.spy"
            global::Sharpy.Builtins.Print(global::StaticInitSourceOrder0001.Host.C);
#line hidden
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static global::Sharpy.StaticInitHost __host;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void __Sweep()
        {
            global::StaticInitSourceOrder0001.Host.__Load_Host();
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

    [global::Sharpy.SharpyModuleType("__main__", "Host")]
    public class Host
    {
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int A__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte A__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void A__init()
        {
            A__field = 1;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void A__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref A__state, "a", A__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int A
        {
            get
            {
                if (!__Swept.Done)
                    A__slow();
                return ref A__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void A__ensure()
        {
            if (!__Swept.Done)
                A__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void A__storeSlow(int value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref A__state, ref A__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int A__set(int value)
        {
            if (__Swept.Done)
                A__field = value;
            else
                A__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? A__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref A__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int A__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = A__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref A__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R A__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                A__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static int Sp__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Sp__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Sp__init()
        {
            Sp__field = 4;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Sp__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Sp__state, "sp", Sp__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static int Sp
        {
            get
            {
                if (!__Swept.Done)
                    Sp__slow();
                return Sp__field;
            }

            set
            {
                if (__Swept.Done)
                    Sp__field = value;
                else
                    Sp__storeSlow(value);
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Sp__storeSlow(int value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Sp__state, ref Sp__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int Sp__set(int value)
        {
            Sp = value;
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int G__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte G__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void G__init()
        {
            G__field = global::StaticInitSourceOrder0001.Host.Sp;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void G__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref G__state, "g", G__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int G
        {
            get
            {
                if (!__Swept.Done)
                    G__slow();
                return ref G__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void G__ensure()
        {
            if (!__Swept.Done)
                G__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void G__storeSlow(int value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref G__state, ref G__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int G__set(int value)
        {
            if (__Swept.Done)
                G__field = value;
            else
                G__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? G__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref G__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int G__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = G__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref G__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R G__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                G__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int C__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte C__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void C__init()
        {
            C__field = global::StaticInitSourceOrder0001.Host.Sp;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void C__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref C__state, "C", C__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref readonly int C
        {
            get
            {
                if (!__Swept.Done)
                    C__slow();
                return ref C__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void C__ensure()
        {
            if (!__Swept.Done)
                C__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static global::Sharpy.StaticInitHost __host;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void __Sweep()
        {
            global::Sharpy.StaticInit.Run(ref A__state, "a", A__init);
            global::Sharpy.StaticInit.Run(ref Sp__state, "sp", Sp__init);
            global::Sharpy.StaticInit.Run(ref G__state, "g", G__init);
            global::Sharpy.StaticInit.Run(ref C__state, "C", C__init);
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
        internal static void __Load_Host()
        {
            if (!__Swept.Done)
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
        }
    }
}
#line default
