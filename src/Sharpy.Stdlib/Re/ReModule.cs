// Generated from src/Sharpy.Stdlib/spy/re_module.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/re_module.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy.ReModule
{
    /// <summary>
    /// Regular expression matching operations.
    /// </summary>
    public static partial class ReModuleModule
    {
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int IGNORECASE__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte IGNORECASE__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void IGNORECASE__init()
        {
            IGNORECASE__field = 2;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void IGNORECASE__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref IGNORECASE__state, "IGNORECASE", IGNORECASE__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int IGNORECASE
        {
            get
            {
                if (!__Swept.Done)
                    IGNORECASE__slow();
                return ref IGNORECASE__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void IGNORECASE__ensure()
        {
            if (!__Swept.Done)
                IGNORECASE__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void IGNORECASE__storeSlow(int value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref IGNORECASE__state, ref IGNORECASE__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int IGNORECASE__set(int value)
        {
            if (__Swept.Done)
                IGNORECASE__field = value;
            else
                IGNORECASE__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? IGNORECASE__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref IGNORECASE__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int IGNORECASE__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = IGNORECASE__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref IGNORECASE__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R IGNORECASE__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                IGNORECASE__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int I__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte I__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void I__init()
        {
            I__field = 2;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void I__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref I__state, "I", I__init);
        [global::Sharpy.SharpyFieldName("I")]
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int I
        {
            get
            {
                if (!__Swept.Done)
                    I__slow();
                return ref I__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void I__ensure()
        {
            if (!__Swept.Done)
                I__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void I__storeSlow(int value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref I__state, ref I__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int I__set(int value)
        {
            if (__Swept.Done)
                I__field = value;
            else
                I__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? I__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref I__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int I__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = I__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref I__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R I__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                I__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int MULTILINE__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte MULTILINE__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void MULTILINE__init()
        {
            MULTILINE__field = 8;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void MULTILINE__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref MULTILINE__state, "MULTILINE", MULTILINE__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int MULTILINE
        {
            get
            {
                if (!__Swept.Done)
                    MULTILINE__slow();
                return ref MULTILINE__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void MULTILINE__ensure()
        {
            if (!__Swept.Done)
                MULTILINE__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void MULTILINE__storeSlow(int value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref MULTILINE__state, ref MULTILINE__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int MULTILINE__set(int value)
        {
            if (__Swept.Done)
                MULTILINE__field = value;
            else
                MULTILINE__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? MULTILINE__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref MULTILINE__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int MULTILINE__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = MULTILINE__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref MULTILINE__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R MULTILINE__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                MULTILINE__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int M__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte M__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void M__init()
        {
            M__field = 8;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void M__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref M__state, "M", M__init);
        [global::Sharpy.SharpyFieldName("M")]
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int M
        {
            get
            {
                if (!__Swept.Done)
                    M__slow();
                return ref M__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void M__ensure()
        {
            if (!__Swept.Done)
                M__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void M__storeSlow(int value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref M__state, ref M__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int M__set(int value)
        {
            if (__Swept.Done)
                M__field = value;
            else
                M__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? M__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref M__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int M__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = M__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref M__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R M__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                M__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int DOTALL__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte DOTALL__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void DOTALL__init()
        {
            DOTALL__field = 16;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void DOTALL__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref DOTALL__state, "DOTALL", DOTALL__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int DOTALL
        {
            get
            {
                if (!__Swept.Done)
                    DOTALL__slow();
                return ref DOTALL__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void DOTALL__ensure()
        {
            if (!__Swept.Done)
                DOTALL__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void DOTALL__storeSlow(int value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref DOTALL__state, ref DOTALL__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int DOTALL__set(int value)
        {
            if (__Swept.Done)
                DOTALL__field = value;
            else
                DOTALL__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? DOTALL__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref DOTALL__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int DOTALL__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = DOTALL__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref DOTALL__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R DOTALL__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                DOTALL__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int S__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte S__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void S__init()
        {
            S__field = 16;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void S__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref S__state, "S", S__init);
        [global::Sharpy.SharpyFieldName("S")]
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int S
        {
            get
            {
                if (!__Swept.Done)
                    S__slow();
                return ref S__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void S__ensure()
        {
            if (!__Swept.Done)
                S__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void S__storeSlow(int value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref S__state, ref S__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int S__set(int value)
        {
            if (__Swept.Done)
                S__field = value;
            else
                S__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? S__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref S__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int S__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = S__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref S__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R S__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                S__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int VERBOSE__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte VERBOSE__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void VERBOSE__init()
        {
            VERBOSE__field = 64;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void VERBOSE__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref VERBOSE__state, "VERBOSE", VERBOSE__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int VERBOSE
        {
            get
            {
                if (!__Swept.Done)
                    VERBOSE__slow();
                return ref VERBOSE__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void VERBOSE__ensure()
        {
            if (!__Swept.Done)
                VERBOSE__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void VERBOSE__storeSlow(int value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref VERBOSE__state, ref VERBOSE__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int VERBOSE__set(int value)
        {
            if (__Swept.Done)
                VERBOSE__field = value;
            else
                VERBOSE__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? VERBOSE__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref VERBOSE__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int VERBOSE__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = VERBOSE__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref VERBOSE__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R VERBOSE__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                VERBOSE__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int X__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte X__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void X__init()
        {
            X__field = 64;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void X__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref X__state, "X", X__init);
        [global::Sharpy.SharpyFieldName("X")]
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
        internal static int UNICODE__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte UNICODE__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void UNICODE__init()
        {
            UNICODE__field = 32;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void UNICODE__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref UNICODE__state, "UNICODE", UNICODE__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int UNICODE
        {
            get
            {
                if (!__Swept.Done)
                    UNICODE__slow();
                return ref UNICODE__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void UNICODE__ensure()
        {
            if (!__Swept.Done)
                UNICODE__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void UNICODE__storeSlow(int value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref UNICODE__state, ref UNICODE__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int UNICODE__set(int value)
        {
            if (__Swept.Done)
                UNICODE__field = value;
            else
                UNICODE__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? UNICODE__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref UNICODE__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int UNICODE__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = UNICODE__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref UNICODE__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R UNICODE__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                UNICODE__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int U__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte U__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void U__init()
        {
            U__field = 32;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void U__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref U__state, "U", U__init);
        [global::Sharpy.SharpyFieldName("U")]
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int U
        {
            get
            {
                if (!__Swept.Done)
                    U__slow();
                return ref U__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void U__ensure()
        {
            if (!__Swept.Done)
                U__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void U__storeSlow(int value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref U__state, ref U__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int U__set(int value)
        {
            if (__Swept.Done)
                U__field = value;
            else
                U__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? U__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref U__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int U__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = U__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref U__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R U__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                U__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int ASCII__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte ASCII__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void ASCII__init()
        {
            ASCII__field = 256;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void ASCII__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref ASCII__state, "ASCII", ASCII__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int ASCII
        {
            get
            {
                if (!__Swept.Done)
                    ASCII__slow();
                return ref ASCII__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void ASCII__ensure()
        {
            if (!__Swept.Done)
                ASCII__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void ASCII__storeSlow(int value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref ASCII__state, ref ASCII__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int ASCII__set(int value)
        {
            if (__Swept.Done)
                ASCII__field = value;
            else
                ASCII__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? ASCII__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref ASCII__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int ASCII__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = ASCII__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref ASCII__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R ASCII__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                ASCII__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int A__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte A__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void A__init()
        {
            A__field = 256;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void A__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref A__state, "A", A__init);
        [global::Sharpy.SharpyFieldName("A")]
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

        /// <summary>
        /// Fast path: check if pattern contains Python-specific syntax.
        /// </summary>
        internal static bool _NeedsTranslation(string pattern)
        {
            int i = 0;
            while (i < pattern.Length)
            {
                if (global::Sharpy.StringHelpers.GetItem(pattern, i) == "\\" && i + 1 < pattern.Length)
                {
                    i = i + 2;
                    continue;
                }

                if (global::Sharpy.StringHelpers.GetItem(pattern, i) == "(" && i + 1 < pattern.Length && global::Sharpy.StringHelpers.GetItem(pattern, i + 1) == "?")
                {
                    if (i + 2 < pattern.Length && global::Sharpy.StringHelpers.GetItem(pattern, i + 2) == "P")
                    {
                        return true;
                    }

                    if (i + 2 < pattern.Length && global::Sharpy.ReModule.ReModuleModule._IsInlineFlagChar(global::Sharpy.StringHelpers.GetItem(pattern, i + 2)))
                    {
                        int j = i + 2;
                        while (j < pattern.Length && global::Sharpy.ReModule.ReModuleModule._IsInlineFlagChar(global::Sharpy.StringHelpers.GetItem(pattern, j)))
                        {
                            j = j + 1;
                        }

                        if (j < pattern.Length && (global::Sharpy.StringHelpers.GetItem(pattern, j) == ")" || global::Sharpy.StringHelpers.GetItem(pattern, j) == ":"))
                        {
                            string flags = pattern.Substring(i + 2, j - (i + 2));
                            if (flags.Contains("a") || flags.Contains("u") || flags.Contains("L"))
                            {
                                return true;
                            }
                        }
                    }
                }

                i = i + 1;
            }

            return false;
        }

        /// <summary>
        /// Check if character is a valid inline flag character.
        /// </summary>
        internal static bool _IsInlineFlagChar(string c)
        {
            return "aiLmsux".Contains(c);
        }

        /// <summary>
        /// Strip Python-only flags (a, u, L), keep .NET-compatible ones (i, m, s, x).
        /// </summary>
        internal static string _FilterInlineFlags(string flags)
        {
            global::System.Text.StringBuilder sb = new global::System.Text.StringBuilder(flags.Length);
            int i = 0;
            while (i < flags.Length)
            {
                string c = global::Sharpy.StringHelpers.GetItem(flags, i);
                if (c != "a" && c != "u" && c != "L")
                {
                    sb.Append(c);
                }

                i = i + 1;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Translate Python regex syntax to .NET regex syntax.
        /// </summary>
        internal static string _TranslatePattern(string pattern)
        {
            if (pattern is null)
            {
                throw new global::Sharpy.TypeError("expected string, got NoneType");
            }

            if (!global::Sharpy.ReModule.ReModuleModule._NeedsTranslation(pattern))
            {
                return pattern;
            }

            global::System.Text.StringBuilder sb = new global::System.Text.StringBuilder(pattern.Length);
            int i = 0;
            while (i < pattern.Length)
            {
                if (global::Sharpy.StringHelpers.GetItem(pattern, i) == "\\" && i + 1 < pattern.Length)
                {
                    sb.Append(global::Sharpy.StringHelpers.GetItem(pattern, i));
                    sb.Append(global::Sharpy.StringHelpers.GetItem(pattern, i + 1));
                    i = i + 2;
                    continue;
                }

                if (global::Sharpy.StringHelpers.GetItem(pattern, i) == "(" && i + 1 < pattern.Length && global::Sharpy.StringHelpers.GetItem(pattern, i + 1) == "?")
                {
                    if (i + 2 < pattern.Length && global::Sharpy.StringHelpers.GetItem(pattern, i + 2) == "P")
                    {
                        if (i + 3 < pattern.Length && global::Sharpy.StringHelpers.GetItem(pattern, i + 3) == "<")
                        {
                            sb.Append("(?<");
                            i = i + 4;
                            continue;
                        }

                        if (i + 3 < pattern.Length && global::Sharpy.StringHelpers.GetItem(pattern, i + 3) == "=")
                        {
                            i = i + 4;
                            int nameStart = i;
                            while (i < pattern.Length && global::Sharpy.StringHelpers.GetItem(pattern, i) != ")")
                            {
                                i = i + 1;
                            }

                            string name = pattern.Substring(nameStart, i - nameStart);
                            sb.Append("\\k<");
                            sb.Append(name);
                            sb.Append(">");
                            if (i < pattern.Length)
                            {
                                i = i + 1;
                            }

                            continue;
                        }
                    }

                    if (i + 2 < pattern.Length && global::Sharpy.ReModule.ReModuleModule._IsInlineFlagChar(global::Sharpy.StringHelpers.GetItem(pattern, i + 2)))
                    {
                        int flagStart = i + 2;
                        int j = flagStart;
                        while (j < pattern.Length && global::Sharpy.ReModule.ReModuleModule._IsInlineFlagChar(global::Sharpy.StringHelpers.GetItem(pattern, j)))
                        {
                            j = j + 1;
                        }

                        if (j < pattern.Length && (global::Sharpy.StringHelpers.GetItem(pattern, j) == ")" || global::Sharpy.StringHelpers.GetItem(pattern, j) == ":"))
                        {
                            string flagsStr = pattern.Substring(flagStart, j - flagStart);
                            string filtered = global::Sharpy.ReModule.ReModuleModule._FilterInlineFlags(flagsStr);
                            if (global::Sharpy.StringHelpers.GetItem(pattern, j) == ")")
                            {
                                if (filtered.Length > 0)
                                {
                                    sb.Append("(?");
                                    sb.Append(filtered);
                                    sb.Append(")");
                                }

                                i = j + 1;
                                continue;
                            }
                            else
                            {
                                if (filtered.Length > 0)
                                {
                                    sb.Append("(?");
                                    sb.Append(filtered);
                                    sb.Append(":");
                                }
                                else
                                {
                                    sb.Append("(?:");
                                }

                                i = j + 1;
                                continue;
                            }
                        }
                    }
                }

                sb.Append(global::Sharpy.StringHelpers.GetItem(pattern, i));
                i = i + 1;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Translate Python replacement syntax to .NET replacement syntax.
        /// </summary>
        internal static string _TranslateReplacement(string template)
        {
            global::System.Text.StringBuilder sb = new global::System.Text.StringBuilder(template.Length);
            int i = 0;
            while (i < template.Length)
            {
                if (global::Sharpy.StringHelpers.GetItem(template, i) == "\\" && i + 1 < template.Length)
                {
                    if (global::Sharpy.StringHelpers.GetItem(template, i + 1) == "g" && i + 2 < template.Length && global::Sharpy.StringHelpers.GetItem(template, i + 2) == "<")
                    {
                        i = i + 3;
                        int nameStart = i;
                        while (i < template.Length && global::Sharpy.StringHelpers.GetItem(template, i) != ">")
                        {
                            i = i + 1;
                        }

                        string grpName = template.Substring(nameStart, i - nameStart);
                        sb.Append("${");
                        sb.Append(grpName);
                        sb.Append("}");
                        if (i < template.Length)
                        {
                            i = i + 1;
                        }

                        continue;
                    }

                    if (global::Sharpy.StringExtensions.Isdigit(global::Sharpy.StringHelpers.GetItem(template, i + 1)))
                    {
                        sb.Append("$");
                        sb.Append(global::Sharpy.StringHelpers.GetItem(template, i + 1));
                        i = i + 2;
                        continue;
                    }

                    sb.Append(global::Sharpy.StringHelpers.GetItem(template, i));
                    sb.Append(global::Sharpy.StringHelpers.GetItem(template, i + 1));
                    i = i + 2;
                    continue;
                }

                sb.Append(global::Sharpy.StringHelpers.GetItem(template, i));
                i = i + 1;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Map Python flag ints to .NET RegexOptions.
        /// </summary>
        internal static global::System.Text.RegularExpressions.RegexOptions _FlagsToOptions(int flags)
        {
            int options = 0;
            if ((flags & global::Sharpy.ReModule.ReModuleModule.IGNORECASE) != 0)
            {
                options = options | ((int)global::System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            if ((flags & global::Sharpy.ReModule.ReModuleModule.MULTILINE) != 0)
            {
                options = options | ((int)global::System.Text.RegularExpressions.RegexOptions.Multiline);
            }

            if ((flags & global::Sharpy.ReModule.ReModuleModule.DOTALL) != 0)
            {
                options = options | ((int)global::System.Text.RegularExpressions.RegexOptions.Singleline);
            }

            if ((flags & global::Sharpy.ReModule.ReModuleModule.VERBOSE) != 0)
            {
                options = options | ((int)global::System.Text.RegularExpressions.RegexOptions.IgnorePatternWhitespace);
            }

            return (global::System.Text.RegularExpressions.RegexOptions)options;
        }

        /// <summary>
        /// Apply endpos truncation to a string.
        /// </summary>
        internal static string _ApplyEndpos(string s, int? endpos)
        {
            if (endpos is null)
            {
                return s;
            }

            int ep = endpos.Value;
            if (ep >= s.Length)
            {
                return s;
            }

            return s.Substring(0, ep);
        }

        /// <summary>
        /// Escape special characters in pattern.
        /// </summary>
        internal static string _EscapePattern(string pattern)
        {
            return global::System.Text.RegularExpressions.Regex.Escape(pattern);
        }

        /// <summary>
        /// Compile a regular expression pattern into a Pattern object.
        /// </summary>
        public static global::Sharpy.ReModule.Pattern Compile(string pattern, int flags = 0)
        {
            return new global::Sharpy.ReModule.Pattern(pattern, flags);
        }

        /// <summary>
        /// Scan through string looking for the first match.
        /// </summary>
        public static global::Sharpy.ReModule.MatchResult? Search(string pattern, string s, int flags = 0)
        {
            global::Sharpy.ReModule.Pattern p = global::Sharpy.ReModule.ReModuleModule.Compile(pattern, flags);
            return p.Search(s, 0, null);
        }

        /// <summary>
        /// Try to apply the pattern at the start of the string.
        /// </summary>
        public static global::Sharpy.ReModule.MatchResult? Match(string pattern, string s, int flags = 0)
        {
            global::Sharpy.ReModule.Pattern p = global::Sharpy.ReModule.ReModuleModule.Compile(pattern, flags);
            return p.Match(s, 0, null);
        }

        /// <summary>
        /// Try to apply the pattern to the entire string.
        /// </summary>
        public static global::Sharpy.ReModule.MatchResult? Fullmatch(string pattern, string s, int flags = 0)
        {
            global::Sharpy.ReModule.Pattern p = global::Sharpy.ReModule.ReModuleModule.Compile(pattern, flags);
            return p.Fullmatch(s, 0, null);
        }

        /// <summary>
        /// Return all non-overlapping matches of pattern in string.
        /// </summary>
        public static Sharpy.List<object> Findall(string pattern, string s, int flags = 0)
        {
            global::Sharpy.ReModule.Pattern p = global::Sharpy.ReModule.ReModuleModule.Compile(pattern, flags);
            return p.Findall(s, 0, null);
        }

        /// <summary>
        /// Return a list of match objects over all non-overlapping matches.
        /// </summary>
        public static Sharpy.List<global::Sharpy.ReModule.MatchResult> Finditer(string pattern, string s, int flags = 0)
        {
            global::Sharpy.ReModule.Pattern p = global::Sharpy.ReModule.ReModuleModule.Compile(pattern, flags);
            return p.Finditer(s, 0, null);
        }

        /// <summary>
        /// Return the string obtained by replacing occurrences.
        /// </summary>
        public static string Sub(string pattern, string repl, string s, int count = 0, int flags = 0)
        {
            global::Sharpy.ReModule.Pattern p = global::Sharpy.ReModule.ReModuleModule.Compile(pattern, flags);
            return p.Sub(repl, s, count);
        }

        /// <summary>
        /// Return the string obtained by replacing occurrences using a callable.
        /// </summary>
        public static string Sub(string pattern, global::System.Func<global::Sharpy.ReModule.MatchResult, string> repl, string s, int count = 0, int flags = 0)
        {
            global::Sharpy.ReModule.Pattern p = global::Sharpy.ReModule.ReModuleModule.Compile(pattern, flags);
            return p.Sub(repl, s, count);
        }

        /// <summary>
        /// Like sub(), but returns (new_string, number_of_subs_made).
        /// </summary>
        public static global::System.ValueTuple<string, int> Subn(string pattern, string repl, string s, int count = 0, int flags = 0)
        {
            global::Sharpy.ReModule.Pattern p = global::Sharpy.ReModule.ReModuleModule.Compile(pattern, flags);
            return p.Subn(repl, s, count);
        }

        /// <summary>
        /// Like sub() with callable, but returns (new_string, number_of_subs_made).
        /// </summary>
        public static global::System.ValueTuple<string, int> Subn(string pattern, global::System.Func<global::Sharpy.ReModule.MatchResult, string> repl, string s, int count = 0, int flags = 0)
        {
            global::Sharpy.ReModule.Pattern p = global::Sharpy.ReModule.ReModuleModule.Compile(pattern, flags);
            return p.Subn(repl, s, count);
        }

        /// <summary>
        /// Split string by the occurrences of pattern.
        /// </summary>
        public static Sharpy.List<string> Split(string pattern, string s, int maxsplit = 0, int flags = 0)
        {
            global::Sharpy.ReModule.Pattern p = global::Sharpy.ReModule.ReModuleModule.Compile(pattern, flags);
            return p.Split(s, maxsplit);
        }

        /// <summary>
        /// Clear the regular expression cache (no-op on .NET).
        /// </summary>
        public static void Purge()
        {
            ;
        }

        /// <summary>
        /// Escape special characters in pattern.
        /// </summary>
        public static string Escape(string pattern)
        {
            return global::Sharpy.ReModule.ReModuleModule._EscapePattern(pattern);
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static global::Sharpy.StaticInitHost __host;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void __Sweep()
        {
            global::Sharpy.StaticInit.Run(ref IGNORECASE__state, "IGNORECASE", IGNORECASE__init);
            global::Sharpy.StaticInit.Run(ref I__state, "I", I__init);
            global::Sharpy.StaticInit.Run(ref MULTILINE__state, "MULTILINE", MULTILINE__init);
            global::Sharpy.StaticInit.Run(ref M__state, "M", M__init);
            global::Sharpy.StaticInit.Run(ref DOTALL__state, "DOTALL", DOTALL__init);
            global::Sharpy.StaticInit.Run(ref S__state, "S", S__init);
            global::Sharpy.StaticInit.Run(ref VERBOSE__state, "VERBOSE", VERBOSE__init);
            global::Sharpy.StaticInit.Run(ref X__state, "X", X__init);
            global::Sharpy.StaticInit.Run(ref UNICODE__state, "UNICODE", UNICODE__init);
            global::Sharpy.StaticInit.Run(ref U__state, "U", U__init);
            global::Sharpy.StaticInit.Run(ref ASCII__state, "ASCII", ASCII__init);
            global::Sharpy.StaticInit.Run(ref A__state, "A", A__init);
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

    /// <summary>
    /// Exception raised when a regex pattern is invalid.
    /// </summary>
    [global::Sharpy.SharpyModuleType("re", "error")]
    [global::Sharpy.SharpyName("error")]
    public class Error : global::System.Exception
    {
        public string Msg;
        public string? Pattern;
        public int? Pos;
        public int? Lineno;
        public int? Colno;
        /// <summary>
        /// Create an error with the specified message and optional pattern/position info.
        /// </summary>
        public Error(string msg, string? pattern = null, int? pos = null) : base(msg)
        {
            this.Msg = msg;
            this.Pattern = pattern;
            this.Pos = pos;
            if (pos is not null && pattern is not null)
            {
                int line = 1;
                int col = pos.Value + 1;
                int idx = 0;
                while (idx < pos.Value && idx < pattern!.Length)
                {
                    if (global::Sharpy.StringHelpers.GetItem(pattern!, idx) == "\n")
                    {
                        line = line + 1;
                        col = pos.Value - idx;
                    }

                    idx = idx + 1;
                }

                this.Lineno = line;
                this.Colno = col;
            }
            else
            {
                this.Lineno = null;
                this.Colno = null;
            }
        }
    }

    /// <summary>
    /// Compiled regular expression pattern, wrapping .NET Regex.
    /// </summary>
    [global::Sharpy.SharpyModuleType("re", "Pattern")]
    public sealed class Pattern
    {
        private global::System.Text.RegularExpressions.Regex _Regex;
        private string _PatternStr;
        private int _Flags;
        private Sharpy.Dict<string, int> _Groupindex;
        private bool _GroupindexCached;
        public string PatternStr
        {
            get
            {
                _ = "The original pattern string.";
                return this._PatternStr;
            }
        }

        public int Flags
        {
            get
            {
                _ = "The flags used to compile this pattern.";
                return this._Flags;
            }
        }

        public int Groups
        {
            get
            {
                _ = "The number of capturing groups in the pattern.";
                return global::Sharpy.Builtins.Len(this._Regex.GetGroupNumbers()) - 1;
            }
        }

        public Sharpy.Dict<string, int> Groupindex
        {
            get
            {
                _ = "A dictionary mapping named group names to group numbers.";
                if (global::Sharpy.Builtins.Len(this._Groupindex) > 0 || this._GroupindexCached)
                {
                    return this._Groupindex;
                }

                var names = this._Regex.GetGroupNames();
                foreach (var __loopVar_0 in names)
                {
                    var name = __loopVar_0;
                    if (!global::Sharpy.StringExtensions.Isdigit(name))
                    {
                        this._Groupindex[name] = this._Regex.GroupNumberFromName(name);
                    }
                }

                this._GroupindexCached = true;
                return this._Groupindex;
            }
        }

        public global::System.Text.RegularExpressions.Regex Regex
        {
            get
            {
                _ = "The internal .NET Regex object.";
                return this._Regex;
            }
        }

        /// <summary>
        /// Scan through string looking for the first match.
        /// </summary>
        public global::Sharpy.ReModule.MatchResult? Search(string s, int pos = 0, int? endpos = null)
        {
            string target = global::Sharpy.ReModule.ReModuleModule._ApplyEndpos(s, endpos);
            global::System.Text.RegularExpressions.Match m = this._Regex.Match(target, pos);
            if (!m.Success)
            {
                return null;
            }

            int actualEndpos = s.Length;
            if (endpos is not null)
            {
                actualEndpos = endpos.Value;
            }

            return new global::Sharpy.ReModule.MatchResult(m, s, this._PatternStr, pos, actualEndpos, this);
        }

        /// <summary>
        /// Try to apply the pattern at the start of the string.
        /// </summary>
        public global::Sharpy.ReModule.MatchResult? Match(string s, int pos = 0, int? endpos = null)
        {
            string target = global::Sharpy.ReModule.ReModuleModule._ApplyEndpos(s, endpos);
            global::System.Text.RegularExpressions.Match m = this._Regex.Match(target, pos);
            if (!m.Success || m.Index != pos)
            {
                return null;
            }

            int actualEndpos = s.Length;
            if (endpos is not null)
            {
                actualEndpos = endpos.Value;
            }

            return new global::Sharpy.ReModule.MatchResult(m, s, this._PatternStr, pos, actualEndpos, this);
        }

        /// <summary>
        /// Try to apply the pattern to the entire string.
        /// </summary>
        public global::Sharpy.ReModule.MatchResult? Fullmatch(string s, int pos = 0, int? endpos = null)
        {
            string target = global::Sharpy.ReModule.ReModuleModule._ApplyEndpos(s, endpos);
            global::System.Text.RegularExpressions.Match m = this._Regex.Match(target, pos);
            if (!m.Success || m.Index != pos || m.Length != target.Length - pos)
            {
                return null;
            }

            int actualEndpos = s.Length;
            if (endpos is not null)
            {
                actualEndpos = endpos.Value;
            }

            return new global::Sharpy.ReModule.MatchResult(m, s, this._PatternStr, pos, actualEndpos, this);
        }

        /// <summary>
        /// Return all non-overlapping matches as a list.
        /// </summary>
        public Sharpy.List<object> Findall(string s, int pos = 0, int? endpos = null)
        {
            string target = global::Sharpy.ReModule.ReModuleModule._ApplyEndpos(s, endpos);
            global::System.Text.RegularExpressions.MatchCollection matches = this._Regex.Matches(target);
            Sharpy.List<object> result = new Sharpy.List<object>()
            {
            };
            bool hasGroups = global::Sharpy.Builtins.Len(this._Regex.GetGroupNumbers()) > 1;
            foreach (var __loopVar_1 in matches)
            {
                var mRaw = __loopVar_1;
                global::System.Text.RegularExpressions.Match m = (global::System.Text.RegularExpressions.Match)mRaw;
                if (m.Index < pos)
                {
                    continue;
                }

                if (!hasGroups)
                {
                    result.Append(m.Value);
                }
                else if (m.Groups.Count == 2)
                {
                    global::System.Text.RegularExpressions.Group g = m.Groups[1];
                    result.Append(g.Success ? g.Value : "");
                }
                else
                {
                    Sharpy.List<object> groupValues = new Sharpy.List<object>()
                    {
                    };
                    int gi = 1;
                    while (gi < m.Groups.Count)
                    {
                        global::System.Text.RegularExpressions.Group grp = m.Groups[gi];
                        groupValues.Append(grp.Success ? grp.Value : "");
                        gi = gi + 1;
                    }

                    result.Append(groupValues);
                }
            }

            return result;
        }

        /// <summary>
        /// Return a list of MatchResult objects over all non-overlapping matches.
        /// </summary>
        public Sharpy.List<global::Sharpy.ReModule.MatchResult> Finditer(string s, int pos = 0, int? endpos = null)
        {
            string target = global::Sharpy.ReModule.ReModuleModule._ApplyEndpos(s, endpos);
            global::System.Text.RegularExpressions.MatchCollection matches = this._Regex.Matches(target);
            Sharpy.List<global::Sharpy.ReModule.MatchResult> result = new Sharpy.List<global::Sharpy.ReModule.MatchResult>()
            {
            };
            int actualEndpos = s.Length;
            if (endpos is not null)
            {
                actualEndpos = endpos.Value;
            }

            foreach (var __loopVar_2 in matches)
            {
                var mRaw = __loopVar_2;
                global::System.Text.RegularExpressions.Match m = (global::System.Text.RegularExpressions.Match)mRaw;
                if (m.Index < pos)
                {
                    continue;
                }

                result.Append(new global::Sharpy.ReModule.MatchResult(m, s, this._PatternStr, pos, actualEndpos, this));
            }

            return result;
        }

        /// <summary>
        /// Return the string obtained by replacing occurrences using a string.
        /// </summary>
        public string Sub(string repl, string s, int count = 0)
        {
            string translated = global::Sharpy.ReModule.ReModuleModule._TranslateReplacement(repl);
            if (count == 0)
            {
                return this._Regex.Replace(s, translated);
            }

            return this._Regex.Replace(s, translated, count);
        }

        /// <summary>
        /// Return the string obtained by replacing occurrences using a callable.
        /// </summary>
        public string Sub(global::System.Func<global::Sharpy.ReModule.MatchResult, string> repl, string s, int count = 0)
        {
            string Evaluator(global::System.Text.RegularExpressions.Match m)
            {
                global::Sharpy.ReModule.MatchResult reMatch = new global::Sharpy.ReModule.MatchResult(m, s, this._PatternStr, 0, s.Length, this);
                return repl(reMatch);
            }

            if (count == 0)
            {
                return this._Regex.Replace(s, Evaluator);
            }

            return this._Regex.Replace(s, Evaluator, count);
        }

        /// <summary>
        /// Like sub(), but returns (new_string, number_of_subs_made).
        /// </summary>
        public global::System.ValueTuple<string, int> Subn(string repl, string s, int count = 0)
        {
            string translated = global::Sharpy.ReModule.ReModuleModule._TranslateReplacement(repl);
            Sharpy.List<int> replacementCount = new Sharpy.List<int>()
            {
                0
            };
            string StrEvaluator(global::System.Text.RegularExpressions.Match m)
            {
                replacementCount[0] = replacementCount.GetItemUnchecked(0) + 1;
                return m.Result(translated);
            }

            string result = default!;
            if (count == 0)
            {
                result = this._Regex.Replace(s, StrEvaluator);
            }
            else
            {
                result = this._Regex.Replace(s, StrEvaluator, count);
            }

            return (result, replacementCount.GetItemUnchecked(0));
        }

        /// <summary>
        /// Like sub() with callable, but returns (new_string, number_of_subs_made).
        /// </summary>
        public global::System.ValueTuple<string, int> Subn(global::System.Func<global::Sharpy.ReModule.MatchResult, string> repl, string s, int count = 0)
        {
            Sharpy.List<int> replacementCount = new Sharpy.List<int>()
            {
                0
            };
            string CallableEvaluator(global::System.Text.RegularExpressions.Match m)
            {
                replacementCount[0] = replacementCount.GetItemUnchecked(0) + 1;
                global::Sharpy.ReModule.MatchResult reMatch = new global::Sharpy.ReModule.MatchResult(m, s, this._PatternStr, 0, s.Length, this);
                return repl(reMatch);
            }

            string result = default!;
            if (count == 0)
            {
                result = this._Regex.Replace(s, CallableEvaluator);
            }
            else
            {
                result = this._Regex.Replace(s, CallableEvaluator, count);
            }

            return (result, replacementCount.GetItemUnchecked(0));
        }

        /// <summary>
        /// Split string by the occurrences of the pattern.
        /// </summary>
        public Sharpy.List<string> Split(string s, int maxsplit = 0)
        {
            Sharpy.List<string> result = new Sharpy.List<string>()
            {
            };
            if (maxsplit == 0)
            {
                foreach (var __loopVar_3 in this._Regex.Split(s))
                {
                    var part = __loopVar_3;
                    result.Append(part);
                }
            }
            else
            {
                foreach (var __loopVar_4 in this._Regex.Split(s, maxsplit + 1))
                {
                    var part_1 = __loopVar_4;
                    result.Append(part_1);
                }
            }

            return result;
        }

        /// <summary>
        /// Returns a string representation of the compiled pattern.
        /// </summary>
        public override string ToString()
        {
            return "re.compile('" + this._PatternStr + "')";
        }

        /// <summary>
        /// Compile a regular expression pattern.
        /// </summary>
        public Pattern(string patternStr, int flags = 0)
        {
            this._PatternStr = patternStr;
            this._Flags = flags;
            this._Groupindex = new Sharpy.Dict<string, int>()
            {
            };
            this._GroupindexCached = false;
            string translated = global::Sharpy.ReModule.ReModuleModule._TranslatePattern(patternStr);
            try
            {
                this._Regex = new global::System.Text.RegularExpressions.Regex(translated, global::Sharpy.ReModule.ReModuleModule._FlagsToOptions(flags));
            }
            catch (global::System.Exception ex)
            {
                throw new global::Sharpy.ReModule.Error(global::Sharpy.Builtins.Str(ex), patternStr);
            }
        }
    }

    /// <summary>
    /// Wraps a .NET Match with Python-compatible regex match API.
    /// </summary>
    [global::Sharpy.SharpyModuleType("re", "MatchResult")]
    public sealed class MatchResult
    {
        private global::System.Text.RegularExpressions.Match _Match;
        private string _String;
        private string _PatternStr;
        private int _Pos;
        private int _Endpos;
        private global::Sharpy.ReModule.Pattern? _Re;
        public string String
        {
            get
            {
                _ = "The input string.";
                return this._String;
            }
        }

        public string Pattern
        {
            get
            {
                _ = "The pattern string.";
                return this._PatternStr;
            }
        }

        public int Pos
        {
            get
            {
                _ = "The start position of the search.";
                return this._Pos;
            }
        }

        public int Endpos
        {
            get
            {
                _ = "The end position of the search.";
                return this._Endpos;
            }
        }

        public global::Sharpy.ReModule.Pattern? Re
        {
            get
            {
                _ = "The compiled pattern object that produced this match.";
                return this._Re;
            }
        }

        public int? Lastindex
        {
            get
            {
                _ = "The integer index of the last matched capturing group.";
                int i = this._Match.Groups.Count - 1;
                while (i >= 1)
                {
                    if (this._Match.Groups[i].Success)
                    {
                        return i;
                    }

                    i = i - 1;
                }

                return null;
            }
        }

        public string? Lastgroup
        {
            get
            {
                _ = "The name of the last matched capturing group, or None if unnamed.";
                int? idx = this.Lastindex;
                if (idx is null)
                {
                    return null;
                }

                if (this._Re is null)
                {
                    return null;
                }

                int grpIdx = idx.Value;
                string name = this._Re!.Regex.GroupNameFromNumber(grpIdx);
                if (name == global::Sharpy.Builtins.Str(grpIdx))
                {
                    return null;
                }

                return name;
            }
        }

        /// <summary>
        /// Return the string matched by group number.
        /// </summary>
        public string? Group(int n = 0)
        {
            if (n < 0 || n >= this._Match.Groups.Count)
            {
                throw new global::Sharpy.IndexError("no such group");
            }

            global::System.Text.RegularExpressions.Group g = this._Match.Groups[n];
            return g.Success ? g.Value : null;
        }

        /// <summary>
        /// Return the string matched by a named group.
        /// </summary>
        public string? Group(string name)
        {
            if (this._Re is not null)
            {
                if (this._Re!.Regex.GroupNumberFromName(name) == -1)
                {
                    throw new global::Sharpy.IndexError("no such group");
                }
            }

            global::System.Text.RegularExpressions.Group g = this._Match.Groups[name];
            return g.Success ? g.Value : null;
        }

        /// <summary>
        /// Return a list of all subgroups (groups 1..n).
        /// </summary>
        public Sharpy.List<string?> Groups()
        {
            Sharpy.List<string?> result = new Sharpy.List<string?>()
            {
            };
            int i = 1;
            while (i < this._Match.Groups.Count)
            {
                global::System.Text.RegularExpressions.Group g = this._Match.Groups[i];
                result.Append(g.Success ? g.Value : null);
                i = i + 1;
            }

            return result;
        }

        /// <summary>
        /// Return a dict of all named subgroups.
        /// </summary>
        public Sharpy.Dict<string, string?> Groupdict()
        {
            Sharpy.Dict<string, string?> result = new Sharpy.Dict<string, string?>()
            {
            };
            if (this._Re is null)
            {
                return result;
            }

            var names = this._Re!.Regex.GetGroupNames();
            foreach (var __loopVar_5 in names)
            {
                var name = __loopVar_5;
                if (!global::Sharpy.StringExtensions.Isdigit(name))
                {
                    global::System.Text.RegularExpressions.Group g = this._Match.Groups[name];
                    result[name] = g.Success ? g.Value : null;
                }
            }

            return result;
        }

        /// <summary>
        /// Start index of the matched group.
        /// </summary>
        public int Start(int groupNum = 0)
        {
            if (groupNum < 0 || groupNum >= this._Match.Groups.Count)
            {
                throw new global::Sharpy.IndexError("no such group");
            }

            global::System.Text.RegularExpressions.Group g = this._Match.Groups[groupNum];
            return g.Success ? g.Index : -1;
        }

        /// <summary>
        /// End index of the matched group.
        /// </summary>
        public int End(int groupNum = 0)
        {
            if (groupNum < 0 || groupNum >= this._Match.Groups.Count)
            {
                throw new global::Sharpy.IndexError("no such group");
            }

            global::System.Text.RegularExpressions.Group g = this._Match.Groups[groupNum];
            return g.Success ? g.Index + g.Length : -1;
        }

        /// <summary>
        /// Returns (start, end) for the matched group.
        /// </summary>
        public global::System.ValueTuple<int, int> Span(int groupNum = 0)
        {
            return (this.Start(groupNum), this.End(groupNum));
        }

        /// <summary>
        /// Return the string obtained by doing backslash substitution on the template.
        /// </summary>
        public string Expand(string template)
        {
            string translated = global::Sharpy.ReModule.ReModuleModule._TranslateReplacement(template);
            return this._Match.Result(translated);
        }

        /// <summary>
        /// String representation of the match.
        /// </summary>
        public override string ToString()
        {
            return "<re.Match object; span=(" + global::Sharpy.Builtins.Str(this._Match.Index) + ", " + global::Sharpy.Builtins.Str(this._Match.Index + this._Match.Length) + "), match='" + this._Match.Value + "'>";
        }

        /// <summary>
        /// Access group by index.
        /// </summary>
        public string? this[int n]
        {
            get
            {
                return this.Group(n);
            }
        }

        /// <summary>
        /// Create a MatchResult wrapping a .NET Match object.
        /// </summary>
        public MatchResult(global::System.Text.RegularExpressions.Match netMatch, string @string, string patternStr, int pos, int endpos, global::Sharpy.ReModule.Pattern? compiledPattern = null)
        {
            this._Match = netMatch;
            this._String = @string;
            this._PatternStr = patternStr;
            this._Pos = pos;
            this._Endpos = endpos;
            this._Re = compiledPattern;
        }
    }
}
