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
#line (499, 5) - (499, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            return time.Monotonic() - start;
#line hidden
        }

        internal static string _TimeoutOutcome(global::System.Exception e)
        {
#line (711, 5) - (712, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if ((object?)e is global::Sharpy.OverflowError)
#line hidden
            {
#line (712, 9) - (712, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                return "OverflowError";
#line hidden
            }

#line (713, 5) - (714, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if ((object?)e is global::Sharpy.ValueError)
#line hidden
            {
#line (714, 9) - (714, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                return "ValueError";
#line hidden
            }

#line (715, 5) - (716, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if ((object?)e is global::Sharpy.SocketModule.Error)
#line hidden
            {
#line (716, 9) - (716, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                return "socket.error";
#line hidden
            }

#line (717, 5) - (717, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            return "other";
#line hidden
        }

        internal static bool _SameTimeout(double? actual, double? expected)
        {
#line (720, 5) - (721, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (actual is null || expected is null)
#line hidden
            {
#line (721, 9) - (721, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                return actual is null && expected is null;
#line hidden
            }

#line (722, 5) - (722, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double a = actual.Value;
#line (723, 5) - (723, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double b = expected.Value;
#line (724, 5) - (724, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            return a == b;
#line hidden
        }

        internal static int _ClosedLoopbackPort()
        {
#line (727, 5) - (727, 62) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var probe = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (728, 5) - (728, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            probe.Bind(("127.0.0.1", 0));
#line (729, 5) - (729, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int port = probe.Getsockname().Item2;
#line (730, 5) - (730, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            probe.Close();
#line (731, 5) - (731, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            return port;
#line hidden
        }

        internal static string _ConnectOutcome(int port, double? timeout, bool viaCreateConnection)
        {
#line (738, 5) - (738, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string outcome = "accepted";
#line (739, 5) - (756, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (viaCreateConnection)
#line hidden
            {
#line (740, 9) - (746, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                try
#line hidden
                {
#line (741, 13) - (741, 95) 20 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    global::Sharpy.SocketModule.Socket c = socket.CreateConnection(("127.0.0.1", port), timeout: timeout);
#line (742, 13) - (742, 22) 20 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    c.Close();
#line hidden
                }
                catch (global::System.Exception e)
                {
#line (744, 13) - (744, 42) 20 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    outcome = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._TimeoutOutcome(e);
#line (745, 13) - (746, 43) 20 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    if ((object?)e is global::Sharpy.SocketModule.Timeout)
#line hidden
                    {
#line (746, 17) - (746, 43) 24 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                        outcome = "socket.timeout";
#line hidden
                    }
                }
            }
            else
            {
#line (748, 9) - (748, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (749, 9) - (749, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Settimeout(timeout);
#line (750, 9) - (755, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                try
#line hidden
                {
#line (751, 13) - (751, 43) 20 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    s.Connect(("127.0.0.1", port));
#line hidden
                }
                catch (global::System.Exception e_1)
                {
#line (753, 13) - (753, 42) 20 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    outcome = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._TimeoutOutcome(e_1);
#line (754, 13) - (755, 43) 20 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    if ((object?)e_1 is global::Sharpy.SocketModule.Timeout)
#line hidden
                    {
#line (755, 17) - (755, 43) 24 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                        outcome = "socket.timeout";
#line hidden
                    }
                }

#line (756, 9) - (756, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Close();
#line hidden
            }

#line (757, 5) - (757, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            return outcome;
#line hidden
        }

        internal static string _ConnectToClosedPort(double? timeout, bool viaCreateConnection, int firstPort)
        {
#line (766, 5) - (766, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int attempt = 0;
#line (767, 5) - (767, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string outcome = "accepted";
#line (768, 5) - (775, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            while (attempt < 5)
#line hidden
            {
#line (769, 9) - (769, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                int port = firstPort;
#line (770, 9) - (771, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (attempt > 0 || firstPort <= 0)
#line hidden
                {
#line (771, 13) - (771, 43) 20 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    port = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ClosedLoopbackPort();
#line hidden
                }

#line (772, 9) - (772, 73) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                outcome = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ConnectOutcome(port, timeout, viaCreateConnection);
#line (773, 9) - (774, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (outcome != "accepted")
#line hidden
                {
#line (774, 13) - (774, 28) 20 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    return outcome;
#line hidden
                }

#line (775, 9) - (775, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                attempt = attempt + 1;
#line hidden
            }

#line (776, 5) - (776, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            return outcome;
#line hidden
        }

        internal static void _AssertTimeoutRule(double? value, string expected)
        {
#line (782, 5) - (782, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double? survivor = 2.0d;
#line (783, 5) - (784, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (expected == "accepted")
#line hidden
            {
#line (784, 9) - (784, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                survivor = value;
#line hidden
            }

#line (785, 5) - (785, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string outcome = "accepted";
#line (786, 5) - (786, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            socket.Setdefaulttimeout(2.0d);
#line (787, 5) - (799, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (788, 9) - (791, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                try
#line hidden
                {
#line (789, 13) - (789, 44) 20 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    socket.Setdefaulttimeout(value);
#line hidden
                }
                catch (global::System.Exception e)
                {
#line (791, 13) - (791, 42) 20 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    outcome = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._TimeoutOutcome(e);
#line hidden
                }

#line (792, 9) - (792, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (!(outcome == expected))
#line hidden
                {
                    throw new global::Sharpy.AssertionError();
                }

#line (793, 9) - (793, 68) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (!(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._SameTimeout(socket.Getdefaulttimeout(), survivor)))
#line hidden
                {
                    throw new global::Sharpy.AssertionError();
                }

#line (794, 9) - (794, 66) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                var fresh = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (795, 9) - (795, 60) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (!(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._SameTimeout(fresh.Gettimeout(), survivor)))
#line hidden
                {
                    throw new global::Sharpy.AssertionError();
                }

#line (796, 9) - (796, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                fresh.Close();
#line hidden
            }
            finally
            {
#line (799, 9) - (799, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                socket.Setdefaulttimeout(null);
#line hidden
            }

