using System;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Threading;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// <see cref="StaticInit"/>, the readiness runtime of plan-775c50 (P77) D3, through test hosts that
/// copy the emitted shape (a backing field, a state byte, <c>Init_x</c>, a ref-returning property
/// whose getter checks <c>Swept.Done</c>, <c>x_set</c>, and a nested <c>Swept</c> holder whose type
/// initializer calls <see cref="StaticInit.TryLoad"/>). Acceptance A4. Each test uses its own
/// closed host type, so its statics are fresh. Every body runs on a bounded background thread: a
/// deadlock fails the test instead of hanging the run, and the tests of this class run one at a
/// time because they share the process-wide lock.
/// </summary>
public class StaticInitTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan Short = TimeSpan.FromSeconds(3);

    // --- D3: a read on the initializing thread -------------------------------------------------

    [Fact]
    public void StaticInit_ReadOnInitializingThread_BeforeAnyStore_RaisesNameError() => Bounded(() =>
    {
        var entries = 0;
        string? observed = null;
        Host2<Tag01>.InitA = () => 1;
        Host2<Tag01>.InitT = () =>
        {
            // A re-entered initializer (the Initializing mark skipped) must not recurse forever.
            if (++entries > 1)
                return 99;
            try
            {
                observed = $"read {Host2<Tag01>.T}";
            }
            catch (NameError e)
            {
                observed = e.Message;
            }
            return 7;
        };

        Host2<Tag01>.T.Should().Be(7);
        observed.Should().Be("name 't' is not defined");
        entries.Should().Be(1);
        Host2<Tag01>.Done.Should().BeTrue();
    });

    [Fact]
    public void StaticInit_ReadOnInitializingThread_AfterAStore_ReturnsTheStore() => Bounded(() =>
    {
        // Q21: `t: int = f()` / `def f(): t = 5; return t + 1` prints 6.
        int? readBack = null;
        Host2<Tag02>.InitA = () => 1;
        Host2<Tag02>.InitT = () =>
        {
            Host2<Tag02>.T_set(5);
            readBack = Host2<Tag02>.T;
            return readBack.Value + 1;
        };

        Host2<Tag02>.T.Should().Be(6);
        readBack.Should().Be(5);
    });

    [Fact]
    public void StaticInit_StoreInsideItsOwnInitializer_IsReplacedByTheResult() => Bounded(() =>
    {
        // Q21: `def f(): t = 5; return 1` prints 1.
        Host2<Tag03>.InitA = () => 1;
        Host2<Tag03>.InitT = () =>
        {
            Host2<Tag03>.T_set(5);
            return 1;
        };

        Host2<Tag03>.T.Should().Be(1);
    });

    [Fact]
    public void StaticInit_StoreBeforeTheInitializer_IsReadThenReplacedAtItsPosition() => Bounded(() =>
    {
        // Q14: an earlier initializer stores t = 5 and reads it; t's initializer still runs: `5 7`.
        var tRuns = 0;
        Host2<Tag04>.InitA = () =>
        {
            Host2<Tag04>.T_set(5);
            return Host2<Tag04>.T;
        };
        Host2<Tag04>.InitT = () =>
        {
            tRuns++;
            return 7;
        };

        Host2<Tag04>.A.Should().Be(5);
        Host2<Tag04>.T.Should().Be(7);
        tRuns.Should().Be(1);
    });

    [Fact]
    public void StaticInit_ForwardRead_InitializesTheLaterValueOnce_AndTheSweepRunsWhole() => Bounded(() =>
    {
        var order = new System.Collections.Generic.List<string>();
        Host2<Tag05>.InitA = () =>
        {
            order.Add("a");
            return Host2<Tag05>.T + 1;
        };
        Host2<Tag05>.InitT = () =>
        {
            order.Add("t");
            return 7;
        };

        // A use of the LATER value from outside runs the whole sweep first.
        Host2<Tag05>.T.Should().Be(7);
        Host2<Tag05>.A.Should().Be(8);
        order.Should().Equal("a", "t");
        Host2<Tag05>.SweepRuns.Should().Be(1);
    });

    // --- D3: a throw ----------------------------------------------------------------------------

    [Fact]
    public void StaticInit_Throw_KeepsItsType_IsDeliveredOnce_ThenTheReadRetries() => Bounded(() =>
    {
        var thrown = new ZeroDivisionError("division by zero");
        var attempts = 0;
        Host2<Tag06>.InitA = () => attempts++ == 0 ? throw thrown : 3;
        Host2<Tag06>.InitT = () => 7;

        // The sweep fails inside Swept's type initializer; the ORIGINAL exception reaches the read.
        Action first = () => _ = Host2<Tag06>.A;
        first.Should().Throw<ZeroDivisionError>().Which.Should().BeSameAs(thrown);
        Host2<Tag06>.Done.Should().BeFalse();

        // Delivered once: the next reads retry per value.
        Host2<Tag06>.A.Should().Be(3);
        Host2<Tag06>.T.Should().Be(7);
        attempts.Should().Be(2);
    });

    [Fact]
    public void StaticInit_NameError_FromAnInitializer_KeepsItsType() => Bounded(() =>
    {
        var entries = 0;
        Host2<Tag07>.InitA = () => 1;
        Host2<Tag07>.InitT = () => ++entries > 1 ? 99 : Host2<Tag07>.T + 1;

        Action read = () => _ = Host2<Tag07>.T;
        read.Should().Throw<NameError>().WithMessage("name 't' is not defined");
        entries.Should().Be(1);
    });

    [Fact]
    public void StaticInit_FailureInARequiredHost_ReachesTheRequirerAsTheOriginal() => Bounded(() =>
    {
        var thrown = new ZeroDivisionError("float division by zero");
        Host2<Tag08Inner>.InitA = () => throw thrown;
        Host2<Tag08Inner>.InitT = () => 1;
        Host2<Tag08Outer>.InitA = () => Host2<Tag08Inner>.A;
        Host2<Tag08Outer>.InitT = () => 1;

        Action read = () => _ = Host2<Tag08Outer>.A;
        read.Should().Throw<ZeroDivisionError>().Which.Should().BeSameAs(thrown);
    });

    [Fact]
    public void StaticInit_Load_DeliversAKeptFailureOnce() => Bounded(() =>
    {
        var thrown = new ValueError("x");
        Host2<Tag09>.InitA = () => throw thrown;
        Host2<Tag09>.InitT = () => 1;

        Action require = Host2<Tag09>.Require;
        require.Should().Throw<ValueError>().Which.Should().BeSameAs(thrown);
        require.Should().NotThrow();
    });

    [Fact]
    public void StaticInit_FailedSweepAndOneRetry_RunTheInitializerTwice_EachReadingAnEarlierStore() => Bounded(() =>
    {
        // W6: an initializer that stores to its own value, throws, and stores again on the retry: `failed 6`.
        var reads = new System.Collections.Generic.List<int>();
        Host2<Tag10>.InitA = () => 1;
        Host2<Tag10>.InitT = () =>
        {
            Host2<Tag10>.T_set(5);
            var r = Host2<Tag10>.T;
            reads.Add(r);
            if (reads.Count == 1)
                throw new ValueError("x");
            return r + 1;
        };

        Action first = () => _ = Host2<Tag10>.T;
        first.Should().Throw<ValueError>();
        Host2<Tag10>.T.Should().Be(6);
        reads.Should().Equal(5, 5);
    });

    [Fact]
    public void StaticInit_StoreMadeOnlyInTheFailedAttempt_IsNotRead_TheRetryRaisesNameError() => Bounded(() =>
    {
        // W6: one that stores only on the failed attempt raises NameError on the retry.
        var attempts = 0;
        Host2<Tag11>.InitA = () => 1;
        Host2<Tag11>.InitT = () =>
        {
            if (attempts++ == 0)
            {
                Host2<Tag11>.T_set(5);
                throw new ValueError("x");
            }
            return attempts > 2 ? 99 : Host2<Tag11>.T + 1;
        };

        Action read = () => _ = Host2<Tag11>.T;
        read.Should().Throw<ValueError>();
        read.Should().Throw<NameError>().WithMessage("name 't' is not defined");
        attempts.Should().Be(2);
    });

    [Fact]
    public void StaticInit_ThrowRestoresTheStateItFound_NotPastAStoreMadeBeforeItBegan() => Bounded(() =>
    {
        // a stores t = 5 (t Assigned); t's initializer reads 5 and throws. The reset is to Assigned:
        // t still reads 5, and its initializer does not run again.
        var tReads = new System.Collections.Generic.List<int>();
        var tRuns = 0;
        Host2<Tag12>.InitA = () =>
        {
            Host2<Tag12>.T_set(5);
            return 1;
        };
        Host2<Tag12>.InitT = () =>
        {
            if (++tRuns > 2)
                return 99;
            tReads.Add(Host2<Tag12>.T);
            throw new ValueError("x");
        };

        Action first = () => _ = Host2<Tag12>.A;
        first.Should().Throw<ValueError>();
        Host2<Tag12>.A.Should().Be(1);
        Host2<Tag12>.T.Should().Be(5);
        tRuns.Should().Be(1);
        tReads.Should().Equal(5);
    });

    // --- D3: the lock ---------------------------------------------------------------------------

    [Fact]
    public void StaticInit_TryLoad_NeverWaitsForTheLock() => Bounded(() =>
    {
        var holder = new HostBox();
        var other = new HostBox();
        var held = new ManualResetEventSlim();
        var release = new ManualResetEventSlim();
        var holding = Start(() => StaticInit.Load(ref holder.Host, () =>
        {
            held.Set();
            release.Wait(Bound);
        }));
        held.Wait(Bound).Should().BeTrue();

        var otherRan = false;
        bool? loaded = null;
        var trying = Start(() => loaded = StaticInit.TryLoad(ref other.Host, () => otherRan = true));
        var returned = trying.Join(Short);
        release.Set();
        holding.Join(Bound).Should().BeTrue();

        returned.Should().BeTrue("TryLoad must not wait for a lock another thread holds");
        loaded.Should().BeFalse();
        otherRan.Should().BeFalse();

        // The host is still NotStarted: Load runs it, and TryLoad then reports it done.
        StaticInit.Load(ref other.Host, () => otherRan = true);
        otherRan.Should().BeTrue();
        StaticInit.TryLoad(ref other.Host, () => throw new InvalidOperationException("ran twice")).Should().BeTrue();
    });

    [Fact]
    public void StaticInit_StoreFromOutside_AfterALockLosingTryLoad_SweepsTheHostFirst() => Bounded(() =>
    {
        var holder = new HostBox();
        var held = new ManualResetEventSlim();
        var release = new ManualResetEventSlim();
        var holding = Start(() => StaticInit.Load(ref holder.Host, () =>
        {
            held.Set();
            release.Wait(Bound);
        }));
        held.Wait(Bound).Should().BeTrue();

        Host2<Tag13>.InitA = () => Host2<Tag13>.T + 1;
        Host2<Tag13>.InitT = () => 7;
        var storing = Start(() => Host2<Tag13>.T_set(5));
        storing.Join(TimeSpan.FromMilliseconds(200)).Should().BeFalse("the store waits for the sweep, outside any type initializer");
        release.Set();
        holding.Join(Bound).Should().BeTrue();
        storing.Join(Bound).Should().BeTrue();

        // The sweep ran before the store (a = t + 1 = 8), and the store landed on the initialized value.
        Host2<Tag13>.A.Should().Be(8);
        Host2<Tag13>.T.Should().Be(5);
        Host2<Tag13>.SweepRuns.Should().Be(1);
        Host2<Tag13>.Done.Should().BeFalse("a lock-losing sweep leaves the slow path on (residue)");
        StaticInit.IsReady(ref Host2<Tag13>.TState).Should().BeTrue();
    });

    [Fact]
    public void StaticInit_TwoThreads_InitializeAValueOnce() => Bounded(() =>
    {
        // Once through the type initializer (the CLR's lock), once through the process-wide lock
        // (a retry after a failed sweep).
        var runs = 0;
        var inInit = new ManualResetEventSlim();
        Thread? second = null;
        LazyG<short>.InitX = () =>
        {
            Interlocked.Increment(ref runs);
            inInit.Set();
            WaitUntilBlocked(() => second);
            return 7;
        };
        int x1 = 0, x2 = 0;
        var first = Start(() => x1 = LazyG<short>.X);
        inInit.Wait(Bound).Should().BeTrue();
        second = Start(() => x2 = LazyG<short>.X);
        first.Join(Bound).Should().BeTrue();
        second.Join(Bound).Should().BeTrue();
        (x1, x2, runs).Should().Be((7, 7, 1));

        var attempts = 0;
        var inRetry = new ManualResetEventSlim();
        Thread? other = null;
        Host2<Tag14>.InitT = () => 1;
        Host2<Tag14>.InitA = () =>
        {
            if (Interlocked.Increment(ref attempts) == 1)
                throw new ValueError("x");
            inRetry.Set();
            WaitUntilBlocked(() => other);
            return 3;
        };
        Action failed = () => _ = Host2<Tag14>.A;
        failed.Should().Throw<ValueError>();
        int a1 = 0, a2 = 0;
        var retrying = Start(() => a1 = Host2<Tag14>.A);
        inRetry.Wait(Bound).Should().BeTrue();
        other = Start(() => a2 = Host2<Tag14>.A);
        retrying.Join(Bound).Should().BeTrue();
        other.Join(Bound).Should().BeTrue();
        (a1, a2, attempts).Should().Be((3, 3, 2));
    });

    [Fact]
    public void StaticInit_TwoThreads_OnTwoLazyClasses_Interleaved_BothFinish() => Bounded(() =>
    {
        // Thread 1 sweeps LazyA (it holds the lock and LazyA.Swept's type-initializer lock) and its
        // initializer reads LazyB while thread 2 is inside LazyB.Swept's type initializer. With a
        // TryLoad that waits, thread 2 waits for the lock thread 1 holds while thread 1 waits for
        // the type initializer thread 2 holds.
        var inSweep = new ManualResetEventSlim();
        var inTypeInit = new ManualResetEventSlim();
        LazyA.InitX = () =>
        {
            inSweep.Set();
            inTypeInit.Wait(Bound);
            return LazyB.Y + 1;
        };
        LazyB.InitY = () => 7;
        LazyB.BeforeTryLoad = () => inTypeInit.Set();

        int a = 0, b = 0;
        var t1 = Start(() => a = LazyA.X);
        var t2 = Start(() =>
        {
            inSweep.Wait(Bound);
            b = LazyB.Y;
        });
        t1.Join(Bound).Should().BeTrue("thread 1 finishes");
        t2.Join(Bound).Should().BeTrue("thread 2 finishes");
        (a, b).Should().Be((8, 7));
    });

    [Fact]
    public void StaticInit_TwoThreads_OnClosedGenericTypes_Interleaved_BothFinish() => Bounded(() =>
    {
        var inSweep = new ManualResetEventSlim();
        var inTypeInit = new ManualResetEventSlim();
        LazyG<int>.InitX = () =>
        {
            inSweep.Set();
            inTypeInit.Wait(Bound);
            return LazyG<long>.X + 1;
        };
        LazyG<long>.InitX = () => 7;
        LazyG<long>.BeforeTryLoad = () => inTypeInit.Set();

        int a = 0, b = 0;
        var t1 = Start(() => a = LazyG<int>.X);
        var t2 = Start(() =>
        {
            inSweep.Wait(Bound);
            b = LazyG<long>.X;
        });
        t1.Join(Bound).Should().BeTrue("thread 1 finishes");
        t2.Join(Bound).Should().BeTrue("thread 2 finishes");
        (a, b).Should().Be((8, 7));
    });

    // --- import cycles (lead ruling for T3) -------------------------------------------------------

    [Fact]
    public void StaticInit_TwoHostsWhoseSweepsRequireEachOther_Finish_EachSweepRunsOnce() => Bounded(() =>
    {
        // A imports B at its top, B imports A at its top. B's sweep sees A partly initialized
        // (Python's partially-initialised module): B.a reads A.t, which A's sweep has not
        // reached, so the read initializes it through Run's state machine.
        var aT = 0;
        Host2<Tag15A>.BeforeValues = () =>
        {
            if (Host2<Tag15A>.SweepRuns == 1) // a re-run sweep must not recurse forever
                Host2<Tag15B>.Require();
        };
        Host2<Tag15B>.BeforeValues = () =>
        {
            if (Host2<Tag15B>.SweepRuns == 1)
                Host2<Tag15A>.Require();
        };
        Host2<Tag15A>.InitA = () => 1;
        Host2<Tag15A>.InitT = () =>
        {
            aT++;
            return 2;
        };
        Host2<Tag15B>.InitA = () => Host2<Tag15A>.T + 10;
        Host2<Tag15B>.InitT = () => 3;

        Host2<Tag15A>.Require();

        (Host2<Tag15A>.A, Host2<Tag15A>.T, Host2<Tag15B>.A, Host2<Tag15B>.T).Should().Be((1, 2, 12, 3));
        (Host2<Tag15A>.SweepRuns, Host2<Tag15B>.SweepRuns, aT).Should().Be((1, 1, 1));
        Host2<Tag15A>.Done.Should().BeTrue();
        Host2<Tag15B>.Done.Should().BeTrue();
    });

    [Fact]
    public void StaticInit_EmptySweep_IsValid() => Bounded(() =>
    {
        var box = new HostBox();
        StaticInit.TryLoad(ref box.Host, static () => { }).Should().BeTrue();
        StaticInit.Load(ref box.Host, () => throw new InvalidOperationException("ran twice"));
        var lost = new HostBox();
        StaticInit.Load(ref lost.Host, static () => { });
        StaticInit.TryLoad(ref lost.Host, () => throw new InvalidOperationException("ran twice")).Should().BeTrue();
    });

    [Fact]
    public void StaticInit_IsReady_OnlyForReady()
    {
        byte state = StaticInitState.Uninitialized;
        StaticInit.IsReady(ref state).Should().BeFalse();
        state = StaticInitState.StoredInitializing;
        StaticInit.IsReady(ref state).Should().BeFalse();
        state = StaticInitState.Ready;
        StaticInit.IsReady(ref state).Should().BeTrue();
    }

    // --- helpers ----------------------------------------------------------------------------------

    private static void Bounded(Action body)
    {
        Stray.Clear();
        ExceptionDispatchInfo? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception e)
            {
                error = ExceptionDispatchInfo.Capture(e);
            }
        })
        { IsBackground = true };
        thread.Start();
        if (!thread.Join(Bound))
            Assert.Fail($"the test did not finish within {Bound.TotalSeconds} s (a deadlock)");
        error?.Throw();
        if (Stray.TryDequeue(out var stray))
            ExceptionDispatchInfo.Capture(stray).Throw();
    }

    /// <summary>Exceptions escaping a worker thread; <see cref="Bounded"/> reports them (an unhandled one would end the test host).</summary>
    private static readonly ConcurrentQueue<Exception> Stray = new();

    private static Thread Start(Action body)
    {
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception e)
            {
                Stray.Enqueue(e);
            }
        })
        { IsBackground = true };
        thread.Start();
        return thread;
    }

    /// <summary>Wait (bounded) until the other thread exists and is blocked.</summary>
    private static void WaitUntilBlocked(Func<Thread?> other)
    {
        var deadline = DateTime.UtcNow + Short;
        while (DateTime.UtcNow < deadline)
        {
            var thread = other();
            if (thread != null && (thread.ThreadState & ThreadState.WaitSleepJoin) != 0)
                return;
            Thread.Sleep(5);
        }
    }

    private sealed class HostBox
    {
        public StaticInitHost Host;
    }

    private sealed class Tag01 { }
    private sealed class Tag02 { }
    private sealed class Tag03 { }
    private sealed class Tag04 { }
    private sealed class Tag05 { }
    private sealed class Tag06 { }
    private sealed class Tag07 { }
    private sealed class Tag08Inner { }
    private sealed class Tag08Outer { }
    private sealed class Tag09 { }
    private sealed class Tag10 { }
    private sealed class Tag11 { }
    private sealed class Tag12 { }
    private sealed class Tag13 { }
    private sealed class Tag14 { }
    private sealed class Tag15A { }
    private sealed class Tag15B { }
}

