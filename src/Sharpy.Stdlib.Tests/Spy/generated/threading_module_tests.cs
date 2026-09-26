// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

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
#line (40, 5) - (40, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.Executed[0] = true;
#line hidden
        }

        public static void SetFired()
        {
#line (44, 5) - (44, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.FiredFlag[0] = true;
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("threading.threading_module_tests", "RunFlagThread")]
    public class RunFlagThread : global::Sharpy.Thread
    {
        public override void Run()
#line 432 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
        {
#line (433, 9) - (433, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.RanFlag[0] = true;
#line hidden
        }
    }

    public partial class ThreadingModuleTestsModuleTests
    {
        [Xunit.FactAttribute]
        public void TestThreadCreateAndJoin()
        {
#line (51, 5) - (51, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.Executed[0] = false;
#line (52, 5) - (52, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var t = new global::Sharpy.Thread(global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.SetExecuted);
#line (53, 5) - (53, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            t.Start();
#line (54, 5) - (54, 13) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            t.Join();
#line (55, 5) - (55, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.Executed.GetItemUnchecked(0));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestThreadJoinWithTimeout()
        {
#line (60, 5) - (61, 62) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            void Sleeper()
#line 60 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            {
#line (61, 9) - (61, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                new global::Sharpy.Lock().Acquire(blocking: true, timeout: 0.05d);
#line hidden
            }

#line (63, 5) - (63, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var t = new global::Sharpy.Thread(Sleeper);
#line (64, 5) - (64, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            t.Start();
#line (65, 5) - (65, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            t.Join(timeout: 5.0d);
#line (66, 5) - (66, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(t.IsAlive);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestThreadName()
        {
#line (71, 5) - (71, 56) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var t = new global::Sharpy.Thread(global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.SetExecuted, name: "worker-1");
#line (72, 5) - (72, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.Equal("worker-1", t.Name);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestThreadDaemon()
        {
#line (77, 5) - (77, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var t = new global::Sharpy.Thread(global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.SetExecuted, daemon: true);
#line (78, 5) - (78, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(t.Daemon);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestThreadIdentIsPositive()
        {
#line (83, 5) - (83, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Sharpy.List<int> ident = new Sharpy.List<int>()
#line hidden
            {
                0
            };
#line (85, 5) - (86, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            void Capture()
#line 85 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            {
#line (86, 9) - (86, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                ident[0] = threading.CurrentThread().Ident;
#line hidden
            }

#line (88, 5) - (88, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var t = new global::Sharpy.Thread(Capture);
#line (89, 5) - (89, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            t.Start();
#line (90, 5) - (90, 13) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            t.Join();
#line (91, 5) - (91, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(ident.GetItemUnchecked(0) > 0);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestThreadVirtualRun()
        {
#line (96, 5) - (96, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.RanFlag[0] = false;
#line (97, 5) - (97, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var t = new global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.RunFlagThread();
#line (98, 5) - (98, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            t.Start();
#line (99, 5) - (99, 13) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            t.Join();
#line (100, 5) - (100, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.RanFlag.GetItemUnchecked(0));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestLockAcquireAndRelease()
        {
#line (107, 5) - (107, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var lk = new global::Sharpy.Lock();
#line (108, 5) - (108, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(lk.Acquire());
#line (109, 5) - (109, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(lk.Locked());
#line (110, 5) - (110, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            lk.Release();
#line (111, 5) - (111, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(lk.Locked());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestLockContextManager()
        {
#line (116, 5) - (116, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var lk = new global::Sharpy.Lock();
#line (117, 5) - (118, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            {
#line hidden
                var __ctx_0 = lk;
                __ctx_0.Enter();
                try
                {
#line (118, 9) - (118, 28) 20 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    Xunit.Assert.True(lk.Locked());
#line hidden
                }
                finally
                {
                    __ctx_0.Exit();
                }
            }

#line (119, 5) - (119, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(lk.Locked());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestLockNonBlockingReturnsFalse()
        {
#line (124, 5) - (124, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var lk = new global::Sharpy.Lock();
#line (125, 5) - (125, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            lk.Acquire();
#line (126, 5) - (126, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(lk.Acquire(blocking: false));
#line (127, 5) - (127, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            lk.Release();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestLockReleaseUnlockedThrowsRuntimeError()
        {
#line (132, 5) - (132, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var lk = new global::Sharpy.Lock();
#line (133, 5) - (134, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            bool __raised_1 = false;
#line hidden
            try
            {
#line (134, 9) - (134, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
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
#line (141, 5) - (141, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var rl = new global::Sharpy.RLock();
#line (142, 5) - (142, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(rl.Acquire());
#line (143, 5) - (143, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(rl.Acquire());
#line (144, 5) - (144, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            rl.Release();
#line (145, 5) - (145, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            rl.Release();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestRlockReleaseUnownedThrowsRuntimeError()
        {
#line (150, 5) - (150, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var rl = new global::Sharpy.RLock();
#line (151, 5) - (152, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            bool __raised_2 = false;
#line hidden
            try
            {
#line (152, 9) - (152, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
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
#line (157, 5) - (157, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var rl = new global::Sharpy.RLock();
#line (158, 5) - (160, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            {
#line hidden
                var __ctx_3 = rl;
                __ctx_3.Enter();
                try
                {
#line (159, 9) - (159, 29) 20 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    Xunit.Assert.True(rl.Acquire());
#line (160, 9) - (160, 21) 20 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
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
#line (167, 5) - (167, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var evt = new global::Sharpy.Event();
#line (168, 5) - (168, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(evt.IsSet());
#line (169, 5) - (169, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            evt.Set();
#line (170, 5) - (170, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(evt.IsSet());
#line (171, 5) - (171, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(evt.Wait());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestEventClear()
        {
#line (176, 5) - (176, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var evt = new global::Sharpy.Event();
#line (177, 5) - (177, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            evt.Set();
#line (178, 5) - (178, 16) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            evt.Clear();
#line (179, 5) - (179, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(evt.IsSet());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestEventWaitTimeout()
        {
#line (184, 5) - (184, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var evt = new global::Sharpy.Event();
#line (185, 5) - (185, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(evt.Wait(timeout: 0.05d));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestEventCrossThread()
        {
#line (190, 5) - (190, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.ReceivedFlag[0] = false;
#line (191, 5) - (191, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var evt = new global::Sharpy.Event();
#line (193, 5) - (195, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            void Waiter()
#line 193 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            {
#line (194, 9) - (194, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                evt.Wait();
#line (195, 9) - (195, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.ReceivedFlag[0] = true;
#line hidden
            }

#line (197, 5) - (197, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var t = new global::Sharpy.Thread(Waiter);
#line (198, 5) - (198, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            t.Start();
#line (199, 5) - (199, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            evt.Set();
#line (200, 5) - (200, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            t.Join(timeout: 2.0d);
#line (201, 5) - (201, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.ReceivedFlag.GetItemUnchecked(0));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSemaphoreCounting()
        {
#line (208, 5) - (208, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var sem = new global::Sharpy.Semaphore(2);
#line (209, 5) - (209, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(sem.Acquire());
#line (210, 5) - (210, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(sem.Acquire());
#line (211, 5) - (211, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(sem.Acquire(blocking: false));
#line (212, 5) - (212, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            sem.Release();
#line (213, 5) - (213, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(sem.Acquire(blocking: false));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSemaphoreNegativeValueThrowsValueError()
        {
#line (218, 5) - (219, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            bool __raised_4 = false;
#line hidden
            try
            {
#line (219, 9) - (219, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
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
#line (224, 5) - (224, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var sem = new global::Sharpy.Semaphore(1);
#line (225, 5) - (226, 48) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            {
#line hidden
                var __ctx_5 = sem;
                __ctx_5.Enter();
                try
                {
#line (226, 9) - (226, 48) 20 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    Xunit.Assert.False(sem.Acquire(blocking: false));
#line hidden
                }
                finally
                {
                    __ctx_5.Exit();
                }
            }

#line (227, 5) - (227, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(sem.Acquire(blocking: false));
#line (228, 5) - (228, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            sem.Release();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestBoundedSemaphoreOverReleaseThrowsValueError()
        {
#line (235, 5) - (235, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var sem = new global::Sharpy.BoundedSemaphore(1);
#line (236, 5) - (236, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            sem.Acquire();
#line (237, 5) - (237, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            sem.Release();
#line (238, 5) - (239, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            bool __raised_6 = false;
#line hidden
            try
            {
#line (239, 9) - (239, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
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
#line (244, 5) - (244, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var sem = new global::Sharpy.BoundedSemaphore(2);
#line (245, 5) - (245, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(sem.Acquire());
#line (246, 5) - (246, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(sem.Acquire());
#line (247, 5) - (247, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(sem.Acquire(blocking: false));
#line (248, 5) - (248, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            sem.Release();
#line (249, 5) - (249, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            sem.Release();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestBoundedSemaphoreContextManager()
        {
#line (254, 5) - (254, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var sem = new global::Sharpy.BoundedSemaphore(1);
#line (255, 5) - (256, 48) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            {
#line hidden
                var __ctx_7 = sem;
                __ctx_7.Enter();
                try
                {
#line (256, 9) - (256, 48) 20 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    Xunit.Assert.False(sem.Acquire(blocking: false));
#line hidden
                }
                finally
                {
                    __ctx_7.Exit();
                }
            }

#line (257, 5) - (257, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(sem.Acquire(blocking: false));
#line (258, 5) - (258, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            sem.Release();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestBarrierSynchronization()
        {
#line (265, 5) - (265, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.BarrierCount[0] = 0;
#line (266, 5) - (266, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var barrier = new global::Sharpy.Barrier(3);
#line (267, 5) - (267, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var @lock = new global::Sharpy.Lock();
#line (269, 5) - (273, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            void Worker()
#line 269 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            {
#line (270, 9) - (270, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                @lock.Acquire();
#line (271, 9) - (271, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.BarrierCount[0] = global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.BarrierCount.GetItemUnchecked(0) + 1;
#line (272, 9) - (272, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                @lock.Release();
#line (273, 9) - (273, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                barrier.Wait();
#line hidden
            }

#line (275, 5) - (275, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Sharpy.List<global::Sharpy.Thread> threads = new Sharpy.List<global::Sharpy.Thread>()
#line hidden
            {
            };
#line (276, 5) - (277, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            foreach (var __loopVar_8 in global::Sharpy.Builtins.Range(3))
#line hidden
            {
                var i = __loopVar_8;
#line (277, 9) - (277, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                threads.Append(new global::Sharpy.Thread(Worker));
#line hidden
            }

#line (278, 5) - (279, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            foreach (var __loopVar_9 in threads)
#line hidden
            {
                var t = __loopVar_9;
#line (279, 9) - (279, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                t.Start();
#line hidden
            }

#line (280, 5) - (281, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            foreach (var __loopVar_10 in threads)
#line hidden
            {
                var t_1 = __loopVar_10;
#line (281, 9) - (281, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                t_1.Join(timeout: 5.0d);
#line hidden
            }

#line (282, 5) - (282, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.BarrierCount.GetItemUnchecked(0));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestBarrierParties()
        {
#line (287, 5) - (287, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var barrier = new global::Sharpy.Barrier(4);
#line (288, 5) - (288, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.Equal(4, barrier.Parties);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestBarrierBrokenIsFalseInitially()
        {
#line (293, 5) - (293, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var barrier = new global::Sharpy.Barrier(2);
#line (294, 5) - (294, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(barrier.Broken);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestBarrierAbortSetsBroken()
        {
#line (299, 5) - (299, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var barrier = new global::Sharpy.Barrier(2);
#line (300, 5) - (300, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            barrier.Abort();
#line (301, 5) - (301, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(barrier.Broken);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestBarrierWaitReturnsPhaseNumber()
        {
#line (306, 5) - (306, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var barrier = new global::Sharpy.Barrier(1);
#line (307, 5) - (307, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var phase = barrier.Wait();
#line (308, 5) - (308, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(phase >= 0);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestBarrierResetClearsBroken()
        {
#line (313, 5) - (313, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var barrier = new global::Sharpy.Barrier(2);
#line (314, 5) - (314, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            barrier.Abort();
#line (315, 5) - (315, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(barrier.Broken);
#line (316, 5) - (316, 20) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            barrier.Reset();
#line (317, 5) - (317, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(barrier.Broken);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimerFiresAfterInterval()
        {
#line (324, 5) - (324, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.FiredFlag[0] = false;
#line (325, 5) - (325, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var timer = new global::Sharpy.Timer(0.05d, global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.SetFired);
#line (326, 5) - (326, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            timer.Start();
#line (327, 5) - (327, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var evt = new global::Sharpy.Event();
#line (328, 5) - (328, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            evt.Wait(timeout: 0.2d);
#line (329, 5) - (329, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            timer.Cancel();
#line (330, 5) - (330, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.FiredFlag.GetItemUnchecked(0));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimerCancelPreventsFiring()
        {
#line (335, 5) - (335, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.FiredFlag[0] = false;
#line (336, 5) - (336, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var timer = new global::Sharpy.Timer(1.0d, global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.SetFired);
#line (337, 5) - (337, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            timer.Start();
#line (338, 5) - (338, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            timer.Cancel();
#line (339, 5) - (339, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var evt = new global::Sharpy.Event();
#line (340, 5) - (340, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            evt.Wait(timeout: 0.1d);
#line (341, 5) - (341, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.False(global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.FiredFlag.GetItemUnchecked(0));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTimerStartTwiceThrowsRuntimeError()
        {
#line (346, 5) - (346, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var timer = new global::Sharpy.Timer(1.0d, global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.SetFired);
#line (347, 5) - (347, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            timer.Start();
#line (348, 5) - (349, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            bool __raised_11 = false;
#line hidden
            try
            {
#line (349, 9) - (349, 22) 16 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                timer.Start();
#line hidden
            }
            catch (RuntimeError)
            {
                __raised_11 = true;
            }

            if (!__raised_11)
                throw new global::Sharpy.AssertionError("Expected RuntimeError to be raised, but no exception was raised");
#line (350, 5) - (350, 19) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            timer.Cancel();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestCurrentThreadReturnsUsableThread()
        {
#line (357, 5) - (357, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var ct = threading.CurrentThread();
#line (358, 5) - (358, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(ct.Ident > 0);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestActiveCountReturnsPositive()
        {
#line (363, 5) - (363, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var count = threading.ActiveCount();
#line (364, 5) - (364, 22) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(count > 0);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestMainThreadReturnsUsableThread()
        {
#line (369, 5) - (369, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var mt = threading.MainThread();
#line (370, 5) - (370, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(mt.Ident > 0);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestEnumerateReturnsNonEmpty()
        {
#line (375, 5) - (375, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var threads = threading.Enumerate();
#line (376, 5) - (376, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(global::Sharpy.Builtins.Len(threads) > 0);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestModuleFactoryLock()
        {
#line (383, 5) - (383, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var lk = new global::Sharpy.Lock();
#line (384, 5) - (384, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(lk.Acquire());
#line (385, 5) - (385, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            lk.Release();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestModuleFactoryRlock()
        {
#line (390, 5) - (390, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var rl = new global::Sharpy.RLock();
#line (391, 5) - (391, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(rl.Acquire());
#line (392, 5) - (392, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            rl.Release();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestModuleFactoryEvent()
        {
#line (397, 5) - (397, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var evt = new global::Sharpy.Event();
#line (398, 5) - (398, 14) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            evt.Set();
#line (399, 5) - (399, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(evt.IsSet());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestModuleFactorySemaphore()
        {
#line (404, 5) - (404, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var sem = new global::Sharpy.Semaphore(3);
#line (405, 5) - (405, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(sem.Acquire());
#line (406, 5) - (406, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            sem.Release();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestModuleFactoryBoundedSemaphore()
        {
#line (411, 5) - (411, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var sem = new global::Sharpy.BoundedSemaphore(3);
#line (412, 5) - (412, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.True(sem.Acquire());
#line (413, 5) - (413, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            sem.Release();
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestModuleFactoryBarrier()
        {
#line (418, 5) - (418, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var b = new global::Sharpy.Barrier(2);
#line (419, 5) - (419, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            Xunit.Assert.Equal(2, b.Parties);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestModuleFactoryTimer()
        {
#line (424, 5) - (424, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            var t = new global::Sharpy.Timer(1.0d, global::Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests.ThreadingModuleTestsModule.SetFired);
#line (425, 5) - (425, 15) 12 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
            t.Cancel();
#line hidden
        }
    }
}
#line default
