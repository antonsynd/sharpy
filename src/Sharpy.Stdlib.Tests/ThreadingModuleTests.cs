using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using SysThread = System.Threading.Thread;

namespace Sharpy.Tests;

public class ThreadingModuleTests
{
    [Fact]
    public void Thread_CreateAndJoin()
    {
        bool executed = false;
        var t = new Thread(() => { executed = true; });
        t.Start();
        t.Join();
        executed.Should().BeTrue();
    }

    [Fact]
    public void Thread_JoinWithTimeout()
    {
        var t = new Thread(() => SysThread.Sleep(50));
        t.Start();
        t.Join(timeout: 5.0);
        t.IsAlive.Should().BeFalse();
    }

    [Fact]
    public void Thread_Name()
    {
        var t = new Thread(() => { }, name: "worker-1");
        t.Name.Should().Be("worker-1");
    }

    [Fact]
    public void Thread_Daemon()
    {
        var t = new Thread(() => { }, daemon: true);
        t.Daemon.Should().BeTrue();
    }

    [Fact]
    public void Thread_Ident_IsPositive()
    {
        int ident = 0;
        var t = new Thread(() => { ident = SysThread.CurrentThread.ManagedThreadId; });
        t.Start();
        t.Join();
        ident.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Thread_VirtualRun()
    {
        bool ran = false;
        var t = new TestThread(() => ran = true);
        t.Start();
        t.Join();
        ran.Should().BeTrue();
    }

    [Fact]
    public void Lock_AcquireAndRelease()
    {
        var lk = new Lock();
        lk.Acquire().Should().BeTrue();
        lk.Locked().Should().BeTrue();
        lk.Release();
        lk.Locked().Should().BeFalse();
    }

    [Fact]
    public void Lock_ContextManager()
    {
        var lk = new Lock();
        var entered = lk.__enter__();
        entered.Should().BeSameAs(lk);
        lk.Locked().Should().BeTrue();
        lk.__exit__();
        lk.Locked().Should().BeFalse();
    }

    [Fact]
    public void Lock_NonBlocking_ReturnsFalse()
    {
        var lk = new Lock();
        lk.Acquire();
        lk.Acquire(blocking: false).Should().BeFalse();
        lk.Release();
    }

    [Fact]
    public void Lock_ReleaseUnlocked_ThrowsRuntimeError()
    {
        var lk = new Lock();
        var act = () => lk.Release();
        act.Should().Throw<RuntimeError>();
    }

    [Fact]
    public void RLock_Reentrancy()
    {
        var rl = new RLock();
        rl.Acquire().Should().BeTrue();
        rl.Acquire().Should().BeTrue();
        rl.Release();
        rl.Release();
    }

    [Fact]
    public void RLock_ReleaseUnowned_ThrowsRuntimeError()
    {
        var rl = new RLock();
        var act = () => rl.Release();
        act.Should().Throw<RuntimeError>();
    }

    [Fact]
    public void RLock_ContextManager()
    {
        var rl = new RLock();
        var entered = rl.__enter__();
        entered.Should().BeSameAs(rl);
        rl.__exit__();
    }

    [Fact]
    public void Event_SetAndWait()
    {
        var evt = new Event();
        evt.IsSet().Should().BeFalse();
        evt.Set();
        evt.IsSet().Should().BeTrue();
        evt.Wait().Should().BeTrue();
    }

    [Fact]
    public void Event_Clear()
    {
        var evt = new Event();
        evt.Set();
        evt.Clear();
        evt.IsSet().Should().BeFalse();
    }

    [Fact]
    public void Event_WaitTimeout()
    {
        var evt = new Event();
        evt.Wait(timeout: 0.05).Should().BeFalse();
    }

    [Fact]
    public void Event_CrossThread()
    {
        var evt = new Event();
        bool received = false;
        var t = new Thread(() =>
        {
            evt.Wait();
            received = true;
        });
        t.Start();
        SysThread.Sleep(50);
        evt.Set();
        t.Join(timeout: 2.0);
        received.Should().BeTrue();
    }

    [Fact]
    public void Semaphore_Counting()
    {
        var sem = new Semaphore(2);
        sem.Acquire().Should().BeTrue();
        sem.Acquire().Should().BeTrue();
        sem.Acquire(blocking: false).Should().BeFalse();
        sem.Release();
        sem.Acquire(blocking: false).Should().BeTrue();
    }

    [Fact]
    public void Semaphore_NegativeValue_ThrowsValueError()
    {
        var act = () => new Semaphore(-1);
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Semaphore_ContextManager()
    {
        var sem = new Semaphore(1);
        var entered = sem.__enter__();
        entered.Should().BeSameAs(sem);
        sem.Acquire(blocking: false).Should().BeFalse();
        sem.__exit__();
        sem.Acquire(blocking: false).Should().BeTrue();
        sem.Release();
    }

    [Fact]
    public void BoundedSemaphore_OverRelease_ThrowsValueError()
    {
        var sem = new BoundedSemaphore(1);
        sem.Acquire();
        sem.Release();
        var act = () => sem.Release();
        act.Should().Throw<ValueError>().WithMessage("*released too many*");
    }

    [Fact]
    public void BoundedSemaphore_NormalUse()
    {
        var sem = new BoundedSemaphore(2);
        sem.Acquire().Should().BeTrue();
        sem.Acquire().Should().BeTrue();
        sem.Acquire(blocking: false).Should().BeFalse();
        sem.Release();
        sem.Release();
    }

    [Fact]
    public void BoundedSemaphore_ContextManager()
    {
        var sem = new BoundedSemaphore(1);
        var entered = sem.__enter__();
        entered.Should().BeSameAs(sem);
        sem.Acquire(blocking: false).Should().BeFalse();
        sem.__exit__();
        sem.Acquire(blocking: false).Should().BeTrue();
        sem.Release();
    }

    [Fact]
    public void Barrier_Synchronization()
    {
        int count = 0;
        var barrier = new Barrier(3);
        var threads = new List<Thread>();

        for (int i = 0; i < 3; i++)
        {
            var t = new Thread(() =>
            {
                System.Threading.Interlocked.Increment(ref count);
                barrier.Wait();
            });
            threads.Add(t);
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join(timeout: 5.0);

        count.Should().Be(3);
    }

    [Fact]
    public void Barrier_Parties()
    {
        var barrier = new Barrier(4);
        barrier.Parties.Should().Be(4);
    }

    [Fact]
    public void Timer_FiresAfterInterval()
    {
        bool fired = false;
        var timer = new Timer(0.05, () => { fired = true; });
        timer.Start();
        SysThread.Sleep(200);
        timer.Dispose();
        fired.Should().BeTrue();
    }

    [Fact]
    public void Timer_CancelPreventsFiring()
    {
        bool fired = false;
        var timer = new Timer(1.0, () => { fired = true; });
        timer.Start();
        timer.Cancel();
        SysThread.Sleep(100);
        fired.Should().BeFalse();
    }

    [Fact]
    public void Timer_StartTwice_ThrowsRuntimeError()
    {
        var timer = new Timer(1.0, () => { });
        timer.Start();
        var act = () => timer.Start();
        act.Should().Throw<RuntimeError>();
        timer.Cancel();
    }

    [Fact]
    public void CurrentThread_ReturnsNonNull()
    {
        var ct = ThreadingModule.CurrentThread();
        ct.Should().NotBeNull();
        ct.Ident.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ActiveCount_ReturnsPositive()
    {
        int count = ThreadingModule.ActiveCount();
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void MainThread_ReturnsNonNull()
    {
        var mt = ThreadingModule.MainThread();
        mt.Should().NotBeNull();
    }

    [Fact]
    public void Enumerate_ReturnsNonEmpty()
    {
        var threads = ThreadingModule.Enumerate();
        threads.Should().NotBeEmpty();
    }

    [Fact]
    public void ModuleFactory_Lock()
    {
        var lk = ThreadingModule.Lock();
        lk.Should().NotBeNull();
        lk.Should().BeOfType<Lock>();
    }

    [Fact]
    public void ModuleFactory_RLock()
    {
        var rl = ThreadingModule.RLock();
        rl.Should().NotBeNull();
        rl.Should().BeOfType<RLock>();
    }

    [Fact]
    public void ModuleFactory_Event()
    {
        var evt = ThreadingModule.Event();
        evt.Should().NotBeNull();
        evt.Should().BeOfType<Event>();
    }

    [Fact]
    public void ModuleFactory_Semaphore()
    {
        var sem = ThreadingModule.Semaphore(3);
        sem.Should().NotBeNull();
        sem.Should().BeOfType<Semaphore>();
    }

    [Fact]
    public void ModuleFactory_BoundedSemaphore()
    {
        var sem = ThreadingModule.BoundedSemaphore(3);
        sem.Should().NotBeNull();
        sem.Should().BeOfType<BoundedSemaphore>();
    }

    [Fact]
    public void ModuleFactory_Barrier()
    {
        var b = ThreadingModule.Barrier(2);
        b.Should().NotBeNull();
        b.Should().BeOfType<Barrier>();
    }

    [Fact]
    public void ModuleFactory_Timer()
    {
        var t = ThreadingModule.Timer(1.0, () => { });
        t.Should().NotBeNull();
        t.Should().BeOfType<Timer>();
        t.Cancel();
    }

    private class TestThread : Thread
    {
        private readonly Action _action;

        public TestThread(Action action) : base()
        {
            _action = action;
        }

        public override void Run()
        {
            _action();
        }
    }
}
