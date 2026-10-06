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
#line (461, 5) - (461, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            return time.Monotonic() - start;
#line hidden
        }

        internal static string _TimeoutOutcome(global::System.Exception e)
        {
#line (660, 5) - (661, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if ((object?)e is global::Sharpy.OverflowError)
#line hidden
            {
#line (661, 9) - (661, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                return "OverflowError";
#line hidden
            }

#line (662, 5) - (663, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if ((object?)e is global::Sharpy.ValueError)
#line hidden
            {
#line (663, 9) - (663, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                return "ValueError";
#line hidden
            }

#line (664, 5) - (665, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if ((object?)e is global::Sharpy.SocketModule.Error)
#line hidden
            {
#line (665, 9) - (665, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                return "socket.error";
#line hidden
            }

#line (666, 5) - (666, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            return "other";
#line hidden
        }

        internal static bool _SameTimeout(double? actual, double? expected)
        {
#line (669, 5) - (670, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (actual is null || expected is null)
#line hidden
            {
#line (670, 9) - (670, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                return actual is null && expected is null;
#line hidden
            }

#line (671, 5) - (671, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double a = actual.Value;
#line (672, 5) - (672, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double b = expected.Value;
#line (673, 5) - (673, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            return a == b;
#line hidden
        }

        internal static int _ClosedLoopbackPort()
        {
#line (676, 5) - (676, 62) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var probe = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (677, 5) - (677, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            probe.Bind(("127.0.0.1", 0));
#line (678, 5) - (678, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int port = probe.Getsockname().Item2;
#line (679, 5) - (679, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            probe.Close();
#line (680, 5) - (680, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            return port;
#line hidden
        }

        internal static void _AssertTimeoutRule(double? value, string expected)
        {
#line (686, 5) - (686, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double? survivor = 2.0d;
#line (687, 5) - (688, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (expected == "accepted")
#line hidden
            {
#line (688, 9) - (688, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                survivor = value;
#line hidden
            }

#line (689, 5) - (689, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string outcome = "accepted";
#line (690, 5) - (690, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            socket.Setdefaulttimeout(2.0d);
#line (691, 5) - (703, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (692, 9) - (695, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                try
#line hidden
                {
#line (693, 13) - (693, 44) 20 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    socket.Setdefaulttimeout(value);
#line hidden
                }
                catch (global::System.Exception e)
                {
#line (695, 13) - (695, 42) 20 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    outcome = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._TimeoutOutcome(e);
#line hidden
                }

#line (696, 9) - (696, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (!(outcome == expected))
#line hidden
                {
                    throw new global::Sharpy.AssertionError();
                }

#line (697, 9) - (697, 68) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (!(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._SameTimeout(socket.Getdefaulttimeout(), survivor)))
#line hidden
                {
                    throw new global::Sharpy.AssertionError();
                }

#line (698, 9) - (698, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var fresh = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (699, 9) - (699, 60) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (!(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._SameTimeout(fresh.Gettimeout(), survivor)))
#line hidden
                {
                    throw new global::Sharpy.AssertionError();
                }

#line (700, 9) - (700, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                fresh.Close();
#line hidden
            }
            finally
            {
#line (703, 9) - (703, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                socket.Setdefaulttimeout(null);
#line hidden
            }

#line (706, 5) - (706, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (707, 5) - (707, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Settimeout(2.0d);
#line (708, 5) - (708, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            outcome = "accepted";
#line (709, 5) - (712, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (710, 9) - (710, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Settimeout(value);
#line hidden
            }
            catch (global::System.Exception e_1)
            {
#line (712, 9) - (712, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                outcome = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._TimeoutOutcome(e_1);
#line hidden
            }

#line (713, 5) - (713, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (!(outcome == expected))
#line hidden
            {
                throw new global::Sharpy.AssertionError();
            }

#line (714, 5) - (714, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (!(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._SameTimeout(s.Gettimeout(), survivor)))
#line hidden
            {
                throw new global::Sharpy.AssertionError();
            }

#line (715, 5) - (715, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line (720, 5) - (720, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int port = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ClosedLoopbackPort();
#line (721, 5) - (721, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            outcome = "accepted";
#line (722, 5) - (727, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (723, 9) - (723, 89) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                global::Sharpy.SocketModule.Socket c = socket.CreateConnection(("127.0.0.1", port), timeout: value);
#line (724, 9) - (724, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                c.Close();
#line hidden
            }
            catch (global::System.Exception e_2)
            {
#line (726, 9) - (726, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                outcome = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._TimeoutOutcome(e_2);
#line (727, 9) - (727, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (!(!((object?)e_2 is global::Sharpy.SocketModule.Timeout)))
#line hidden
                {
                    throw new global::Sharpy.AssertionError();
                }
            }

