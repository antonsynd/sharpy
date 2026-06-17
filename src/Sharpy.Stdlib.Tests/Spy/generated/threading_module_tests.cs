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
#line 434 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                {
#line (435, 9) - (435, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
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
#line (117, 5) - (117, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                lk.Acquire();
#line (118, 5) - (118, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(lk.Locked());
#line (119, 5) - (119, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                lk.Release();
#line (120, 5) - (120, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.False(lk.Locked());
            }

            [Xunit.FactAttribute]
            public void TestLockNonBlockingReturnsFalse()
            {
#line (125, 5) - (125, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var lk = new global::Sharpy.Lock();
#line (126, 5) - (126, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                lk.Acquire();
#line (127, 5) - (127, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.False(lk.Acquire(blocking: false));
#line (128, 5) - (128, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                lk.Release();
            }

            [Xunit.FactAttribute]
            public void TestLockReleaseUnlockedThrowsRuntimeError()
            {
#line (133, 5) - (133, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var lk = new global::Sharpy.Lock();
#line (134, 5) - (140, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.Throws<RuntimeError>((global::System.Action)(() =>
                {
#line (135, 9) - (135, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    lk.Release();
                }));
            }

            [Xunit.FactAttribute]
            public void TestRlockReentrancy()
            {
#line (142, 5) - (142, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var rl = new global::Sharpy.RLock();
#line (143, 5) - (143, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(rl.Acquire());
#line (144, 5) - (144, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(rl.Acquire());
#line (145, 5) - (145, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                rl.Release();
#line (146, 5) - (146, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                rl.Release();
            }

            [Xunit.FactAttribute]
            public void TestRlockReleaseUnownedThrowsRuntimeError()
            {
#line (151, 5) - (151, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var rl = new global::Sharpy.RLock();
#line (152, 5) - (156, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.Throws<RuntimeError>((global::System.Action)(() =>
                {
#line (153, 9) - (153, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    rl.Release();
                }));
            }

            [Xunit.FactAttribute]
            public void TestRlockContextManager()
            {
#line (158, 5) - (158, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var rl = new global::Sharpy.RLock();
#line (159, 5) - (159, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                rl.Acquire();
#line (160, 5) - (160, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                rl.Release();
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
#line (225, 5) - (225, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                sem.Acquire();
#line (226, 5) - (226, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.False(sem.Acquire(blocking: false));
#line (227, 5) - (227, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                sem.Release();
#line (228, 5) - (228, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(sem.Acquire(blocking: false));
#line (229, 5) - (229, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                sem.Release();
            }

            [Xunit.FactAttribute]
            public void TestBoundedSemaphoreOverReleaseThrowsValueError()
            {
#line (236, 5) - (236, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var sem = new global::Sharpy.BoundedSemaphore(1);
#line (237, 5) - (237, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                sem.Acquire();
#line (238, 5) - (238, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                sem.Release();
#line (239, 5) - (243, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (240, 9) - (240, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    sem.Release();
                }));
            }

            [Xunit.FactAttribute]
            public void TestBoundedSemaphoreNormalUse()
            {
#line (245, 5) - (245, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var sem = new global::Sharpy.BoundedSemaphore(2);
#line (246, 5) - (246, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(sem.Acquire());
#line (247, 5) - (247, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(sem.Acquire());
#line (248, 5) - (248, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.False(sem.Acquire(blocking: false));
#line (249, 5) - (249, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                sem.Release();
#line (250, 5) - (250, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                sem.Release();
            }

            [Xunit.FactAttribute]
            public void TestBoundedSemaphoreContextManager()
            {
#line (255, 5) - (255, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var sem = new global::Sharpy.BoundedSemaphore(1);
#line (256, 5) - (256, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                sem.Acquire();
#line (257, 5) - (257, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.False(sem.Acquire(blocking: false));
#line (258, 5) - (258, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                sem.Release();
#line (259, 5) - (259, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(sem.Acquire(blocking: false));
#line (260, 5) - (260, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                sem.Release();
            }

            [Xunit.FactAttribute]
            public void TestBarrierSynchronization()
            {
#line (267, 5) - (267, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                BarrierCount[0] = 0;
#line (268, 5) - (268, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var barrier = new global::Sharpy.Barrier(3);
#line (269, 5) - (269, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var @lock = new global::Sharpy.Lock();
#line (271, 5) - (277, 5) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                void Worker()
#line 271 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                {
#line (272, 9) - (272, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    @lock.Acquire();
#line (273, 9) - (273, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    BarrierCount[0] = BarrierCount[0] + 1;
#line (274, 9) - (274, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    @lock.Release();
#line (275, 9) - (275, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    barrier.Wait();
                }

