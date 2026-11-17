// Snapshot: Recursive function with return type annotation
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace FibonacciRecursive
{
    public static partial class FibonacciRecursiveModule
    {
        public static int Fibonacci(int n)
        {
#line (2, 5) - (3, 18) 12 "fibonacci_recursive.spy"
            if (n <= 1)
#line hidden
            {
#line (3, 9) - (3, 18) 16 "fibonacci_recursive.spy"
                return n;
#line hidden
            }

#line (4, 5) - (4, 48) 12 "fibonacci_recursive.spy"
            return global::FibonacciRecursive.FibonacciRecursiveModule.Fibonacci(n - 1) + global::FibonacciRecursive.FibonacciRecursiveModule.Fibonacci(n - 2);
#line hidden
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int Result__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Result__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Result__init()
        {
            Result__field = global::FibonacciRecursive.FibonacciRecursiveModule.Fibonacci(10);
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Result__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Result__state, "result", Result__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int Result
        {
            get
            {
                if (!__Swept.Done)
                    Result__slow();
                return ref Result__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Result__ensure()
        {
            if (!__Swept.Done)
                Result__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int Result__set(int value)
        {
            if (__Swept.Done)
                Result__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Result__state, ref Result__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? Result__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Result__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int Result__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = Result__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Result__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Result__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                Result__set(spill.Value!);
            return result;
        }

        public static void Main()
        {
            __Load();
#line (9, 5) - (9, 18) 12 "fibonacci_recursive.spy"
            global::Sharpy.Builtins.Print(global::FibonacciRecursive.FibonacciRecursiveModule.Result);
#line hidden
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static global::Sharpy.StaticInitHost __host;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void __Sweep()
        {
            global::Sharpy.StaticInit.Run(ref Result__state, "result", Result__init);
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