/// <summary>
/// The emitted shape of a host with two Guarded values, <c>a</c> then <c>t</c> (declaration order),
/// one closed type per test. The initializers are the test's (<see cref="InitA"/>, <see cref="InitT"/>,
/// set before the first use).
/// </summary>
internal static class Host2<TTag>
{
    internal static Func<int>? InitA;
    internal static Func<int>? InitT;
    internal static Action? BeforeValues;
    internal static int SweepRuns;

    private static StaticInitHost s_host;
    private static int s_a;
    private static byte s_a_state;
    private static int s_t;
    internal static byte TState;

    private static void Sweep()
    {
        SweepRuns++;
        BeforeValues?.Invoke(); // an import at the top of the module, when the test sets one
        StaticInit.Run(ref s_a_state, "a", Init_a);
        StaticInit.Run(ref TState, "t", Init_t);
    }

    private static void Init_a() { s_a = InitA!(); }
    private static void Init_t() { s_t = InitT!(); }

    private static class Swept
    {
        internal static readonly bool Done;

        static Swept()
        {
            Done = StaticInit.TryLoad(ref s_host, Sweep);
        }
    }

    internal static ref int A
    {
        get
        {
            if (!Swept.Done)
                Slow_a();
            return ref s_a;
        }
    }

    internal static ref int T
    {
        get
        {
            if (!Swept.Done)
                Slow_t();
            return ref s_t;
        }
    }

