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
#line (473, 5) - (473, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            return time.Monotonic() - start;
#line hidden
        }

        internal static string _TimeoutOutcome(global::System.Exception e)
        {
#line (675, 5) - (676, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if ((object?)e is global::Sharpy.OverflowError)
#line hidden
            {
#line (676, 9) - (676, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                return "OverflowError";
#line hidden
            }

#line (677, 5) - (678, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if ((object?)e is global::Sharpy.ValueError)
#line hidden
            {
#line (678, 9) - (678, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                return "ValueError";
#line hidden
            }

#line (679, 5) - (680, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if ((object?)e is global::Sharpy.SocketModule.Error)
#line hidden
            {
#line (680, 9) - (680, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                return "socket.error";
#line hidden
            }

#line (681, 5) - (681, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            return "other";
#line hidden
        }

        internal static bool _SameTimeout(double? actual, double? expected)
        {
#line (684, 5) - (685, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (actual is null || expected is null)
#line hidden
            {
#line (685, 9) - (685, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                return actual is null && expected is null;
#line hidden
            }

#line (686, 5) - (686, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double a = actual.Value;
#line (687, 5) - (687, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double b = expected.Value;
#line (688, 5) - (688, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            return a == b;
#line hidden
        }

        internal static int _ClosedLoopbackPort()
        {
#line (691, 5) - (691, 62) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var probe = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (692, 5) - (692, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            probe.Bind(("127.0.0.1", 0));
#line (693, 5) - (693, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int port = probe.Getsockname().Item2;
#line (694, 5) - (694, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            probe.Close();
#line (695, 5) - (695, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            return port;
#line hidden
        }

        internal static void _AssertTimeoutRule(double? value, string expected)
        {
#line (701, 5) - (701, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double? survivor = 2.0d;
#line (702, 5) - (703, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (expected == "accepted")
#line hidden
            {
#line (703, 9) - (703, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                survivor = value;
#line hidden
            }

#line (704, 5) - (704, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string outcome = "accepted";
#line (705, 5) - (705, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            socket.Setdefaulttimeout(2.0d);
#line (706, 5) - (718, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (707, 9) - (710, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                try
#line hidden
                {
#line (708, 13) - (708, 44) 20 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    socket.Setdefaulttimeout(value);
#line hidden
                }
                catch (global::System.Exception e)
                {
#line (710, 13) - (710, 42) 20 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    outcome = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._TimeoutOutcome(e);
#line hidden
                }

#line (711, 9) - (711, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (!(outcome == expected))
#line hidden
                {
                    throw new global::Sharpy.AssertionError();
                }

#line (712, 9) - (712, 68) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (!(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._SameTimeout(socket.Getdefaulttimeout(), survivor)))
#line hidden
                {
                    throw new global::Sharpy.AssertionError();
                }

#line (713, 9) - (713, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var fresh = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (714, 9) - (714, 60) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (!(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._SameTimeout(fresh.Gettimeout(), survivor)))
#line hidden
                {
                    throw new global::Sharpy.AssertionError();
                }

#line (715, 9) - (715, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                fresh.Close();
#line hidden
            }
            finally
            {
#line (718, 9) - (718, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                socket.Setdefaulttimeout(null);
#line hidden
            }

#line (721, 5) - (721, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (722, 5) - (722, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Settimeout(2.0d);
#line (723, 5) - (723, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            outcome = "accepted";
#line (724, 5) - (727, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (725, 9) - (725, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Settimeout(value);
#line hidden
            }
            catch (global::System.Exception e_1)
            {
#line (727, 9) - (727, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                outcome = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._TimeoutOutcome(e_1);
#line hidden
            }

#line (728, 5) - (728, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (!(outcome == expected))
#line hidden
            {
                throw new global::Sharpy.AssertionError();
            }

#line (729, 5) - (729, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (!(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._SameTimeout(s.Gettimeout(), survivor)))
#line hidden
            {
                throw new global::Sharpy.AssertionError();
            }

