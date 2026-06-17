// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using static global::Sharpy.Unittest;
using threading = global::Sharpy.ThreadingModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Threading.ThreadingModuleTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Threading
    {
        [global::Sharpy.SharpyModule("threading.threading_module_tests")]
        public static partial class ThreadingModuleTests
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
#line (40, 5) - (40, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Executed[0] = true;
            }

            public static void SetFired()
            {
#line (44, 5) - (44, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                FiredFlag[0] = true;
            }

            public class RunFlagThread : global::Sharpy.Thread
            {
                public override void Run()
#line 432 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                {
#line (433, 9) - (433, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    RanFlag[0] = true;
                }
            }
        }
    }

    public static partial class Threading
    {
        public partial class ThreadingModuleTestsTests
        {
            [Xunit.FactAttribute]
            public void TestThreadCreateAndJoin()
            {
#line (51, 5) - (51, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Executed[0] = false;
#line (52, 5) - (52, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var t = new global::Sharpy.Thread(SetExecuted);
#line (53, 5) - (53, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                t.Start();
#line (54, 5) - (54, 13) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                t.Join();
#line (55, 5) - (55, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(Executed[0]);
            }

            [Xunit.FactAttribute]
            public void TestThreadJoinWithTimeout()
            {
#line (60, 5) - (63, 5) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                void Sleeper()
#line 60 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                {
#line (61, 9) - (61, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    new global::Sharpy.Lock().Acquire(blocking: true, timeout: 0.05d);
                }

#line (63, 5) - (63, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var t = new global::Sharpy.Thread(Sleeper);
#line (64, 5) - (64, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                t.Start();
#line (65, 5) - (65, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                t.Join(timeout: 5.0d);
#line (66, 5) - (66, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.False(t.IsAlive);
            }

            [Xunit.FactAttribute]
            public void TestThreadName()
            {
#line (71, 5) - (71, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var t = new global::Sharpy.Thread(SetExecuted, name: "worker-1");
#line (72, 5) - (72, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.Equal("worker-1", t.Name);
            }

            [Xunit.FactAttribute]
            public void TestThreadDaemon()
            {
#line (77, 5) - (77, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var t = new global::Sharpy.Thread(SetExecuted, daemon: true);
#line (78, 5) - (78, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(t.Daemon);
            }

            [Xunit.FactAttribute]
            public void TestThreadIdentIsPositive()
            {
#line (83, 5) - (83, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Sharpy.List<int> ident = new Sharpy.List<int>()
                {
                    0
                };
#line (85, 5) - (88, 5) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                void Capture()
#line 85 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                {
#line (86, 9) - (86, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    ident[0] = threading.CurrentThread().Ident;
                }

#line (88, 5) - (88, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var t = new global::Sharpy.Thread(Capture);
#line (89, 5) - (89, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                t.Start();
#line (90, 5) - (90, 13) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                t.Join();
#line (91, 5) - (91, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(ident[0] > 0);
            }

            [Xunit.FactAttribute]
            public void TestThreadVirtualRun()
            {
#line (96, 5) - (96, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                RanFlag[0] = false;
#line (97, 5) - (97, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var t = new RunFlagThread();
#line (98, 5) - (98, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                t.Start();
#line (99, 5) - (99, 13) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                t.Join();
#line (100, 5) - (100, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(RanFlag[0]);
            }

            [Xunit.FactAttribute]
            public void TestLockAcquireAndRelease()
            {
#line (107, 5) - (107, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var lk = new global::Sharpy.Lock();
#line (108, 5) - (108, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(lk.Acquire());
#line (109, 5) - (109, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(lk.Locked());
#line (110, 5) - (110, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                lk.Release();
#line (111, 5) - (111, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.False(lk.Locked());
            }

            [Xunit.FactAttribute]
            public void TestLockContextManager()
            {
#line (116, 5) - (116, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var lk = new global::Sharpy.Lock();
#line (117, 5) - (119, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                {
                    var __ctx_0 = lk;
                    __ctx_0.Enter();
                    try
                    {
#line (118, 9) - (118, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                        Xunit.Assert.True(lk.Locked());
                    }
                    finally
                    {
                        __ctx_0.Exit();
                    }
                }

