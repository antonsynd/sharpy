// Generated from src/Sharpy.Stdlib/spy/socket_module.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/socket_module.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy.SocketModule
{
    /// <summary>
    /// Low-level networking interface (TCP, UDP, DNS).
    /// </summary>
    public static partial class SocketModuleModule
    {
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int AF_INET__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte AF_INET__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void AF_INET__init()
        {
            AF_INET__field = (int)global::System.Net.Sockets.AddressFamily.InterNetwork;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void AF_INET__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref AF_INET__state, "AF_INET", AF_INET__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int AF_INET
        {
            get
            {
                if (!__Swept.Done)
                    AF_INET__slow();
                return ref AF_INET__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void AF_INET__ensure()
        {
            if (!__Swept.Done)
                AF_INET__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int AF_INET__set(int value)
        {
            if (__Swept.Done)
                AF_INET__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref AF_INET__state, ref AF_INET__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? AF_INET__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref AF_INET__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int AF_INET__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = AF_INET__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref AF_INET__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R AF_INET__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                AF_INET__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int AF_INET6__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte AF_INET6__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void AF_INET6__init()
        {
            AF_INET6__field = (int)global::System.Net.Sockets.AddressFamily.InterNetworkV6;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void AF_INET6__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref AF_INET6__state, "AF_INET6", AF_INET6__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int AF_INET6
        {
            get
            {
                if (!__Swept.Done)
                    AF_INET6__slow();
                return ref AF_INET6__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void AF_INET6__ensure()
        {
            if (!__Swept.Done)
                AF_INET6__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int AF_INET6__set(int value)
        {
            if (__Swept.Done)
                AF_INET6__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref AF_INET6__state, ref AF_INET6__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? AF_INET6__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref AF_INET6__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int AF_INET6__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = AF_INET6__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref AF_INET6__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R AF_INET6__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                AF_INET6__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int AF_UNIX__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte AF_UNIX__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void AF_UNIX__init()
        {
            AF_UNIX__field = (int)global::System.Net.Sockets.AddressFamily.Unix;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void AF_UNIX__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref AF_UNIX__state, "AF_UNIX", AF_UNIX__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int AF_UNIX
        {
            get
            {
                if (!__Swept.Done)
                    AF_UNIX__slow();
                return ref AF_UNIX__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void AF_UNIX__ensure()
        {
            if (!__Swept.Done)
                AF_UNIX__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int AF_UNIX__set(int value)
        {
            if (__Swept.Done)
                AF_UNIX__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref AF_UNIX__state, ref AF_UNIX__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? AF_UNIX__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref AF_UNIX__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int AF_UNIX__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = AF_UNIX__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref AF_UNIX__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R AF_UNIX__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                AF_UNIX__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SOCK_STREAM__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte SOCK_STREAM__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SOCK_STREAM__init()
        {
            SOCK_STREAM__field = (int)global::System.Net.Sockets.SocketType.Stream;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SOCK_STREAM__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref SOCK_STREAM__state, "SOCK_STREAM", SOCK_STREAM__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int SOCK_STREAM
        {
            get
            {
                if (!__Swept.Done)
                    SOCK_STREAM__slow();
                return ref SOCK_STREAM__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void SOCK_STREAM__ensure()
        {
            if (!__Swept.Done)
                SOCK_STREAM__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SOCK_STREAM__set(int value)
        {
            if (__Swept.Done)
                SOCK_STREAM__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref SOCK_STREAM__state, ref SOCK_STREAM__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? SOCK_STREAM__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref SOCK_STREAM__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int SOCK_STREAM__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = SOCK_STREAM__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref SOCK_STREAM__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R SOCK_STREAM__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                SOCK_STREAM__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SOCK_DGRAM__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte SOCK_DGRAM__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SOCK_DGRAM__init()
        {
            SOCK_DGRAM__field = (int)global::System.Net.Sockets.SocketType.Dgram;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SOCK_DGRAM__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref SOCK_DGRAM__state, "SOCK_DGRAM", SOCK_DGRAM__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int SOCK_DGRAM
        {
            get
            {
                if (!__Swept.Done)
                    SOCK_DGRAM__slow();
                return ref SOCK_DGRAM__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void SOCK_DGRAM__ensure()
        {
            if (!__Swept.Done)
                SOCK_DGRAM__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SOCK_DGRAM__set(int value)
        {
            if (__Swept.Done)
                SOCK_DGRAM__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref SOCK_DGRAM__state, ref SOCK_DGRAM__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? SOCK_DGRAM__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref SOCK_DGRAM__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int SOCK_DGRAM__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = SOCK_DGRAM__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref SOCK_DGRAM__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R SOCK_DGRAM__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                SOCK_DGRAM__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SOCK_RAW__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte SOCK_RAW__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SOCK_RAW__init()
        {
            SOCK_RAW__field = (int)global::System.Net.Sockets.SocketType.Raw;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SOCK_RAW__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref SOCK_RAW__state, "SOCK_RAW", SOCK_RAW__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int SOCK_RAW
        {
            get
            {
                if (!__Swept.Done)
                    SOCK_RAW__slow();
                return ref SOCK_RAW__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void SOCK_RAW__ensure()
        {
            if (!__Swept.Done)
                SOCK_RAW__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SOCK_RAW__set(int value)
        {
            if (__Swept.Done)
                SOCK_RAW__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref SOCK_RAW__state, ref SOCK_RAW__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? SOCK_RAW__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref SOCK_RAW__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int SOCK_RAW__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = SOCK_RAW__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref SOCK_RAW__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R SOCK_RAW__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                SOCK_RAW__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int IPPROTO_IP__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte IPPROTO_IP__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void IPPROTO_IP__init()
        {
            IPPROTO_IP__field = (int)global::System.Net.Sockets.ProtocolType.IP;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void IPPROTO_IP__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref IPPROTO_IP__state, "IPPROTO_IP", IPPROTO_IP__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int IPPROTO_IP
        {
            get
            {
                if (!__Swept.Done)
                    IPPROTO_IP__slow();
                return ref IPPROTO_IP__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void IPPROTO_IP__ensure()
        {
            if (!__Swept.Done)
                IPPROTO_IP__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int IPPROTO_IP__set(int value)
        {
            if (__Swept.Done)
                IPPROTO_IP__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref IPPROTO_IP__state, ref IPPROTO_IP__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? IPPROTO_IP__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref IPPROTO_IP__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int IPPROTO_IP__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = IPPROTO_IP__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref IPPROTO_IP__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R IPPROTO_IP__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                IPPROTO_IP__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int IPPROTO_TCP__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte IPPROTO_TCP__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void IPPROTO_TCP__init()
        {
            IPPROTO_TCP__field = (int)global::System.Net.Sockets.ProtocolType.Tcp;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void IPPROTO_TCP__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref IPPROTO_TCP__state, "IPPROTO_TCP", IPPROTO_TCP__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int IPPROTO_TCP
        {
            get
            {
                if (!__Swept.Done)
                    IPPROTO_TCP__slow();
                return ref IPPROTO_TCP__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void IPPROTO_TCP__ensure()
        {
            if (!__Swept.Done)
                IPPROTO_TCP__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int IPPROTO_TCP__set(int value)
        {
            if (__Swept.Done)
                IPPROTO_TCP__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref IPPROTO_TCP__state, ref IPPROTO_TCP__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? IPPROTO_TCP__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref IPPROTO_TCP__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int IPPROTO_TCP__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = IPPROTO_TCP__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref IPPROTO_TCP__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R IPPROTO_TCP__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                IPPROTO_TCP__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int IPPROTO_UDP__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte IPPROTO_UDP__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void IPPROTO_UDP__init()
        {
            IPPROTO_UDP__field = (int)global::System.Net.Sockets.ProtocolType.Udp;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void IPPROTO_UDP__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref IPPROTO_UDP__state, "IPPROTO_UDP", IPPROTO_UDP__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int IPPROTO_UDP
        {
            get
            {
                if (!__Swept.Done)
                    IPPROTO_UDP__slow();
                return ref IPPROTO_UDP__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void IPPROTO_UDP__ensure()
        {
            if (!__Swept.Done)
                IPPROTO_UDP__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int IPPROTO_UDP__set(int value)
        {
            if (__Swept.Done)
                IPPROTO_UDP__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref IPPROTO_UDP__state, ref IPPROTO_UDP__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? IPPROTO_UDP__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref IPPROTO_UDP__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int IPPROTO_UDP__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = IPPROTO_UDP__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref IPPROTO_UDP__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R IPPROTO_UDP__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                IPPROTO_UDP__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SOL_SOCKET__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte SOL_SOCKET__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SOL_SOCKET__init()
        {
            SOL_SOCKET__field = (int)global::System.Net.Sockets.SocketOptionLevel.Socket;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SOL_SOCKET__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref SOL_SOCKET__state, "SOL_SOCKET", SOL_SOCKET__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int SOL_SOCKET
        {
            get
            {
                if (!__Swept.Done)
                    SOL_SOCKET__slow();
                return ref SOL_SOCKET__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void SOL_SOCKET__ensure()
        {
            if (!__Swept.Done)
                SOL_SOCKET__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SOL_SOCKET__set(int value)
        {
            if (__Swept.Done)
                SOL_SOCKET__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref SOL_SOCKET__state, ref SOL_SOCKET__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? SOL_SOCKET__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref SOL_SOCKET__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int SOL_SOCKET__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = SOL_SOCKET__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref SOL_SOCKET__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R SOL_SOCKET__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                SOL_SOCKET__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SO_REUSEADDR__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte SO_REUSEADDR__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SO_REUSEADDR__init()
        {
            SO_REUSEADDR__field = (int)global::System.Net.Sockets.SocketOptionName.ReuseAddress;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SO_REUSEADDR__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref SO_REUSEADDR__state, "SO_REUSEADDR", SO_REUSEADDR__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int SO_REUSEADDR
        {
            get
            {
                if (!__Swept.Done)
                    SO_REUSEADDR__slow();
                return ref SO_REUSEADDR__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void SO_REUSEADDR__ensure()
        {
            if (!__Swept.Done)
                SO_REUSEADDR__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SO_REUSEADDR__set(int value)
        {
            if (__Swept.Done)
                SO_REUSEADDR__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref SO_REUSEADDR__state, ref SO_REUSEADDR__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? SO_REUSEADDR__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref SO_REUSEADDR__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int SO_REUSEADDR__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = SO_REUSEADDR__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref SO_REUSEADDR__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R SO_REUSEADDR__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                SO_REUSEADDR__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SO_KEEPALIVE__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte SO_KEEPALIVE__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SO_KEEPALIVE__init()
        {
            SO_KEEPALIVE__field = (int)global::System.Net.Sockets.SocketOptionName.KeepAlive;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SO_KEEPALIVE__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref SO_KEEPALIVE__state, "SO_KEEPALIVE", SO_KEEPALIVE__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int SO_KEEPALIVE
        {
            get
            {
                if (!__Swept.Done)
                    SO_KEEPALIVE__slow();
                return ref SO_KEEPALIVE__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void SO_KEEPALIVE__ensure()
        {
            if (!__Swept.Done)
                SO_KEEPALIVE__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SO_KEEPALIVE__set(int value)
        {
            if (__Swept.Done)
                SO_KEEPALIVE__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref SO_KEEPALIVE__state, ref SO_KEEPALIVE__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? SO_KEEPALIVE__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref SO_KEEPALIVE__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int SO_KEEPALIVE__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = SO_KEEPALIVE__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref SO_KEEPALIVE__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R SO_KEEPALIVE__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                SO_KEEPALIVE__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SO_BROADCAST__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte SO_BROADCAST__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SO_BROADCAST__init()
        {
            SO_BROADCAST__field = (int)global::System.Net.Sockets.SocketOptionName.Broadcast;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SO_BROADCAST__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref SO_BROADCAST__state, "SO_BROADCAST", SO_BROADCAST__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int SO_BROADCAST
        {
            get
            {
                if (!__Swept.Done)
                    SO_BROADCAST__slow();
                return ref SO_BROADCAST__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void SO_BROADCAST__ensure()
        {
            if (!__Swept.Done)
                SO_BROADCAST__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SO_BROADCAST__set(int value)
        {
            if (__Swept.Done)
                SO_BROADCAST__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref SO_BROADCAST__state, ref SO_BROADCAST__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? SO_BROADCAST__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref SO_BROADCAST__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int SO_BROADCAST__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = SO_BROADCAST__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref SO_BROADCAST__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R SO_BROADCAST__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                SO_BROADCAST__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SO_RCVBUF__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte SO_RCVBUF__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SO_RCVBUF__init()
        {
            SO_RCVBUF__field = (int)global::System.Net.Sockets.SocketOptionName.ReceiveBuffer;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SO_RCVBUF__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref SO_RCVBUF__state, "SO_RCVBUF", SO_RCVBUF__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int SO_RCVBUF
        {
            get
            {
                if (!__Swept.Done)
                    SO_RCVBUF__slow();
                return ref SO_RCVBUF__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void SO_RCVBUF__ensure()
        {
            if (!__Swept.Done)
                SO_RCVBUF__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SO_RCVBUF__set(int value)
        {
            if (__Swept.Done)
                SO_RCVBUF__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref SO_RCVBUF__state, ref SO_RCVBUF__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? SO_RCVBUF__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref SO_RCVBUF__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int SO_RCVBUF__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = SO_RCVBUF__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref SO_RCVBUF__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R SO_RCVBUF__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                SO_RCVBUF__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SO_SNDBUF__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte SO_SNDBUF__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SO_SNDBUF__init()
        {
            SO_SNDBUF__field = (int)global::System.Net.Sockets.SocketOptionName.SendBuffer;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SO_SNDBUF__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref SO_SNDBUF__state, "SO_SNDBUF", SO_SNDBUF__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int SO_SNDBUF
        {
            get
            {
                if (!__Swept.Done)
                    SO_SNDBUF__slow();
                return ref SO_SNDBUF__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void SO_SNDBUF__ensure()
        {
            if (!__Swept.Done)
                SO_SNDBUF__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SO_SNDBUF__set(int value)
        {
            if (__Swept.Done)
                SO_SNDBUF__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref SO_SNDBUF__state, ref SO_SNDBUF__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? SO_SNDBUF__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref SO_SNDBUF__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int SO_SNDBUF__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = SO_SNDBUF__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref SO_SNDBUF__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R SO_SNDBUF__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                SO_SNDBUF__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int TCP_NODELAY__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte TCP_NODELAY__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void TCP_NODELAY__init()
        {
            TCP_NODELAY__field = (int)global::System.Net.Sockets.SocketOptionName.NoDelay;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void TCP_NODELAY__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref TCP_NODELAY__state, "TCP_NODELAY", TCP_NODELAY__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int TCP_NODELAY
        {
            get
            {
                if (!__Swept.Done)
                    TCP_NODELAY__slow();
                return ref TCP_NODELAY__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void TCP_NODELAY__ensure()
        {
            if (!__Swept.Done)
                TCP_NODELAY__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int TCP_NODELAY__set(int value)
        {
            if (__Swept.Done)
                TCP_NODELAY__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref TCP_NODELAY__state, ref TCP_NODELAY__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? TCP_NODELAY__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref TCP_NODELAY__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int TCP_NODELAY__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = TCP_NODELAY__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref TCP_NODELAY__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R TCP_NODELAY__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                TCP_NODELAY__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SHUT_RD__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte SHUT_RD__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SHUT_RD__init()
        {
            SHUT_RD__field = (int)global::System.Net.Sockets.SocketShutdown.Receive;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SHUT_RD__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref SHUT_RD__state, "SHUT_RD", SHUT_RD__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int SHUT_RD
        {
            get
            {
                if (!__Swept.Done)
                    SHUT_RD__slow();
                return ref SHUT_RD__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void SHUT_RD__ensure()
        {
            if (!__Swept.Done)
                SHUT_RD__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SHUT_RD__set(int value)
        {
            if (__Swept.Done)
                SHUT_RD__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref SHUT_RD__state, ref SHUT_RD__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? SHUT_RD__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref SHUT_RD__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int SHUT_RD__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = SHUT_RD__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref SHUT_RD__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R SHUT_RD__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                SHUT_RD__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SHUT_WR__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte SHUT_WR__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SHUT_WR__init()
        {
            SHUT_WR__field = (int)global::System.Net.Sockets.SocketShutdown.Send;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SHUT_WR__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref SHUT_WR__state, "SHUT_WR", SHUT_WR__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int SHUT_WR
        {
            get
            {
                if (!__Swept.Done)
                    SHUT_WR__slow();
                return ref SHUT_WR__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void SHUT_WR__ensure()
        {
            if (!__Swept.Done)
                SHUT_WR__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SHUT_WR__set(int value)
        {
            if (__Swept.Done)
                SHUT_WR__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref SHUT_WR__state, ref SHUT_WR__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? SHUT_WR__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref SHUT_WR__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int SHUT_WR__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = SHUT_WR__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref SHUT_WR__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R SHUT_WR__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                SHUT_WR__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SHUT_RDWR__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte SHUT_RDWR__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SHUT_RDWR__init()
        {
            SHUT_RDWR__field = (int)global::System.Net.Sockets.SocketShutdown.Both;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SHUT_RDWR__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref SHUT_RDWR__state, "SHUT_RDWR", SHUT_RDWR__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int SHUT_RDWR
        {
            get
            {
                if (!__Swept.Done)
                    SHUT_RDWR__slow();
                return ref SHUT_RDWR__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void SHUT_RDWR__ensure()
        {
            if (!__Swept.Done)
                SHUT_RDWR__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SHUT_RDWR__set(int value)
        {
            if (__Swept.Done)
                SHUT_RDWR__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref SHUT_RDWR__state, ref SHUT_RDWR__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? SHUT_RDWR__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref SHUT_RDWR__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int SHUT_RDWR__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = SHUT_RDWR__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref SHUT_RDWR__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R SHUT_RDWR__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                SHUT_RDWR__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SOMAXCONN__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte SOMAXCONN__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SOMAXCONN__init()
        {
            SOMAXCONN__field = 128;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void SOMAXCONN__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref SOMAXCONN__state, "SOMAXCONN", SOMAXCONN__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int SOMAXCONN
        {
            get
            {
                if (!__Swept.Done)
                    SOMAXCONN__slow();
                return ref SOMAXCONN__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void SOMAXCONN__ensure()
        {
            if (!__Swept.Done)
                SOMAXCONN__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int SOMAXCONN__set(int value)
        {
            if (__Swept.Done)
                SOMAXCONN__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref SOMAXCONN__state, ref SOMAXCONN__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? SOMAXCONN__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref SOMAXCONN__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int SOMAXCONN__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = SOMAXCONN__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref SOMAXCONN__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R SOMAXCONN__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                SOMAXCONN__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static double? _DefaultTimeout__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte _DefaultTimeout__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void _DefaultTimeout__init()
        {
            _DefaultTimeout__field = null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void _DefaultTimeout__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref _DefaultTimeout__state, "_default_timeout", _DefaultTimeout__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref double? _DefaultTimeout
        {
            get
            {
                if (!__Swept.Done)
                    _DefaultTimeout__slow();
                return ref _DefaultTimeout__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void _DefaultTimeout__ensure()
        {
            if (!__Swept.Done)
                _DefaultTimeout__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static double? _DefaultTimeout__set(double? value)
        {
            if (__Swept.Done)
                _DefaultTimeout__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref _DefaultTimeout__state, ref _DefaultTimeout__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<double?>? _DefaultTimeout__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref _DefaultTimeout__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<double?>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref double? _DefaultTimeout__out(out global::System.Runtime.CompilerServices.StrongBox<double?>? spill)
        {
            spill = _DefaultTimeout__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref _DefaultTimeout__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R _DefaultTimeout__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<double?>? spill)
        {
            if (spill != null)
                _DefaultTimeout__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::Sharpy.Lock _Lock__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte _Lock__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void _Lock__init()
        {
            _Lock__field = new global::Sharpy.Lock();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void _Lock__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref _Lock__state, "_lock", _Lock__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref global::Sharpy.Lock _Lock
        {
            get
            {
                if (!__Swept.Done)
                    _Lock__slow();
                return ref _Lock__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void _Lock__ensure()
        {
            if (!__Swept.Done)
                _Lock__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::Sharpy.Lock _Lock__set(global::Sharpy.Lock value)
        {
            if (__Swept.Done)
                _Lock__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref _Lock__state, ref _Lock__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<global::Sharpy.Lock>? _Lock__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref _Lock__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<global::Sharpy.Lock>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref global::Sharpy.Lock _Lock__out(out global::System.Runtime.CompilerServices.StrongBox<global::Sharpy.Lock>? spill)
        {
            spill = _Lock__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref _Lock__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R _Lock__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<global::Sharpy.Lock>? spill)
        {
            if (spill != null)
                _Lock__set(spill.Value!);
            return result;
        }

        /// <summary>
        /// Refuse a timeout value python3 refuses, at the call that receives it (#2228).
        /// The one rule shared by every timeout-accepting entry point (settimeout,
        /// setdefaulttimeout, create_connection(timeout=)), applied before any state
        /// changes, in python3's order: NaN -> ValueError; a value whose nanosecond
        /// count does not fit int64 (+-inf, |t| >= ~9.22e9 s) -> OverflowError;
        /// negative -> ValueError. None (blocking) and 0.0 (non-blocking) are accepted.
        /// </summary>
        internal static void _CheckTimeout(double? timeout)
        {
            if (timeout is null)
            {
                return;
            }

            double value = timeout.Value;
            if (global::System.Double.IsNaN(value))
            {
                throw new global::Sharpy.ValueError("Invalid value NaN (not a number)");
            }

            double ns = value * 1000000000.0d;
            if (ns >= 9223372036854775808.0d || ns < -9223372036854775808.0d)
            {
                throw new global::Sharpy.OverflowError("timestamp out of range for platform time_t");
            }

            if (value < 0.0d)
            {
                throw new global::Sharpy.ValueError("Timeout value out of range");
            }
        }

        /// <summary>
        /// The socket.timeout for a .NET SocketException whose code is `TimedOut`.
        /// .NET reports the socket's own timeout and a kernel `ETIMEDOUT` alike, so the caller says
        /// which it was: `own_timeout` True (a timeout is set on the socket, so its expiry is the
        /// likelier) gives python's `timed out` with errno None; False (no timeout is set, so only the
        /// kernel can time out) gives the platform's errno (110 on Linux), as python's `TimeoutError`
        /// does. A kernel `ETIMEDOUT` while a timeout is set cannot be told apart and carries None
        /// (docs/deviations.yaml, socket-errno-is-dotnet-socket-error).
        /// </summary>
        internal static global::Sharpy.SocketModule.Timeout _TimedOut(global::System.Net.Sockets.SocketException ex, bool ownTimeout)
        {
            if (ownTimeout)
            {
                return new global::Sharpy.SocketModule.Timeout("timed out", ex);
            }

            return new global::Sharpy.SocketModule.Timeout(ex.Message, ex, global::Sharpy.SocketModule.Error.FromSocketException(ex).Errno);
        }

        /// <summary>
        /// Whether `value` is the number of one of an int-backed enum's `members` (Enum.GetValues).
        /// </summary>
        internal static bool _IsMember(global::System.Array members, int value)
        {
            int i = 0;
            while (i < members.Length)
            {
                var member = members.GetValue(i);
                if (member != null && ((int)member) == value)
                {
                    return true;
                }

                i = i + 1;
            }

            return false;
        }

        /// <summary>
        /// The socket.error for a .NET SocketException at a call site where .NET folds the errno the
        /// platform's call returns into `folded`, whose NativeErrorCode names a different errno: on
        /// Linux, `folded` becomes `linux_errno` with its strerror text; anything else goes to the seam
        /// (`error.from_socket_exception`). Keyed on the CLR `SocketError`, never the message.
        /// Each call site has its own entry point below (one row of the rule each), so a call site
        /// cannot name a rule that does not exist. Off Linux nothing is rewritten: on Windows .NET's
        /// code is WinSock's own, which CPython reports; macOS is unmeasured (docs/deviations.yaml,
        /// socket-errno-is-dotnet-socket-error).
        /// </summary>
        internal static global::Sharpy.SocketModule.Error _RewriteErrno(global::System.Net.Sockets.SocketException ex, global::System.Net.Sockets.SocketError folded, int linuxErrno, string strerror)
        {
            if (global::System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(global::System.Runtime.InteropServices.OSPlatform.Linux) && ex.SocketErrorCode == folded)
            {
                return new global::Sharpy.SocketModule.Error(strerror, ex, linuxErrno);
            }

            return global::Sharpy.SocketModule.Error.FromSocketException(ex);
        }

        /// <summary>
        /// `connect` failing on `sock`. Only a non-blocking STREAM connect can be in progress: .NET
        /// folds the kernel's EINPROGRESS into `WouldBlock` (native EAGAIN, 11), and CPython reports 115
        /// `EINPROGRESS`. A datagram connect never returns EINPROGRESS (it completes at once), so its
        /// `WouldBlock` is a real EAGAIN (e.g. no ephemeral port left to auto-bind), which CPython
        /// reports as 11; and a blocking connect reaches `WouldBlock` only on a real EAGAIN too. Both go
        /// to the seam unchanged.
        /// </summary>
        internal static global::Sharpy.SocketModule.Error _ConnectError(global::System.Net.Sockets.SocketException ex, global::System.Net.Sockets.Socket sock)
        {
            if (sock.Blocking || sock.SocketType != global::System.Net.Sockets.SocketType.Stream)
            {
                return global::Sharpy.SocketModule.Error.FromSocketException(ex);
            }

            return global::Sharpy.SocketModule.SocketModuleModule._RewriteErrno(ex, global::System.Net.Sockets.SocketError.WouldBlock, 115, "Operation now in progress");
        }

        /// <summary>
        /// `setsockopt`/`getsockopt` on an option .NET refused itself (socket._kernel_says_enoprotoopt):
        /// .NET's `OperationNotSupported` (95); the kernel would answer 92 `ENOPROTOOPT`.
        /// </summary>
        internal static global::Sharpy.SocketModule.Error _UnknownOptionError(global::System.Net.Sockets.SocketException ex)
        {
            return global::Sharpy.SocketModule.SocketModuleModule._RewriteErrno(ex, global::System.Net.Sockets.SocketError.OperationNotSupported, 92, "Protocol not available");
        }

        /// <summary>
        /// A call that allocates a descriptor (`socket()`, `accept`): .NET folds EMFILE and ENFILE into
        /// `TooManyOpenSockets` (native ENFILE, 23); CPython reports the process limit, 24 `EMFILE`.
        /// </summary>
        internal static global::Sharpy.SocketModule.Error _NewDescriptorError(global::System.Net.Sockets.SocketException ex)
        {
            return global::Sharpy.SocketModule.SocketModuleModule._RewriteErrno(ex, global::System.Net.Sockets.SocketError.TooManyOpenSockets, 24, "Too many open files");
        }

        /// <summary>
        /// Return the default timeout in seconds for new sockets, or None.
        /// </summary>
        public static double? Getdefaulttimeout()
        {
            {
                var __ctx_2 = global::Sharpy.SocketModule.SocketModuleModule._Lock;
                __ctx_2.Enter();
                try
                {
                    return global::Sharpy.SocketModule.SocketModuleModule._DefaultTimeout;
                }
                finally
                {
                    __ctx_2.Exit();
                }
            }
        }

        /// <summary>
        /// Set the default timeout for new sockets. None means blocking mode.
        /// </summary>
        public static void Setdefaulttimeout(double? timeout)
        {
            global::Sharpy.SocketModule.SocketModuleModule._CheckTimeout(timeout);
            {
                var __ctx_3 = global::Sharpy.SocketModule.SocketModuleModule._Lock;
                __ctx_3.Enter();
                try
                {
                    _DefaultTimeout__set(timeout);
                }
                finally
                {
                    __ctx_3.Exit();
                }
            }
        }

        /// <summary>
        /// Connect to a TCP (host, port) address and return the connected socket.
        /// The socket takes the global default timeout (getdefaulttimeout()).
        /// </summary>
        public static global::Sharpy.SocketModule.Socket CreateConnection((string host, int port) address)
        {
            double? @default = null;
            {
                var __ctx_4 = global::Sharpy.SocketModule.SocketModuleModule._Lock;
                __ctx_4.Enter();
                try
                {
                    @default = global::Sharpy.SocketModule.SocketModuleModule._DefaultTimeout;
                }
                finally
                {
                    __ctx_4.Exit();
                }
            }

            return global::Sharpy.SocketModule.SocketModuleModule.CreateConnection(address, @default);
        }

        /// <summary>
        /// Connect to a TCP (host, port) address with the given timeout and return the connected socket.
        /// None means blocking mode, regardless of the global default timeout.
        /// </summary>
        public static global::Sharpy.SocketModule.Socket CreateConnection((string host, int port) address, double? timeout)
        {
            global::Sharpy.SocketModule.SocketModuleModule._CheckTimeout(timeout);
            global::Sharpy.SocketModule.Socket sock = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM, 0);
            try
            {
                sock.Settimeout(timeout);
                sock.Connect(address);
                return sock;
            }
            catch (global::System.Exception)
            {
                sock.Close();
                throw;
            }
        }

        /// <summary>
        /// Return the hostname of the current machine.
        /// </summary>
        public static string Gethostname()
        {
            return global::System.Net.Dns.GetHostName();
        }

        /// <summary>
        /// Resolve a hostname to an IPv4 address string.
        /// </summary>
        public static string Gethostbyname(string hostname)
        {
            try
            {
                var addresses = global::System.Net.Dns.GetHostAddresses(hostname);
                int i = 0;
                while (i < global::Sharpy.Builtins.Len(addresses))
                {
                    if (((int)global::Sharpy.ArrayHelpers.GetItem(addresses, i).AddressFamily) == global::Sharpy.SocketModule.SocketModuleModule.AF_INET)
                    {
                        return global::Sharpy.ArrayHelpers.GetItem(addresses, i).ToString();
                    }

                    i = i + 1;
                }

                if (global::Sharpy.Builtins.Len(addresses) > 0)
                {
                    return global::Sharpy.ArrayHelpers.GetItem(addresses, 0).ToString();
                }

                throw new global::Sharpy.SocketModule.Gaierror("Name or service not known", (int?)((int)global::System.Net.Sockets.SocketError.HostNotFound));
            }
            catch (global::System.Net.Sockets.SocketException ex)
            {
                throw new global::Sharpy.SocketModule.Gaierror(ex.Message, ex, ((int)ex.SocketErrorCode));
            }
        }

        /// <summary>
        /// Return the fully qualified domain name of the local host.
        /// </summary>
        public static string Getfqdn()
        {
            try
            {
                string hostName = global::System.Net.Dns.GetHostName();
                var entry = global::System.Net.Dns.GetHostEntry(hostName);
                return entry.HostName;
            }
            catch (global::System.Exception)
            {
                return "localhost";
            }
        }

        /// <summary>
        /// Resolve a socket address to a (host, service) tuple.
        /// </summary>
        public static (string host, string service) Getnameinfo((string host, int port) sockaddr, int flags = 0)
        {
            try
            {
                var entry = global::System.Net.Dns.GetHostEntry(sockaddr.Item1);
                string host = entry.HostName;
                string service = global::Sharpy.Builtins.Str(sockaddr.Item2);
                return (host, service);
            }
            catch (global::System.Net.Sockets.SocketException ex)
            {
                throw new global::Sharpy.SocketModule.Gaierror(ex.Message, ex, ((int)ex.SocketErrorCode));
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static global::Sharpy.StaticInitHost __host;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void __Sweep()
        {
            global::Sharpy.StaticInit.Run(ref AF_INET__state, "AF_INET", AF_INET__init);
            global::Sharpy.StaticInit.Run(ref AF_INET6__state, "AF_INET6", AF_INET6__init);
            global::Sharpy.StaticInit.Run(ref AF_UNIX__state, "AF_UNIX", AF_UNIX__init);
            global::Sharpy.StaticInit.Run(ref SOCK_STREAM__state, "SOCK_STREAM", SOCK_STREAM__init);
            global::Sharpy.StaticInit.Run(ref SOCK_DGRAM__state, "SOCK_DGRAM", SOCK_DGRAM__init);
            global::Sharpy.StaticInit.Run(ref SOCK_RAW__state, "SOCK_RAW", SOCK_RAW__init);
            global::Sharpy.StaticInit.Run(ref IPPROTO_IP__state, "IPPROTO_IP", IPPROTO_IP__init);
            global::Sharpy.StaticInit.Run(ref IPPROTO_TCP__state, "IPPROTO_TCP", IPPROTO_TCP__init);
            global::Sharpy.StaticInit.Run(ref IPPROTO_UDP__state, "IPPROTO_UDP", IPPROTO_UDP__init);
            global::Sharpy.StaticInit.Run(ref SOL_SOCKET__state, "SOL_SOCKET", SOL_SOCKET__init);
            global::Sharpy.StaticInit.Run(ref SO_REUSEADDR__state, "SO_REUSEADDR", SO_REUSEADDR__init);
            global::Sharpy.StaticInit.Run(ref SO_KEEPALIVE__state, "SO_KEEPALIVE", SO_KEEPALIVE__init);
            global::Sharpy.StaticInit.Run(ref SO_BROADCAST__state, "SO_BROADCAST", SO_BROADCAST__init);
            global::Sharpy.StaticInit.Run(ref SO_RCVBUF__state, "SO_RCVBUF", SO_RCVBUF__init);
            global::Sharpy.StaticInit.Run(ref SO_SNDBUF__state, "SO_SNDBUF", SO_SNDBUF__init);
            global::Sharpy.StaticInit.Run(ref TCP_NODELAY__state, "TCP_NODELAY", TCP_NODELAY__init);
            global::Sharpy.StaticInit.Run(ref SHUT_RD__state, "SHUT_RD", SHUT_RD__init);
            global::Sharpy.StaticInit.Run(ref SHUT_WR__state, "SHUT_WR", SHUT_WR__init);
            global::Sharpy.StaticInit.Run(ref SHUT_RDWR__state, "SHUT_RDWR", SHUT_RDWR__init);
            global::Sharpy.StaticInit.Run(ref SOMAXCONN__state, "SOMAXCONN", SOMAXCONN__init);
            global::Sharpy.StaticInit.Run(ref _DefaultTimeout__state, "_default_timeout", _DefaultTimeout__init);
            global::Sharpy.StaticInit.Run(ref _Lock__state, "_lock", _Lock__init);
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
    /// Base exception for socket-related errors. Corresponds to Python's socket.error.
    /// Derives from OSError, so `except OSError` catches every socket exception. Python's
    /// socket.error IS OSError; here it is a subclass of its own (docs/deviations.yaml,
    /// socket-error-timeout-distinct-classes).
    /// `errno` is the platform's errno for a failed socket call, and None where python's is None:
    /// an error constructed without one, and a `timeout` raised because the socket's own timeout
    /// (`settimeout`) expired.
    /// </summary>
    [global::Sharpy.SharpyModuleType("socket", "error")]
    [global::Sharpy.SharpyName("error")]
    public class Error : global::Sharpy.OSError
    {
        public int? Errno;
        /// <summary>
        /// Create a socket error from a .NET SocketException.
        /// errno is the platform's own code, `SocketException.NativeErrorCode`: the POSIX errno on
        /// Unix and the WinSock code on Windows, which is what CPython reports on each. The
        /// `SocketError` value is kept instead for a name resolution failure (`HostNotFound`,
        /// `TryAgain`, `NoRecovery`, `NoData`), whose NativeErrorCode on Unix is not an `EAI_*`
        /// code, and wherever NativeErrorCode is not positive (an internal value on Unix, e.g.
        /// for `SocketError.SocketError`) (docs/deviations.yaml, socket-errno-is-dotnet-socket-error).
        /// </summary>
        public static global::Sharpy.SocketModule.Error FromSocketException(global::System.Net.Sockets.SocketException ex)
        {
            global::System.Net.Sockets.SocketError code = ex.SocketErrorCode;
            if (code == global::System.Net.Sockets.SocketError.HostNotFound || code == global::System.Net.Sockets.SocketError.TryAgain || code == global::System.Net.Sockets.SocketError.NoRecovery || code == global::System.Net.Sockets.SocketError.NoData)
            {
                return new global::Sharpy.SocketModule.Error(ex.Message, ex, ((int)code));
            }

            int native = ex.NativeErrorCode;
            if (native <= 0)
            {
                return new global::Sharpy.SocketModule.Error(ex.Message, ex, ((int)code));
            }

            return new global::Sharpy.SocketModule.Error(ex.Message, ex, native);
        }

        /// <summary>
        /// Create a socket error with the specified message and optional errno.
        /// </summary>
        public Error(string message, int? errno = null) : base(message)
        {
            this.Errno = errno;
        }

        /// <summary>
        /// Create a socket error wrapping an inner exception.
        /// </summary>
        public Error(string message, global::System.Exception inner, int? errno = null) : base(message, inner)
        {
            this.Errno = errno;
        }
    }

    /// <summary>
    /// Raised when a socket operation times out. Corresponds to Python's socket.timeout.
    /// `errno` is None when the socket's own timeout expired, as python's; a kernel `ETIMEDOUT`
    /// carries the platform's errno.
    /// </summary>
    [global::Sharpy.SharpyModuleType("socket", "timeout")]
    [global::Sharpy.SharpyName("timeout")]
    public class Timeout : global::Sharpy.SocketModule.Error
    {
        /// <summary>
        /// Create a socket timeout error with the specified message.
        /// </summary>
        public Timeout(string message, int? errno = null) : base(message, errno)
        {
        }

        /// <summary>
        /// Create a socket timeout error wrapping an inner exception.
        /// </summary>
        public Timeout(string message, global::System.Exception inner, int? errno = null) : base(message, inner, errno)
        {
        }
    }

    /// <summary>
    /// Raised for address-related errors (e.g., DNS failures). Python's socket.gaierror.
    /// </summary>
    [global::Sharpy.SharpyModuleType("socket", "gaierror")]
    [global::Sharpy.SharpyName("gaierror")]
    public class Gaierror : global::Sharpy.SocketModule.Error
    {
        /// <summary>
        /// Create a GAI error with the specified message.
        /// </summary>
        public Gaierror(string message, int? errno = null) : base(message, errno)
        {
        }

        /// <summary>
        /// Create a GAI error wrapping an inner exception.
        /// </summary>
        public Gaierror(string message, global::System.Exception inner, int? errno = null) : base(message, inner, errno)
        {
        }
    }

    /// <summary>
    /// Raised for legacy address-related errors. Corresponds to Python's socket.herror.
    /// </summary>
    [global::Sharpy.SharpyModuleType("socket", "herror")]
    [global::Sharpy.SharpyName("herror")]
    public class Herror : global::Sharpy.SocketModule.Error
    {
        /// <summary>
        /// Create an herror with the specified message.
        /// </summary>
        public Herror(string message, int? errno = null) : base(message, errno)
        {
        }

        /// <summary>
        /// Create an herror wrapping an inner exception.
        /// </summary>
        public Herror(string message, global::System.Exception inner, int? errno = null) : base(message, inner, errno)
        {
        }
    }

    /// <summary>
    /// Wraps System.Net.Sockets.Socket to provide a Python-like socket API.
    /// Supports TCP and UDP communication, socket options, and timeout handling.
    /// </summary>
    [global::Sharpy.SharpyModuleType("socket", "socket")]
    [global::Sharpy.SharpyName("socket")]
    public sealed class Socket : global::System.IDisposable
    {
        private global::System.Net.Sockets.Socket _Socket;
        private double? _Timeout;
        public int Family
        {
            get
            {
                _ = "The address family of the socket.";
                return (int)this._Socket.AddressFamily;
            }
        }

        public int type
        {
            get
            {
                _ = "The socket type.";
                return (int)this._Socket.SocketType;
            }
        }

        public int Proto
        {
            get
            {
                _ = "The protocol type.";
                return (int)this._Socket.ProtocolType;
            }
        }

        /// <summary>
        /// Connect to a remote (host, port) address.
        /// </summary>
        public void Connect((string host, int port) address)
        {
            try
            {
                var ipAddresses = global::System.Net.Dns.GetHostAddresses(address.Item1);
                if (global::Sharpy.Builtins.Len(ipAddresses) == 0)
                {
                    throw new global::Sharpy.SocketModule.Gaierror("Name or service not known", (int?)((int)global::System.Net.Sockets.SocketError.HostNotFound));
                }

                global::System.Net.IPEndPoint endpoint = new global::System.Net.IPEndPoint(global::Sharpy.ArrayHelpers.GetItem(ipAddresses, 0), address.Item2);
                int ms = this._TimeoutMs();
                if (ms > 0)
                {
                    var pending = this._Socket.BeginConnect(endpoint, null, null);
                    if (!pending.AsyncWaitHandle.WaitOne(ms))
                    {
                        throw new global::Sharpy.SocketModule.Timeout("timed out");
                    }

                    this._Socket.EndConnect(pending);
                }
                else
                {
                    this._Socket.Connect(endpoint);
                }
            }
            catch (global::System.Net.Sockets.SocketException ex) when (ex.SocketErrorCode == global::System.Net.Sockets.SocketError.TimedOut)
            {
                throw global::Sharpy.SocketModule.SocketModuleModule._TimedOut(ex, false);
            }
            catch (global::System.Net.Sockets.SocketException ex_1)
            {
                throw global::Sharpy.SocketModule.SocketModuleModule._ConnectError(ex_1, this._Socket);
            }
        }

        /// <summary>
        /// Bind the socket to a local (host, port) address.
        /// </summary>
        public void Bind((string host, int port) address)
        {
            try
            {
                global::System.Net.IPAddress ipAddr = global::System.Net.IPAddress.Any;
                string host = address.Item1;
                if (host == "" || host == "0.0.0.0")
                {
                    ipAddr = global::System.Net.IPAddress.Any;
                }
                else if (host == "::")
                {
                    ipAddr = global::System.Net.IPAddress.IPv6Any;
                }
                else
                {
                    ipAddr = global::System.Net.IPAddress.Parse(host);
                }

                global::System.Net.IPEndPoint endpoint = new global::System.Net.IPEndPoint(ipAddr, address.Item2);
                this._Socket.Bind(endpoint);
            }
            catch (global::System.Net.Sockets.SocketException ex)
            {
                throw global::Sharpy.SocketModule.Error.FromSocketException(ex);
            }
        }

        /// <summary>
        /// Enable a server to accept connections with the given backlog.
        /// </summary>
        public void Listen(int backlog = 5)
        {
            try
            {
                this._Socket.Listen(backlog);
            }
            catch (global::System.Net.Sockets.SocketException ex)
            {
                throw global::Sharpy.SocketModule.Error.FromSocketException(ex);
            }
        }

        /// <summary>
        /// Accept a connection, returning (new socket, (remote_host, remote_port)).
        /// </summary>
        public (global::Sharpy.SocketModule.Socket conn, (string host, int port) addr) Accept()
        {
            try
            {
                int ms = this._TimeoutMs();
                if (ms > 0 && !this._WaitReadable(ms))
                {
                    throw new global::Sharpy.SocketModule.Timeout("timed out");
                }

                var accepted = this._Socket.Accept();
                var remote = accepted.RemoteEndPoint;
                if (remote is null)
                {
                    throw new global::Sharpy.SocketModule.Error("Accepted connection has no remote endpoint.");
                }

                global::System.Net.IPEndPoint remoteEp = (global::System.Net.IPEndPoint)remote!;
                global::Sharpy.SocketModule.Socket conn = new global::Sharpy.SocketModule.Socket(accepted);
                return (conn, (remoteEp.Address.ToString(), remoteEp.Port));
            }
            catch (global::System.Net.Sockets.SocketException ex) when (ex.SocketErrorCode == global::System.Net.Sockets.SocketError.TimedOut)
            {
                throw global::Sharpy.SocketModule.SocketModuleModule._TimedOut(ex, this._TimeoutMs() > 0);
            }
            catch (global::System.Net.Sockets.SocketException ex_1)
            {
                throw global::Sharpy.SocketModule.SocketModuleModule._NewDescriptorError(ex_1);
            }
        }

        /// <summary>
        /// Send data to the socket, returning the number of bytes sent.
        /// </summary>
        public int Send(Sharpy.Bytes data)
        {
            try
            {
                return this._Socket.Send(data.ToArray());
            }
            catch (global::System.Net.Sockets.SocketException ex) when (ex.SocketErrorCode == global::System.Net.Sockets.SocketError.TimedOut)
            {
                throw global::Sharpy.SocketModule.SocketModuleModule._TimedOut(ex, this._TimeoutMs() > 0);
            }
            catch (global::System.Net.Sockets.SocketException ex_1)
            {
                throw global::Sharpy.SocketModule.Error.FromSocketException(ex_1);
            }
        }

        /// <summary>
        /// Send all data to the socket, continuing until every byte is sent.
        /// </summary>
        public void Sendall(Sharpy.Bytes data)
        {
            try
            {
                var buffer = data.ToArray();
                int total = global::Sharpy.Builtins.Len(data);
                int totalSent = 0;
                while (totalSent < total)
                {
                    int sent = this._Socket.Send(buffer, totalSent, total - totalSent, global::System.Net.Sockets.SocketFlags.None);
                    if (sent == 0)
                    {
                        throw new global::Sharpy.SocketModule.Error("Connection reset by peer", (int?)((int)global::System.Net.Sockets.SocketError.ConnectionReset));
                    }

                    totalSent = totalSent + sent;
                }
            }
            catch (global::System.Net.Sockets.SocketException ex) when (ex.SocketErrorCode == global::System.Net.Sockets.SocketError.TimedOut)
            {
                throw global::Sharpy.SocketModule.SocketModuleModule._TimedOut(ex, this._TimeoutMs() > 0);
            }
            catch (global::System.Net.Sockets.SocketException ex_1)
            {
                throw global::Sharpy.SocketModule.Error.FromSocketException(ex_1);
            }
        }

        /// <summary>
        /// Receive up to bufsize bytes from the socket.
        /// </summary>
        public Sharpy.Bytes Recv(int bufsize)
        {
            try
            {
                var buffer = new byte[bufsize];
                int received = this._Socket.Receive(buffer);
                var result = new byte[received];
                global::System.Array.Copy(buffer, result, received);
                return new global::Sharpy.Bytes(result);
            }
            catch (global::System.Net.Sockets.SocketException ex) when (ex.SocketErrorCode == global::System.Net.Sockets.SocketError.TimedOut)
            {
                throw global::Sharpy.SocketModule.SocketModuleModule._TimedOut(ex, this._TimeoutMs() > 0);
            }
            catch (global::System.Net.Sockets.SocketException ex_1)
            {
                throw global::Sharpy.SocketModule.Error.FromSocketException(ex_1);
            }
        }

        /// <summary>
        /// Send data to a specific (host, port) address (UDP).
        /// </summary>
        public int Sendto(Sharpy.Bytes data, (string host, int port) address)
        {
            try
            {
                global::System.Net.IPAddress ipAddr = global::System.Net.IPAddress.Parse(address.Item1);
                global::System.Net.IPEndPoint endpoint = new global::System.Net.IPEndPoint(ipAddr, address.Item2);
                return this._Socket.SendTo(data.ToArray(), endpoint);
            }
            catch (global::System.Net.Sockets.SocketException ex) when (ex.SocketErrorCode == global::System.Net.Sockets.SocketError.TimedOut)
            {
                throw global::Sharpy.SocketModule.SocketModuleModule._TimedOut(ex, this._TimeoutMs() > 0);
            }
            catch (global::System.Net.Sockets.SocketException ex_1)
            {
                throw global::Sharpy.SocketModule.Error.FromSocketException(ex_1);
            }
        }

        /// <summary>
        /// Receive data and the sender's address (UDP).
        /// </summary>
        public (Sharpy.Bytes data, (string host, int port) addr) Recvfrom(int bufsize)
        {
            try
            {
                var buffer = new byte[bufsize];
                global::System.Net.EndPoint remote = new global::System.Net.IPEndPoint(global::System.Net.IPAddress.Any, 0);
                int received = this._Socket.ReceiveFrom(buffer, ref remote);
                var result = new byte[received];
                global::System.Array.Copy(buffer, result, received);
                global::System.Net.IPEndPoint ep = (global::System.Net.IPEndPoint)remote;
                return (new global::Sharpy.Bytes(result), (ep.Address.ToString(), ep.Port));
            }
            catch (global::System.Net.Sockets.SocketException ex) when (ex.SocketErrorCode == global::System.Net.Sockets.SocketError.TimedOut)
            {
                throw global::Sharpy.SocketModule.SocketModuleModule._TimedOut(ex, this._TimeoutMs() > 0);
            }
            catch (global::System.Net.Sockets.SocketException ex_1)
            {
                throw global::Sharpy.SocketModule.Error.FromSocketException(ex_1);
            }
        }

        /// <summary>
        /// Set a socket option (e.g., SOL_SOCKET, SO_REUSEADDR).
        /// </summary>
        public void Setsockopt(int level, int optname, int value)
        {
            try
            {
                this._Socket.SetSocketOption((global::System.Net.Sockets.SocketOptionLevel)level, (global::System.Net.Sockets.SocketOptionName)optname, value);
            }
            catch (global::System.Net.Sockets.SocketException ex)
            {
                if (this._KernelSaysEnoprotoopt(level, optname, true))
                {
                    throw global::Sharpy.SocketModule.SocketModuleModule._UnknownOptionError(ex);
                }

                throw global::Sharpy.SocketModule.Error.FromSocketException(ex);
            }
        }

        /// <summary>
        /// Get a socket option value.
        /// </summary>
        public int Getsockopt(int level, int optname)
        {
            try
            {
                var opt = this._Socket.GetSocketOption((global::System.Net.Sockets.SocketOptionLevel)level, (global::System.Net.Sockets.SocketOptionName)optname);
                if (opt is null)
                {
                    return 0;
                }

                return (int)opt!;
            }
            catch (global::System.Net.Sockets.SocketException ex)
            {
                if (this._KernelSaysEnoprotoopt(level, optname, false))
                {
                    throw global::Sharpy.SocketModule.SocketModuleModule._UnknownOptionError(ex);
                }

                throw global::Sharpy.SocketModule.Error.FromSocketException(ex);
            }
        }

        /// <summary>
        /// Whether .NET refused (level, optname) itself, having no member for it, where Linux
        /// would have answered ENOPROTOOPT (each arm measured with CPython 3.15.2).
        /// A level or option name that is no member of .NET's enum never reaches the kernel. Linux
        /// answers an unknown option with ENOPROTOOPT at SOL_SOCKET on every socket; on an AF_INET
        /// socket also at every level for setsockopt, and for getsockopt at IPPROTO_IP and at the
        /// socket's own protocol (IPPROTO_TCP on SOCK_STREAM, IPPROTO_UDP on SOCK_DGRAM). Elsewhere
        /// it answers EOPNOTSUPP, already .NET's number (AF_INET6 off SOL_SOCKET is unmeasured and
        /// left as .NET reports it). A defined member .NET has no Linux mapping for is not told
        /// apart here.
        /// </summary>
        private bool _KernelSaysEnoprotoopt(int level, int optname, bool setting)
        {
            bool levelKnown = global::Sharpy.SocketModule.SocketModuleModule._IsMember(global::System.Enum.GetValues(global::System.Net.Sockets.SocketOptionLevel.Socket.GetType()), level);
            bool nameKnown = global::Sharpy.SocketModule.SocketModuleModule._IsMember(global::System.Enum.GetValues(global::System.Net.Sockets.SocketOptionName.Debug.GetType()), optname);
            if (levelKnown && nameKnown)
            {
                return false;
            }

            if (level == global::Sharpy.SocketModule.SocketModuleModule.SOL_SOCKET)
            {
                return true;
            }

            if (this._Socket.AddressFamily != global::System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return false;
            }

            if (setting || level == global::Sharpy.SocketModule.SocketModuleModule.IPPROTO_IP)
            {
                return true;
            }

            global::System.Net.Sockets.SocketType kind = this._Socket.SocketType;
            return (level == global::Sharpy.SocketModule.SocketModuleModule.IPPROTO_TCP && kind == global::System.Net.Sockets.SocketType.Stream) || (level == global::Sharpy.SocketModule.SocketModuleModule.IPPROTO_UDP && kind == global::System.Net.Sockets.SocketType.Dgram);
        }

        /// <summary>
        /// Set the timeout in seconds for blocking operations, or None for blocking mode.
        /// </summary>
        public void Settimeout(double? timeout)
        {
            global::Sharpy.SocketModule.SocketModuleModule._CheckTimeout(timeout);
            this._Timeout = timeout;
            if (timeout is null)
            {
                this._Socket.Blocking = true;
                this._Socket.ReceiveTimeout = 0;
                this._Socket.SendTimeout = 0;
            }
            else
            {
                double value = timeout.Value;
                if (value == 0.0d)
                {
                    this._Socket.Blocking = false;
                }
                else
                {
                    this._Socket.Blocking = true;
                    int ms = this._TimeoutMs();
                    this._Socket.ReceiveTimeout = ms;
                    this._Socket.SendTimeout = ms;
                }
            }
        }

        /// <summary>
        /// The positive timeout in whole milliseconds (at least 1), or 0 when the
        /// socket is blocking (None) or non-blocking (0.0).
        /// </summary>
        private int _TimeoutMs()
        {
            double? t = this._Timeout;
            if (t is null)
            {
                return 0;
            }

            double value = t.Value;
            if (value <= 0.0d)
            {
                return 0;
            }

            int maxMs = 2147483647;
            double msFloat = value * 1000.0d;
            if (msFloat >= ((double)maxMs))
            {
                return maxMs;
            }

            int ms = global::Sharpy.NumericCheckedCast.ToInt(msFloat);
            if (ms < 1)
            {
                return 1;
            }

            return ms;
        }

        /// <summary>
        /// Wait up to ms milliseconds for the socket to become readable (for a
        /// listening socket: a connection is pending); False when the wait times out.
        /// </summary>
        private bool _WaitReadable(int ms)
        {
            int remaining = ms;
            while (remaining > 0)
            {
                int chunk = remaining;
                if (chunk > 1000000)
                {
                    chunk = 1000000;
                }

                if (this._Socket.Poll(chunk * 1000, global::System.Net.Sockets.SelectMode.SelectRead))
                {
                    return true;
                }

                remaining = remaining - chunk;
            }

            return false;
        }

        /// <summary>
        /// Return the timeout in seconds, or None if in blocking mode.
        /// </summary>
        public double? Gettimeout()
        {
            return this._Timeout;
        }

        /// <summary>
        /// Set blocking (True) or non-blocking (False) mode.
        /// </summary>
        public void Setblocking(bool flag)
        {
            if (flag)
            {
                this.Settimeout(null);
            }
            else
            {
                this.Settimeout(0.0d);
            }
        }

        /// <summary>
        /// Return whether the socket is in blocking mode.
        /// </summary>
        public bool Getblocking()
        {
            return this._Socket.Blocking;
        }

        /// <summary>
        /// Shut down one or both halves of the connection (SHUT_RD/WR/RDWR).
        /// </summary>
        public void Shutdown(int how)
        {
            try
            {
                this._Socket.Shutdown((global::System.Net.Sockets.SocketShutdown)how);
            }
            catch (global::System.Net.Sockets.SocketException ex)
            {
                throw global::Sharpy.SocketModule.Error.FromSocketException(ex);
            }
        }

        /// <summary>
        /// Close the socket.
        /// </summary>
        public void Close()
        {
            this._Socket.Close();
        }

        /// <summary>
        /// Return the local (host, port) address the socket is bound to.
        /// </summary>
        public (string host, int port) Getsockname()
        {
            var endpoint = this._Socket.LocalEndPoint;
            if (endpoint is null)
            {
                throw new global::Sharpy.SocketModule.Error("Socket is not bound to an address.");
            }

            global::System.Net.IPEndPoint ep = (global::System.Net.IPEndPoint)endpoint!;
            return (ep.Address.ToString(), ep.Port);
        }

        /// <summary>
        /// Return the remote (host, port) address the socket is connected to.
        /// </summary>
        public (string host, int port) Getpeername()
        {
            var endpoint = this._Socket.RemoteEndPoint;
            if (endpoint is null)
            {
                throw new global::Sharpy.SocketModule.Error("Socket is not connected.");
            }

            global::System.Net.IPEndPoint ep = (global::System.Net.IPEndPoint)endpoint!;
            return (ep.Address.ToString(), ep.Port);
        }

        /// <summary>
        /// Return the socket handle (file descriptor) as an integer.
        /// </summary>
        public int Fileno()
        {
            return global::Sharpy.NumericCheckedCast.ToInt(this._Socket.Handle.ToInt64());
        }

        /// <summary>
        /// Dispose the underlying socket resources.
        /// </summary>
        public void Dispose()
        {
            this._Socket.Dispose();
        }

        public global::Sharpy.SocketModule.Socket Enter()
        {
            return this;
        }

        public void Exit()
        {
            this.Dispose();
        }

        public override string ToString()
        {
            int fd = this.Fileno();
            int fam = this.Family;
            int typ = this.type;
            int pr = this.Proto;
            return FormattableString.Invariant($"<socket fd={(global::Sharpy.Builtins.Str(fd))}, family={(global::Sharpy.Builtins.Str(fam))}, type={(global::Sharpy.Builtins.Str(typ))}, proto={(global::Sharpy.Builtins.Str(pr))}>");
        }

        /// <summary>
        /// Create a new socket with the given address family, type, and protocol.
        /// </summary>
        public Socket(int family = 2, int sockType = 1, int proto = 0)
        {
            try
            {
                this._Socket = new global::System.Net.Sockets.Socket((global::System.Net.Sockets.AddressFamily)family, (global::System.Net.Sockets.SocketType)sockType, (global::System.Net.Sockets.ProtocolType)proto);
            }
            catch (global::System.Net.Sockets.SocketException ex)
            {
                throw global::Sharpy.SocketModule.SocketModuleModule._NewDescriptorError(ex);
            }

            this._Timeout = null;
            double? @default = null;
            {
                var __ctx_0 = global::Sharpy.SocketModule.SocketModuleModule._Lock;
                __ctx_0.Enter();
                try
                {
                    @default = global::Sharpy.SocketModule.SocketModuleModule._DefaultTimeout;
                }
                finally
                {
                    __ctx_0.Exit();
                }
            }

            if (@default is not null)
            {
                this.Settimeout(@default.Value);
            }
        }

        /// <summary>
        /// Wrap an existing .NET socket (used for accepted connections).
        /// The wrapper's .NET state is made to match what it records: an accepted .NET socket
        /// inherits the listener's Send/ReceiveTimeout and non-blocking mode, where python's
        /// accepted socket is blocking. Then the default timeout applies, as python's `accept`
        /// does (getdefaulttimeout(), else blocking, whatever the listener's mode).
        /// </summary>
        public Socket(global::System.Net.Sockets.Socket existing)
        {
            this._Socket = existing;
            this.Settimeout(null);
            double? @default = null;
            {
                var __ctx_1 = global::Sharpy.SocketModule.SocketModuleModule._Lock;
                __ctx_1.Enter();
                try
                {
                    @default = global::Sharpy.SocketModule.SocketModuleModule._DefaultTimeout;
                }
                finally
                {
                    __ctx_1.Exit();
                }
            }

            if (@default is not null)
            {
                this.Settimeout(@default.Value);
            }
        }
    }
}