#line (730, 5) - (730, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line (735, 5) - (735, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int port = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ClosedLoopbackPort();
#line (736, 5) - (736, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            outcome = "accepted";
#line (737, 5) - (742, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (738, 9) - (738, 89) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                global::Sharpy.SocketModule.Socket c = socket.CreateConnection(("127.0.0.1", port), timeout: value);
#line (739, 9) - (739, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                c.Close();
#line hidden
            }
            catch (global::System.Exception e_2)
            {
#line (741, 9) - (741, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                outcome = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._TimeoutOutcome(e_2);
#line (742, 9) - (742, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (!(!((object?)e_2 is global::Sharpy.SocketModule.Timeout)))
#line hidden
                {
                    throw new global::Sharpy.AssertionError();
                }
            }

#line (743, 5) - (746, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (expected == "accepted")
#line hidden
            {
#line (744, 9) - (744, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (!(outcome == "socket.error"))
#line hidden
                {
                    throw new global::Sharpy.AssertionError();
                }
            }
            else
            {
#line (746, 9) - (746, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
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
#line (336, 5) - (336, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed < 30.0d);
#line (337, 5) - (337, 82) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True((isTimeout && elapsed >= 0.5d) || (!isTimeout && elapsed < 1.0d));
#line (338, 5) - (338, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestErrorHierarchy()
        {
#line (346, 5) - (346, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool timeoutCaught = false;
#line (347, 5) - (350, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (348, 9) - (348, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                throw new global::Sharpy.SocketModule.Timeout("timed out");
#line hidden
            }
            catch (global::Sharpy.SocketModule.Error)
            {
#line (350, 9) - (350, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                timeoutCaught = true;
#line hidden
            }

#line (351, 5) - (351, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(timeoutCaught);
#line (353, 5) - (353, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool gaierrorCaught = false;
#line (354, 5) - (357, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (355, 9) - (355, 57) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                throw new global::Sharpy.SocketModule.Gaierror("name resolution failed");
#line hidden
            }
            catch (global::Sharpy.SocketModule.Error)
            {
#line (357, 9) - (357, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                gaierrorCaught = true;
#line hidden
            }

#line (358, 5) - (358, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(gaierrorCaught);
#line (360, 5) - (360, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool herrorCaught = false;
#line (361, 5) - (364, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (362, 9) - (362, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                throw new global::Sharpy.SocketModule.Herror("host error");
#line hidden
            }
            catch (global::Sharpy.SocketModule.Error)
            {
#line (364, 9) - (364, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                herrorCaught = true;
#line hidden
            }