#line (119, 5) - (119, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.False(lk.Locked());
            }

            [Xunit.FactAttribute]
            public void TestLockNonBlockingReturnsFalse()
            {
#line (124, 5) - (124, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var lk = new global::Sharpy.Lock();
#line (125, 5) - (125, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                lk.Acquire();
#line (126, 5) - (126, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.False(lk.Acquire(blocking: false));
#line (127, 5) - (127, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                lk.Release();
            }

            [Xunit.FactAttribute]
            public void TestLockReleaseUnlockedThrowsRuntimeError()
            {
#line (132, 5) - (132, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var lk = new global::Sharpy.Lock();
#line (133, 5) - (139, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.Throws<RuntimeError>((global::System.Action)(() =>
                {
#line (134, 9) - (134, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    lk.Release();
                }));
            }

            [Xunit.FactAttribute]
            public void TestRlockReentrancy()
            {
#line (141, 5) - (141, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var rl = new global::Sharpy.RLock();
#line (142, 5) - (142, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(rl.Acquire());
#line (143, 5) - (143, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(rl.Acquire());
#line (144, 5) - (144, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                rl.Release();
#line (145, 5) - (145, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                rl.Release();
            }

            [Xunit.FactAttribute]
            public void TestRlockReleaseUnownedThrowsRuntimeError()
            {
#line (150, 5) - (150, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var rl = new global::Sharpy.RLock();
#line (151, 5) - (155, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.Throws<RuntimeError>((global::System.Action)(() =>
                {
#line (152, 9) - (152, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    rl.Release();
                }));
            }

            [Xunit.FactAttribute]
            public void TestRlockContextManager()
            {
#line (157, 5) - (157, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var rl = new global::Sharpy.RLock();
#line (158, 5) - (165, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                {
                    var __ctx_1 = rl;
                    __ctx_1.Enter();
                    try
                    {
#line (159, 9) - (159, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                        Xunit.Assert.True(rl.Acquire());
#line (160, 9) - (160, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                        rl.Release();
                    }
                    finally
                    {
                        __ctx_1.Exit();
                    }
                }
            }

            [Xunit.FactAttribute]
            public void TestEventSetAndWait()
            {
#line (167, 5) - (167, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var evt = new global::Sharpy.Event();
#line (168, 5) - (168, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.False(evt.IsSet());
#line (169, 5) - (169, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                evt.Set();
#line (170, 5) - (170, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(evt.IsSet());
#line (171, 5) - (171, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(evt.Wait());
            }

            [Xunit.FactAttribute]
            public void TestEventClear()
            {
#line (176, 5) - (176, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var evt = new global::Sharpy.Event();
#line (177, 5) - (177, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                evt.Set();
#line (178, 5) - (178, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                evt.Clear();
#line (179, 5) - (179, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.False(evt.IsSet());
            }

            [Xunit.FactAttribute]
            public void TestEventWaitTimeout()
            {
#line (184, 5) - (184, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var evt = new global::Sharpy.Event();
#line (185, 5) - (185, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.False(evt.Wait(timeout: 0.05d));
            }

            [Xunit.FactAttribute]
            public void TestEventCrossThread()
            {
#line (190, 5) - (190, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                ReceivedFlag[0] = false;
#line (191, 5) - (191, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var evt = new global::Sharpy.Event();
#line (193, 5) - (197, 5) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                void Waiter()
#line 193 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                {
#line (194, 9) - (194, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    evt.Wait();
#line (195, 9) - (195, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    ReceivedFlag[0] = true;
                }

#line (197, 5) - (197, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var t = new global::Sharpy.Thread(Waiter);
#line (198, 5) - (198, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                t.Start();
#line (199, 5) - (199, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                evt.Set();
#line (200, 5) - (200, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                t.Join(timeout: 2.0d);
#line (201, 5) - (201, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(ReceivedFlag[0]);
            }

            [Xunit.FactAttribute]
            public void TestSemaphoreCounting()
            {
#line (208, 5) - (208, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var sem = new global::Sharpy.Semaphore(2);
#line (209, 5) - (209, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(sem.Acquire());
#line (210, 5) - (210, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(sem.Acquire());
#line (211, 5) - (211, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.False(sem.Acquire(blocking: false));
#line (212, 5) - (212, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                sem.Release();
#line (213, 5) - (213, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(sem.Acquire(blocking: false));
            }

            [Xunit.FactAttribute]
            public void TestSemaphoreNegativeValueThrowsValueError()
            {
#line (218, 5) - (222, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (219, 9) - (219, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    new global::Sharpy.Semaphore(-1);
                }));
            }

            [Xunit.FactAttribute]
            public void TestSemaphoreContextManager()
            {
#line (224, 5) - (224, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var sem = new global::Sharpy.Semaphore(1);
#line (225, 5) - (227, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                {
                    var __ctx_2 = sem;
                    __ctx_2.Enter();
                    try
                    {
#line (226, 9) - (226, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                        Xunit.Assert.False(sem.Acquire(blocking: false));
                    }
                    finally
                    {
                        __ctx_2.Exit();
                    }
                }