#line (728, 5) - (731, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (expected == "accepted")
#line hidden
            {
#line (729, 9) - (729, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (!(outcome == "socket.error"))
#line hidden
                {
                    throw new global::Sharpy.AssertionError();
                }
            }
            else
            {
#line (731, 9) - (731, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
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
#line (465, 5) - (465, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (466, 5) - (466, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (467, 5) - (467, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (468, 5) - (468, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Settimeout(1.0d);
#line (469, 5) - (469, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (470, 5) - (471, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool __raised_1 = false;
#line hidden
            try
            {
#line (471, 9) - (471, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Accept();
#line hidden
            }
            catch (global::Sharpy.SocketModule.Timeout)
            {
                __raised_1 = true;
            }

            if (!__raised_1)
                throw new global::Sharpy.AssertionError("Expected timeout to be raised, but no exception was raised");
#line (472, 5) - (472, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double elapsed = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start);
#line (473, 5) - (473, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed >= 0.5d);
#line (474, 5) - (474, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed < 5.0d);
#line (475, 5) - (475, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAcceptWithTimeoutReturnsPendingConnection()
        {
#line (479, 5) - (479, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (480, 5) - (480, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (481, 5) - (481, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (482, 5) - (482, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Settimeout(1.0d);
#line (483, 5) - (483, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (484, 5) - (484, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (485, 5) - (485, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (486, 5) - (486, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, addr) = server.Accept();
#line (487, 5) - (487, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("127.0.0.1", addr.Item1);
#line (488, 5) - (488, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (489, 5) - (489, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (490, 5) - (490, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAcceptWithSubMillisecondTimeoutRaisesTimeout()
        {
#line (495, 5) - (495, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (496, 5) - (496, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (497, 5) - (497, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (498, 5) - (498, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Settimeout(0.0001d);
#line (499, 5) - (499, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (500, 5) - (501, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool __raised_2 = false;
#line hidden
            try
            {
#line (501, 9) - (501, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Accept();
#line hidden
            }
            catch (global::Sharpy.SocketModule.Timeout)
            {
                __raised_2 = true;
            }

            if (!__raised_2)
                throw new global::Sharpy.AssertionError("Expected timeout to be raised, but no exception was raised");
#line (502, 5) - (502, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start) < 5.0d);
#line (503, 5) - (503, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAcceptNonBlockingRaisesErrorImmediately()
        {
#line (507, 5) - (507, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (508, 5) - (508, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (509, 5) - (509, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (510, 5) - (510, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Settimeout(0.0d);
#line (511, 5) - (511, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool caught = false;
#line (512, 5) - (512, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool isTimeout = false;
#line (513, 5) - (513, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (514, 5) - (518, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (515, 9) - (515, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Accept();
#line hidden
            }
            catch (global::System.Exception e)
            {
#line (517, 9) - (517, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                caught = (object?)e is global::Sharpy.SocketModule.Error;
#line (518, 9) - (518, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                isTimeout = (object?)e is global::Sharpy.SocketModule.Timeout;
#line hidden
            }

#line (519, 5) - (519, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(caught);
#line (520, 5) - (520, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.False(isTimeout);
#line (521, 5) - (521, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start) < 0.5d);
#line (522, 5) - (522, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestConnectNonBlockingRaisesErrorImmediately()
        {
#line (526, 5) - (526, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (527, 5) - (527, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Settimeout(0.0d);
#line (528, 5) - (528, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool caught = false;
#line (529, 5) - (529, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool isTimeout = false;
#line (530, 5) - (530, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (531, 5) - (535, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (532, 9) - (532, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Connect(("192.0.2.1", 1));
#line hidden
            }
            catch (global::System.Exception e)
            {
#line (534, 9) - (534, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                caught = (object?)e is global::Sharpy.SocketModule.Error;
#line (535, 9) - (535, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                isTimeout = (object?)e is global::Sharpy.SocketModule.Timeout;
#line hidden
            }

#line (536, 5) - (536, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(caught);
#line (537, 5) - (537, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.False(isTimeout);
#line (538, 5) - (538, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start) < 0.5d);
#line (539, 5) - (539, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestConnectWithTimeoutSucceedsToListeningServer()
        {
#line (543, 5) - (543, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (544, 5) - (544, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (545, 5) - (545, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (546, 5) - (546, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (547, 5) - (547, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (548, 5) - (548, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Settimeout(1.0d);
#line (549, 5) - (549, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (550, 5) - (550, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::System.ValueTuple<string, int> peer = client.Getpeername();
#line (551, 5) - (551, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("127.0.0.1", peer.Item1);
#line (552, 5) - (552, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(port, peer.Item2);
#line (553, 5) - (553, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (554, 5) - (554, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Sendall(new Sharpy.Bytes(new byte[] { 112, 105, 110, 103 }));
#line (555, 5) - (555, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Sharpy.Bytes received = conn.Recv(16);
#line (556, 5) - (556, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(new Sharpy.Bytes(new byte[] { 112, 105, 110, 103 }), received);
#line (557, 5) - (557, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (558, 5) - (558, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (559, 5) - (559, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestCreateConnectionWithTimeoutSucceedsToListeningServer()
        {
#line (563, 5) - (563, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (564, 5) - (564, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (565, 5) - (565, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (566, 5) - (566, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (567, 5) - (567, 88) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.SocketModule.Socket client = socket.CreateConnection(("127.0.0.1", port), timeout: 1.0d);
#line (568, 5) - (568, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::System.ValueTuple<string, int> peer = client.Getpeername();
#line (569, 5) - (569, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(port, peer.Item2);
#line (570, 5) - (570, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (571, 5) - (571, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (572, 5) - (572, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (573, 5) - (573, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestConnectWithTimeoutRefusedRaisesErrorNotTimeout()
        {
#line (578, 5) - (578, 62) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var probe = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (579, 5) - (579, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            probe.Bind(("127.0.0.1", 0));
#line (580, 5) - (580, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = probe.Getsockname().Item2;
#line (581, 5) - (581, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            probe.Close();
#line (582, 5) - (582, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (583, 5) - (583, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Settimeout(1.0d);
#line (584, 5) - (584, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool caught = false;
#line (585, 5) - (585, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool isTimeout = false;
#line (586, 5) - (590, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (587, 9) - (587, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Connect(("127.0.0.1", port));
#line hidden
            }
            catch (global::System.Exception e)
            {
#line (589, 9) - (589, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                caught = (object?)e is global::Sharpy.SocketModule.Error;
#line (590, 9) - (590, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                isTimeout = (object?)e is global::Sharpy.SocketModule.Timeout;
#line hidden
            }

#line (591, 5) - (591, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(caught);
#line (592, 5) - (592, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.False(isTimeout);
#line (593, 5) - (593, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestRecvWithTimeoutRaisesTimeout()
        {
#line (597, 5) - (597, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (598, 5) - (598, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (599, 5) - (599, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (600, 5) - (600, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (601, 5) - (601, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (602, 5) - (602, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (603, 5) - (603, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (604, 5) - (604, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Settimeout(1.0d);
#line (605, 5) - (605, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (606, 5) - (607, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool __raised_3 = false;
#line hidden
            try
            {
#line (607, 9) - (607, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                client.Recv(16);
#line hidden
            }
            catch (global::Sharpy.SocketModule.Timeout)
            {
                __raised_3 = true;
            }

            if (!__raised_3)
                throw new global::Sharpy.AssertionError("Expected timeout to be raised, but no exception was raised");
#line (608, 5) - (608, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double elapsed = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start);
#line (609, 5) - (609, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed >= 0.5d);
#line (610, 5) - (610, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed < 5.0d);
#line (611, 5) - (611, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (612, 5) - (612, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (613, 5) - (613, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSendWithTimeoutRaisesTimeoutWhenPeerDoesNotRead()
        {
#line (617, 5) - (617, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (618, 5) - (618, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (619, 5) - (619, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (620, 5) - (620, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (621, 5) - (621, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (622, 5) - (622, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (623, 5) - (623, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (624, 5) - (624, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Settimeout(1.0d);
#line (625, 5) - (625, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Sharpy.Bytes chunk = new Sharpy.Bytes(new byte[] { 120 }) * 65536;
#line (626, 5) - (626, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool isTimeout = false;
#line (627, 5) - (627, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (628, 5) - (632, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (629, 9) - (630, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                while (global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start) < 10.0d)
#line hidden
                {
#line (630, 13) - (630, 31) 20 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    client.Send(chunk);
#line hidden
                }
            }
            catch (global::System.Exception e)
            {
#line (632, 9) - (632, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                isTimeout = (object?)e is global::Sharpy.SocketModule.Timeout;
#line hidden
            }

#line (633, 5) - (633, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(isTimeout);
#line (634, 5) - (634, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start) < 5.0d);
#line (635, 5) - (635, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (636, 5) - (636, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (637, 5) - (637, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSettimeoutNegativeRaisesValueError()
        {
#line (641, 5) - (641, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (642, 5) - (643, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool __raised_4 = false;
#line hidden
            try
            {
#line (643, 9) - (643, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Settimeout(-1.0d);
#line hidden
            }
            catch (ValueError)
            {
                __raised_4 = true;
            }

            if (!__raised_4)
                throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
#line (644, 5) - (644, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Null(s.Gettimeout());
#line (645, 5) - (645, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeoutRuleAcceptsNoneZeroAndPositive()
        {
#line (735, 5) - (735, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(null, "accepted");
#line (736, 5) - (736, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(0.0d, "accepted");
#line (737, 5) - (737, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(-0.0d, "accepted");
#line (738, 5) - (738, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(1.5d, "accepted");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeoutRuleAcceptsVeryLargeWithinInt64Nanoseconds()
        {
#line (742, 5) - (742, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(1e6d, "accepted");
#line (743, 5) - (743, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(9.2e9d, "accepted");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeoutRuleRefusesNegativeWithValueError()
        {
#line (747, 5) - (747, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(-1.0d, "ValueError");
#line (748, 5) - (748, 47) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(-1e-10d, "ValueError");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeoutRuleRefusesNanWithValueError()
        {
#line (752, 5) - (752, 53) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(global::Sharpy.Builtins.Float("nan"), "ValueError");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeoutRuleRefusesInt64NanosecondOverflowWithOverflowError()
        {
#line (756, 5) - (756, 56) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(global::Sharpy.Builtins.Float("inf"), "OverflowError");
#line (757, 5) - (757, 57) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(global::Sharpy.Builtins.Float("-inf"), "OverflowError");
#line (758, 5) - (758, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(9.3e9d, "OverflowError");
#line (759, 5) - (759, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(1e300d, "OverflowError");
#line (760, 5) - (760, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(-1e300d, "OverflowError");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestVeryLargeTimeoutStillGovernsLoopbackConnectAndRecv()
        {
#line (766, 5) - (766, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (767, 5) - (767, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (768, 5) - (768, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (769, 5) - (769, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (770, 5) - (770, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (771, 5) - (771, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Settimeout(9.2e9d);
#line (772, 5) - (772, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (773, 5) - (773, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (774, 5) - (774, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Settimeout(9.2e9d);
#line (775, 5) - (775, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int sent = client.Send(new Sharpy.Bytes(new byte[] { 112, 105, 110, 103 }));
#line (776, 5) - (776, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(4, sent);
#line (777, 5) - (777, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Sharpy.Bytes received = conn.Recv(16);
#line (778, 5) - (778, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(new Sharpy.Bytes(new byte[] { 112, 105, 110, 103 }), received);
#line (779, 5) - (779, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (780, 5) - (780, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (781, 5) - (781, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestStrContainsSocketInfo()
        {
#line (787, 5) - (787, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (788, 5) - (788, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string text = global::Sharpy.Builtins.Str(s);
#line (789, 5) - (789, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(text.Contains("socket"));
#line (790, 5) - (790, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(text.Contains("family="));
#line (791, 5) - (791, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(text.Contains("type="));
#line (792, 5) - (792, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGetpeernameAfterConnectReturnsRemoteAddr()
        {
#line (798, 5) - (798, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (799, 5) - (799, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Setsockopt(global::Sharpy.SocketModule.SocketModuleModule.SOL_SOCKET, global::Sharpy.SocketModule.SocketModuleModule.SO_REUSEADDR, 1);
#line (800, 5) - (800, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (801, 5) - (801, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (802, 5) - (802, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (804, 5) - (804, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (805, 5) - (805, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (807, 5) - (807, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::System.ValueTuple<string, int> peer = client.Getpeername();
#line (808, 5) - (808, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("127.0.0.1", peer.Item1);
#line (809, 5) - (809, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(port, peer.Item2);
#line (811, 5) - (811, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (812, 5) - (812, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (813, 5) - (813, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (814, 5) - (814, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSetblockingFalseSetsNonBlocking()
        {
#line (820, 5) - (820, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (821, 5) - (821, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Setblocking(false);
#line (822, 5) - (822, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.False(s.Getblocking());
#line (823, 5) - (823, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSetblockingTrueSetsBlocking()
        {
#line (827, 5) - (827, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (828, 5) - (828, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Setblocking(false);
#line (829, 5) - (829, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Setblocking(true);
#line (830, 5) - (830, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(s.Getblocking());
#line (831, 5) - (831, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGetnameinfoLocalhostReturnsHostAndService()
        {
#line (837, 5) - (837, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (host, service) = socket.Getnameinfo(("127.0.0.1", 80));
#line (838, 5) - (838, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.NotEqual("", host);
#line (839, 5) - (839, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("80", service);
#line hidden
        }
    }
}
#line default