    private static void Slow_a() => StaticInit.Read(ref s_host, Sweep, ref s_a_state, "a", Init_a);
    private static void Slow_t() => StaticInit.Read(ref s_host, Sweep, ref TState, "t", Init_t);

    internal static void T_set(int v)
    {
        if (Swept.Done)
            s_t = v;
        else
            StaticInit.Store(ref s_host, Sweep, ref TState, ref s_t, v);
    }

    /// <summary>What the entry point or an importer emits to require the host.</summary>
    internal static void Require()
    {
        if (!Swept.Done)
            StaticInit.Load(ref s_host, Sweep);
    }

    internal static bool Done => Swept.Done;
}

/// <summary>A lazy class with one Guarded value and a hook in its <c>Swept</c> type initializer.</summary>
internal static class LazyA
{
    internal static Func<int>? InitX;

    private static StaticInitHost s_host;
    private static int s_x;
    private static byte s_x_state;

    private static void Sweep() => StaticInit.Run(ref s_x_state, "x", Init_x);
    private static void Init_x() { s_x = InitX!(); }

    private static class Swept
    {
        internal static readonly bool Done;

        static Swept()
        {
            Done = StaticInit.TryLoad(ref s_host, Sweep);
        }
    }

    internal static ref int X
    {
        get
        {
            if (!Swept.Done)
                Slow_x();
            return ref s_x;
        }
    }

