// Snapshot: Arithmetic operators (+, -, *, /, //, %, **)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace ArithmeticOperators
{
    public static partial class ArithmeticOperatorsModule
    {
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int A__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte A__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void A__init()
        {
            A__field = 15;
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
        internal static int A__set(int value)
        {
            if (__Swept.Done)
                A__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref A__state, ref A__field, value);
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
        internal static int B__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte B__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void B__init()
        {
            B__field = 4;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void B__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref B__state, "b", B__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int B
        {
            get
            {
                if (!__Swept.Done)
                    B__slow();
                return ref B__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void B__ensure()
        {
            if (!__Swept.Done)
                B__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int B__set(int value)
        {
            if (__Swept.Done)
                B__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref B__state, ref B__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? B__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref B__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int B__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = B__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref B__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R B__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                B__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SumResult__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte SumResult__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SumResult__init()
        {
            SumResult__field = global::ArithmeticOperators.ArithmeticOperatorsModule.A + global::ArithmeticOperators.ArithmeticOperatorsModule.B;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SumResult__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref SumResult__state, "sum_result", SumResult__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int SumResult
        {
            get
            {
                if (!__Swept.Done)
                    SumResult__slow();
                return ref SumResult__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void SumResult__ensure()
        {
            if (!__Swept.Done)
                SumResult__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SumResult__set(int value)
        {
            if (__Swept.Done)
                SumResult__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref SumResult__state, ref SumResult__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? SumResult__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref SumResult__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int SumResult__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = SumResult__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref SumResult__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R SumResult__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                SumResult__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int DiffResult__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte DiffResult__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void DiffResult__init()
        {
            DiffResult__field = global::ArithmeticOperators.ArithmeticOperatorsModule.A - global::ArithmeticOperators.ArithmeticOperatorsModule.B;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void DiffResult__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref DiffResult__state, "diff_result", DiffResult__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int DiffResult
        {
            get
            {
                if (!__Swept.Done)
                    DiffResult__slow();
                return ref DiffResult__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void DiffResult__ensure()
        {
            if (!__Swept.Done)
                DiffResult__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int DiffResult__set(int value)
        {
            if (__Swept.Done)
                DiffResult__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref DiffResult__state, ref DiffResult__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? DiffResult__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref DiffResult__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int DiffResult__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = DiffResult__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref DiffResult__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R DiffResult__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                DiffResult__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int ProdResult__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte ProdResult__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void ProdResult__init()
        {
            ProdResult__field = global::ArithmeticOperators.ArithmeticOperatorsModule.A * global::ArithmeticOperators.ArithmeticOperatorsModule.B;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void ProdResult__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref ProdResult__state, "prod_result", ProdResult__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int ProdResult
        {
            get
            {
                if (!__Swept.Done)
                    ProdResult__slow();
                return ref ProdResult__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void ProdResult__ensure()
        {
            if (!__Swept.Done)
                ProdResult__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int ProdResult__set(int value)
        {
            if (__Swept.Done)
                ProdResult__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref ProdResult__state, ref ProdResult__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? ProdResult__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref ProdResult__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int ProdResult__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = ProdResult__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref ProdResult__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R ProdResult__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                ProdResult__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int DivResult__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte DivResult__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void DivResult__init()
        {
            DivResult__field = global::Sharpy.Builtins.FloorDiv(global::ArithmeticOperators.ArithmeticOperatorsModule.A, global::ArithmeticOperators.ArithmeticOperatorsModule.B);
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void DivResult__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref DivResult__state, "div_result", DivResult__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int DivResult
        {
            get
            {
                if (!__Swept.Done)
                    DivResult__slow();
                return ref DivResult__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void DivResult__ensure()
        {
            if (!__Swept.Done)
                DivResult__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int DivResult__set(int value)
        {
            if (__Swept.Done)
                DivResult__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref DivResult__state, ref DivResult__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? DivResult__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref DivResult__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int DivResult__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = DivResult__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref DivResult__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R DivResult__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                DivResult__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int ModResult__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte ModResult__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void ModResult__init()
        {
            ModResult__field = global::Sharpy.Builtins.FloorMod(global::ArithmeticOperators.ArithmeticOperatorsModule.A, global::ArithmeticOperators.ArithmeticOperatorsModule.B);
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void ModResult__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref ModResult__state, "mod_result", ModResult__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int ModResult
        {
            get
            {
                if (!__Swept.Done)
                    ModResult__slow();
                return ref ModResult__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void ModResult__ensure()
        {
            if (!__Swept.Done)
                ModResult__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int ModResult__set(int value)
        {
            if (__Swept.Done)
                ModResult__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref ModResult__state, ref ModResult__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? ModResult__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref ModResult__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int ModResult__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = ModResult__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref ModResult__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R ModResult__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                ModResult__set(spill.Value!);
            return result;
        }

        public static void Main()
        {
            __Load();
#line (13, 5) - (13, 22) 12 "arithmetic_operators.spy"
            global::Sharpy.Builtins.Print(global::ArithmeticOperators.ArithmeticOperatorsModule.SumResult);
#line (14, 5) - (14, 23) 12 "arithmetic_operators.spy"
            global::Sharpy.Builtins.Print(global::ArithmeticOperators.ArithmeticOperatorsModule.DiffResult);
#line (15, 5) - (15, 23) 12 "arithmetic_operators.spy"
            global::Sharpy.Builtins.Print(global::ArithmeticOperators.ArithmeticOperatorsModule.ProdResult);
#line (16, 5) - (16, 22) 12 "arithmetic_operators.spy"
            global::Sharpy.Builtins.Print(global::ArithmeticOperators.ArithmeticOperatorsModule.DivResult);
#line (17, 5) - (17, 22) 12 "arithmetic_operators.spy"
            global::Sharpy.Builtins.Print(global::ArithmeticOperators.ArithmeticOperatorsModule.ModResult);
#line hidden
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static global::Sharpy.StaticInitHost __host;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void __Sweep()
        {
            global::Sharpy.StaticInit.Run(ref A__state, "a", A__init);
            global::Sharpy.StaticInit.Run(ref B__state, "b", B__init);
            global::Sharpy.StaticInit.Run(ref SumResult__state, "sum_result", SumResult__init);
            global::Sharpy.StaticInit.Run(ref DiffResult__state, "diff_result", DiffResult__init);
            global::Sharpy.StaticInit.Run(ref ProdResult__state, "prod_result", ProdResult__init);
            global::Sharpy.StaticInit.Run(ref DivResult__state, "div_result", DivResult__init);
            global::Sharpy.StaticInit.Run(ref ModResult__state, "mod_result", ModResult__init);
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