#line (277, 5) - (277, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Sharpy.List<global::Sharpy.Thread> threads = new Sharpy.List<global::Sharpy.Thread>()
                {
                };
#line (278, 5) - (280, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                foreach (var __loopVar_0 in global::Sharpy.Builtins.Range(3))
                {
                    var i = __loopVar_0;
#line (279, 9) - (279, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    threads.Append(new global::Sharpy.Thread(Worker));
                }

#line (280, 5) - (282, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                foreach (var __loopVar_1 in threads)
                {
                    var t = __loopVar_1;
#line (281, 9) - (281, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    t.Start();
                }

#line (282, 5) - (284, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                foreach (var __loopVar_2 in threads)
                {
                    var t = __loopVar_2;
#line (283, 9) - (283, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    t.Join(timeout: 5.0d);
                }

#line (284, 5) - (284, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.Equal(3, BarrierCount[0]);
            }

            [Xunit.FactAttribute]
            public void TestBarrierParties()
            {
#line (289, 5) - (289, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var barrier = new global::Sharpy.Barrier(4);
#line (290, 5) - (290, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.Equal(4, barrier.Parties);
            }

            [Xunit.FactAttribute]
            public void TestBarrierBrokenIsFalseInitially()
            {
#line (295, 5) - (295, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var barrier = new global::Sharpy.Barrier(2);
#line (296, 5) - (296, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.False(barrier.Broken);
            }

            [Xunit.FactAttribute]
            public void TestBarrierAbortSetsBroken()
            {
#line (301, 5) - (301, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var barrier = new global::Sharpy.Barrier(2);
#line (302, 5) - (302, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                barrier.Abort();
#line (303, 5) - (303, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(barrier.Broken);
            }

            [Xunit.FactAttribute]
            public void TestBarrierWaitReturnsPhaseNumber()
            {
#line (308, 5) - (308, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var barrier = new global::Sharpy.Barrier(1);
#line (309, 5) - (309, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var phase = barrier.Wait();
#line (310, 5) - (310, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(phase >= 0);
            }

            [Xunit.FactAttribute]
            public void TestBarrierResetClearsBroken()
            {
#line (315, 5) - (315, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var barrier = new global::Sharpy.Barrier(2);
#line (316, 5) - (316, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                barrier.Abort();
#line (317, 5) - (317, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(barrier.Broken);
#line (318, 5) - (318, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                barrier.Reset();
#line (319, 5) - (319, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.False(barrier.Broken);
            }

            [Xunit.FactAttribute]
            public void TestTimerFiresAfterInterval()
            {
#line (326, 5) - (326, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                FiredFlag[0] = false;
#line (327, 5) - (327, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var timer = new global::Sharpy.Timer(0.05d, SetFired);
#line (328, 5) - (328, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                timer.Start();
#line (329, 5) - (329, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var evt = new global::Sharpy.Event();
#line (330, 5) - (330, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                evt.Wait(timeout: 0.2d);
#line (331, 5) - (331, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                timer.Cancel();
#line (332, 5) - (332, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(FiredFlag[0]);
            }

            [Xunit.FactAttribute]
            public void TestTimerCancelPreventsFiring()
            {
#line (337, 5) - (337, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                FiredFlag[0] = false;
#line (338, 5) - (338, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var timer = new global::Sharpy.Timer(1.0d, SetFired);
#line (339, 5) - (339, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                timer.Start();
#line (340, 5) - (340, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                timer.Cancel();
#line (341, 5) - (341, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var evt = new global::Sharpy.Event();
#line (342, 5) - (342, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                evt.Wait(timeout: 0.1d);
#line (343, 5) - (343, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.False(FiredFlag[0]);
            }

            [Xunit.FactAttribute]
            public void TestTimerStartTwiceThrowsRuntimeError()
            {
#line (348, 5) - (348, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var timer = new global::Sharpy.Timer(1.0d, SetFired);
#line (349, 5) - (349, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                timer.Start();
#line (350, 5) - (352, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.Throws<RuntimeError>((global::System.Action)(() =>
                {
#line (351, 9) - (351, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                    timer.Start();
                }));