    private static void Slow_x() => StaticInit.Read(ref s_host, Sweep, ref s_x_state, "x", Init_x);
}

/// <summary>A second lazy class; <see cref="BeforeTryLoad"/> runs inside its <c>Swept</c> type initializer.</summary>
internal static class LazyB
{
    internal static Func<int>? InitY;
    internal static Action? BeforeTryLoad;

    private static StaticInitHost s_host;
    private static int s_y;
    private static byte s_y_state;

    private static void Sweep() => StaticInit.Run(ref s_y_state, "y", Init_y);
    private static void Init_y() { s_y = InitY!(); }

    private static class Swept
    {
        internal static readonly bool Done;

        static Swept()
        {
            BeforeTryLoad?.Invoke();
            Done = StaticInit.TryLoad(ref s_host, Sweep);
        }
    }

    internal static ref int Y
    {
        get
        {
            if (!Swept.Done)
                Slow_y();
            return ref s_y;
        }
    }

    private static void Slow_y() => StaticInit.Read(ref s_host, Sweep, ref s_y_state, "y", Init_y);
}

/// <summary>A lazy generic class: each closed type is its own host, with its own state and <c>Swept</c>.</summary>
internal static class LazyG<TArg>
{
    internal static Func<int>? InitX;
    internal static Action? BeforeTryLoad;

    private static StaticInitHost s_host;
    private static int s_x;
    private static byte s_x_state;

    private static void Sweep() => StaticInit.Run(ref s_x_state, "x", Init_x);
    private static void Init_x() { s_x = InitX!(); }

    private static class Swept
    {
        internal static readonly bool Done;

        static Swept()
        {
            BeforeTryLoad?.Invoke();
            Done = StaticInit.TryLoad(ref s_host, Sweep);
        }
    }

    internal static ref int X
    {
        get
        {
            if (!Swept.Done)
                Slow_x();
            return ref s_x;
        }
    }

    private static void Slow_x() => StaticInit.Read(ref s_host, Sweep, ref s_x_state, "x", Init_x);
}
