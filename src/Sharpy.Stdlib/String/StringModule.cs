// Generated from src/Sharpy.Stdlib/spy/string_module.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/string_module.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy.StringModule
{
    /// <summary>
    /// Common string constants (ascii_letters, digits, punctuation, etc.).
    /// </summary>
    public static partial class StringModuleModule
    {
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string AsciiLowercase__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte AsciiLowercase__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void AsciiLowercase__init()
        {
            AsciiLowercase__field = "abcdefghijklmnopqrstuvwxyz";
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void AsciiLowercase__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref AsciiLowercase__state, "ascii_lowercase", AsciiLowercase__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref string AsciiLowercase
        {
            get
            {
                if (!__Swept.Done)
                    AsciiLowercase__slow();
                return ref AsciiLowercase__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void AsciiLowercase__ensure()
        {
            if (!__Swept.Done)
                AsciiLowercase__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void AsciiLowercase__storeSlow(string value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref AsciiLowercase__state, ref AsciiLowercase__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string AsciiLowercase__set(string value)
        {
            if (__Swept.Done)
                AsciiLowercase__field = value;
            else
                AsciiLowercase__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<string>? AsciiLowercase__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref AsciiLowercase__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<string>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref string AsciiLowercase__out(out global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            spill = AsciiLowercase__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref AsciiLowercase__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R AsciiLowercase__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            if (spill != null)
                AsciiLowercase__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string AsciiUppercase__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte AsciiUppercase__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void AsciiUppercase__init()
        {
            AsciiUppercase__field = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void AsciiUppercase__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref AsciiUppercase__state, "ascii_uppercase", AsciiUppercase__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref string AsciiUppercase
        {
            get
            {
                if (!__Swept.Done)
                    AsciiUppercase__slow();
                return ref AsciiUppercase__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void AsciiUppercase__ensure()
        {
            if (!__Swept.Done)
                AsciiUppercase__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void AsciiUppercase__storeSlow(string value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref AsciiUppercase__state, ref AsciiUppercase__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string AsciiUppercase__set(string value)
        {
            if (__Swept.Done)
                AsciiUppercase__field = value;
            else
                AsciiUppercase__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<string>? AsciiUppercase__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref AsciiUppercase__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<string>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref string AsciiUppercase__out(out global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            spill = AsciiUppercase__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref AsciiUppercase__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R AsciiUppercase__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            if (spill != null)
                AsciiUppercase__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string AsciiLetters__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte AsciiLetters__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void AsciiLetters__init()
        {
            AsciiLetters__field = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void AsciiLetters__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref AsciiLetters__state, "ascii_letters", AsciiLetters__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref string AsciiLetters
        {
            get
            {
                if (!__Swept.Done)
                    AsciiLetters__slow();
                return ref AsciiLetters__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void AsciiLetters__ensure()
        {
            if (!__Swept.Done)
                AsciiLetters__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void AsciiLetters__storeSlow(string value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref AsciiLetters__state, ref AsciiLetters__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string AsciiLetters__set(string value)
        {
            if (__Swept.Done)
                AsciiLetters__field = value;
            else
                AsciiLetters__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<string>? AsciiLetters__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref AsciiLetters__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<string>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref string AsciiLetters__out(out global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            spill = AsciiLetters__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref AsciiLetters__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R AsciiLetters__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            if (spill != null)
                AsciiLetters__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string Digits__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Digits__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Digits__init()
        {
            Digits__field = "0123456789";
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Digits__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Digits__state, "digits", Digits__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref string Digits
        {
            get
            {
                if (!__Swept.Done)
                    Digits__slow();
                return ref Digits__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Digits__ensure()
        {
            if (!__Swept.Done)
                Digits__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Digits__storeSlow(string value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Digits__state, ref Digits__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string Digits__set(string value)
        {
            if (__Swept.Done)
                Digits__field = value;
            else
                Digits__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<string>? Digits__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Digits__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<string>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref string Digits__out(out global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            spill = Digits__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Digits__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Digits__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            if (spill != null)
                Digits__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string Hexdigits__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Hexdigits__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Hexdigits__init()
        {
            Hexdigits__field = "0123456789abcdefABCDEF";
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Hexdigits__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Hexdigits__state, "hexdigits", Hexdigits__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref string Hexdigits
        {
            get
            {
                if (!__Swept.Done)
                    Hexdigits__slow();
                return ref Hexdigits__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Hexdigits__ensure()
        {
            if (!__Swept.Done)
                Hexdigits__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Hexdigits__storeSlow(string value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Hexdigits__state, ref Hexdigits__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string Hexdigits__set(string value)
        {
            if (__Swept.Done)
                Hexdigits__field = value;
            else
                Hexdigits__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<string>? Hexdigits__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Hexdigits__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<string>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref string Hexdigits__out(out global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            spill = Hexdigits__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Hexdigits__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Hexdigits__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            if (spill != null)
                Hexdigits__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string Octdigits__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Octdigits__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Octdigits__init()
        {
            Octdigits__field = "01234567";
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Octdigits__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Octdigits__state, "octdigits", Octdigits__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref string Octdigits
        {
            get
            {
                if (!__Swept.Done)
                    Octdigits__slow();
                return ref Octdigits__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Octdigits__ensure()
        {
            if (!__Swept.Done)
                Octdigits__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Octdigits__storeSlow(string value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Octdigits__state, ref Octdigits__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string Octdigits__set(string value)
        {
            if (__Swept.Done)
                Octdigits__field = value;
            else
                Octdigits__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<string>? Octdigits__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Octdigits__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<string>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref string Octdigits__out(out global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            spill = Octdigits__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Octdigits__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Octdigits__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            if (spill != null)
                Octdigits__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string Punctuation__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Punctuation__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Punctuation__init()
        {
            Punctuation__field = "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Punctuation__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Punctuation__state, "punctuation", Punctuation__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref string Punctuation
        {
            get
            {
                if (!__Swept.Done)
                    Punctuation__slow();
                return ref Punctuation__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Punctuation__ensure()
        {
            if (!__Swept.Done)
                Punctuation__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Punctuation__storeSlow(string value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Punctuation__state, ref Punctuation__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string Punctuation__set(string value)
        {
            if (__Swept.Done)
                Punctuation__field = value;
            else
                Punctuation__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<string>? Punctuation__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Punctuation__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<string>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref string Punctuation__out(out global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            spill = Punctuation__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Punctuation__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Punctuation__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            if (spill != null)
                Punctuation__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string Whitespace__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Whitespace__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Whitespace__init()
        {
            Whitespace__field = " \t\n\r\v\f";
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Whitespace__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Whitespace__state, "whitespace", Whitespace__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref string Whitespace
        {
            get
            {
                if (!__Swept.Done)
                    Whitespace__slow();
                return ref Whitespace__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Whitespace__ensure()
        {
            if (!__Swept.Done)
                Whitespace__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Whitespace__storeSlow(string value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Whitespace__state, ref Whitespace__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string Whitespace__set(string value)
        {
            if (__Swept.Done)
                Whitespace__field = value;
            else
                Whitespace__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<string>? Whitespace__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Whitespace__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<string>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref string Whitespace__out(out global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            spill = Whitespace__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Whitespace__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Whitespace__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            if (spill != null)
                Whitespace__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string Printable__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Printable__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Printable__init()
        {
            Printable__field = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~ \t\n\r\v\f";
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Printable__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Printable__state, "printable", Printable__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref string Printable
        {
            get
            {
                if (!__Swept.Done)
                    Printable__slow();
                return ref Printable__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Printable__ensure()
        {
            if (!__Swept.Done)
                Printable__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Printable__storeSlow(string value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Printable__state, ref Printable__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string Printable__set(string value)
        {
            if (__Swept.Done)
                Printable__field = value;
            else
                Printable__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<string>? Printable__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Printable__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<string>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref string Printable__out(out global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            spill = Printable__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Printable__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Printable__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            if (spill != null)
                Printable__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static global::Sharpy.StaticInitHost __host;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void __Sweep()
        {
            global::Sharpy.StaticInit.Run(ref AsciiLowercase__state, "ascii_lowercase", AsciiLowercase__init);
            global::Sharpy.StaticInit.Run(ref AsciiUppercase__state, "ascii_uppercase", AsciiUppercase__init);
            global::Sharpy.StaticInit.Run(ref AsciiLetters__state, "ascii_letters", AsciiLetters__init);
            global::Sharpy.StaticInit.Run(ref Digits__state, "digits", Digits__init);
            global::Sharpy.StaticInit.Run(ref Hexdigits__state, "hexdigits", Hexdigits__init);
            global::Sharpy.StaticInit.Run(ref Octdigits__state, "octdigits", Octdigits__init);
            global::Sharpy.StaticInit.Run(ref Punctuation__state, "punctuation", Punctuation__init);
            global::Sharpy.StaticInit.Run(ref Whitespace__state, "whitespace", Whitespace__init);
            global::Sharpy.StaticInit.Run(ref Printable__state, "printable", Printable__init);
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