#line (352, 5) - (352, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                timer.Cancel();
            }

            [Xunit.FactAttribute]
            public void TestCurrentThreadReturnsUsableThread()
            {
#line (359, 5) - (359, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var ct = threading.CurrentThread();
#line (360, 5) - (360, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(ct.Ident > 0);
            }

            [Xunit.FactAttribute]
            public void TestActiveCountReturnsPositive()
            {
#line (365, 5) - (365, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var count = threading.ActiveCount();
#line (366, 5) - (366, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(count > 0);
            }

            [Xunit.FactAttribute]
            public void TestMainThreadReturnsUsableThread()
            {
#line (371, 5) - (371, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var mt = threading.MainThread();
#line (372, 5) - (372, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(mt.Ident > 0);
            }

            [Xunit.FactAttribute]
            public void TestEnumerateReturnsNonEmpty()
            {
#line (377, 5) - (377, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var threads = threading.Enumerate();
#line (378, 5) - (378, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(global::Sharpy.Builtins.Len(threads) > 0);
            }

            [Xunit.FactAttribute]
            public void TestModuleFactoryLock()
            {
#line (385, 5) - (385, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var lk = new global::Sharpy.Lock();
#line (386, 5) - (386, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(lk.Acquire());
#line (387, 5) - (387, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                lk.Release();
            }

            [Xunit.FactAttribute]
            public void TestModuleFactoryRlock()
            {
#line (392, 5) - (392, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var rl = new global::Sharpy.RLock();
#line (393, 5) - (393, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(rl.Acquire());
#line (394, 5) - (394, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                rl.Release();
            }

            [Xunit.FactAttribute]
            public void TestModuleFactoryEvent()
            {
#line (399, 5) - (399, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var evt = new global::Sharpy.Event();
#line (400, 5) - (400, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                evt.Set();
#line (401, 5) - (401, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(evt.IsSet());
            }

            [Xunit.FactAttribute]
            public void TestModuleFactorySemaphore()
            {
#line (406, 5) - (406, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var sem = new global::Sharpy.Semaphore(3);
#line (407, 5) - (407, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(sem.Acquire());
#line (408, 5) - (408, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                sem.Release();
            }

            [Xunit.FactAttribute]
            public void TestModuleFactoryBoundedSemaphore()
            {
#line (413, 5) - (413, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var sem = new global::Sharpy.BoundedSemaphore(3);
#line (414, 5) - (414, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.True(sem.Acquire());
#line (415, 5) - (415, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                sem.Release();
            }

            [Xunit.FactAttribute]
            public void TestModuleFactoryBarrier()
            {
#line (420, 5) - (420, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var b = new global::Sharpy.Barrier(2);
#line (421, 5) - (421, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                Xunit.Assert.Equal(2, b.Parties);
            }

            [Xunit.FactAttribute]
            public void TestModuleFactoryTimer()
            {
#line (426, 5) - (426, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                var t = new global::Sharpy.Timer(1.0d, SetFired);
#line (427, 5) - (427, 15) 1 "src/Sharpy.Stdlib.Tests/Spy/threading/threading_module_tests.spy"
                t.Cancel();
            }
        }
    }
}