#line (802, 5) - (802, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (803, 5) - (803, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Settimeout(2.0d);
#line (804, 5) - (804, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            outcome = "accepted";
#line (805, 5) - (808, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (806, 9) - (806, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Settimeout(value);
#line hidden
            }
            catch (global::System.Exception e_1)
            {
#line (808, 9) - (808, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                outcome = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._TimeoutOutcome(e_1);
#line hidden
            }

#line (809, 5) - (809, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (!(outcome == expected))
#line hidden
            {
                throw new global::Sharpy.AssertionError();
            }

#line (810, 5) - (810, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (!(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._SameTimeout(s.Gettimeout(), survivor)))
#line hidden
            {
                throw new global::Sharpy.AssertionError();
            }

#line (811, 5) - (811, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line (816, 5) - (816, 54) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            outcome = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ConnectToClosedPort(value, true, 0);
#line (817, 5) - (820, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (expected == "accepted")
#line hidden
            {
#line (818, 9) - (818, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                if (!(outcome == "socket.error"))
#line hidden
                {
                    throw new global::Sharpy.AssertionError();
                }
            }
            else
            {
#line (820, 9) - (820, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
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
#line (47, 5) - (47, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.SocketModule.SocketModuleModule.AF_INET);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAfInet6HasCorrectValue()
        {
#line (51, 5) - (51, 84) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.SocketModule.SocketModuleModule.AF_INET6 == 23 || global::Sharpy.SocketModule.SocketModuleModule.AF_INET6 == 10 || global::Sharpy.SocketModule.SocketModuleModule.AF_INET6 == 30);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSockStreamHasCorrectValue()
        {
#line (55, 5) - (55, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(1, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSockDgramHasCorrectValue()
        {
#line (59, 5) - (59, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.SocketModule.SocketModuleModule.SOCK_DGRAM);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSockRawHasCorrectValue()
        {
#line (63, 5) - (63, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.SocketModule.SocketModuleModule.SOCK_RAW);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIpprotoTcpHasCorrectValue()
        {
#line (67, 5) - (67, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(6, global::Sharpy.SocketModule.SocketModuleModule.IPPROTO_TCP);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIpprotoUdpHasCorrectValue()
        {
#line (71, 5) - (71, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(17, global::Sharpy.SocketModule.SocketModuleModule.IPPROTO_UDP);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestShutRdHasCorrectValue()
        {
#line (75, 5) - (75, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(0, global::Sharpy.SocketModule.SocketModuleModule.SHUT_RD);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestShutWrHasCorrectValue()
        {
#line (79, 5) - (79, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(1, global::Sharpy.SocketModule.SocketModuleModule.SHUT_WR);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestShutRdwrHasCorrectValue()
        {
#line (83, 5) - (83, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.SocketModule.SocketModuleModule.SHUT_RDWR);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTcpNodelayHasCorrectValue()
        {
#line (87, 5) - (87, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.NotEqual(0, global::Sharpy.SocketModule.SocketModuleModule.TCP_NODELAY);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSoRcvbufHasCorrectValue()
        {
#line (91, 5) - (91, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.NotEqual(0, global::Sharpy.SocketModule.SocketModuleModule.SO_RCVBUF);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSoSndbufHasCorrectValue()
        {
#line (95, 5) - (95, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.NotEqual(0, global::Sharpy.SocketModule.SocketModuleModule.SO_SNDBUF);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSomaxconnIs128()
        {
#line (99, 5) - (99, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(128, global::Sharpy.SocketModule.SocketModuleModule.SOMAXCONN);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSocketCreatesStreamSocket()
        {
#line (108, 5) - (108, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (109, 5) - (109, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, s.Family);
#line (110, 5) - (110, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSocketCreatesDatagramSocket()
        {
#line (114, 5) - (114, 57) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_DGRAM);
#line (115, 5) - (115, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, s.Family);
#line (116, 5) - (116, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSocketDefaultParamsCreatesIpv4Stream()
        {
#line (120, 5) - (120, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket();
#line (121, 5) - (121, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, s.Family);
#line (122, 5) - (122, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTcpServerBindListenAcceptWorks()
        {
#line (128, 5) - (128, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (129, 5) - (129, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Setsockopt(global::Sharpy.SocketModule.SocketModuleModule.SOL_SOCKET, global::Sharpy.SocketModule.SocketModuleModule.SO_REUSEADDR, 1);
#line (130, 5) - (130, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (131, 5) - (131, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(5);
#line (133, 5) - (133, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::System.ValueTuple<string, int> serverAddr = server.Getsockname();
#line (134, 5) - (134, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("127.0.0.1", serverAddr.Item1);
#line (135, 5) - (135, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(serverAddr.Item2 > 0);
#line (137, 5) - (137, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (138, 5) - (138, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", serverAddr.Item2));
#line (140, 5) - (140, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, addr) = server.Accept();
#line (141, 5) - (141, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("127.0.0.1", addr.Item1);
#line (142, 5) - (142, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(addr.Item2 > 0);
#line (144, 5) - (144, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (145, 5) - (145, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (146, 5) - (146, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTcpSendRecvWorks()
        {
#line (150, 5) - (150, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (151, 5) - (151, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Setsockopt(global::Sharpy.SocketModule.SocketModuleModule.SOL_SOCKET, global::Sharpy.SocketModule.SocketModuleModule.SO_REUSEADDR, 1);
#line (152, 5) - (152, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (153, 5) - (153, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (154, 5) - (154, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (156, 5) - (156, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (157, 5) - (157, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (159, 5) - (159, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (161, 5) - (161, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Sharpy.Bytes message = new Sharpy.Bytes(new byte[] { 104, 101, 108, 108, 111 });
#line (162, 5) - (162, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Sendall(message);
#line (164, 5) - (164, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Sharpy.Bytes received = conn.Recv(1024);
#line (165, 5) - (165, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(new Sharpy.Bytes(new byte[] { 104, 101, 108, 108, 111 }), received);
#line (167, 5) - (167, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (168, 5) - (168, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (169, 5) - (169, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestUdpSendtoRecvfromWorks()
        {
#line (175, 5) - (175, 64) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var receiver = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_DGRAM);
#line (176, 5) - (176, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            receiver.Bind(("127.0.0.1", 0));
#line (177, 5) - (177, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = receiver.Getsockname().Item2;
#line (179, 5) - (179, 62) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var sender = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_DGRAM);
#line (180, 5) - (180, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Sharpy.Bytes message = new Sharpy.Bytes(new byte[] { 117, 100, 112, 32, 116, 101, 115, 116 });
#line (181, 5) - (181, 48) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            sender.Sendto(message, ("127.0.0.1", port));
#line (183, 5) - (183, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (data, addr) = receiver.Recvfrom(1024);
#line (184, 5) - (184, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(new Sharpy.Bytes(new byte[] { 117, 100, 112, 32, 116, 101, 115, 116 }), data);
#line (185, 5) - (185, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("127.0.0.1", addr.Item1);
#line (187, 5) - (187, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            sender.Close();
#line (188, 5) - (188, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            receiver.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGethostbynameLocalhostReturnsLoopback()
        {
#line (194, 5) - (194, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string ip = socket.Gethostbyname("localhost");
#line (195, 5) - (195, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(ip == "127.0.0.1" || ip == "::1");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGethostnameReturnsNonEmpty()
        {
#line (199, 5) - (199, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string hostname = socket.Gethostname();
#line (200, 5) - (200, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.NotEqual("", hostname);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGethostbynameInvalidHostRaisesGaierror()
        {
#line (204, 5) - (205, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool __raised_0 = false;
#line hidden
            try
            {
#line (205, 9) - (205, 65) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
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
#line (211, 5) - (211, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (212, 5) - (212, 60) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Setsockopt(global::Sharpy.SocketModule.SocketModuleModule.SOL_SOCKET, global::Sharpy.SocketModule.SocketModuleModule.SO_REUSEADDR, 1);
#line (213, 5) - (213, 69) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int val = s.Getsockopt(global::Sharpy.SocketModule.SocketModuleModule.SOL_SOCKET, global::Sharpy.SocketModule.SocketModuleModule.SO_REUSEADDR);
#line (214, 5) - (214, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.NotEqual(0, val);
#line (215, 5) - (215, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSettimeoutGettimeoutWorks()
        {
#line (221, 5) - (221, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (222, 5) - (222, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Settimeout(5.0d);
#line (223, 5) - (223, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var t = s.Gettimeout();
#line (224, 5) - (224, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.NotNull(t);
#line (225, 5) - (226, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (t is not null)
#line hidden
            {
#line (226, 9) - (226, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(5.0d, t.Value);
#line hidden
            }

#line (228, 5) - (228, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Settimeout(null);
#line (229, 5) - (229, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Null(s.Gettimeout());
#line (230, 5) - (230, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGetdefaulttimeoutReturnsNullByDefault()
        {
#line (236, 5) - (236, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            socket.Setdefaulttimeout(null);
#line (237, 5) - (237, 47) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Null(socket.Getdefaulttimeout());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSetdefaulttimeoutSetsAndGets()
        {
#line (241, 5) - (241, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            socket.Setdefaulttimeout(10.0d);
#line (242, 5) - (242, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var d = socket.Getdefaulttimeout();
#line (243, 5) - (243, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.NotNull(d);
#line (244, 5) - (245, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (d is not null)
#line hidden
            {
#line (245, 9) - (245, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(10.0d, d.Value);
#line hidden
            }

#line (246, 5) - (246, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            socket.Setdefaulttimeout(null);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSetdefaulttimeoutAppliedToNewSockets()
        {
#line (250, 5) - (250, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            socket.Setdefaulttimeout(3.0d);
#line (251, 5) - (251, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket();
#line (252, 5) - (252, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var t = s.Gettimeout();
#line (253, 5) - (253, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.NotNull(t);
#line (254, 5) - (255, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (t is not null)
#line hidden
            {
#line (255, 9) - (255, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(3.0d, t.Value);
#line hidden
            }

#line (256, 5) - (256, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line (257, 5) - (257, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            socket.Setdefaulttimeout(null);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestHtonsNtohsRoundTrips()
        {
#line (263, 5) - (263, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int original = 8080;
#line (264, 5) - (264, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int network = socket.Htons(original);
#line (265, 5) - (265, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int host = socket.Ntohs(network);
#line (266, 5) - (266, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(original, host);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestHtonlNtohlRoundTrips()
        {
#line (270, 5) - (270, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int original = 305419896;
#line (271, 5) - (271, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int network = socket.Htonl(original);
#line (272, 5) - (272, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int host = socket.Ntohl(network);
#line (273, 5) - (273, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(original, host);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestInetAtonNtoaRoundTrips()
        {
#line (279, 5) - (279, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string ip = "192.168.1.1";
#line (280, 5) - (280, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Sharpy.Bytes packed = socket.Inet_aton(ip);
#line (281, 5) - (281, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string result = socket.Inet_ntoa(packed);
#line (282, 5) - (282, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(ip, result);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestInetPtonNtopIpv4RoundTrips()
        {
#line (286, 5) - (286, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string ip = "10.0.0.1";
#line (287, 5) - (287, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Sharpy.Bytes packed = socket.Inet_pton(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, ip);
#line (288, 5) - (288, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(4, global::Sharpy.Builtins.Len(packed));
#line (289, 5) - (289, 60) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string result = socket.Inet_ntop(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, packed);
#line (290, 5) - (290, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(ip, result);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestInetPtonNtopIpv6RoundTrips()
        {
#line (294, 5) - (294, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string ip = "::1";
#line (295, 5) - (295, 59) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Sharpy.Bytes packed = socket.Inet_pton(global::Sharpy.SocketModule.SocketModuleModule.AF_INET6, ip);
#line (296, 5) - (296, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(16, global::Sharpy.Builtins.Len(packed));
#line (297, 5) - (297, 61) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string result = socket.Inet_ntop(global::Sharpy.SocketModule.SocketModuleModule.AF_INET6, packed);
#line (298, 5) - (298, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(ip, result);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSocketIpv6CanCreate()
        {
#line (304, 5) - (313, 16) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            if (!global::System.Net.Sockets.Socket.OSSupportsIPv6)
#line hidden
            {
#line (307, 9) - (307, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                bool caught = false;
#line (308, 9) - (311, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                try
#line hidden
                {
#line (309, 13) - (309, 63) 20 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET6, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line hidden
                }
                catch (global::System.Exception e)
                {
#line (311, 13) - (311, 49) 20 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    caught = (object?)e is global::Sharpy.SocketModule.Error;
#line hidden
                }

#line (312, 9) - (312, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.True(caught);
#line (313, 9) - (313, 16) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                return;
#line hidden
            }

#line (314, 5) - (314, 59) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET6, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (315, 5) - (315, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(global::Sharpy.SocketModule.SocketModuleModule.AF_INET6, s.Family);
#line (316, 5) - (316, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSocketUnsupportedFamilyRaisesSocketError()
        {
#line (323, 5) - (323, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool caught = false;
#line (324, 5) - (327, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (325, 9) - (325, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                new global::Sharpy.SocketModule.Socket(999, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line hidden
            }
            catch (global::System.Exception e)
            {
#line (327, 9) - (327, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                caught = (object?)e is global::Sharpy.SocketModule.Error;
#line hidden
            }

#line (328, 5) - (328, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(caught);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestConnectInvalidAddressRaisesError()
        {
#line (339, 5) - (339, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (340, 5) - (340, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Settimeout(1.0d);
#line (341, 5) - (341, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool caught = false;
#line (342, 5) - (342, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool isTimeout = false;
#line (343, 5) - (343, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (344, 5) - (348, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (345, 9) - (345, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Connect(("192.0.2.1", 1));
#line hidden
            }
            catch (global::System.Exception e)
            {
#line (347, 9) - (347, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                caught = (object?)e is global::Sharpy.SocketModule.Error;
#line (348, 9) - (348, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                isTimeout = (object?)e is global::Sharpy.SocketModule.Timeout;
#line hidden
            }

#line (349, 5) - (349, 47) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double elapsed = time.Monotonic() - start;
#line (350, 5) - (350, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(caught);
#line (359, 5) - (359, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed < 30.0d);
#line (360, 5) - (360, 82) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True((isTimeout && elapsed >= 0.5d) || (!isTimeout && elapsed < 1.0d));
#line (361, 5) - (361, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestErrorHierarchy()
        {
#line (369, 5) - (369, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool timeoutCaught = false;
#line (370, 5) - (373, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (371, 9) - (371, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                throw new global::Sharpy.SocketModule.Timeout("timed out");
#line hidden
            }
            catch (global::Sharpy.SocketModule.Error)
            {
#line (373, 9) - (373, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                timeoutCaught = true;
#line hidden
            }

#line (374, 5) - (374, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(timeoutCaught);
#line (376, 5) - (376, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool gaierrorCaught = false;
#line (377, 5) - (380, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (378, 9) - (378, 57) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                throw new global::Sharpy.SocketModule.Gaierror("name resolution failed");
#line hidden
            }
            catch (global::Sharpy.SocketModule.Error)
            {
#line (380, 9) - (380, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                gaierrorCaught = true;
#line hidden
            }

#line (381, 5) - (381, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(gaierrorCaught);
#line (383, 5) - (383, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool herrorCaught = false;
#line (384, 5) - (387, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (385, 9) - (385, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                throw new global::Sharpy.SocketModule.Herror("host error");
#line hidden
            }
            catch (global::Sharpy.SocketModule.Error)
            {
#line (387, 9) - (387, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                herrorCaught = true;
#line hidden
            }

#line (388, 5) - (388, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(herrorCaught);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestCreateConnectionConnectsToLocalServer()
        {
#line (394, 5) - (394, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (395, 5) - (395, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Setsockopt(global::Sharpy.SocketModule.SocketModuleModule.SOL_SOCKET, global::Sharpy.SocketModule.SocketModuleModule.SO_REUSEADDR, 1);
#line (396, 5) - (396, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (397, 5) - (397, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (398, 5) - (398, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (400, 5) - (400, 75) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.SocketModule.Socket client = socket.CreateConnection(("127.0.0.1", port));
#line (402, 5) - (402, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::System.ValueTuple<string, int> peer = client.Getpeername();
#line (403, 5) - (403, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("127.0.0.1", peer.Item1);
#line (404, 5) - (404, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(port, peer.Item2);
#line (406, 5) - (406, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (407, 5) - (407, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (408, 5) - (408, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (409, 5) - (409, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestCreateConnectionInvalidAddressRaises()
        {
#line (416, 5) - (416, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool caught = false;
#line (417, 5) - (417, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool isTimeout = false;
#line (418, 5) - (418, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (419, 5) - (423, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (420, 9) - (420, 64) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                socket.CreateConnection(("192.0.2.1", 1), timeout: 1.0d);
#line hidden
            }
            catch (global::System.Exception e)
            {
#line (422, 9) - (422, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                caught = (object?)e is global::Sharpy.SocketModule.Error;
#line (423, 9) - (423, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                isTimeout = (object?)e is global::Sharpy.SocketModule.Timeout;
#line hidden
            }

#line (424, 5) - (424, 47) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double elapsed = time.Monotonic() - start;
#line (425, 5) - (425, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(caught);
#line (428, 5) - (428, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed < 30.0d);
#line (429, 5) - (429, 82) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True((isTimeout && elapsed >= 0.5d) || (!isTimeout && elapsed < 1.0d));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestCreateConnectionTimeoutOmittedNoneAndValue()
        {
#line (440, 5) - (440, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (441, 5) - (441, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Setsockopt(global::Sharpy.SocketModule.SocketModuleModule.SOL_SOCKET, global::Sharpy.SocketModule.SocketModuleModule.SO_REUSEADDR, 1);
#line (442, 5) - (442, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (443, 5) - (443, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(8);
#line (444, 5) - (444, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (445, 5) - (445, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            socket.Setdefaulttimeout(30.0d);
#line (446, 5) - (466, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (447, 9) - (447, 80) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                global::Sharpy.SocketModule.Socket omitted = socket.CreateConnection(("127.0.0.1", port));
#line (448, 9) - (448, 94) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                global::Sharpy.SocketModule.Socket nonePositional = socket.CreateConnection(("127.0.0.1", port), null);
#line (449, 9) - (449, 99) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                global::Sharpy.SocketModule.Socket noneKeyword = socket.CreateConnection(("127.0.0.1", port), timeout: null);
#line (450, 9) - (450, 95) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                global::Sharpy.SocketModule.Socket valuePositional = socket.CreateConnection(("127.0.0.1", port), 20.0d);
#line (451, 9) - (451, 100) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                global::Sharpy.SocketModule.Socket valueKeyword = socket.CreateConnection(("127.0.0.1", port), timeout: 20.0d);
#line (453, 9) - (453, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(30.0d, omitted.Gettimeout());
#line (454, 9) - (454, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Null(nonePositional.Gettimeout());
#line (455, 9) - (455, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Null(noneKeyword.Gettimeout());
#line (456, 9) - (456, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(20.0d, valuePositional.Gettimeout());
#line (457, 9) - (457, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                Xunit.Assert.Equal(20.0d, valueKeyword.Gettimeout());
#line (459, 9) - (459, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                omitted.Close();
#line (460, 9) - (460, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                nonePositional.Close();
#line (461, 9) - (461, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                noneKeyword.Close();
#line (462, 9) - (462, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                valuePositional.Close();
#line (463, 9) - (463, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                valueKeyword.Close();
#line hidden
            }
            finally
            {
#line (465, 9) - (465, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                socket.Setdefaulttimeout(null);
#line (466, 9) - (466, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Close();
#line hidden
            }
        }

        [Xunit.FactAttribute]
        public void TestCreateConnectionOmittedTimeoutWithoutDefaultIsBlocking()
        {
#line (471, 5) - (471, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (472, 5) - (472, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Setsockopt(global::Sharpy.SocketModule.SocketModuleModule.SOL_SOCKET, global::Sharpy.SocketModule.SocketModuleModule.SO_REUSEADDR, 1);
#line (473, 5) - (473, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (474, 5) - (474, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (475, 5) - (475, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (476, 5) - (476, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            socket.Setdefaulttimeout(null);
#line (477, 5) - (477, 75) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.SocketModule.Socket client = socket.CreateConnection(("127.0.0.1", port));
#line (478, 5) - (478, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Null(client.Gettimeout());
#line (479, 5) - (479, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (480, 5) - (480, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAcceptWithTimeoutRaisesTimeout()
        {
#line (503, 5) - (503, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (504, 5) - (504, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (505, 5) - (505, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (506, 5) - (506, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Settimeout(1.0d);
#line (507, 5) - (507, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (508, 5) - (509, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool __raised_1 = false;
#line hidden
            try
            {
#line (509, 9) - (509, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Accept();
#line hidden
            }
            catch (global::Sharpy.SocketModule.Timeout)
            {
                __raised_1 = true;
            }

            if (!__raised_1)
                throw new global::Sharpy.AssertionError("Expected timeout to be raised, but no exception was raised");
#line (510, 5) - (510, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double elapsed = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start);
#line (511, 5) - (511, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed >= 0.5d);
#line (512, 5) - (512, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed < 30.0d);
#line (513, 5) - (513, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAcceptWithTimeoutReturnsPendingConnection()
        {
#line (517, 5) - (517, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (518, 5) - (518, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (519, 5) - (519, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (520, 5) - (520, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Settimeout(10.0d);
#line (521, 5) - (521, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (522, 5) - (522, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (523, 5) - (523, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (524, 5) - (524, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, addr) = server.Accept();
#line (525, 5) - (525, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("127.0.0.1", addr.Item1);
#line (526, 5) - (526, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (527, 5) - (527, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (528, 5) - (528, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAcceptWithSubMillisecondTimeoutRaisesTimeout()
        {
#line (533, 5) - (533, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (534, 5) - (534, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (535, 5) - (535, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (536, 5) - (536, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Settimeout(0.0001d);
#line (537, 5) - (537, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (538, 5) - (539, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool __raised_2 = false;
#line hidden
            try
            {
#line (539, 9) - (539, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Accept();
#line hidden
            }
            catch (global::Sharpy.SocketModule.Timeout)
            {
                __raised_2 = true;
            }

            if (!__raised_2)
                throw new global::Sharpy.AssertionError("Expected timeout to be raised, but no exception was raised");
#line (540, 5) - (540, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start) < 30.0d);
#line (541, 5) - (541, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestAcceptNonBlockingRaisesErrorImmediately()
        {
#line (545, 5) - (545, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (546, 5) - (546, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (547, 5) - (547, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (548, 5) - (548, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Settimeout(0.0d);
#line (549, 5) - (549, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool caught = false;
#line (550, 5) - (550, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool isTimeout = false;
#line (551, 5) - (551, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (552, 5) - (556, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (553, 9) - (553, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                server.Accept();
#line hidden
            }
            catch (global::System.Exception e)
            {
#line (555, 9) - (555, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                caught = (object?)e is global::Sharpy.SocketModule.Error;
#line (556, 9) - (556, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                isTimeout = (object?)e is global::Sharpy.SocketModule.Timeout;
#line hidden
            }

#line (557, 5) - (557, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(caught);
#line (558, 5) - (558, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.False(isTimeout);
#line (559, 5) - (559, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start) < 10.0d);
#line (560, 5) - (560, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestConnectNonBlockingRaisesErrorImmediately()
        {
#line (564, 5) - (564, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (565, 5) - (565, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Settimeout(0.0d);
#line (566, 5) - (566, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool caught = false;
#line (567, 5) - (567, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool isTimeout = false;
#line (568, 5) - (568, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (569, 5) - (573, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (570, 9) - (570, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Connect(("192.0.2.1", 1));
#line hidden
            }
            catch (global::System.Exception e)
            {
#line (572, 9) - (572, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                caught = (object?)e is global::Sharpy.SocketModule.Error;
#line (573, 9) - (573, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                isTimeout = (object?)e is global::Sharpy.SocketModule.Timeout;
#line hidden
            }

#line (574, 5) - (574, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(caught);
#line (575, 5) - (575, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.False(isTimeout);
#line (576, 5) - (576, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start) < 10.0d);
#line (577, 5) - (577, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestConnectWithTimeoutSucceedsToListeningServer()
        {
#line (581, 5) - (581, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (582, 5) - (582, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (583, 5) - (583, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (584, 5) - (584, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (585, 5) - (585, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (586, 5) - (586, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Settimeout(10.0d);
#line (587, 5) - (587, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (588, 5) - (588, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::System.ValueTuple<string, int> peer = client.Getpeername();
#line (589, 5) - (589, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("127.0.0.1", peer.Item1);
#line (590, 5) - (590, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(port, peer.Item2);
#line (591, 5) - (591, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (592, 5) - (592, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Sendall(new Sharpy.Bytes(new byte[] { 112, 105, 110, 103 }));
#line (593, 5) - (593, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Sharpy.Bytes received = conn.Recv(16);
#line (594, 5) - (594, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(new Sharpy.Bytes(new byte[] { 112, 105, 110, 103 }), received);
#line (595, 5) - (595, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (596, 5) - (596, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (597, 5) - (597, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestCreateConnectionWithTimeoutSucceedsToListeningServer()
        {
#line (601, 5) - (601, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (602, 5) - (602, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (603, 5) - (603, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (604, 5) - (604, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (605, 5) - (605, 89) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.SocketModule.Socket client = socket.CreateConnection(("127.0.0.1", port), timeout: 10.0d);
#line (606, 5) - (606, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::System.ValueTuple<string, int> peer = client.Getpeername();
#line (607, 5) - (607, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(port, peer.Item2);
#line (608, 5) - (608, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (609, 5) - (609, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (610, 5) - (610, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (611, 5) - (611, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestConnectWithTimeoutRefusedRaisesErrorNotTimeout()
        {
#line (616, 5) - (616, 70) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("socket.error", global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ConnectToClosedPort(10.0d, false, 0));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestConnectHelperReportsAcceptedForAListeningPort()
        {
#line (623, 5) - (623, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (624, 5) - (624, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (625, 5) - (625, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(8);
#line (626, 5) - (626, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int port = server.Getsockname().Item2;
#line (627, 5) - (627, 62) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("accepted", global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ConnectOutcome(port, 10.0d, false));
#line (628, 5) - (628, 61) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("accepted", global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ConnectOutcome(port, 10.0d, true));
#line (629, 5) - (629, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestConnectToClosedPortRetriesAnAcceptedFirstPick()
        {
#line (635, 5) - (635, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (636, 5) - (636, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (637, 5) - (637, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(8);
#line (638, 5) - (638, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int port = server.Getsockname().Item2;
#line (639, 5) - (639, 73) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("socket.error", global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ConnectToClosedPort(10.0d, false, port));
#line (640, 5) - (640, 72) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("socket.error", global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ConnectToClosedPort(10.0d, true, port));
#line (641, 5) - (641, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestRecvWithTimeoutRaisesTimeout()
        {
#line (645, 5) - (645, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (646, 5) - (646, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (647, 5) - (647, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (648, 5) - (648, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (649, 5) - (649, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (650, 5) - (650, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (651, 5) - (651, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (652, 5) - (652, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Settimeout(1.0d);
#line (653, 5) - (653, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (654, 5) - (655, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool __raised_3 = false;
#line hidden
            try
            {
#line (655, 9) - (655, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                client.Recv(16);
#line hidden
            }
            catch (global::Sharpy.SocketModule.Timeout)
            {
                __raised_3 = true;
            }

            if (!__raised_3)
                throw new global::Sharpy.AssertionError("Expected timeout to be raised, but no exception was raised");
#line (656, 5) - (656, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double elapsed = global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start);
#line (657, 5) - (657, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed >= 0.5d);
#line (658, 5) - (658, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(elapsed < 30.0d);
#line (659, 5) - (659, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (660, 5) - (660, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (661, 5) - (661, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSendWithTimeoutRaisesTimeoutWhenPeerDoesNotRead()
        {
#line (665, 5) - (665, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (666, 5) - (666, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (667, 5) - (667, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (668, 5) - (668, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (669, 5) - (669, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (670, 5) - (670, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (671, 5) - (671, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (672, 5) - (672, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Settimeout(1.0d);
#line (673, 5) - (673, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Sharpy.Bytes chunk = new Sharpy.Bytes(new byte[] { 120 }) * 65536;
#line (674, 5) - (674, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool isTimeout = false;
#line (675, 5) - (675, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            double start = time.Monotonic();
#line (676, 5) - (680, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            try
#line hidden
            {
#line (677, 9) - (678, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                while (global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start) < 30.0d)
#line hidden
                {
#line (678, 13) - (678, 31) 20 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                    client.Send(chunk);
#line hidden
                }
            }
            catch (global::System.Exception e)
            {
#line (680, 9) - (680, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                isTimeout = (object?)e is global::Sharpy.SocketModule.Timeout;
#line hidden
            }

#line (681, 5) - (681, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(isTimeout);
#line (682, 5) - (682, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._ElapsedSince(start) < 30.0d);
#line (683, 5) - (683, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (684, 5) - (684, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (685, 5) - (685, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSettimeoutNegativeRaisesValueError()
        {
#line (689, 5) - (689, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (690, 5) - (691, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            bool __raised_4 = false;
#line hidden
            try
            {
#line (691, 9) - (691, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
                s.Settimeout(-1.0d);
#line hidden
            }
            catch (ValueError)
            {
                __raised_4 = true;
            }

            if (!__raised_4)
                throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
#line (692, 5) - (692, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Null(s.Gettimeout());
#line (693, 5) - (693, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeoutRuleAcceptsNoneZeroAndPositive()
        {
#line (824, 5) - (824, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(null, "accepted");
#line (825, 5) - (825, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(0.0d, "accepted");
#line (826, 5) - (826, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(-0.0d, "accepted");
#line (827, 5) - (827, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(10.5d, "accepted");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeoutRuleAcceptsVeryLargeWithinInt64Nanoseconds()
        {
#line (831, 5) - (831, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(1e6d, "accepted");
#line (832, 5) - (832, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(9.2e9d, "accepted");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeoutRuleRefusesNegativeWithValueError()
        {
#line (836, 5) - (836, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(-1.0d, "ValueError");
#line (837, 5) - (837, 47) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(-1e-10d, "ValueError");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeoutRuleRefusesNanWithValueError()
        {
#line (841, 5) - (841, 53) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(global::Sharpy.Builtins.Float("nan"), "ValueError");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimeoutRuleRefusesInt64NanosecondOverflowWithOverflowError()
        {
#line (845, 5) - (845, 56) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(global::Sharpy.Builtins.Float("inf"), "OverflowError");
#line (846, 5) - (846, 57) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(global::Sharpy.Builtins.Float("-inf"), "OverflowError");
#line (847, 5) - (847, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(9.3e9d, "OverflowError");
#line (848, 5) - (848, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(1e300d, "OverflowError");
#line (849, 5) - (849, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Socket.SocketModuleTests.SocketModuleTestsModule._AssertTimeoutRule(-1e300d, "OverflowError");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestVeryLargeTimeoutStillGovernsLoopbackConnectAndRecv()
        {
#line (855, 5) - (855, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (856, 5) - (856, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (857, 5) - (857, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (858, 5) - (858, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (859, 5) - (859, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (860, 5) - (860, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Settimeout(9.2e9d);
#line (861, 5) - (861, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (862, 5) - (862, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (863, 5) - (863, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Settimeout(9.2e9d);
#line (864, 5) - (864, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            int sent = client.Send(new Sharpy.Bytes(new byte[] { 112, 105, 110, 103 }));
#line (865, 5) - (865, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(4, sent);
#line (866, 5) - (866, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Sharpy.Bytes received = conn.Recv(16);
#line (867, 5) - (867, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(new Sharpy.Bytes(new byte[] { 112, 105, 110, 103 }), received);
#line (868, 5) - (868, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (869, 5) - (869, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (870, 5) - (870, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestStrContainsSocketInfo()
        {
#line (876, 5) - (876, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (877, 5) - (877, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            string text = global::Sharpy.Builtins.Str(s);
#line (878, 5) - (878, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(text.Contains("socket"));
#line (879, 5) - (879, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(text.Contains("family="));
#line (880, 5) - (880, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(text.Contains("type="));
#line (881, 5) - (881, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGetpeernameAfterConnectReturnsRemoteAddr()
        {
#line (887, 5) - (887, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var server = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (888, 5) - (888, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Setsockopt(global::Sharpy.SocketModule.SocketModuleModule.SOL_SOCKET, global::Sharpy.SocketModule.SocketModuleModule.SO_REUSEADDR, 1);
#line (889, 5) - (889, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Bind(("127.0.0.1", 0));
#line (890, 5) - (890, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Listen(1);
#line (891, 5) - (891, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var port = server.Getsockname().Item2;
#line (893, 5) - (893, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var client = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (894, 5) - (894, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Connect(("127.0.0.1", port));
#line (896, 5) - (896, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            global::System.ValueTuple<string, int> peer = client.Getpeername();
#line (897, 5) - (897, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("127.0.0.1", peer.Item1);
#line (898, 5) - (898, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal(port, peer.Item2);
#line (900, 5) - (900, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (conn, __spy_underscore) = server.Accept();
#line (901, 5) - (901, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            conn.Close();
#line (902, 5) - (902, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            client.Close();
#line (903, 5) - (903, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            server.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSetblockingFalseSetsNonBlocking()
        {
#line (909, 5) - (909, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (910, 5) - (910, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Setblocking(false);
#line (911, 5) - (911, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.False(s.Getblocking());
#line (912, 5) - (912, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSetblockingTrueSetsBlocking()
        {
#line (916, 5) - (916, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var s = new global::Sharpy.SocketModule.Socket(global::Sharpy.SocketModule.SocketModuleModule.AF_INET, global::Sharpy.SocketModule.SocketModuleModule.SOCK_STREAM);
#line (917, 5) - (917, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Setblocking(false);
#line (918, 5) - (918, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Setblocking(true);
#line (919, 5) - (919, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.True(s.Getblocking());
#line (920, 5) - (920, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            s.Close();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestGetnameinfoLocalhostReturnsHostAndService()
        {
#line (926, 5) - (926, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            var (host, service) = socket.Getnameinfo(("127.0.0.1", 80));
#line (927, 5) - (927, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.NotEqual("", host);
#line (928, 5) - (928, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/socket/socket_module_tests.spy"
            Xunit.Assert.Equal("80", service);
#line hidden
        }
    }
}
#line default
