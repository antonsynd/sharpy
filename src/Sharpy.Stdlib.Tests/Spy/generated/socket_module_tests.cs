// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

#pragma warning disable xUnit2009, xUnit2017

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using socket = global::Sharpy.SocketModule.SocketModuleModule;
using time = global::Sharpy.TimeModule;
using Xunit;

namespace Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests
{
    [global::Sharpy.SharpyModule("socket.socket_module_tests")]
    public static partial class SocketModuleTestsModule
    {
        internal static double _ElapsedSince(double start)
        {
#line (413, 5) - (413, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            return time.Monotonic() - start;
#line hidden
        }

        internal static string _TimeoutOutcome(global::System.Exception e)
        {
#line (612, 5) - (613, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if ((object?)e is global::Sharpy.OverflowError)
#line hidden
            {
#line (613, 9) - (613, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                return "OverflowError";
#line hidden
            }

#line (614, 5) - (615, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if ((object?)e is global::Sharpy.ValueError)
#line hidden
            {
#line (615, 9) - (615, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                return "ValueError";
#line hidden
            }

#line (616, 5) - (617, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if ((object?)e is global::Sharpy.SocketModule.Error)
#line hidden
            {
#line (617, 9) - (617, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                return "socket.error";
#line hidden
            }

#line (618, 5) - (618, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            return "other";
#line hidden
        }

        internal static bool _SameTimeout(double? actual, double? expected)
        {
#line (621, 5) - (622, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (actual is null || expected is null)
#line hidden
            {
#line (622, 9) - (622, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                return actual is null && expected is null;
#line hidden
            }

#line (623, 5) - (623, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double a = actual.Value;
#line (624, 5) - (624, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double b = expected.Value;
#line (625, 5) - (625, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            return a == b;
#line hidden
        }

        internal static int _ClosedLoopbackPort()
        {
#line (628, 5) - (628, 62) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var probe = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (629, 5) - (629, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            probe.Bind(("127.0.0.1", 0));
#line (630, 5) - (630, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int port = probe.Getsockname().Item2;
#line (631, 5) - (631, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            probe.Close();
#line (632, 5) - (632, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            return port;
#line hidden
        }

        internal static void _AssertTimeoutRule(double? value, string expected)
        {
#line (638, 5) - (638, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double? survivor = 2.0d;
#line (639, 5) - (640, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (expected == "accepted")
#line hidden
            {
#line (640, 9) - (640, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                survivor = value;
#line hidden
            }

#line (641, 5) - (641, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string outcome = "accepted";
#line (642, 5) - (642, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            socket.Setdefaulttimeout(2.0d);
#line (643, 5) - (655, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (644, 9) - (647, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                try
#line hidden
                {
#line (645, 13) - (645, 44) 20 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    socket.Setdefaulttimeout(value);
#line hidden
                }
                catch (global::System.Exception e)
                {
#line (647, 13) - (647, 42) 20 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    outcome = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._TimeoutOutcome(e);
#line hidden
                }

#line (648, 9) - (648, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (!(outcome == expected))
#line hidden
                {
                    throw new global::Sharpy.AssertionError();
                }

#line (649, 9) - (649, 68) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (!(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._SameTimeout(socket.Getdefaulttimeout(), survivor)))
#line hidden
                {
                    throw new global::Sharpy.AssertionError();
                }

#line (650, 9) - (650, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var fresh = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (651, 9) - (651, 60) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (!(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._SameTimeout(fresh.Gettimeout(), survivor)))
#line hidden
                {
                    throw new global::Sharpy.AssertionError();
                }

#line (652, 9) - (652, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                fresh.Close();
#line hidden
            }
            finally
            {
#line (655, 9) - (655, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                socket.Setdefaulttimeout(null);
#line hidden
            }

#line (658, 5) - (658, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (659, 5) - (659, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Settimeout(2.0d);
#line (660, 5) - (660, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            outcome = "accepted";
#line (661, 5) - (664, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (662, 9) - (662, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Settimeout(value);
#line hidden
            }
            catch (global::System.Exception e_1)
            {
#line (664, 9) - (664, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                outcome = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._TimeoutOutcome(e_1);
#line hidden
            }

#line (665, 5) - (665, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (!(outcome == expected))
#line hidden
            {
                throw new global::Sharpy.AssertionError();
            }

#line (666, 5) - (666, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (!(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._SameTimeout(s.Gettimeout(), survivor)))
#line hidden
            {
                throw new global::Sharpy.AssertionError();
            }

#line (667, 5) - (667, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line (672, 5) - (672, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int port = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ClosedLoopbackPort();
#line (673, 5) - (673, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            outcome = "accepted";
#line (674, 5) - (679, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (675, 9) - (675, 89) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                global::Sharpy.SocketModule.Socket c = socket.CreateConnection(("127.0.0.1", port), timeout: value);
#line (676, 9) - (676, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                c.Close();
#line hidden
            }
            catch (global::System.Exception e_2)
            {
#line (678, 9) - (678, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                outcome = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._TimeoutOutcome(e_2);
#line (679, 9) - (679, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (!(!((object?)e_2 is global::Sharpy.SocketModule.Timeout)))
#line hidden
                {
                    throw new global::Sharpy.AssertionError();
                }
            }