#line (227, 5) - (227, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(sem.Acquire(blocking: false));
#line (228, 5) - (228, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                sem.Release();
            }

            [Xunit.FactAttribute]
            public void TestBoundedSemaphoreOverReleaseThrowsValueError()
            {
#line (235, 5) - (235, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var sem = new global::Sharpy.BoundedSemaphore(1);
#line (236, 5) - (236, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                sem.Acquire();
#line (237, 5) - (237, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                sem.Release();
#line (238, 5) - (242, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (239, 9) - (239, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    sem.Release();
                }));
            }

            [Xunit.FactAttribute]
            public void TestBoundedSemaphoreNormalUse()
            {
#line (244, 5) - (244, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var sem = new global::Sharpy.BoundedSemaphore(2);
#line (245, 5) - (245, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(sem.Acquire());
#line (246, 5) - (246, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(sem.Acquire());
#line (247, 5) - (247, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.False(sem.Acquire(blocking: false));
#line (248, 5) - (248, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                sem.Release();
#line (249, 5) - (249, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                sem.Release();
            }

            [Xunit.FactAttribute]
            public void TestBoundedSemaphoreContextManager()
            {
#line (254, 5) - (254, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var sem = new global::Sharpy.BoundedSemaphore(1);
#line (255, 5) - (257, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                {
                    var __ctx_3 = sem;
                    __ctx_3.Enter();
                    try
                    {
#line (256, 9) - (256, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                        Xunit.Assert.False(sem.Acquire(blocking: false));
                    }
                    finally
                    {
                        __ctx_3.Exit();
                    }
                }

#line (257, 5) - (257, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(sem.Acquire(blocking: false));
#line (258, 5) - (258, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                sem.Release();
            }

            [Xunit.FactAttribute]
            public void TestBarrierSynchronization()
            {
#line (265, 5) - (265, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                BarrierCount[0] = 0;
#line (266, 5) - (266, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var barrier = new global::Sharpy.Barrier(3);
#line (267, 5) - (267, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var @lock = new global::Sharpy.Lock();
#line (269, 5) - (275, 5) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                void Worker()
#line 269 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                {
#line (270, 9) - (270, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    @lock.Acquire();
#line (271, 9) - (271, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    BarrierCount[0] = BarrierCount[0] + 1;
#line (272, 9) - (272, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    @lock.Release();
#line (273, 9) - (273, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    barrier.Wait();
                }

#line (275, 5) - (275, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Sharpy.List<global::Sharpy.Thread> threads = new Sharpy.List<global::Sharpy.Thread>()
                {
                };
#line (276, 5) - (278, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                foreach (var __loopVar_4 in global::Sharpy.Builtins.Range(3))
                {
                    var i = __loopVar_4;
#line (277, 9) - (277, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    threads.Append(new global::Sharpy.Thread(Worker));
                }

#line (278, 5) - (280, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                foreach (var __loopVar_5 in threads)
                {
                    var t = __loopVar_5;
#line (279, 9) - (279, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    t.Start();
                }

#line (280, 5) - (282, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                foreach (var __loopVar_6 in threads)
                {
                    var t = __loopVar_6;
#line (281, 9) - (281, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    t.Join(timeout: 5.0d);
                }

