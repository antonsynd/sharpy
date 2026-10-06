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
using threading = global::Sharpy.ThreadingModule;
using Xunit;

namespace Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests
{
    [global::Sharpy.SharpyModule("threading.threading_module_tests")]
    public static partial class ThreadingModuleTestsModule
    {
        public static Sharpy.List<bool> Executed = new Sharpy.List<bool>()
        {
            false
        };
        public static Sharpy.List<bool> RanFlag = new Sharpy.List<bool>()
        {
            false
        };
        public static Sharpy.List<bool> FiredFlag = new Sharpy.List<bool>()
        {
            false
        };
        public static Sharpy.List<bool> ReceivedFlag = new Sharpy.List<bool>()
        {
            false
        };
        public static Sharpy.List<int> BarrierCount = new Sharpy.List<int>()
        {
            0
        };
        public static void SetExecuted()
        {
#line (41, 5) - (41, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.Executed[0] = true;
#line hidden
        }

        public static void SetFired()
        {
#line (45, 5) - (45, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.FiredFlag[0] = true;
#line hidden
        }

        public static void JoinThread(global::Sharpy.Thread t)
        {
#line (329, 5) - (329, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            t.Join(timeout: 5.0d);
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("threading.threading_module_tests", "RunFlagThread")]
    public class RunFlagThread : global::Sharpy.Thread
    {
        public override void Run()
#line 474 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
        {
#line (475, 9) - (475, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.RanFlag[0] = true;
#line hidden
        }
    }