#line (680, 5) - (683, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (expected == "accepted")
#line hidden
            {
#line (681, 9) - (681, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (!(outcome == "socket.error"))
#line hidden
                {
                    throw new global::Sharpy.AssertionError();
                }
            }
            else
            {
#line (683, 9) - (683, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (!(outcome == expected))
#line hidden
                {
                    throw new global::Sharpy.AssertionError();
                }
            }
        }
    }

    public partial class SocketModuleTestsModuleTests
    {
        [Xunit.FactAttribute]
        public void TestAfInetHasCorrectValue()
        {
#line (46, 5) - (46, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.SocketModule.SocketModuleModule.AF_INET);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAfInet6HasCorrectValue()
        {
#line (50, 5) - (50, 84) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.SocketModule.SocketModuleModule.AF_INET6 == 23 || global::Sharpy.SocketModule.SocketModuleModule.AF_INET6 == 10 || global::Sharpy.SocketModule.SocketModuleModule.AF_INET6 == 30);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSockStreamHasCorrectValue()
        {
#line (54, 5) - (54, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(1, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSockDgramHasCorrectValue()
        {
#line (58, 5) - (58, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.SocketModule.SocketModuleModule.SOCK_DGRAM);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSockRawHasCorrectValue()
        {
#line (62, 5) - (62, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.SocketModule.SocketModuleModule.SOCK_RAW);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIpprotoTcpHasCorrectValue()
        {
#line (66, 5) - (66, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(6, global::Sharpy.SocketModule.SocketModuleModule.IPPROTO_TCP);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIpprotoUdpHasCorrectValue()
        {
#line (70, 5) - (70, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(17, global::Sharpy.SocketModule.SocketModuleModule.IPPROTO_UDP);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestShutRdHasCorrectValue()
        {
#line (74, 5) - (74, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(0, global::Sharpy.SocketModule.SocketModuleModule.SHUT_RD);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestShutWrHasCorrectValue()
        {
#line (78, 5) - (78, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(1, global::Sharpy.SocketModule.SocketModuleModule.SHUT_WR);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestShutRdwrHasCorrectValue()
        {
#line (82, 5) - (82, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.SocketModule.SocketModuleModule.SHUT_RDWR);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTcpNodelayHasCorrectValue()
        {
#line (86, 5) - (86, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.NotEqual(0, global::Sharpy.SocketModule.SocketModuleModule.TCP_NODELAY);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSoRcvbufHasCorrectValue()
        {
#line (90, 5) - (90, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.NotEqual(0, global::Sharpy.SocketModule.SocketModuleModule.SO_RCVBUF);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSoSndbufHasCorrectValue()
        {
#line (94, 5) - (94, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.NotEqual(0, global::Sharpy.SocketModule.SocketModuleModule.SO_SNDBUF);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSomaxconnIs128()
        {
#line (98, 5) - (98, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(128, global::Sharpy.SocketModule.SocketModuleModule.SOMAXCONN);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSocketCreatesStreamSocket()
        {
#line (107, 5) - (107, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (108, 5) - (108, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, s.Family);
#line (109, 5) - (109, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSocketCreatesDatagramSocket()
        {
#line (113, 5) - (113, 57) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_DGRAM);
#line (114, 5) - (114, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, s.Family);
#line (115, 5) - (115, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSocketDefaultParamsCreatesIpv4Stream()
        {
#line (119, 5) - (119, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket();
#line (120, 5) - (120, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, s.Family);
#line (121, 5) - (121, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTcpServerBindListenAcceptWorks()
        {
#line (127, 5) - (127, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (128, 5) - (128, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Setsockopt(global::Sharpy.SocketModule.SocketModuleModule.SOL_SOCKET, global::Sharpy.SocketModule.SocketModuleModule.SO_REUSEADDR, 1);
#line (129, 5) - (129, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (130, 5) - (130, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(5);
#line (132, 5) - (132, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::System.ValueTuple<string, int> serverAddr = server.Getsockname();
#line (133, 5) - (133, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("127.0.0.1", serverAddr.Item1);
#line (134, 5) - (134, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(serverAddr.Item2 > 0);
#line (136, 5) - (136, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (137, 5) - (137, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", serverAddr.Item2));
#line (139, 5) - (139, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, addr) = server.Accept();
#line (140, 5) - (140, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("127.0.0.1", addr.Item1);
#line (141, 5) - (141, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(addr.Item2 > 0);
#line (143, 5) - (143, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (144, 5) - (144, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (145, 5) - (145, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTcpSendRecvWorks()
        {
#line (149, 5) - (149, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (150, 5) - (150, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Setsockopt(global::Sharpy.SocketModule.SocketModuleModule.SOL_SOCKET, global::Sharpy.SocketModule.SocketModuleModule.SO_REUSEADDR, 1);
#line (151, 5) - (151, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (152, 5) - (152, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (153, 5) - (153, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (155, 5) - (155, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (156, 5) - (156, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (158, 5) - (158, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (160, 5) - (160, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Sharpy.Bytes message = new Sharpy.Bytes(new byte[] { 104, 101, 108, 108, 111 });
#line (161, 5) - (161, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Sendall(message);
#line (163, 5) - (163, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Sharpy.Bytes received = conn.Recv(1024);
#line (164, 5) - (164, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(new Sharpy.Bytes(new byte[] { 104, 101, 108, 108, 111 }), received);
#line (166, 5) - (166, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (167, 5) - (167, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (168, 5) - (168, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestUdpSendtoRecvfromWorks()
        {
#line (174, 5) - (174, 64) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var receiver = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_DGRAM);
#line (175, 5) - (175, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            receiver.Bind(("127.0.0.1", 0));
#line (176, 5) - (176, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = receiver.Getsockname().Item2;
#line (178, 5) - (178, 62) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var sender = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_DGRAM);
#line (179, 5) - (179, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Sharpy.Bytes message = new Sharpy.Bytes(new byte[] { 117, 100, 112, 32, 116, 101, 115, 116 });
#line (180, 5) - (180, 48) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            sender.Sendto(message, ("127.0.0.1", port));
#line (182, 5) - (182, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (data, addr) = receiver.Recvfrom(1024);
#line (183, 5) - (183, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(new Sharpy.Bytes(new byte[] { 117, 100, 112, 32, 116, 101, 115, 116 }), data);
#line (184, 5) - (184, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("127.0.0.1", addr.Item1);
#line (186, 5) - (186, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            sender.Close();
#line (187, 5) - (187, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            receiver.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGethostbynameLocalhostReturnsLoopback()
        {
#line (193, 5) - (193, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string ip = socket.Gethostbyname("localhost");
#line (194, 5) - (194, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(ip == "127.0.0.1" || ip == "::1");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGethostnameReturnsNonEmpty()
        {
#line (198, 5) - (198, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string hostname = socket.Gethostname();
#line (199, 5) - (199, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.NotEqual("", hostname);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGethostbynameInvalidHostRaisesGaierror()
        {
#line (203, 5) - (204, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool __raised_0 = false;
#line hidden
            try
            {
#line (204, 9) - (204, 65) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                socket.Gethostbyname("this.host.does.not.exist.invalid");
#line hidden
            }
            catch (global::Sharpy.SocketModule.Gaierror)
            {
                __raised_0 = true;
            }

            if (!__raised_0)
                throw new global::Sharpy.AssertionError("Expected gaierror to be raised, but no exception was raised");
        }

        [Xunit.FactAttribute]
        public void TestSetsockoptGetsockoptReuseAddr()
        {
#line (210, 5) - (210, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (211, 5) - (211, 60) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Setsockopt(global::Sharpy.SocketModule.SocketModuleModule.SOL_SOCKET, global::Sharpy.SocketModule.SocketModuleModule.SO_REUSEADDR, 1);
#line (212, 5) - (212, 69) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int val = s.Getsockopt(global::Sharpy.SocketModule.SocketModuleModule.SOL_SOCKET, global::Sharpy.SocketModule.SocketModuleModule.SO_REUSEADDR);
#line (213, 5) - (213, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.NotEqual(0, val);
#line (214, 5) - (214, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSettimeoutGettimeoutWorks()
        {
#line (220, 5) - (220, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (221, 5) - (221, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Settimeout(5.0d);
#line (222, 5) - (222, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var t = s.Gettimeout();
#line (223, 5) - (223, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.NotNull(t);
#line (224, 5) - (225, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (t is not null)
#line hidden
            {
#line (225, 9) - (225, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(5.0d, t.Value);
#line hidden
            }

#line (227, 5) - (227, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Settimeout(null);
#line (228, 5) - (228, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Null(s.Gettimeout());
#line (229, 5) - (229, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGetdefaulttimeoutReturnsNullByDefault()
        {
#line (235, 5) - (235, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            socket.Setdefaulttimeout(null);
#line (236, 5) - (236, 47) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Null(socket.Getdefaulttimeout());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSetdefaulttimeoutSetsAndGets()
        {
#line (240, 5) - (240, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            socket.Setdefaulttimeout(10.0d);
#line (241, 5) - (241, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var d = socket.Getdefaulttimeout();
#line (242, 5) - (242, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.NotNull(d);
#line (243, 5) - (244, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (d is not null)
#line hidden
            {
#line (244, 9) - (244, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(10.0d, d.Value);
#line hidden
            }

#line (245, 5) - (245, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            socket.Setdefaulttimeout(null);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSetdefaulttimeoutAppliedToNewSockets()
        {
#line (249, 5) - (249, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            socket.Setdefaulttimeout(3.0d);
#line (250, 5) - (250, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket();
#line (251, 5) - (251, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var t = s.Gettimeout();
#line (252, 5) - (252, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.NotNull(t);
#line (253, 5) - (254, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (t is not null)
#line hidden
            {
#line (254, 9) - (254, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(3.0d, t.Value);
#line hidden
            }

#line (255, 5) - (255, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line (256, 5) - (256, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            socket.Setdefaulttimeout(null);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestHtonsNtohsRoundTrips()
        {
#line (262, 5) - (262, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int original = 8080;
#line (263, 5) - (263, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int network = socket.Htons(original);
#line (264, 5) - (264, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int host = socket.Ntohs(network);
#line (265, 5) - (265, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(original, host);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestHtonlNtohlRoundTrips()
        {
#line (269, 5) - (269, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int original = 305419896;
#line (270, 5) - (270, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int network = socket.Htonl(original);
#line (271, 5) - (271, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int host = socket.Ntohl(network);
#line (272, 5) - (272, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(original, host);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestInetAtonNtoaRoundTrips()
        {
#line (278, 5) - (278, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string ip = "192.168.1.1";
#line (279, 5) - (279, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Sharpy.Bytes packed = socket.Inet_aton(ip);
#line (280, 5) - (280, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string result = socket.Inet_ntoa(packed);
#line (281, 5) - (281, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(ip, result);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestInetPtonNtopIpv4RoundTrips()
        {
#line (285, 5) - (285, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string ip = "10.0.0.1";
#line (286, 5) - (286, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Sharpy.Bytes packed = socket.Inet_pton(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, ip);
#line (287, 5) - (287, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(4, global::Sharpy.Builtins.Len(packed));
#line (288, 5) - (288, 60) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string result = socket.Inet_ntop(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, packed);
#line (289, 5) - (289, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(ip, result);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestInetPtonNtopIpv6RoundTrips()
        {
#line (293, 5) - (293, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string ip = "::1";
#line (294, 5) - (294, 59) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Sharpy.Bytes packed = socket.Inet_pton(global::Sharpy.SocketModule.SocketModuleModule.AF_INET6, ip);
#line (295, 5) - (295, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(16, global::Sharpy.Builtins.Len(packed));
#line (296, 5) - (296, 61) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string result = socket.Inet_ntop(global::Sharpy.SocketModule.SocketModuleModule.AF_INET6, packed);
#line (297, 5) - (297, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(ip, result);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSocketIpv6CanCreate()
        {
#line (303, 5) - (303, 59) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET6, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (304, 5) - (304, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(global::Sharpy.SocketModule.SocketModuleModule.AF_INET6, s.Family);
#line (305, 5) - (305, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestConnectInvalidAddressRaisesError()
        {
#line (316, 5) - (316, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (317, 5) - (317, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Settimeout(1.0d);
#line (318, 5) - (318, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool caught = false;
#line (319, 5) - (319, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool isTimeout = false;
#line (320, 5) - (320, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (321, 5) - (325, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (322, 9) - (322, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Connect(("192.0.2.1", 1));
#line hidden
            }
            catch (global::System.Exception e)
            {
#line (324, 9) - (324, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                caught = (object?)e is global::Sharpy.SocketModule.Error;
#line (325, 9) - (325, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                isTimeout = (object?)e is global::Sharpy.SocketModule.Timeout;
#line hidden
            }

#line (326, 5) - (326, 47) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double elapsed = time.Monotonic() - start;
#line (327, 5) - (327, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(caught);
#line (332, 5) - (332, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed < 5.0d);
#line (333, 5) - (333, 82) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True((isTimeout && elapsed >= 0.5d) || (!isTimeout && elapsed < 0.5d));
#line (334, 5) - (334, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestErrorHierarchy()
        {
#line (342, 5) - (342, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool timeoutCaught = false;
#line (343, 5) - (346, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (344, 9) - (344, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                throw new global::Sharpy.SocketModule.Timeout("timed out");
#line hidden
            }
            catch (global::Sharpy.SocketModule.Error)
            {
#line (346, 9) - (346, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                timeoutCaught = true;
#line hidden
            }

#line (347, 5) - (347, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(timeoutCaught);
#line (349, 5) - (349, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool gaierrorCaught = false;
#line (350, 5) - (353, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (351, 9) - (351, 57) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                throw new global::Sharpy.SocketModule.Gaierror("name resolution failed");
#line hidden
            }
            catch (global::Sharpy.SocketModule.Error)
            {
#line (353, 9) - (353, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                gaierrorCaught = true;
#line hidden
            }

#line (354, 5) - (354, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(gaierrorCaught);
#line (356, 5) - (356, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool herrorCaught = false;
#line (357, 5) - (360, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (358, 9) - (358, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                throw new global::Sharpy.SocketModule.Herror("host error");
#line hidden
            }
            catch (global::Sharpy.SocketModule.Error)
            {
#line (360, 9) - (360, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                herrorCaught = true;
#line hidden
            }

#line (361, 5) - (361, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(herrorCaught);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestCreateConnectionConnectsToLocalServer()
        {
#line (367, 5) - (367, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (368, 5) - (368, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Setsockopt(global::Sharpy.SocketModule.SocketModuleModule.SOL_SOCKET, global::Sharpy.SocketModule.SocketModuleModule.SO_REUSEADDR, 1);
#line (369, 5) - (369, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (370, 5) - (370, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (371, 5) - (371, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (373, 5) - (373, 75) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.SocketModule.Socket client = socket.CreateConnection(("127.0.0.1", port));
#line (375, 5) - (375, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::System.ValueTuple<string, int> peer = client.Getpeername();
#line (376, 5) - (376, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("127.0.0.1", peer.Item1);
#line (377, 5) - (377, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(port, peer.Item2);
#line (379, 5) - (379, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (380, 5) - (380, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (381, 5) - (381, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (382, 5) - (382, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestCreateConnectionInvalidAddressRaises()
        {
#line (389, 5) - (389, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool caught = false;
#line (390, 5) - (390, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool isTimeout = false;
#line (391, 5) - (391, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (392, 5) - (396, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (393, 9) - (393, 64) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                socket.CreateConnection(("192.0.2.1", 1), timeout: 1.0d);
#line hidden
            }
            catch (global::System.Exception e)
            {
#line (395, 9) - (395, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                caught = (object?)e is global::Sharpy.SocketModule.Error;
#line (396, 9) - (396, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                isTimeout = (object?)e is global::Sharpy.SocketModule.Timeout;
#line hidden
            }

#line (397, 5) - (397, 47) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double elapsed = time.Monotonic() - start;
#line (398, 5) - (398, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(caught);
#line (401, 5) - (401, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed < 5.0d);
#line (402, 5) - (402, 82) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True((isTimeout && elapsed >= 0.5d) || (!isTimeout && elapsed < 0.5d));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAcceptWithTimeoutRaisesTimeout()
        {
#line (417, 5) - (417, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (418, 5) - (418, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (419, 5) - (419, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (420, 5) - (420, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Settimeout(1.0d);
#line (421, 5) - (421, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (422, 5) - (423, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool __raised_1 = false;
#line hidden
            try
            {
#line (423, 9) - (423, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Accept();
#line hidden
            }
            catch (global::Sharpy.SocketModule.Timeout)
            {
                __raised_1 = true;
            }

            if (!__raised_1)
                throw new global::Sharpy.AssertionError("Expected timeout to be raised, but no exception was raised");
#line (424, 5) - (424, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double elapsed = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start);
#line (425, 5) - (425, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed >= 0.5d);
#line (426, 5) - (426, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed < 5.0d);
#line (427, 5) - (427, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAcceptWithTimeoutReturnsPendingConnection()
        {
#line (431, 5) - (431, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (432, 5) - (432, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (433, 5) - (433, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (434, 5) - (434, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Settimeout(1.0d);
#line (435, 5) - (435, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (436, 5) - (436, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (437, 5) - (437, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (438, 5) - (438, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, addr) = server.Accept();
#line (439, 5) - (439, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("127.0.0.1", addr.Item1);
#line (440, 5) - (440, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (441, 5) - (441, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (442, 5) - (442, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAcceptWithSubMillisecondTimeoutRaisesTimeout()
        {
#line (447, 5) - (447, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (448, 5) - (448, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (449, 5) - (449, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (450, 5) - (450, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Settimeout(0.0001d);
#line (451, 5) - (451, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (452, 5) - (453, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool __raised_2 = false;
#line hidden
            try
            {
#line (453, 9) - (453, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Accept();
#line hidden
            }
            catch (global::Sharpy.SocketModule.Timeout)
            {
                __raised_2 = true;
            }

            if (!__raised_2)
                throw new global::Sharpy.AssertionError("Expected timeout to be raised, but no exception was raised");
#line (454, 5) - (454, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start) < 5.0d);
#line (455, 5) - (455, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAcceptNonBlockingRaisesErrorImmediately()
        {
#line (459, 5) - (459, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (460, 5) - (460, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (461, 5) - (461, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (462, 5) - (462, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Settimeout(0.0d);
#line (463, 5) - (463, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool caught = false;
#line (464, 5) - (464, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool isTimeout = false;
#line (465, 5) - (465, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (466, 5) - (470, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (467, 9) - (467, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Accept();
#line hidden
            }
            catch (global::System.Exception e)
            {
#line (469, 9) - (469, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                caught = (object?)e is global::Sharpy.SocketModule.Error;
#line (470, 9) - (470, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                isTimeout = (object?)e is global::Sharpy.SocketModule.Timeout;
#line hidden
            }

#line (471, 5) - (471, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(caught);
#line (472, 5) - (472, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.False(isTimeout);
#line (473, 5) - (473, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start) < 0.5d);
#line (474, 5) - (474, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestConnectNonBlockingRaisesErrorImmediately()
        {
#line (478, 5) - (478, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (479, 5) - (479, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Settimeout(0.0d);
#line (480, 5) - (480, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool caught = false;
#line (481, 5) - (481, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool isTimeout = false;
#line (482, 5) - (482, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (483, 5) - (487, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (484, 9) - (484, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Connect(("192.0.2.1", 1));
#line hidden
            }
            catch (global::System.Exception e)
            {
#line (486, 9) - (486, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                caught = (object?)e is global::Sharpy.SocketModule.Error;
#line (487, 9) - (487, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                isTimeout = (object?)e is global::Sharpy.SocketModule.Timeout;
#line hidden
            }

#line (488, 5) - (488, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(caught);
#line (489, 5) - (489, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.False(isTimeout);
#line (490, 5) - (490, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start) < 0.5d);
#line (491, 5) - (491, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestConnectWithTimeoutSucceedsToListeningServer()
        {
#line (495, 5) - (495, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (496, 5) - (496, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (497, 5) - (497, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (498, 5) - (498, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (499, 5) - (499, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (500, 5) - (500, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Settimeout(1.0d);
#line (501, 5) - (501, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (502, 5) - (502, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::System.ValueTuple<string, int> peer = client.Getpeername();
#line (503, 5) - (503, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("127.0.0.1", peer.Item1);
#line (504, 5) - (504, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(port, peer.Item2);
#line (505, 5) - (505, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (506, 5) - (506, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Sendall(new Sharpy.Bytes(new byte[] { 112, 105, 110, 103 }));
#line (507, 5) - (507, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Sharpy.Bytes received = conn.Recv(16);
#line (508, 5) - (508, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(new Sharpy.Bytes(new byte[] { 112, 105, 110, 103 }), received);
#line (509, 5) - (509, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (510, 5) - (510, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (511, 5) - (511, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestCreateConnectionWithTimeoutSucceedsToListeningServer()
        {
#line (515, 5) - (515, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (516, 5) - (516, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (517, 5) - (517, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (518, 5) - (518, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (519, 5) - (519, 88) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.SocketModule.Socket client = socket.CreateConnection(("127.0.0.1", port), timeout: 1.0d);
#line (520, 5) - (520, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::System.ValueTuple<string, int> peer = client.Getpeername();
#line (521, 5) - (521, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(port, peer.Item2);
#line (522, 5) - (522, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (523, 5) - (523, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (524, 5) - (524, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (525, 5) - (525, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestConnectWithTimeoutRefusedRaisesErrorNotTimeout()
        {
#line (530, 5) - (530, 62) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var probe = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (531, 5) - (531, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            probe.Bind(("127.0.0.1", 0));
#line (532, 5) - (532, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = probe.Getsockname().Item2;
#line (533, 5) - (533, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            probe.Close();
#line (534, 5) - (534, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (535, 5) - (535, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Settimeout(1.0d);
#line (536, 5) - (536, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool caught = false;
#line (537, 5) - (537, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool isTimeout = false;
#line (538, 5) - (542, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (539, 9) - (539, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Connect(("127.0.0.1", port));
#line hidden
            }
            catch (global::System.Exception e)
            {
#line (541, 9) - (541, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                caught = (object?)e is global::Sharpy.SocketModule.Error;
#line (542, 9) - (542, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                isTimeout = (object?)e is global::Sharpy.SocketModule.Timeout;
#line hidden
            }

#line (543, 5) - (543, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(caught);
#line (544, 5) - (544, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.False(isTimeout);
#line (545, 5) - (545, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestRecvWithTimeoutRaisesTimeout()
        {
#line (549, 5) - (549, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (550, 5) - (550, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (551, 5) - (551, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (552, 5) - (552, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (553, 5) - (553, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (554, 5) - (554, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (555, 5) - (555, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (556, 5) - (556, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Settimeout(1.0d);
#line (557, 5) - (557, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (558, 5) - (559, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool __raised_3 = false;
#line hidden
            try
            {
#line (559, 9) - (559, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                client.Recv(16);
#line hidden
            }
            catch (global::Sharpy.SocketModule.Timeout)
            {
                __raised_3 = true;
            }

            if (!__raised_3)
                throw new global::Sharpy.AssertionError("Expected timeout to be raised, but no exception was raised");
#line (560, 5) - (560, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double elapsed = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start);
#line (561, 5) - (561, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed >= 0.5d);
#line (562, 5) - (562, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed < 5.0d);
#line (563, 5) - (563, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (564, 5) - (564, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (565, 5) - (565, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSendWithTimeoutRaisesTimeoutWhenPeerDoesNotRead()
        {
#line (569, 5) - (569, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (570, 5) - (570, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (571, 5) - (571, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (572, 5) - (572, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (573, 5) - (573, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (574, 5) - (574, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (575, 5) - (575, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (576, 5) - (576, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Settimeout(1.0d);
#line (577, 5) - (577, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Sharpy.Bytes chunk = new Sharpy.Bytes(new byte[] { 120 }) * 65536;
#line (578, 5) - (578, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool isTimeout = false;
#line (579, 5) - (579, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (580, 5) - (584, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (581, 9) - (582, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                while (global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start) < 10.0d)
#line hidden
                {
#line (582, 13) - (582, 31) 20 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    client.Send(chunk);
#line hidden
                }
            }
            catch (global::System.Exception e)
            {
#line (584, 9) - (584, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                isTimeout = (object?)e is global::Sharpy.SocketModule.Timeout;
#line hidden
            }

#line (585, 5) - (585, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(isTimeout);
#line (586, 5) - (586, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start) < 5.0d);
#line (587, 5) - (587, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (588, 5) - (588, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (589, 5) - (589, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSettimeoutNegativeRaisesValueError()
        {
#line (593, 5) - (593, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (594, 5) - (595, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool __raised_4 = false;
#line hidden
            try
            {
#line (595, 9) - (595, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Settimeout(-1.0d);
#line hidden
            }
            catch (ValueError)
            {
                __raised_4 = true;
            }

            if (!__raised_4)
                throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
#line (596, 5) - (596, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Null(s.Gettimeout());
#line (597, 5) - (597, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeoutRuleAcceptsNoneZeroAndPositive()
        {
#line (687, 5) - (687, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(null, "accepted");
#line (688, 5) - (688, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(0.0d, "accepted");
#line (689, 5) - (689, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(-0.0d, "accepted");
#line (690, 5) - (690, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(1.5d, "accepted");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeoutRuleAcceptsVeryLargeWithinInt64Nanoseconds()
        {
#line (694, 5) - (694, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(1e6d, "accepted");
#line (695, 5) - (695, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(9.2e9d, "accepted");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeoutRuleRefusesNegativeWithValueError()
        {
#line (699, 5) - (699, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(-1.0d, "ValueError");
#line (700, 5) - (700, 47) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(-1e-10d, "ValueError");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeoutRuleRefusesNanWithValueError()
        {
#line (704, 5) - (704, 53) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(global::Sharpy.Builtins.Float("nan"), "ValueError");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeoutRuleRefusesInt64NanosecondOverflowWithOverflowError()
        {
#line (708, 5) - (708, 56) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(global::Sharpy.Builtins.Float("inf"), "OverflowError");
#line (709, 5) - (709, 57) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(global::Sharpy.Builtins.Float("-inf"), "OverflowError");
#line (710, 5) - (710, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(9.3e9d, "OverflowError");
#line (711, 5) - (711, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(1e300d, "OverflowError");
#line (712, 5) - (712, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(-1e300d, "OverflowError");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestVeryLargeTimeoutStillGovernsLoopbackConnectAndRecv()
        {
#line (718, 5) - (718, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (719, 5) - (719, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (720, 5) - (720, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (721, 5) - (721, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (722, 5) - (722, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (723, 5) - (723, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Settimeout(9.2e9d);
#line (724, 5) - (724, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (725, 5) - (725, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (726, 5) - (726, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Settimeout(9.2e9d);
#line (727, 5) - (727, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int sent = client.Send(new Sharpy.Bytes(new byte[] { 112, 105, 110, 103 }));
#line (728, 5) - (728, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(4, sent);
#line (729, 5) - (729, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Sharpy.Bytes received = conn.Recv(16);
#line (730, 5) - (730, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(new Sharpy.Bytes(new byte[] { 112, 105, 110, 103 }), received);
#line (731, 5) - (731, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (732, 5) - (732, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (733, 5) - (733, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestStrContainsSocketInfo()
        {
#line (739, 5) - (739, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (740, 5) - (740, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string text = global::Sharpy.Builtins.Str(s);
#line (741, 5) - (741, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(text.Contains("socket"));
#line (742, 5) - (742, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(text.Contains("family="));
#line (743, 5) - (743, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(text.Contains("type="));
#line (744, 5) - (744, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGetpeernameAfterConnectReturnsRemoteAddr()
        {
#line (750, 5) - (750, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (751, 5) - (751, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Setsockopt(global::Sharpy.SocketModule.SocketModuleModule.SOL_SOCKET, global::Sharpy.SocketModule.SocketModuleModule.SO_REUSEADDR, 1);
#line (752, 5) - (752, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (753, 5) - (753, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (754, 5) - (754, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (756, 5) - (756, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (757, 5) - (757, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (759, 5) - (759, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::System.ValueTuple<string, int> peer = client.Getpeername();
#line (760, 5) - (760, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("127.0.0.1", peer.Item1);
#line (761, 5) - (761, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(port, peer.Item2);
#line (763, 5) - (763, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (764, 5) - (764, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (765, 5) - (765, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (766, 5) - (766, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSetblockingFalseSetsNonBlocking()
        {
#line (772, 5) - (772, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (773, 5) - (773, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Setblocking(false);
#line (774, 5) - (774, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.False(s.Getblocking());
#line (775, 5) - (775, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSetblockingTrueSetsBlocking()
        {
#line (779, 5) - (779, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (780, 5) - (780, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Setblocking(false);
#line (781, 5) - (781, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Setblocking(true);
#line (782, 5) - (782, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(s.Getblocking());
#line (783, 5) - (783, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGetnameinfoLocalhostReturnsHostAndService()
        {
#line (789, 5) - (789, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (host, service) = socket.Getnameinfo(("127.0.0.1", 80));
#line (790, 5) - (790, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.NotEqual("", host);
#line (791, 5) - (791, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("80", service);
#line hidden
        }
    }
}
#line default
