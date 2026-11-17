// Snapshot: Null coalescing operator (??)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace NullCoalescing0003
{
    public static partial class NullCoalescing0003Module
    {
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static Optional<int> X__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte X__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void X__init()
        {
            X__field = Optional<int>.None;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void X__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref X__state, "x", X__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref Optional<int> X
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
        internal static Optional<int> X__set(Optional<int> value)
        {
            if (__Swept.Done)
                X__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref X__state, ref X__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<Optional<int>>? X__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref X__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<Optional<int>>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref Optional<int> X__out(out global::System.Runtime.CompilerServices.StrongBox<Optional<int>>? spill)
        {
            spill = X__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref X__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R X__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<Optional<int>>? spill)
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
            Y__field = (global::NullCoalescing0003.NullCoalescing0003Module.X).UnwrapOr(42);
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
        internal static int Y__set(int value)
        {
            if (__Swept.Done)
                Y__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Y__state, ref Y__field, value);
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
        internal static Optional<int> A__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte A__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void A__init()
        {
            A__field = Optional<int>.Some(100);
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void A__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref A__state, "a", A__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref Optional<int> A
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
        internal static Optional<int> A__set(Optional<int> value)
        {
            if (__Swept.Done)
                A__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref A__state, ref A__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<Optional<int>>? A__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref A__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<Optional<int>>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref Optional<int> A__out(out global::System.Runtime.CompilerServices.StrongBox<Optional<int>>? spill)
        {
            spill = A__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref A__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R A__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<Optional<int>>? spill)
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
            B__field = (global::NullCoalescing0003.NullCoalescing0003Module.A).UnwrapOr(999);
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
        internal static Optional<string> Name__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Name__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Name__init()
        {
            Name__field = Optional<string>.None;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Name__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Name__state, "name", Name__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref Optional<string> Name
        {
            get
            {
                if (!__Swept.Done)
                    Name__slow();
                return ref Name__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Name__ensure()
        {
            if (!__Swept.Done)
                Name__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static Optional<string> Name__set(Optional<string> value)
        {
            if (__Swept.Done)
                Name__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Name__state, ref Name__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<Optional<string>>? Name__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Name__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<Optional<string>>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref Optional<string> Name__out(out global::System.Runtime.CompilerServices.StrongBox<Optional<string>>? spill)
        {
            spill = Name__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Name__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Name__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<Optional<string>>? spill)
        {
            if (spill != null)
                Name__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string DefaultName__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte DefaultName__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void DefaultName__init()
        {
            DefaultName__field = (global::NullCoalescing0003.NullCoalescing0003Module.Name).UnwrapOr("Guest");
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void DefaultName__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref DefaultName__state, "default_name", DefaultName__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref string DefaultName
        {
            get
            {
                if (!__Swept.Done)
                    DefaultName__slow();
                return ref DefaultName__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void DefaultName__ensure()
        {
            if (!__Swept.Done)
                DefaultName__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string DefaultName__set(string value)
        {
            if (__Swept.Done)
                DefaultName__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref DefaultName__state, ref DefaultName__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<string>? DefaultName__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref DefaultName__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<string>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref string DefaultName__out(out global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            spill = DefaultName__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref DefaultName__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R DefaultName__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            if (spill != null)
                DefaultName__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static Optional<int> First__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte First__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void First__init()
        {
            First__field = Optional<int>.None;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void First__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref First__state, "first", First__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref Optional<int> First
        {
            get
            {
                if (!__Swept.Done)
                    First__slow();
                return ref First__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void First__ensure()
        {
            if (!__Swept.Done)
                First__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static Optional<int> First__set(Optional<int> value)
        {
            if (__Swept.Done)
                First__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref First__state, ref First__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<Optional<int>>? First__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref First__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<Optional<int>>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref Optional<int> First__out(out global::System.Runtime.CompilerServices.StrongBox<Optional<int>>? spill)
        {
            spill = First__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref First__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R First__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<Optional<int>>? spill)
        {
            if (spill != null)
                First__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static Optional<int> Second__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Second__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Second__init()
        {
            Second__field = Optional<int>.None;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Second__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Second__state, "second", Second__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref Optional<int> Second
        {
            get
            {
                if (!__Swept.Done)
                    Second__slow();
                return ref Second__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Second__ensure()
        {
            if (!__Swept.Done)
                Second__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static Optional<int> Second__set(Optional<int> value)
        {
            if (__Swept.Done)
                Second__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Second__state, ref Second__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<Optional<int>>? Second__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Second__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<Optional<int>>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref Optional<int> Second__out(out global::System.Runtime.CompilerServices.StrongBox<Optional<int>>? spill)
        {
            spill = Second__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Second__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Second__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<Optional<int>>? spill)
        {
            if (spill != null)
                Second__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int Third__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Third__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Third__init()
        {
            Third__field = 77;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Third__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Third__state, "third", Third__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int Third
        {
            get
            {
                if (!__Swept.Done)
                    Third__slow();
                return ref Third__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Third__ensure()
        {
            if (!__Swept.Done)
                Third__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int Third__set(int value)
        {
            if (__Swept.Done)
                Third__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Third__state, ref Third__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? Third__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Third__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int Third__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = Third__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Third__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Third__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                Third__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int Result__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Result__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Result__init()
        {
            Result__field = ((global::NullCoalescing0003.NullCoalescing0003Module.First).IsSome ? global::NullCoalescing0003.NullCoalescing0003Module.First : global::NullCoalescing0003.NullCoalescing0003Module.Second).UnwrapOr(global::NullCoalescing0003.NullCoalescing0003Module.Third);
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
#line (16, 5) - (16, 13) 12 "null_coalescing_0003.spy"
            global::Sharpy.Builtins.Print(global::NullCoalescing0003.NullCoalescing0003Module.Y);
#line (19, 5) - (19, 13) 12 "null_coalescing_0003.spy"
            global::Sharpy.Builtins.Print(global::NullCoalescing0003.NullCoalescing0003Module.B);
#line (22, 5) - (22, 24) 12 "null_coalescing_0003.spy"
            global::Sharpy.Builtins.Print(global::NullCoalescing0003.NullCoalescing0003Module.DefaultName);
#line (25, 5) - (25, 18) 12 "null_coalescing_0003.spy"
            global::Sharpy.Builtins.Print(global::NullCoalescing0003.NullCoalescing0003Module.Result);
#line hidden
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static global::Sharpy.StaticInitHost __host;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void __Sweep()
        {
            global::Sharpy.StaticInit.Run(ref X__state, "x", X__init);
            global::Sharpy.StaticInit.Run(ref Y__state, "y", Y__init);
            global::Sharpy.StaticInit.Run(ref A__state, "a", A__init);
            global::Sharpy.StaticInit.Run(ref B__state, "b", B__init);
            global::Sharpy.StaticInit.Run(ref Name__state, "name", Name__init);
            global::Sharpy.StaticInit.Run(ref DefaultName__state, "default_name", DefaultName__init);
            global::Sharpy.StaticInit.Run(ref First__state, "first", First__init);
            global::Sharpy.StaticInit.Run(ref Second__state, "second", Second__init);
            global::Sharpy.StaticInit.Run(ref Third__state, "third", Third__init);
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