    public partial class ThreadingModuleTestsModuleTests
    {
        [Xunit.FactAttribute]
        public void TestThreadCreateAndJoin()
        {
#line (52, 5) - (52, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.Executed[0] = false;
#line (53, 5) - (53, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var t = new global::Sharpy.Thread(global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.SetExecuted);
#line (54, 5) - (54, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            t.Start();
#line (55, 5) - (55, 13) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            t.Join();
#line (56, 5) - (56, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.Executed.GetItemUnchecked(0));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestThreadJoinWithTimeout()
        {
#line (61, 5) - (62, 62) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            void Sleeper()
#line 61 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            {
#line (62, 9) - (62, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                new global::Sharpy.Lock().Acquire(blocking: true, timeout: 0.05d);
#line hidden
            }

#line (64, 5) - (64, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var t = new global::Sharpy.Thread(Sleeper);
#line (65, 5) - (65, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            t.Start();
#line (66, 5) - (66, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            t.Join(timeout: 5.0d);
#line (67, 5) - (67, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(t.IsAlive);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestThreadName()
        {
#line (72, 5) - (72, 56) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var t = new global::Sharpy.Thread(global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.SetExecuted, name: "worker-1");
#line (73, 5) - (73, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.Equal("worker-1", t.Name);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestThreadDaemon()
        {
#line (78, 5) - (78, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var t = new global::Sharpy.Thread(global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.SetExecuted, daemon: true);
#line (79, 5) - (79, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(t.Daemon);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestThreadIdentIsPositive()
        {
#line (84, 5) - (84, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Sharpy.List<int> ident = new Sharpy.List<int>()
#line hidden
            {
                0
            };
#line (86, 5) - (87, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            void Capture()
#line 86 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            {
#line (87, 9) - (87, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                ident[0] = threading.CurrentThread().Ident;
#line hidden
            }

#line (89, 5) - (89, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var t = new global::Sharpy.Thread(Capture);
#line (90, 5) - (90, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            t.Start();
#line (91, 5) - (91, 13) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            t.Join();
#line (92, 5) - (92, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(ident.GetItemUnchecked(0) > 0);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestThreadVirtualRun()
        {
#line (97, 5) - (97, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.RanFlag[0] = false;
#line (98, 5) - (98, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var t = new global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.RunFlagThread();
#line (99, 5) - (99, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            t.Start();
#line (100, 5) - (100, 13) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            t.Join();
#line (101, 5) - (101, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.RanFlag.GetItemUnchecked(0));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestLockAcquireAndRelease()
        {
#line (108, 5) - (108, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var lk = new global::Sharpy.Lock();
#line (109, 5) - (109, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(lk.Acquire());
#line (110, 5) - (110, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(lk.Locked());
#line (111, 5) - (111, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            lk.Release();
#line (112, 5) - (112, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(lk.Locked());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestLockContextManager()
        {
#line (117, 5) - (117, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var lk = new global::Sharpy.Lock();
#line (118, 5) - (119, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            {
#line hidden
                var __ctx_0 = lk;
                __ctx_0.Enter();
                try
                {
#line (119, 9) - (119, 28) 20 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    Xunit.Assert.True(lk.Locked());
#line hidden
                }
                finally
                {
                    __ctx_0.Exit();
                }
            }

#line (120, 5) - (120, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(lk.Locked());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestLockNonBlockingReturnsFalse()
        {
#line (125, 5) - (125, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var lk = new global::Sharpy.Lock();
#line (126, 5) - (126, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            lk.Acquire();
#line (127, 5) - (127, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(lk.Acquire(blocking: false));
#line (128, 5) - (128, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            lk.Release();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestLockReleaseUnlockedThrowsRuntimeError()
        {
#line (133, 5) - (133, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var lk = new global::Sharpy.Lock();
#line (134, 5) - (135, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            bool __raised_1 = false;
#line hidden
            try
            {
#line (135, 9) - (135, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                lk.Release();
#line hidden
            }
            catch (RuntimeError)
            {
                __raised_1 = true;
            }

            if (!__raised_1)
                throw new global::Sharpy.AssertionError("Expected RuntimeError to be raised, but no exception was raised");
        }

        [Xunit.FactAttribute]
        public void TestRlockReentrancy()
        {
#line (142, 5) - (142, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var rl = new global::Sharpy.RLock();
#line (143, 5) - (143, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(rl.Acquire());
#line (144, 5) - (144, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(rl.Acquire());
#line (145, 5) - (145, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            rl.Release();
#line (146, 5) - (146, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            rl.Release();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestRlockReleaseUnownedThrowsRuntimeError()
        {
#line (151, 5) - (151, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var rl = new global::Sharpy.RLock();
#line (152, 5) - (153, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            bool __raised_2 = false;
#line hidden
            try
            {
#line (153, 9) - (153, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                rl.Release();
#line hidden
            }
            catch (RuntimeError)
            {
                __raised_2 = true;
            }

            if (!__raised_2)
                throw new global::Sharpy.AssertionError("Expected RuntimeError to be raised, but no exception was raised");
        }

        [Xunit.FactAttribute]
        public void TestRlockContextManager()
        {
#line (158, 5) - (158, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var rl = new global::Sharpy.RLock();
#line (159, 5) - (161, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            {
#line hidden
                var __ctx_3 = rl;
                __ctx_3.Enter();
                try
                {
#line (160, 9) - (160, 29) 20 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    Xunit.Assert.True(rl.Acquire());
#line (161, 9) - (161, 21) 20 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    rl.Release();
#line hidden
                }
                finally
                {
                    __ctx_3.Exit();
                }
            }
        }

        [Xunit.FactAttribute]
        public void TestEventSetAndWait()
        {
#line (168, 5) - (168, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var evt = new global::Sharpy.Event();
#line (169, 5) - (169, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(evt.IsSet());
#line (170, 5) - (170, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            evt.Set();
#line (171, 5) - (171, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(evt.IsSet());
#line (172, 5) - (172, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(evt.Wait());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestEventClear()
        {
#line (177, 5) - (177, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var evt = new global::Sharpy.Event();
#line (178, 5) - (178, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            evt.Set();
#line (179, 5) - (179, 16) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            evt.Clear();
#line (180, 5) - (180, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(evt.IsSet());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestEventWaitTimeout()
        {
#line (185, 5) - (185, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var evt = new global::Sharpy.Event();
#line (186, 5) - (186, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(evt.Wait(timeout: 0.05d));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestEventCrossThread()
        {
#line (191, 5) - (191, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.ReceivedFlag[0] = false;
#line (192, 5) - (192, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var evt = new global::Sharpy.Event();
#line (194, 5) - (196, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            void Waiter()
#line 194 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            {
#line (195, 9) - (195, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                evt.Wait();
#line (196, 9) - (196, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.ReceivedFlag[0] = true;
#line hidden
            }

#line (198, 5) - (198, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var t = new global::Sharpy.Thread(Waiter);
#line (199, 5) - (199, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            t.Start();
#line (200, 5) - (200, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            evt.Set();
#line (201, 5) - (201, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            t.Join(timeout: 2.0d);
#line (202, 5) - (202, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.ReceivedFlag.GetItemUnchecked(0));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSemaphoreCounting()
        {
#line (209, 5) - (209, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var sem = new global::Sharpy.Semaphore(2);
#line (210, 5) - (210, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(sem.Acquire());
#line (211, 5) - (211, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(sem.Acquire());
#line (212, 5) - (212, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(sem.Acquire(blocking: false));
#line (213, 5) - (213, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            sem.Release();
#line (214, 5) - (214, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(sem.Acquire(blocking: false));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSemaphoreNegativeValueThrowsValueError()
        {
#line (219, 5) - (220, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            bool __raised_4 = false;
#line hidden
            try
            {
#line (220, 9) - (220, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                new global::Sharpy.Semaphore(-1);
#line hidden
            }
            catch (ValueError)
            {
                __raised_4 = true;
            }

            if (!__raised_4)
                throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
        }

        [Xunit.FactAttribute]
        public void TestSemaphoreContextManager()
        {
#line (225, 5) - (225, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var sem = new global::Sharpy.Semaphore(1);
#line (226, 5) - (227, 48) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            {
#line hidden
                var __ctx_5 = sem;
                __ctx_5.Enter();
                try
                {
#line (227, 9) - (227, 48) 20 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    Xunit.Assert.False(sem.Acquire(blocking: false));
#line hidden
                }
                finally
                {
                    __ctx_5.Exit();
                }
            }

#line (228, 5) - (228, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(sem.Acquire(blocking: false));
#line (229, 5) - (229, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            sem.Release();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestBoundedSemaphoreOverReleaseThrowsValueError()
        {
#line (236, 5) - (236, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var sem = new global::Sharpy.BoundedSemaphore(1);
#line (237, 5) - (237, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            sem.Acquire();
#line (238, 5) - (238, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            sem.Release();
#line (239, 5) - (240, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            bool __raised_6 = false;
#line hidden
            try
            {
#line (240, 9) - (240, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                sem.Release();
#line hidden
            }
            catch (ValueError)
            {
                __raised_6 = true;
            }

            if (!__raised_6)
                throw new global::Sharpy.AssertionError("Expected ValueError to be raised, but no exception was raised");
        }

        [Xunit.FactAttribute]
        public void TestBoundedSemaphoreNormalUse()
        {
#line (245, 5) - (245, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var sem = new global::Sharpy.BoundedSemaphore(2);
#line (246, 5) - (246, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(sem.Acquire());
#line (247, 5) - (247, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(sem.Acquire());
#line (248, 5) - (248, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(sem.Acquire(blocking: false));
#line (249, 5) - (249, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            sem.Release();
#line (250, 5) - (250, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            sem.Release();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestBoundedSemaphoreContextManager()
        {
#line (255, 5) - (255, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var sem = new global::Sharpy.BoundedSemaphore(1);
#line (256, 5) - (257, 48) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            {
#line hidden
                var __ctx_7 = sem;
                __ctx_7.Enter();
                try
                {
#line (257, 9) - (257, 48) 20 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    Xunit.Assert.False(sem.Acquire(blocking: false));
#line hidden
                }
                finally
                {
                    __ctx_7.Exit();
                }
            }

#line (258, 5) - (258, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(sem.Acquire(blocking: false));
#line (259, 5) - (259, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            sem.Release();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestBarrierSynchronization()
        {
#line (266, 5) - (266, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.BarrierCount[0] = 0;
#line (267, 5) - (267, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var barrier = new global::Sharpy.Barrier(3);
#line (268, 5) - (268, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var @lock = new global::Sharpy.Lock();
#line (270, 5) - (274, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            void Worker()
#line 270 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            {
#line (271, 9) - (271, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                @lock.Acquire();
#line (272, 9) - (272, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.BarrierCount[0] = global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.BarrierCount.GetItemUnchecked(0) + 1;
#line (273, 9) - (273, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                @lock.Release();
#line (274, 9) - (274, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                barrier.Wait();
#line hidden
            }

#line (276, 5) - (276, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Sharpy.List<global::Sharpy.Thread> threads = new Sharpy.List<global::Sharpy.Thread>()
#line hidden
            {
            };
#line (277, 5) - (278, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            foreach (var __loopVar_8 in global::Sharpy.Builtins.Range(3))
#line hidden
            {
                var i = __loopVar_8;
#line (278, 9) - (278, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                threads.Append(new global::Sharpy.Thread(Worker));
#line hidden
            }

#line (279, 5) - (280, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            foreach (var __loopVar_9 in threads)
#line hidden
            {
                var t = __loopVar_9;
#line (280, 9) - (280, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                t.Start();
#line hidden
            }

#line (281, 5) - (282, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            foreach (var __loopVar_10 in threads)
#line hidden
            {
                var t_1 = __loopVar_10;
#line (282, 9) - (282, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                t_1.Join(timeout: 5.0d);
#line hidden
            }

#line (283, 5) - (283, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.BarrierCount.GetItemUnchecked(0));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestBarrierParties()
        {
#line (288, 5) - (288, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var barrier = new global::Sharpy.Barrier(4);
#line (289, 5) - (289, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.Equal(4, barrier.Parties);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestBarrierBrokenIsFalseInitially()
        {
#line (294, 5) - (294, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var barrier = new global::Sharpy.Barrier(2);
#line (295, 5) - (295, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(barrier.Broken);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestBarrierAbortSetsBroken()
        {
#line (300, 5) - (300, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var barrier = new global::Sharpy.Barrier(2);
#line (301, 5) - (301, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            barrier.Abort();
#line (302, 5) - (302, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(barrier.Broken);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestBarrierWaitReturnsPhaseNumber()
        {
#line (307, 5) - (307, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var barrier = new global::Sharpy.Barrier(1);
#line (308, 5) - (308, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var phase = barrier.Wait();
#line (309, 5) - (309, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(phase >= 0);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestBarrierResetClearsBroken()
        {
#line (314, 5) - (314, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var barrier = new global::Sharpy.Barrier(2);
#line (315, 5) - (315, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            barrier.Abort();
#line (316, 5) - (316, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(barrier.Broken);
#line (317, 5) - (317, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            barrier.Reset();
#line (318, 5) - (318, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(barrier.Broken);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimerFiresAfterInterval()
        {
#line (334, 5) - (334, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var fired = new global::Sharpy.Event();
#line (336, 5) - (337, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            void OnFire()
#line 336 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            {
#line (337, 9) - (337, 20) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                fired.Set();
#line hidden
            }

#line (339, 5) - (339, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var timer = new global::Sharpy.Timer(0.05d, OnFire);
#line (340, 5) - (340, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            timer.Start();
#line (341, 5) - (341, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(fired.Wait(timeout: 5.0d));
#line (342, 5) - (342, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            timer.Join(timeout: 5.0d);
#line (343, 5) - (343, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(timer.IsAlive);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimerIsAThread()
        {
#line (348, 5) - (348, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var timer = new global::Sharpy.Timer(1.0d, global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.SetFired);
#line (349, 5) - (349, 48) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.IsAssignableFrom<global::Sharpy.Thread>((object?)timer);
#line (350, 5) - (350, 60) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            global::Sharpy.Thread th = new global::Sharpy.Timer(1.0d, global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.SetFired);
#line (351, 5) - (351, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.IsAssignableFrom<global::Sharpy.Timer>((object?)th);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimerJoinWaitsForFunction()
        {
#line (356, 5) - (356, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.FiredFlag[0] = false;
#line (357, 5) - (357, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var timer = new global::Sharpy.Timer(0.05d, global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.SetFired);
#line (358, 5) - (358, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            timer.Start();
#line (359, 5) - (359, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            timer.Join();
#line (360, 5) - (360, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.FiredFlag.GetItemUnchecked(0));
#line (361, 5) - (361, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(timer.IsAlive);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimerCancelPreventsFiring()
        {
#line (366, 5) - (366, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.FiredFlag[0] = false;
#line (367, 5) - (367, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var timer = new global::Sharpy.Timer(30.0d, global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.SetFired);
#line (368, 5) - (368, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            timer.Start();
#line (369, 5) - (369, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            timer.Cancel();
#line (370, 5) - (370, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.JoinThread(timer);
#line (371, 5) - (371, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(timer.IsAlive);
#line (372, 5) - (372, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.FiredFlag.GetItemUnchecked(0));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimerCancelBeforeStartNeverFires()
        {
#line (377, 5) - (377, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.FiredFlag[0] = false;
#line (378, 5) - (378, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var timer = new global::Sharpy.Timer(0.05d, global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.SetFired);
#line (379, 5) - (379, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            timer.Cancel();
#line (380, 5) - (380, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            timer.Start();
#line (381, 5) - (381, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            timer.Join();
#line (382, 5) - (382, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.FiredFlag.GetItemUnchecked(0));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimerStartTwiceThrowsRuntimeError()
        {
#line (387, 5) - (387, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var timer = new global::Sharpy.Timer(1.0d, global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.SetFired);
#line (388, 5) - (388, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            timer.Start();
#line (389, 5) - (390, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            bool __raised_11 = false;
#line hidden
            try
            {
#line (390, 9) - (390, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                timer.Start();
#line hidden
            }
            catch (RuntimeError)
            {
                __raised_11 = true;
            }

            if (!__raised_11)
                throw new global::Sharpy.AssertionError("Expected RuntimeError to be raised, but no exception was raised");
#line (391, 5) - (391, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            timer.Cancel();
#line (392, 5) - (392, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            timer.Join();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestCurrentThreadReturnsUsableThread()
        {
#line (399, 5) - (399, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var ct = threading.CurrentThread();
#line (400, 5) - (400, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(ct.Ident > 0);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestActiveCountReturnsPositive()
        {
#line (405, 5) - (405, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var count = threading.ActiveCount();
#line (406, 5) - (406, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(count > 0);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestMainThreadReturnsUsableThread()
        {
#line (411, 5) - (411, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var mt = threading.MainThread();
#line (412, 5) - (412, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(mt.Ident > 0);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestEnumerateReturnsNonEmpty()
        {
#line (417, 5) - (417, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var threads = threading.Enumerate();
#line (418, 5) - (418, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Builtins.Len(threads) > 0);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestModuleFactoryLock()
        {
#line (425, 5) - (425, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var lk = new global::Sharpy.Lock();
#line (426, 5) - (426, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(lk.Acquire());
#line (427, 5) - (427, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            lk.Release();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestModuleFactoryRlock()
        {
#line (432, 5) - (432, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var rl = new global::Sharpy.RLock();
#line (433, 5) - (433, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(rl.Acquire());
#line (434, 5) - (434, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            rl.Release();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestModuleFactoryEvent()
        {
#line (439, 5) - (439, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var evt = new global::Sharpy.Event();
#line (440, 5) - (440, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            evt.Set();
#line (441, 5) - (441, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(evt.IsSet());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestModuleFactorySemaphore()
        {
#line (446, 5) - (446, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var sem = new global::Sharpy.Semaphore(3);
#line (447, 5) - (447, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(sem.Acquire());
#line (448, 5) - (448, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            sem.Release();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestModuleFactoryBoundedSemaphore()
        {
#line (453, 5) - (453, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var sem = new global::Sharpy.BoundedSemaphore(3);
#line (454, 5) - (454, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(sem.Acquire());
#line (455, 5) - (455, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            sem.Release();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestModuleFactoryBarrier()
        {
#line (460, 5) - (460, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var b = new global::Sharpy.Barrier(2);
#line (461, 5) - (461, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.Equal(2, b.Parties);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestModuleFactoryTimer()
        {
#line (466, 5) - (466, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var t = new global::Sharpy.Timer(1.0d, global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.SetFired);
#line (467, 5) - (467, 15) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            t.Cancel();
#line hidden
        }
    }
}
#line default