#line (282, 5) - (282, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.Equal(3, BarrierCount[0]);
            }

            [Xunit.FactAttribute]
            public void TestBarrierParties()
            {
#line (287, 5) - (287, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var barrier = new global::Sharpy.Barrier(4);
#line (288, 5) - (288, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.Equal(4, barrier.Parties);
            }

            [Xunit.FactAttribute]
            public void TestBarrierBrokenIsFalseInitially()
            {
#line (293, 5) - (293, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var barrier = new global::Sharpy.Barrier(2);
#line (294, 5) - (294, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.False(barrier.Broken);
            }

            [Xunit.FactAttribute]
            public void TestBarrierAbortSetsBroken()
            {
#line (299, 5) - (299, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var barrier = new global::Sharpy.Barrier(2);
#line (300, 5) - (300, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                barrier.Abort();
#line (301, 5) - (301, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(barrier.Broken);
            }

            [Xunit.FactAttribute]
            public void TestBarrierWaitReturnsPhaseNumber()
            {
#line (306, 5) - (306, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var barrier = new global::Sharpy.Barrier(1);
#line (307, 5) - (307, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var phase = barrier.Wait();
#line (308, 5) - (308, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(phase >= 0);
            }

            [Xunit.FactAttribute]
            public void TestBarrierResetClearsBroken()
            {
#line (313, 5) - (313, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var barrier = new global::Sharpy.Barrier(2);
#line (314, 5) - (314, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                barrier.Abort();
#line (315, 5) - (315, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(barrier.Broken);
#line (316, 5) - (316, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                barrier.Reset();
#line (317, 5) - (317, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.False(barrier.Broken);
            }

            [Xunit.FactAttribute]
            public void TestTimerFiresAfterInterval()
            {
#line (324, 5) - (324, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                FiredFlag[0] = false;
#line (325, 5) - (325, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var timer = new global::Sharpy.Timer(0.05d, SetFired);
#line (326, 5) - (326, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                timer.Start();
#line (327, 5) - (327, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var evt = new global::Sharpy.Event();
#line (328, 5) - (328, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                evt.Wait(timeout: 0.2d);
#line (329, 5) - (329, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                timer.Cancel();
#line (330, 5) - (330, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(FiredFlag[0]);
            }

            [Xunit.FactAttribute]
            public void TestTimerCancelPreventsFiring()
            {
#line (335, 5) - (335, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                FiredFlag[0] = false;
#line (336, 5) - (336, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var timer = new global::Sharpy.Timer(1.0d, SetFired);
#line (337, 5) - (337, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                timer.Start();
#line (338, 5) - (338, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                timer.Cancel();
#line (339, 5) - (339, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var evt = new global::Sharpy.Event();
#line (340, 5) - (340, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                evt.Wait(timeout: 0.1d);
#line (341, 5) - (341, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.False(FiredFlag[0]);
            }

            [Xunit.FactAttribute]
            public void TestTimerStartTwiceThrowsRuntimeError()
            {
#line (346, 5) - (346, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var timer = new global::Sharpy.Timer(1.0d, SetFired);
#line (347, 5) - (347, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                timer.Start();
#line (348, 5) - (350, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.Throws<RuntimeError>((global::System.Action)(() =>
                {
#line (349, 9) - (349, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    timer.Start();
                }));
#line (350, 5) - (350, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                timer.Cancel();
            }

            [Xunit.FactAttribute]
            public void TestCurrentThreadReturnsUsableThread()
            {
#line (357, 5) - (357, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var ct = threading.CurrentThread();
#line (358, 5) - (358, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(ct.Ident > 0);
            }

            [Xunit.FactAttribute]
            public void TestActiveCountReturnsPositive()
            {
#line (363, 5) - (363, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var count = threading.ActiveCount();
#line (364, 5) - (364, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(count > 0);
            }

            [Xunit.FactAttribute]
            public void TestMainThreadReturnsUsableThread()
            {
#line (369, 5) - (369, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var mt = threading.MainThread();
#line (370, 5) - (370, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(mt.Ident > 0);
            }

            [Xunit.FactAttribute]
            public void TestEnumerateReturnsNonEmpty()
            {
#line (375, 5) - (375, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var threads = threading.Enumerate();
#line (376, 5) - (376, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Len(threads) > 0);
            }

            [Xunit.FactAttribute]
            public void TestModuleFactoryLock()
            {
#line (383, 5) - (383, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var lk = new global::Sharpy.Lock();
#line (384, 5) - (384, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(lk.Acquire());
#line (385, 5) - (385, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                lk.Release();
            }

            [Xunit.FactAttribute]
            public void TestModuleFactoryRlock()
            {
#line (390, 5) - (390, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var rl = new global::Sharpy.RLock();
#line (391, 5) - (391, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(rl.Acquire());
#line (392, 5) - (392, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                rl.Release();
            }

            [Xunit.FactAttribute]
            public void TestModuleFactoryEvent()
            {
#line (397, 5) - (397, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var evt = new global::Sharpy.Event();
#line (398, 5) - (398, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                evt.Set();
#line (399, 5) - (399, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(evt.IsSet());
            }

            [Xunit.FactAttribute]
            public void TestModuleFactorySemaphore()
            {
#line (404, 5) - (404, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var sem = new global::Sharpy.Semaphore(3);
#line (405, 5) - (405, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(sem.Acquire());
#line (406, 5) - (406, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                sem.Release();
            }

            [Xunit.FactAttribute]
            public void TestModuleFactoryBoundedSemaphore()
            {
#line (411, 5) - (411, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var sem = new global::Sharpy.BoundedSemaphore(3);
#line (412, 5) - (412, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(sem.Acquire());
#line (413, 5) - (413, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                sem.Release();
            }

            [Xunit.FactAttribute]
            public void TestModuleFactoryBarrier()
            {
#line (418, 5) - (418, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var b = new global::Sharpy.Barrier(2);
#line (419, 5) - (419, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.Equal(2, b.Parties);
            }

            [Xunit.FactAttribute]
            public void TestModuleFactoryTimer()
            {
#line (424, 5) - (424, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var t = new global::Sharpy.Timer(1.0d, SetFired);
#line (425, 5) - (425, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                t.Cancel();
            }
        }
    }
}
