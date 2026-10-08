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
#line (467, 5) - (467, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            return time.Monotonic() - start;
#line hidden
        }

        internal static string _TimeoutOutcome(global::System.Exception e)
        {
#line (666, 5) - (667, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if ((object?)e is global::Sharpy.OverflowError)
#line hidden
            {
#line (667, 9) - (667, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                return "OverflowError";
#line hidden
            }

#line (668, 5) - (669, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if ((object?)e is global::Sharpy.ValueError)
#line hidden
            {
#line (669, 9) - (669, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                return "ValueError";
#line hidden
            }

#line (670, 5) - (671, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if ((object?)e is global::Sharpy.SocketModule.Error)
#line hidden
            {
#line (671, 9) - (671, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                return "socket.error";
#line hidden
            }

#line (672, 5) - (672, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            return "other";
#line hidden
        }

        internal static bool _SameTimeout(double? actual, double? expected)
        {
#line (675, 5) - (676, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (actual is null || expected is null)
#line hidden
            {
#line (676, 9) - (676, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                return actual is null && expected is null;
#line hidden
            }

#line (677, 5) - (677, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double a = actual.Value;
#line (678, 5) - (678, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double b = expected.Value;
#line (679, 5) - (679, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            return a == b;
#line hidden
        }

        internal static int _ClosedLoopbackPort()
        {
#line (682, 5) - (682, 62) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var probe = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (683, 5) - (683, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            probe.Bind(("127.0.0.1", 0));
#line (684, 5) - (684, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int port = probe.Getsockname().Item2;
#line (685, 5) - (685, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            probe.Close();
#line (686, 5) - (686, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            return port;
#line hidden
        }

        internal static void _AssertTimeoutRule(double? value, string expected)
        {
#line (692, 5) - (692, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double? survivor = 2.0d;
#line (693, 5) - (694, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (expected == "accepted")
#line hidden
            {
#line (694, 9) - (694, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                survivor = value;
#line hidden
            }

#line (695, 5) - (695, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string outcome = "accepted";
#line (696, 5) - (696, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            socket.Setdefaulttimeout(2.0d);
#line (697, 5) - (709, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (698, 9) - (701, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                try
#line hidden
                {
#line (699, 13) - (699, 44) 20 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    socket.Setdefaulttimeout(value);
#line hidden
                }
                catch (global::System.Exception e)
                {
#line (701, 13) - (701, 42) 20 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    outcome = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._TimeoutOutcome(e);
#line hidden
                }

#line (702, 9) - (702, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (!(outcome == expected))
#line hidden
                {
                    throw new global::Sharpy.AssertionError();
                }

#line (703, 9) - (703, 68) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (!(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._SameTimeout(socket.Getdefaulttimeout(), survivor)))
#line hidden
                {
                    throw new global::Sharpy.AssertionError();
                }

#line (704, 9) - (704, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var fresh = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (705, 9) - (705, 60) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (!(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._SameTimeout(fresh.Gettimeout(), survivor)))
#line hidden
                {
                    throw new global::Sharpy.AssertionError();
                }

#line (706, 9) - (706, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                fresh.Close();
#line hidden
            }
            finally
            {
#line (709, 9) - (709, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                socket.Setdefaulttimeout(null);
#line hidden
            }

#line (712, 5) - (712, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (713, 5) - (713, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Settimeout(2.0d);
#line (714, 5) - (714, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            outcome = "accepted";
#line (715, 5) - (718, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (716, 9) - (716, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Settimeout(value);
#line hidden
            }
            catch (global::System.Exception e_1)
            {
#line (718, 9) - (718, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                outcome = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._TimeoutOutcome(e_1);
#line hidden
            }

#line (719, 5) - (719, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (!(outcome == expected))
#line hidden
            {
                throw new global::Sharpy.AssertionError();
            }

#line (720, 5) - (720, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (!(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._SameTimeout(s.Gettimeout(), survivor)))
#line hidden
            {
                throw new global::Sharpy.AssertionError();
            }

#line (721, 5) - (721, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line (726, 5) - (726, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int port = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ClosedLoopbackPort();
#line (727, 5) - (727, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            outcome = "accepted";
#line (728, 5) - (733, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (729, 9) - (729, 89) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                global::Sharpy.SocketModule.Socket c = socket.CreateConnection(("127.0.0.1", port), timeout: value);
#line (730, 9) - (730, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                c.Close();
#line hidden
            }
            catch (global::System.Exception e_2)
            {
#line (732, 9) - (732, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                outcome = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._TimeoutOutcome(e_2);
#line (733, 9) - (733, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (!(!((object?)e_2 is global::Sharpy.SocketModule.Timeout)))
#line hidden
                {
                    throw new global::Sharpy.AssertionError();
                }
            }