#line (365, 5) - (365, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(herrorCaught);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestCreateConnectionConnectsToLocalServer()
        {
#line (371, 5) - (371, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (372, 5) - (372, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Setsockopt(global::Sharpy.SocketModule.SocketModuleModule.SOL_SOCKET, global::Sharpy.SocketModule.SocketModuleModule.SO_REUSEADDR, 1);
#line (373, 5) - (373, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (374, 5) - (374, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (375, 5) - (375, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (377, 5) - (377, 75) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.SocketModule.Socket client = socket.CreateConnection(("127.0.0.1", port));
#line (379, 5) - (379, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::System.ValueTuple<string, int> peer = client.Getpeername();
#line (380, 5) - (380, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("127.0.0.1", peer.Item1);
#line (381, 5) - (381, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(port, peer.Item2);
#line (383, 5) - (383, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (384, 5) - (384, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (385, 5) - (385, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (386, 5) - (386, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestCreateConnectionInvalidAddressRaises()
        {
#line (393, 5) - (393, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool caught = false;
#line (394, 5) - (394, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool isTimeout = false;
#line (395, 5) - (395, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (396, 5) - (400, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (397, 9) - (397, 64) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                socket.CreateConnection(("192.0.2.1", 1), timeout: 1.0d);
#line hidden
            }
            catch (global::System.Exception e)
            {
#line (399, 9) - (399, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                caught = (object?)e is global::Sharpy.SocketModule.Error;
#line (400, 9) - (400, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                isTimeout = (object?)e is global::Sharpy.SocketModule.Timeout;
#line hidden
            }

#line (401, 5) - (401, 47) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double elapsed = time.Monotonic() - start;
#line (402, 5) - (402, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(caught);
#line (405, 5) - (405, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed < 30.0d);
#line (406, 5) - (406, 82) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True((isTimeout && elapsed >= 0.5d) || (!isTimeout && elapsed < 1.0d));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestCreateConnectionTimeoutOmittedNoneAndValue()
        {
#line (414, 5) - (414, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (415, 5) - (415, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Setsockopt(global::Sharpy.SocketModule.SocketModuleModule.SOL_SOCKET, global::Sharpy.SocketModule.SocketModuleModule.SO_REUSEADDR, 1);
#line (416, 5) - (416, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (417, 5) - (417, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(8);
#line (418, 5) - (418, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (419, 5) - (419, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            socket.Setdefaulttimeout(5.0d);
#line (420, 5) - (440, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (421, 9) - (421, 80) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                global::Sharpy.SocketModule.Socket omitted = socket.CreateConnection(("127.0.0.1", port));
#line (422, 9) - (422, 94) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                global::Sharpy.SocketModule.Socket nonePositional = socket.CreateConnection(("127.0.0.1", port), null);
#line (423, 9) - (423, 99) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                global::Sharpy.SocketModule.Socket noneKeyword = socket.CreateConnection(("127.0.0.1", port), timeout: null);
#line (424, 9) - (424, 94) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                global::Sharpy.SocketModule.Socket valuePositional = socket.CreateConnection(("127.0.0.1", port), 2.0d);
#line (425, 9) - (425, 99) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                global::Sharpy.SocketModule.Socket valueKeyword = socket.CreateConnection(("127.0.0.1", port), timeout: 2.0d);
#line (427, 9) - (427, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(5.0d, omitted.Gettimeout());
#line (428, 9) - (428, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Null(nonePositional.Gettimeout());
#line (429, 9) - (429, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Null(noneKeyword.Gettimeout());
#line (430, 9) - (430, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(2.0d, valuePositional.Gettimeout());
#line (431, 9) - (431, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(2.0d, valueKeyword.Gettimeout());
#line (433, 9) - (433, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                omitted.Close();
#line (434, 9) - (434, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                nonePositional.Close();
#line (435, 9) - (435, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                noneKeyword.Close();
#line (436, 9) - (436, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                valuePositional.Close();
#line (437, 9) - (437, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                valueKeyword.Close();
#line hidden
            }
            finally
            {
#line (439, 9) - (439, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                socket.Setdefaulttimeout(null);
#line (440, 9) - (440, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Close();
#line hidden
            }
        }

        [Xunit.FactAttribute]
        public void TestCreateConnectionOmittedTimeoutWithoutDefaultIsBlocking()
        {
#line (445, 5) - (445, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (446, 5) - (446, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Setsockopt(global::Sharpy.SocketModule.SocketModuleModule.SOL_SOCKET, global::Sharpy.SocketModule.SocketModuleModule.SO_REUSEADDR, 1);
#line (447, 5) - (447, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (448, 5) - (448, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (449, 5) - (449, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (450, 5) - (450, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            socket.Setdefaulttimeout(null);
#line (451, 5) - (451, 75) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.SocketModule.Socket client = socket.CreateConnection(("127.0.0.1", port));
#line (452, 5) - (452, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Null(client.Gettimeout());
#line (453, 5) - (453, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (454, 5) - (454, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAcceptWithTimeoutRaisesTimeout()
        {
#line (477, 5) - (477, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (478, 5) - (478, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (479, 5) - (479, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (480, 5) - (480, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Settimeout(1.0d);
#line (481, 5) - (481, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (482, 5) - (483, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool __raised_1 = false;
#line hidden
            try
            {
#line (483, 9) - (483, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Accept();
#line hidden
            }
            catch (global::Sharpy.SocketModule.Timeout)
            {
                __raised_1 = true;
            }

            if (!__raised_1)
                throw new global::Sharpy.AssertionError("Expected timeout to be raised, but no exception was raised");
#line (484, 5) - (484, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double elapsed = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start);
#line (485, 5) - (485, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed >= 0.5d);
#line (486, 5) - (486, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed < 30.0d);
#line (487, 5) - (487, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAcceptWithTimeoutReturnsPendingConnection()
        {
#line (491, 5) - (491, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (492, 5) - (492, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (493, 5) - (493, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (494, 5) - (494, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Settimeout(10.0d);
#line (495, 5) - (495, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (496, 5) - (496, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (497, 5) - (497, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (498, 5) - (498, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, addr) = server.Accept();
#line (499, 5) - (499, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("127.0.0.1", addr.Item1);
#line (500, 5) - (500, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (501, 5) - (501, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (502, 5) - (502, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAcceptWithSubMillisecondTimeoutRaisesTimeout()
        {
#line (507, 5) - (507, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (508, 5) - (508, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (509, 5) - (509, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (510, 5) - (510, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Settimeout(0.0001d);
#line (511, 5) - (511, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (512, 5) - (513, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool __raised_2 = false;
#line hidden
            try
            {
#line (513, 9) - (513, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Accept();
#line hidden
            }
            catch (global::Sharpy.SocketModule.Timeout)
            {
                __raised_2 = true;
            }

            if (!__raised_2)
                throw new global::Sharpy.AssertionError("Expected timeout to be raised, but no exception was raised");
#line (514, 5) - (514, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start) < 30.0d);
#line (515, 5) - (515, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAcceptNonBlockingRaisesErrorImmediately()
        {
#line (519, 5) - (519, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (520, 5) - (520, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (521, 5) - (521, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (522, 5) - (522, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Settimeout(0.0d);
#line (523, 5) - (523, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool caught = false;
#line (524, 5) - (524, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool isTimeout = false;
#line (525, 5) - (525, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (526, 5) - (530, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (527, 9) - (527, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Accept();
#line hidden
            }
            catch (global::System.Exception e)
            {
#line (529, 9) - (529, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                caught = (object?)e is global::Sharpy.SocketModule.Error;
#line (530, 9) - (530, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                isTimeout = (object?)e is global::Sharpy.SocketModule.Timeout;
#line hidden
            }

#line (531, 5) - (531, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(caught);
#line (532, 5) - (532, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.False(isTimeout);
#line (533, 5) - (533, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start) < 10.0d);
#line (534, 5) - (534, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestConnectNonBlockingRaisesErrorImmediately()
        {
#line (538, 5) - (538, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (539, 5) - (539, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Settimeout(0.0d);
#line (540, 5) - (540, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool caught = false;
#line (541, 5) - (541, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool isTimeout = false;
#line (542, 5) - (542, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (543, 5) - (547, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (544, 9) - (544, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Connect(("192.0.2.1", 1));
#line hidden
            }
            catch (global::System.Exception e)
            {
#line (546, 9) - (546, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                caught = (object?)e is global::Sharpy.SocketModule.Error;
#line (547, 9) - (547, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                isTimeout = (object?)e is global::Sharpy.SocketModule.Timeout;
#line hidden
            }

#line (548, 5) - (548, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(caught);
#line (549, 5) - (549, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.False(isTimeout);
#line (550, 5) - (550, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start) < 10.0d);
#line (551, 5) - (551, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestConnectWithTimeoutSucceedsToListeningServer()
        {
#line (555, 5) - (555, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (556, 5) - (556, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (557, 5) - (557, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (558, 5) - (558, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (559, 5) - (559, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (560, 5) - (560, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Settimeout(10.0d);
#line (561, 5) - (561, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (562, 5) - (562, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::System.ValueTuple<string, int> peer = client.Getpeername();
#line (563, 5) - (563, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("127.0.0.1", peer.Item1);
#line (564, 5) - (564, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(port, peer.Item2);
#line (565, 5) - (565, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (566, 5) - (566, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Sendall(new Sharpy.Bytes(new byte[] { 112, 105, 110, 103 }));
#line (567, 5) - (567, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Sharpy.Bytes received = conn.Recv(16);
#line (568, 5) - (568, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(new Sharpy.Bytes(new byte[] { 112, 105, 110, 103 }), received);
#line (569, 5) - (569, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (570, 5) - (570, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (571, 5) - (571, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestCreateConnectionWithTimeoutSucceedsToListeningServer()
        {
#line (575, 5) - (575, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (576, 5) - (576, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (577, 5) - (577, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (578, 5) - (578, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (579, 5) - (579, 89) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.SocketModule.Socket client = socket.CreateConnection(("127.0.0.1", port), timeout: 10.0d);
#line (580, 5) - (580, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::System.ValueTuple<string, int> peer = client.Getpeername();
#line (581, 5) - (581, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(port, peer.Item2);
#line (582, 5) - (582, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (583, 5) - (583, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (584, 5) - (584, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (585, 5) - (585, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestConnectWithTimeoutRefusedRaisesErrorNotTimeout()
        {
#line (590, 5) - (590, 62) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var probe = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (591, 5) - (591, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            probe.Bind(("127.0.0.1", 0));
#line (592, 5) - (592, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = probe.Getsockname().Item2;
#line (593, 5) - (593, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            probe.Close();
#line (594, 5) - (594, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (595, 5) - (595, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Settimeout(10.0d);
#line (596, 5) - (596, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool caught = false;
#line (597, 5) - (597, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool isTimeout = false;
#line (598, 5) - (602, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (599, 9) - (599, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Connect(("127.0.0.1", port));
#line hidden
            }
            catch (global::System.Exception e)
            {
#line (601, 9) - (601, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                caught = (object?)e is global::Sharpy.SocketModule.Error;
#line (602, 9) - (602, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                isTimeout = (object?)e is global::Sharpy.SocketModule.Timeout;
#line hidden
            }

#line (603, 5) - (603, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(caught);
#line (604, 5) - (604, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.False(isTimeout);
#line (605, 5) - (605, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestRecvWithTimeoutRaisesTimeout()
        {
#line (609, 5) - (609, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (610, 5) - (610, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (611, 5) - (611, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (612, 5) - (612, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (613, 5) - (613, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (614, 5) - (614, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (615, 5) - (615, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (616, 5) - (616, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Settimeout(1.0d);
#line (617, 5) - (617, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (618, 5) - (619, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool __raised_3 = false;
#line hidden
            try
            {
#line (619, 9) - (619, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                client.Recv(16);
#line hidden
            }
            catch (global::Sharpy.SocketModule.Timeout)
            {
                __raised_3 = true;
            }

            if (!__raised_3)
                throw new global::Sharpy.AssertionError("Expected timeout to be raised, but no exception was raised");
#line (620, 5) - (620, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double elapsed = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start);
#line (621, 5) - (621, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed >= 0.5d);
#line (622, 5) - (622, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed < 30.0d);
#line (623, 5) - (623, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (624, 5) - (624, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (625, 5) - (625, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSendWithTimeoutRaisesTimeoutWhenPeerDoesNotRead()
        {
#line (629, 5) - (629, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (630, 5) - (630, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (631, 5) - (631, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (632, 5) - (632, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (633, 5) - (633, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (634, 5) - (634, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (635, 5) - (635, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (636, 5) - (636, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Settimeout(1.0d);
#line (637, 5) - (637, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Sharpy.Bytes chunk = new Sharpy.Bytes(new byte[] { 120 }) * 65536;
#line (638, 5) - (638, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool isTimeout = false;
#line (639, 5) - (639, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (640, 5) - (644, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (641, 9) - (642, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                while (global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start) < 30.0d)
#line hidden
                {
#line (642, 13) - (642, 31) 20 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    client.Send(chunk);
#line hidden
                }
            }
            catch (global::System.Exception e)
            {
#line (644, 9) - (644, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                isTimeout = (object?)e is global::Sharpy.SocketModule.Timeout;
#line hidden
            }

#line (645, 5) - (645, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(isTimeout);
#line (646, 5) - (646, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start) < 30.0d);
#line (647, 5) - (647, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (648, 5) - (648, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (649, 5) - (649, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSettimeoutNegativeRaisesValueError()
        {
#line (653, 5) - (653, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (654, 5) - (655, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool __raised_4 = false;
#line hidden
            try
            {
#line (655, 9) - (655, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Settimeout(-1.0d);
#line hidden
            }
            catch (ValueError)
            {
                __raised_4 = true;
            }

            if (!__raised_4)
                throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
#line (656, 5) - (656, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Null(s.Gettimeout());
#line (657, 5) - (657, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeoutRuleAcceptsNoneZeroAndPositive()
        {
#line (750, 5) - (750, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(null, "accepted");
#line (751, 5) - (751, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(0.0d, "accepted");
#line (752, 5) - (752, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(-0.0d, "accepted");
#line (753, 5) - (753, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(10.5d, "accepted");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeoutRuleAcceptsVeryLargeWithinInt64Nanoseconds()
        {
#line (757, 5) - (757, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(1e6d, "accepted");
#line (758, 5) - (758, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(9.2e9d, "accepted");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeoutRuleRefusesNegativeWithValueError()
        {
#line (762, 5) - (762, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(-1.0d, "ValueError");
#line (763, 5) - (763, 47) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(-1e-10d, "ValueError");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeoutRuleRefusesNanWithValueError()
        {
#line (767, 5) - (767, 53) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(global::Sharpy.Builtins.Float("nan"), "ValueError");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeoutRuleRefusesInt64NanosecondOverflowWithOverflowError()
        {
#line (771, 5) - (771, 56) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(global::Sharpy.Builtins.Float("inf"), "OverflowError");
#line (772, 5) - (772, 57) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(global::Sharpy.Builtins.Float("-inf"), "OverflowError");
#line (773, 5) - (773, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(9.3e9d, "OverflowError");
#line (774, 5) - (774, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(1e300d, "OverflowError");
#line (775, 5) - (775, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(-1e300d, "OverflowError");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestVeryLargeTimeoutStillGovernsLoopbackConnectAndRecv()
        {
#line (781, 5) - (781, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (782, 5) - (782, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (783, 5) - (783, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (784, 5) - (784, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (785, 5) - (785, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (786, 5) - (786, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Settimeout(9.2e9d);
#line (787, 5) - (787, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (788, 5) - (788, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (789, 5) - (789, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Settimeout(9.2e9d);
#line (790, 5) - (790, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int sent = client.Send(new Sharpy.Bytes(new byte[] { 112, 105, 110, 103 }));
#line (791, 5) - (791, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(4, sent);
#line (792, 5) - (792, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Sharpy.Bytes received = conn.Recv(16);
#line (793, 5) - (793, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(new Sharpy.Bytes(new byte[] { 112, 105, 110, 103 }), received);
#line (794, 5) - (794, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (795, 5) - (795, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (796, 5) - (796, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestStrContainsSocketInfo()
        {
#line (802, 5) - (802, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (803, 5) - (803, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string text = global::Sharpy.Builtins.Str(s);
#line (804, 5) - (804, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(text.Contains("socket"));
#line (805, 5) - (805, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(text.Contains("family="));
#line (806, 5) - (806, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(text.Contains("type="));
#line (807, 5) - (807, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGetpeernameAfterConnectReturnsRemoteAddr()
        {
#line (813, 5) - (813, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (814, 5) - (814, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Setsockopt(global::Sharpy.SocketModule.SocketModuleModule.SOL_SOCKET, global::Sharpy.SocketModule.SocketModuleModule.SO_REUSEADDR, 1);
#line (815, 5) - (815, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (816, 5) - (816, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (817, 5) - (817, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (819, 5) - (819, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (820, 5) - (820, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (822, 5) - (822, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::System.ValueTuple<string, int> peer = client.Getpeername();
#line (823, 5) - (823, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("127.0.0.1", peer.Item1);
#line (824, 5) - (824, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(port, peer.Item2);
#line (826, 5) - (826, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (827, 5) - (827, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (828, 5) - (828, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (829, 5) - (829, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSetblockingFalseSetsNonBlocking()
        {
#line (835, 5) - (835, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (836, 5) - (836, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Setblocking(false);
#line (837, 5) - (837, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.False(s.Getblocking());
#line (838, 5) - (838, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSetblockingTrueSetsBlocking()
        {
#line (842, 5) - (842, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (843, 5) - (843, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Setblocking(false);
#line (844, 5) - (844, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Setblocking(true);
#line (845, 5) - (845, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(s.Getblocking());
#line (846, 5) - (846, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGetnameinfoLocalhostReturnsHostAndService()
        {
#line (852, 5) - (852, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (host, service) = socket.Getnameinfo(("127.0.0.1", 80));
#line (853, 5) - (853, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.NotEqual("", host);
#line (854, 5) - (854, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("80", service);
#line hidden
        }
    }
}
#line default
