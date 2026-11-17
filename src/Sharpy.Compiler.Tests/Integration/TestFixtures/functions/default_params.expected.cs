// Snapshot: Function with default parameter values
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace DefaultParams
{
    public static partial class DefaultParamsModule
    {
        public static int CalculatePrice(int @base, double taxRate = 0.1d, int discount = 0)
        {
#line (6, 5) - (6, 34) 12 "default_params.spy"
            int baseCents = @base * 100;
#line (7, 5) - (7, 64) 12 "default_params.spy"
            int taxCents = global::Sharpy.Builtins.FloorDiv((baseCents * global::Sharpy.Builtins.Int(taxRate * 100)), 100);
#line (8, 5) - (8, 47) 12 "default_params.spy"
            int totalCents = baseCents + taxCents;
#line (9, 5) - (9, 42) 12 "default_params.spy"
            int discountCents = discount * 100;
#line (10, 5) - (10, 53) 12 "default_params.spy"
            int finalCents = totalCents - discountCents;
#line (11, 5) - (11, 31) 12 "default_params.spy"
            return global::Sharpy.Builtins.FloorDiv(finalCents, 100);
#line hidden
        }

        public static string Greet(string name, string greeting = "Hello", string punctuation = "!")
        {
#line (15, 5) - (15, 21) 12 "default_params.spy"
            return greeting;
#line hidden
        }

        public static int Power(int @base, int exponent = 2)
        {
#line (18, 5) - (18, 21) 12 "default_params.spy"
            int result = 1;
#line (19, 5) - (19, 16) 12 "default_params.spy"
            int i = 0;
#line (20, 5) - (22, 18) 12 "default_params.spy"
            while (i < exponent)
#line hidden
            {
#line (21, 9) - (21, 31) 16 "default_params.spy"
                result = result * @base;
#line (22, 9) - (22, 18) 16 "default_params.spy"
                i = i + 1;
#line hidden
            }

#line (23, 5) - (23, 19) 12 "default_params.spy"
            return result;
#line hidden
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int Price1__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Price1__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Price1__init()
        {
            Price1__field = global::DefaultParams.DefaultParamsModule.CalculatePrice(100);
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Price1__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Price1__state, "price1", Price1__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int Price1
        {
            get
            {
                if (!__Swept.Done)
                    Price1__slow();
                return ref Price1__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Price1__ensure()
        {
            if (!__Swept.Done)
                Price1__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int Price1__set(int value)
        {
            if (__Swept.Done)
                Price1__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Price1__state, ref Price1__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? Price1__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Price1__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int Price1__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = Price1__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Price1__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Price1__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                Price1__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int Price2__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Price2__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Price2__init()
        {
            Price2__field = global::DefaultParams.DefaultParamsModule.CalculatePrice(100, 0.2d);
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Price2__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Price2__state, "price2", Price2__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int Price2
        {
            get
            {
                if (!__Swept.Done)
                    Price2__slow();
                return ref Price2__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Price2__ensure()
        {
            if (!__Swept.Done)
                Price2__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int Price2__set(int value)
        {
            if (__Swept.Done)
                Price2__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Price2__state, ref Price2__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? Price2__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Price2__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int Price2__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = Price2__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Price2__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Price2__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                Price2__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int Price3__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Price3__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Price3__init()
        {
            Price3__field = global::DefaultParams.DefaultParamsModule.CalculatePrice(100, 0.15d, 10);
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Price3__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Price3__state, "price3", Price3__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int Price3
        {
            get
            {
                if (!__Swept.Done)
                    Price3__slow();
                return ref Price3__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Price3__ensure()
        {
            if (!__Swept.Done)
                Price3__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int Price3__set(int value)
        {
            if (__Swept.Done)
                Price3__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Price3__state, ref Price3__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? Price3__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Price3__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int Price3__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = Price3__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Price3__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Price3__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                Price3__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int Squared__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Squared__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Squared__init()
        {
            Squared__field = global::DefaultParams.DefaultParamsModule.Power(5);
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Squared__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Squared__state, "squared", Squared__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int Squared
        {
            get
            {
                if (!__Swept.Done)
                    Squared__slow();
                return ref Squared__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Squared__ensure()
        {
            if (!__Swept.Done)
                Squared__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int Squared__set(int value)
        {
            if (__Swept.Done)
                Squared__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Squared__state, ref Squared__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? Squared__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Squared__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int Squared__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = Squared__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Squared__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Squared__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                Squared__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int Cubed__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Cubed__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Cubed__init()
        {
            Cubed__field = global::DefaultParams.DefaultParamsModule.Power(3, 3);
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Cubed__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Cubed__state, "cubed", Cubed__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int Cubed
        {
            get
            {
                if (!__Swept.Done)
                    Cubed__slow();
                return ref Cubed__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Cubed__ensure()
        {
            if (!__Swept.Done)
                Cubed__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int Cubed__set(int value)
        {
            if (__Swept.Done)
                Cubed__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Cubed__state, ref Cubed__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? Cubed__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Cubed__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int Cubed__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = Cubed__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Cubed__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Cubed__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                Cubed__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int Price4__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Price4__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Price4__init()
        {
            Price4__field = global::DefaultParams.DefaultParamsModule.CalculatePrice(50, 0.1d, 5);
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Price4__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Price4__state, "price4", Price4__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int Price4
        {
            get
            {
                if (!__Swept.Done)
                    Price4__slow();
                return ref Price4__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Price4__ensure()
        {
            if (!__Swept.Done)
                Price4__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int Price4__set(int value)
        {
            if (__Swept.Done)
                Price4__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Price4__state, ref Price4__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? Price4__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Price4__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int Price4__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = Price4__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Price4__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Price4__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                Price4__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int Price5__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Price5__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Price5__init()
        {
            Price5__field = global::DefaultParams.DefaultParamsModule.CalculatePrice(200, 0.1d, 20);
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Price5__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Price5__state, "price5", Price5__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int Price5
        {
            get
            {
                if (!__Swept.Done)
                    Price5__slow();
                return ref Price5__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Price5__ensure()
        {
            if (!__Swept.Done)
                Price5__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int Price5__set(int value)
        {
            if (__Swept.Done)
                Price5__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Price5__state, ref Price5__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? Price5__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Price5__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int Price5__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = Price5__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Price5__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Price5__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                Price5__set(spill.Value!);
            return result;
        }

        public static void Main()
        {
            __Load();
#line (35, 5) - (35, 18) 12 "default_params.spy"
            global::Sharpy.Builtins.Print(global::DefaultParams.DefaultParamsModule.Price1);
#line (38, 5) - (38, 18) 12 "default_params.spy"
            global::Sharpy.Builtins.Print(global::DefaultParams.DefaultParamsModule.Price2);
#line (41, 5) - (41, 18) 12 "default_params.spy"
            global::Sharpy.Builtins.Print(global::DefaultParams.DefaultParamsModule.Price3);
#line (44, 5) - (44, 19) 12 "default_params.spy"
            global::Sharpy.Builtins.Print(global::DefaultParams.DefaultParamsModule.Squared);
#line (47, 5) - (47, 17) 12 "default_params.spy"
            global::Sharpy.Builtins.Print(global::DefaultParams.DefaultParamsModule.Cubed);
#line (50, 5) - (50, 18) 12 "default_params.spy"
            global::Sharpy.Builtins.Print(global::DefaultParams.DefaultParamsModule.Price4);
#line (53, 5) - (53, 18) 12 "default_params.spy"
            global::Sharpy.Builtins.Print(global::DefaultParams.DefaultParamsModule.Price5);
#line hidden
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static global::Sharpy.StaticInitHost __host;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void __Sweep()
        {
            global::Sharpy.StaticInit.Run(ref Price1__state, "price1", Price1__init);
            global::Sharpy.StaticInit.Run(ref Price2__state, "price2", Price2__init);
            global::Sharpy.StaticInit.Run(ref Price3__state, "price3", Price3__init);
            global::Sharpy.StaticInit.Run(ref Squared__state, "squared", Squared__init);
            global::Sharpy.StaticInit.Run(ref Cubed__state, "cubed", Cubed__init);
            global::Sharpy.StaticInit.Run(ref Price4__state, "price4", Price4__init);
            global::Sharpy.StaticInit.Run(ref Price5__state, "price5", Price5__init);
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