#line (734, 5) - (737, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (expected == "accepted")
#line hidden
            {
#line (735, 9) - (735, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (!(outcome == "socket.error"))
#line hidden
                {
                    throw new global::Sharpy.AssertionError();
                }
            }
            else
            {
#line (737, 9) - (737, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
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
#line (332, 5) - (332, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed < 30.0d);
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
#line (401, 5) - (401, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed < 30.0d);
#line (402, 5) - (402, 82) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True((isTimeout && elapsed >= 0.5d) || (!isTimeout && elapsed < 0.5d));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestCreateConnectionTimeoutOmittedNoneAndValue()
        {
#line (410, 5) - (410, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (411, 5) - (411, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Setsockopt(global::Sharpy.SocketModule.SocketModuleModule.SOL_SOCKET, global::Sharpy.SocketModule.SocketModuleModule.SO_REUSEADDR, 1);
#line (412, 5) - (412, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (413, 5) - (413, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(8);
#line (414, 5) - (414, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (415, 5) - (415, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            socket.Setdefaulttimeout(5.0d);
#line (416, 5) - (436, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (417, 9) - (417, 80) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                global::Sharpy.SocketModule.Socket omitted = socket.CreateConnection(("127.0.0.1", port));
#line (418, 9) - (418, 94) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                global::Sharpy.SocketModule.Socket nonePositional = socket.CreateConnection(("127.0.0.1", port), null);
#line (419, 9) - (419, 99) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                global::Sharpy.SocketModule.Socket noneKeyword = socket.CreateConnection(("127.0.0.1", port), timeout: null);
#line (420, 9) - (420, 94) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                global::Sharpy.SocketModule.Socket valuePositional = socket.CreateConnection(("127.0.0.1", port), 2.0d);
#line (421, 9) - (421, 99) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                global::Sharpy.SocketModule.Socket valueKeyword = socket.CreateConnection(("127.0.0.1", port), timeout: 2.0d);
#line (423, 9) - (423, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(5.0d, omitted.Gettimeout());
#line (424, 9) - (424, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Null(nonePositional.Gettimeout());
#line (425, 9) - (425, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Null(noneKeyword.Gettimeout());
#line (426, 9) - (426, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(2.0d, valuePositional.Gettimeout());
#line (427, 9) - (427, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(2.0d, valueKeyword.Gettimeout());
#line (429, 9) - (429, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                omitted.Close();
#line (430, 9) - (430, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                nonePositional.Close();
#line (431, 9) - (431, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                noneKeyword.Close();
#line (432, 9) - (432, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                valuePositional.Close();
#line (433, 9) - (433, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                valueKeyword.Close();
#line hidden
            }
            finally
            {
#line (435, 9) - (435, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                socket.Setdefaulttimeout(null);
#line (436, 9) - (436, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Close();
#line hidden
            }
        }

        [Xunit.FactAttribute]
        public void TestCreateConnectionOmittedTimeoutWithoutDefaultIsBlocking()
        {
#line (441, 5) - (441, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (442, 5) - (442, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Setsockopt(global::Sharpy.SocketModule.SocketModuleModule.SOL_SOCKET, global::Sharpy.SocketModule.SocketModuleModule.SO_REUSEADDR, 1);
#line (443, 5) - (443, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (444, 5) - (444, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (445, 5) - (445, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (446, 5) - (446, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            socket.Setdefaulttimeout(null);
#line (447, 5) - (447, 75) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.SocketModule.Socket client = socket.CreateConnection(("127.0.0.1", port));
#line (448, 5) - (448, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Null(client.Gettimeout());
#line (449, 5) - (449, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (450, 5) - (450, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAcceptWithTimeoutRaisesTimeout()
        {
#line (471, 5) - (471, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (472, 5) - (472, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (473, 5) - (473, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (474, 5) - (474, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Settimeout(1.0d);
#line (475, 5) - (475, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (476, 5) - (477, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool __raised_1 = false;
#line hidden
            try
            {
#line (477, 9) - (477, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Accept();
#line hidden
            }
            catch (global::Sharpy.SocketModule.Timeout)
            {
                __raised_1 = true;
            }

            if (!__raised_1)
                throw new global::Sharpy.AssertionError("Expected timeout to be raised, but no exception was raised");
#line (478, 5) - (478, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double elapsed = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start);
#line (479, 5) - (479, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed >= 0.5d);
#line (480, 5) - (480, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed < 30.0d);
#line (481, 5) - (481, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAcceptWithTimeoutReturnsPendingConnection()
        {
#line (485, 5) - (485, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (486, 5) - (486, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (487, 5) - (487, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (488, 5) - (488, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Settimeout(10.0d);
#line (489, 5) - (489, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (490, 5) - (490, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (491, 5) - (491, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (492, 5) - (492, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, addr) = server.Accept();
#line (493, 5) - (493, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("127.0.0.1", addr.Item1);
#line (494, 5) - (494, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (495, 5) - (495, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (496, 5) - (496, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAcceptWithSubMillisecondTimeoutRaisesTimeout()
        {
#line (501, 5) - (501, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (502, 5) - (502, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (503, 5) - (503, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (504, 5) - (504, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Settimeout(0.0001d);
#line (505, 5) - (505, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (506, 5) - (507, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool __raised_2 = false;
#line hidden
            try
            {
#line (507, 9) - (507, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Accept();
#line hidden
            }
            catch (global::Sharpy.SocketModule.Timeout)
            {
                __raised_2 = true;
            }

            if (!__raised_2)
                throw new global::Sharpy.AssertionError("Expected timeout to be raised, but no exception was raised");
#line (508, 5) - (508, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start) < 30.0d);
#line (509, 5) - (509, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAcceptNonBlockingRaisesErrorImmediately()
        {
#line (513, 5) - (513, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (514, 5) - (514, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (515, 5) - (515, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (516, 5) - (516, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Settimeout(0.0d);
#line (517, 5) - (517, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool caught = false;
#line (518, 5) - (518, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool isTimeout = false;
#line (519, 5) - (519, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (520, 5) - (524, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (521, 9) - (521, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Accept();
#line hidden
            }
            catch (global::System.Exception e)
            {
#line (523, 9) - (523, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                caught = (object?)e is global::Sharpy.SocketModule.Error;
#line (524, 9) - (524, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                isTimeout = (object?)e is global::Sharpy.SocketModule.Timeout;
#line hidden
            }

#line (525, 5) - (525, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(caught);
#line (526, 5) - (526, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.False(isTimeout);
#line (527, 5) - (527, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start) < 0.5d);
#line (528, 5) - (528, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestConnectNonBlockingRaisesErrorImmediately()
        {
#line (532, 5) - (532, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (533, 5) - (533, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Settimeout(0.0d);
#line (534, 5) - (534, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool caught = false;
#line (535, 5) - (535, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool isTimeout = false;
#line (536, 5) - (536, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (537, 5) - (541, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (538, 9) - (538, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Connect(("192.0.2.1", 1));
#line hidden
            }
            catch (global::System.Exception e)
            {
#line (540, 9) - (540, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                caught = (object?)e is global::Sharpy.SocketModule.Error;
#line (541, 9) - (541, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                isTimeout = (object?)e is global::Sharpy.SocketModule.Timeout;
#line hidden
            }

#line (542, 5) - (542, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(caught);
#line (543, 5) - (543, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.False(isTimeout);
#line (544, 5) - (544, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start) < 0.5d);
#line (545, 5) - (545, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestConnectWithTimeoutSucceedsToListeningServer()
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
#line (554, 5) - (554, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Settimeout(10.0d);
#line (555, 5) - (555, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (556, 5) - (556, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::System.ValueTuple<string, int> peer = client.Getpeername();
#line (557, 5) - (557, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("127.0.0.1", peer.Item1);
#line (558, 5) - (558, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(port, peer.Item2);
#line (559, 5) - (559, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (560, 5) - (560, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Sendall(new Sharpy.Bytes(new byte[] { 112, 105, 110, 103 }));
#line (561, 5) - (561, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Sharpy.Bytes received = conn.Recv(16);
#line (562, 5) - (562, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(new Sharpy.Bytes(new byte[] { 112, 105, 110, 103 }), received);
#line (563, 5) - (563, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (564, 5) - (564, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (565, 5) - (565, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestCreateConnectionWithTimeoutSucceedsToListeningServer()
        {
#line (569, 5) - (569, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (570, 5) - (570, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (571, 5) - (571, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (572, 5) - (572, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (573, 5) - (573, 89) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.SocketModule.Socket client = socket.CreateConnection(("127.0.0.1", port), timeout: 10.0d);
#line (574, 5) - (574, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::System.ValueTuple<string, int> peer = client.Getpeername();
#line (575, 5) - (575, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(port, peer.Item2);
#line (576, 5) - (576, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (577, 5) - (577, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (578, 5) - (578, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (579, 5) - (579, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestConnectWithTimeoutRefusedRaisesErrorNotTimeout()
        {
#line (584, 5) - (584, 62) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var probe = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (585, 5) - (585, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            probe.Bind(("127.0.0.1", 0));
#line (586, 5) - (586, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = probe.Getsockname().Item2;
#line (587, 5) - (587, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            probe.Close();
#line (588, 5) - (588, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (589, 5) - (589, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Settimeout(10.0d);
#line (590, 5) - (590, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool caught = false;
#line (591, 5) - (591, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool isTimeout = false;
#line (592, 5) - (596, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (593, 9) - (593, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Connect(("127.0.0.1", port));
#line hidden
            }
            catch (global::System.Exception e)
            {
#line (595, 9) - (595, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                caught = (object?)e is global::Sharpy.SocketModule.Error;
#line (596, 9) - (596, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                isTimeout = (object?)e is global::Sharpy.SocketModule.Timeout;
#line hidden
            }

#line (597, 5) - (597, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(caught);
#line (598, 5) - (598, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.False(isTimeout);
#line (599, 5) - (599, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestRecvWithTimeoutRaisesTimeout()
        {
#line (603, 5) - (603, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (604, 5) - (604, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (605, 5) - (605, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (606, 5) - (606, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (607, 5) - (607, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (608, 5) - (608, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (609, 5) - (609, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (610, 5) - (610, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Settimeout(1.0d);
#line (611, 5) - (611, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (612, 5) - (613, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool __raised_3 = false;
#line hidden
            try
            {
#line (613, 9) - (613, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                client.Recv(16);
#line hidden
            }
            catch (global::Sharpy.SocketModule.Timeout)
            {
                __raised_3 = true;
            }

            if (!__raised_3)
                throw new global::Sharpy.AssertionError("Expected timeout to be raised, but no exception was raised");
#line (614, 5) - (614, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double elapsed = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start);
#line (615, 5) - (615, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed >= 0.5d);
#line (616, 5) - (616, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed < 30.0d);
#line (617, 5) - (617, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (618, 5) - (618, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (619, 5) - (619, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSendWithTimeoutRaisesTimeoutWhenPeerDoesNotRead()
        {
#line (623, 5) - (623, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (624, 5) - (624, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (625, 5) - (625, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (626, 5) - (626, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (627, 5) - (627, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (628, 5) - (628, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (629, 5) - (629, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (630, 5) - (630, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Settimeout(1.0d);
#line (631, 5) - (631, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Sharpy.Bytes chunk = new Sharpy.Bytes(new byte[] { 120 }) * 65536;
#line (632, 5) - (632, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool isTimeout = false;
#line (633, 5) - (633, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (634, 5) - (638, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (635, 9) - (636, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                while (global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start) < 30.0d)
#line hidden
                {
#line (636, 13) - (636, 31) 20 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    client.Send(chunk);
#line hidden
                }
            }
            catch (global::System.Exception e)
            {
#line (638, 9) - (638, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                isTimeout = (object?)e is global::Sharpy.SocketModule.Timeout;
#line hidden
            }

#line (639, 5) - (639, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(isTimeout);
#line (640, 5) - (640, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start) < 30.0d);
#line (641, 5) - (641, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (642, 5) - (642, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (643, 5) - (643, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSettimeoutNegativeRaisesValueError()
        {
#line (647, 5) - (647, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (648, 5) - (649, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool __raised_4 = false;
#line hidden
            try
            {
#line (649, 9) - (649, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Settimeout(-1.0d);
#line hidden
            }
            catch (ValueError)
            {
                __raised_4 = true;
            }

            if (!__raised_4)
                throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
#line (650, 5) - (650, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Null(s.Gettimeout());
#line (651, 5) - (651, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeoutRuleAcceptsNoneZeroAndPositive()
        {
#line (741, 5) - (741, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(null, "accepted");
#line (742, 5) - (742, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(0.0d, "accepted");
#line (743, 5) - (743, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(-0.0d, "accepted");
#line (744, 5) - (744, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(1.5d, "accepted");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeoutRuleAcceptsVeryLargeWithinInt64Nanoseconds()
        {
#line (748, 5) - (748, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(1e6d, "accepted");
#line (749, 5) - (749, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(9.2e9d, "accepted");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeoutRuleRefusesNegativeWithValueError()
        {
#line (753, 5) - (753, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(-1.0d, "ValueError");
#line (754, 5) - (754, 47) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(-1e-10d, "ValueError");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeoutRuleRefusesNanWithValueError()
        {
#line (758, 5) - (758, 53) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(global::Sharpy.Builtins.Float("nan"), "ValueError");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeoutRuleRefusesInt64NanosecondOverflowWithOverflowError()
        {
#line (762, 5) - (762, 56) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(global::Sharpy.Builtins.Float("inf"), "OverflowError");
#line (763, 5) - (763, 57) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(global::Sharpy.Builtins.Float("-inf"), "OverflowError");
#line (764, 5) - (764, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(9.3e9d, "OverflowError");
#line (765, 5) - (765, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(1e300d, "OverflowError");
#line (766, 5) - (766, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(-1e300d, "OverflowError");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestVeryLargeTimeoutStillGovernsLoopbackConnectAndRecv()
        {
#line (772, 5) - (772, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (773, 5) - (773, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (774, 5) - (774, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (775, 5) - (775, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (776, 5) - (776, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (777, 5) - (777, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Settimeout(9.2e9d);
#line (778, 5) - (778, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (779, 5) - (779, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (780, 5) - (780, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Settimeout(9.2e9d);
#line (781, 5) - (781, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int sent = client.Send(new Sharpy.Bytes(new byte[] { 112, 105, 110, 103 }));
#line (782, 5) - (782, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(4, sent);
#line (783, 5) - (783, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Sharpy.Bytes received = conn.Recv(16);
#line (784, 5) - (784, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(new Sharpy.Bytes(new byte[] { 112, 105, 110, 103 }), received);
#line (785, 5) - (785, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (786, 5) - (786, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (787, 5) - (787, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestStrContainsSocketInfo()
        {
#line (793, 5) - (793, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (794, 5) - (794, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string text = global::Sharpy.Builtins.Str(s);
#line (795, 5) - (795, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(text.Contains("socket"));
#line (796, 5) - (796, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(text.Contains("family="));
#line (797, 5) - (797, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(text.Contains("type="));
#line (798, 5) - (798, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGetpeernameAfterConnectReturnsRemoteAddr()
        {
#line (804, 5) - (804, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (805, 5) - (805, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Setsockopt(global::Sharpy.SocketModule.SocketModuleModule.SOL_SOCKET, global::Sharpy.SocketModule.SocketModuleModule.SO_REUSEADDR, 1);
#line (806, 5) - (806, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (807, 5) - (807, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (808, 5) - (808, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (810, 5) - (810, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (811, 5) - (811, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (813, 5) - (813, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::System.ValueTuple<string, int> peer = client.Getpeername();
#line (814, 5) - (814, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("127.0.0.1", peer.Item1);
#line (815, 5) - (815, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(port, peer.Item2);
#line (817, 5) - (817, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (818, 5) - (818, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (819, 5) - (819, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (820, 5) - (820, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSetblockingFalseSetsNonBlocking()
        {
#line (826, 5) - (826, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (827, 5) - (827, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Setblocking(false);
#line (828, 5) - (828, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.False(s.Getblocking());
#line (829, 5) - (829, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSetblockingTrueSetsBlocking()
        {
#line (833, 5) - (833, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (834, 5) - (834, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Setblocking(false);
#line (835, 5) - (835, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Setblocking(true);
#line (836, 5) - (836, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(s.Getblocking());
#line (837, 5) - (837, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGetnameinfoLocalhostReturnsHostAndService()
        {
#line (843, 5) - (843, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (host, service) = socket.Getnameinfo(("127.0.0.1", 80));
#line (844, 5) - (844, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.NotEqual("", host);
#line (845, 5) - (845, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("80", service);
#line hidden
        }
    }
}
#line default
