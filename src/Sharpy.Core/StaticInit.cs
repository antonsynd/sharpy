using System;
using System.Runtime.ExceptionServices;
using System.Threading;

// Design note: plan-775c50 (P77, N3 "values ready before use"), decision D3; the states are D4's
// and the host sweep is D5's. This file is the whole run-time half; the compiler emits the
// per-value and per-host code described on StaticInit.
//
// ONE process-wide re-entrant lock: StaticInit.Gate, a Monitor. Every sweep, every
// initializer run by this class and every state change below Ready happens under it.
//
// D3 predicates and how this code meets them:
//
// P1. "No thread waits for the lock while holding a type-initializer lock."
//     The only member meant for a type initializer (a host's Swept holder) is TryLoad, and it
//     takes the lock with Monitor.TryEnter at a zero timeout: it never waits. Load, Read, Store
//     and Run may wait, and the emitted code calls them only (a) outside any type initializer,
//     or (b) inside a running sweep, whose thread already holds the lock (Monitor is
//     re-entrant, so the call does not wait). Inside a Swept type initializer user code runs
//     only in a sweep TryLoad started, i.e. on the thread that holds the lock; a Swept type
//     initializer whose TryLoad lost the lock runs no user code and finishes at once. So a
//     thread blocked on another thread's Swept type initializer waits for a bounded TryLoad,
//     or for the lock holder, which waits for no lock the blocked thread holds.
//     (Residue, stated by A11: a type initializer the compiler did not emit, e.g. a C#
//     consumer's static constructor, that reads a value of a host not yet swept.)
//
// P2. "No read returns a value never given one."
//     Read returns only in Assigned, StoredInitializing or Ready: the first two are entered
//     only by Store, which writes the field under the lock in the same step; Ready only after
//     the initializer returned (it wrote the field). Initializing raises NameError (the read is
//     on the initializing thread: the initializer holds the lock, so no other thread can see
//     the state). Uninitialized runs the initializer first. A throw puts back the state the run
//     found, so a store made inside a failed attempt that began Uninitialized leaves no
//     "stored" state behind. The fast path (Swept.Done true) is taken only after the host's
//     sweep returned, when every value of the host is Ready.
//
// D3's other clauses: a throw restores the state found and rethrows the ORIGINAL exception
// (`catch { state = found; throw; }`, never wrapped); Load runs a host's sweep once (the
// NotStarted -> Running step happens once, under the lock); a sweep's failure is delivered
// once, to what required the host (TryLoad captures it, the next Load/Read/Store rethrows it
// with ExceptionDispatchInfo, then the host is Retrying), and from then on each use retries
// per value (Read runs an Uninitialized value's initializer).

namespace Sharpy
{
    /// <summary>
    /// The readiness states of one Guarded module or <c>@static</c> value (plan-775c50 D4),
    /// held in a <see cref="byte"/> field beside the value's backing field. The numbering is
    /// part of the contract: a state at or above <see cref="Assigned"/> holds a value, and
    /// <see cref="Ready"/> is terminal. See <see cref="StaticInit"/> for the transitions.
    /// </summary>
    public static class StaticInitState
    {
        /// <summary>Never stored to; the initializer has not run (the default of the field).</summary>
        public const byte Uninitialized = 0;

        /// <summary>The initializer is running on the thread that holds the lock; no store since it began from <see cref="Uninitialized"/>.</summary>
        public const byte Initializing = 1;

        /// <summary><see cref="Uninitialized"/> once stored to: a read returns the store; the initializer still runs at its position and replaces it.</summary>
        public const byte Assigned = 2;

        /// <summary><see cref="Initializing"/> once stored to (or begun from <see cref="Assigned"/>): a read returns the store; the initializer's result replaces it.</summary>
        public const byte StoredInitializing = 3;

        /// <summary>The initializer has returned and its result is in the field. Terminal.</summary>
        public const byte Ready = 4;
    }

    /// <summary>
    /// The sweep state of one host (a module, a class with Guarded values, or one closed generic
    /// type), declared by the emitted code as a static field (<c>static StaticInitHost s_host;</c>,
    /// no initializer) and passed by reference to <see cref="StaticInit"/>. Its fields are only
    /// read and written by <see cref="StaticInit"/>, under its lock.
    /// </summary>
    public struct StaticInitHost
    {
        internal const byte NotStarted = 0;
        internal const byte Running = 1;
        internal const byte Done = 2;
        internal const byte Failed = 3;
        internal const byte Retrying = 4;

        /// <summary>NotStarted, Running (on the thread that holds the lock), Done, Failed (the failure not yet delivered), Retrying (delivered; per-value retry).</summary>
        internal byte Phase;

        /// <summary>The sweep's failure while <see cref="Phase"/> is Failed; null otherwise.</summary>
        internal ExceptionDispatchInfo? Failure;
    }

    /// <summary>
    /// The readiness runtime for Sharpy module values and class <c>@static</c> values
    /// (plan-775c50 D3; rule N3): every such value with an initializer that is not a C#
    /// <c>const</c> (a Guarded value) is ready before its first read on every path, a read of a
    /// value never given one raises <see cref="NameError"/>, and a throwing initializer leaves
    /// the value as it found it, so the next read retries.
    /// </summary>
    /// <remarks>
    /// <para><b>Emitted shape.</b> Per Guarded value <c>x</c> of type <c>T</c> in a host:</para>
    /// <code>
    /// static T s_x;                    // backing field, no initializer
    /// static byte s_x_state;           // a StaticInitState constant, no initializer
    /// static void Init_x() { s_x = &lt;initializer&gt;; }
    /// static void Slow_x() =&gt; StaticInit.Read(ref s_host, Sweep, ref s_x_state, "x", Init_x);
    /// public static ref T x { get { if (!Swept.Done) Slow_x(); return ref s_x; } }
    /// public static void x_set(T v) { if (Swept.Done) s_x = v; else StaticInit.Store(ref s_host, Sweep, ref s_x_state, ref s_x, v); }
    /// </code>
    /// <para>Per host, once:</para>
    /// <code>
    /// static StaticInitHost s_host;    // no initializer
    /// static void Sweep() { StaticInit.Run(ref s_a_state, "a", Init_a); StaticInit.Run(ref s_x_state, "x", Init_x); ... }
    /// static class Swept { internal static readonly bool Done; static Swept() { Done = StaticInit.TryLoad(ref s_host, Sweep); } }
    /// </code>
    /// <para>The host's sweep runs, in declaration order, every value's <see cref="Run"/> (and, at
    /// an import's or a nested class's position, that host's requirement, below). Declare
    /// <c>Swept</c> with an explicit static constructor, as above: a type with one is not
    /// <c>beforefieldinit</c>, so its sweep runs at its first access and never earlier. Nothing
    /// else in the host may have a static initializer. A computed <c>const</c> returns
    /// <c>ref readonly</c>; a property default keeps <c>get</c> / <c>set</c> with the same calls.</para>
    /// <para><b>Requiring a host</b> (the entry point for each module in import order, an
    /// importer's sweep at the import, a sweep at a nested class's line):
    /// <c>if (!H.Swept.Done) StaticInit.Load(ref H.s_host, H.Sweep);</c>. Reading <c>Swept.Done</c>
    /// first lets <see cref="TryLoad"/> run the sweep where it can, so the flag is true for the
    /// rest of the process; <see cref="Load"/> then delivers a failure, or runs the sweep when
    /// <see cref="TryLoad"/> lost the lock. Calling <see cref="Load"/> without touching
    /// <c>Swept.Done</c> first is correct but leaves the host on the slow path.</para>
    /// <para><b>An <c>out</c> operand</b> (D4): when <c>Swept.Done</c> is false, call
    /// <see cref="Load"/>, then <see cref="IsReady"/>: true, pass the backing field; false, pass a
    /// temporary and store it with <c>x_set</c> on return.</para>
    /// <para><b>Value states</b> (<see cref="StaticInitState"/>) and what each operation does.
    /// Every operation takes the lock; <see cref="Read"/> and <see cref="Store"/> first require
    /// the host (as <see cref="Load"/>), so a use from outside the host's running sweep runs the
    /// whole sweep before it returns or throws.</para>
    /// <list type="table">
    /// <listheader><term>State</term><description>Read · Store · Run (the sweep at the value's position)</description></listheader>
    /// <item><term>Uninitialized</term><description>Read: run the initializer (a forward read), return. · Store: write, → Assigned. · Run: run the initializer.</description></item>
    /// <item><term>Initializing</term><description>Read: raise <see cref="NameError"/> <c>name 'x' is not defined</c> (a read of a value never given one, on the initializing thread). · Store: write, → StoredInitializing. · Run: raise <see cref="NameError"/> (not reached by a sweep).</description></item>
    /// <item><term>Assigned</term><description>Read: return the store. · Store: write. · Run: run the initializer (it reads the store until it returns).</description></item>
    /// <item><term>StoredInitializing</term><description>Read: return the store. · Store: write. · Run: nothing (not reached by a sweep).</description></item>
    /// <item><term>Ready</term><description>Read: return. · Store: write. · Run: nothing (a forward read initialized it).</description></item>
    /// </list>
    /// <para>Running an initializer: the state goes Uninitialized → Initializing or
    /// Assigned → StoredInitializing; the initializer returns → Ready; it throws → the state it
    /// found (Uninitialized or Assigned, never past a store made before it began) and the
    /// ORIGINAL exception propagates. The backing field keeps the last value written to it
    /// (C#'s rule for a static field); a state below Assigned never exposes it.</para>
    /// <para><b>Host phases</b>: NotStarted → Running (the sweep, once, under the lock) → Done, or
    /// → Failed (the sweep threw inside <see cref="TryLoad"/>: the exception is kept) → Retrying
    /// (the next <see cref="Load"/>, <see cref="Read"/> or <see cref="Store"/> rethrew it, once);
    /// a sweep that throws inside <see cref="Load"/> (or a requirement in <see cref="Read"/> /
    /// <see cref="Store"/>) goes straight to Retrying, its exception delivered to that caller. A
    /// sweep stops at its first throw; after it, each value is initialized by its own first read
    /// (retry per value), and a value stored to before the failure keeps its store.</para>
    /// <para><b>Import cycles.</b> Requiring a host whose sweep is Running on this thread
    /// (<see cref="Load"/>, <see cref="Read"/>, <see cref="Store"/>; <see cref="TryLoad"/> answers
    /// false) returns at once and never re-runs the sweep: when A's sweep requires B and B's
    /// requires A, B's sweep sees A partly initialized, as Python's partially-initialised module,
    /// and a value A has not reached yet initializes on its read. An empty sweep is valid: a
    /// host with no Guarded value may still have a load entry (<c>static void Sweep() { }</c>),
    /// which costs one lock round trip, once.</para>
    /// </remarks>
    public static class StaticInit
    {
        /// <summary>The ONE process-wide re-entrant lock (D3).</summary>
        private static readonly object Gate = new object();

        /// <summary>
        /// For a host's <c>Swept</c> type initializer: take the lock WITHOUT WAITING and, if the
        /// host has not started, run its sweep. Never waits for the lock (D3's departure).
        /// </summary>
        /// <param name="host">The host's state field.</param>
        /// <param name="sweep">The host's sweep: <see cref="Run"/> for each value in declaration order.</param>
        /// <returns>
        /// True when the host's sweep has completed (now or earlier): every value is Ready.
        /// False when the lock is held by another thread, the host's sweep is running on this
        /// thread, or the sweep failed (now or earlier). A failure is kept for the next
        /// <see cref="Load"/>, <see cref="Read"/> or <see cref="Store"/> to deliver; it never
        /// escapes a type initializer.
        /// </returns>
        public static bool TryLoad(ref StaticInitHost host, Action sweep)
        {
            if (!Monitor.TryEnter(Gate))
                return false;
            try
            {
                switch (host.Phase)
                {
                    case StaticInitHost.Done:
                        return true;
                    case StaticInitHost.NotStarted:
                        host.Phase = StaticInitHost.Running;
                        try
                        {
                            sweep();
                        }
                        catch (Exception failure)
                        {
                            host.Failure = ExceptionDispatchInfo.Capture(failure);
                            host.Phase = StaticInitHost.Failed;
                            return false;
                        }
                        host.Phase = StaticInitHost.Done;
                        return true;
                    default:
                        return false;
                }
            }
            finally
            {
                Monitor.Exit(Gate);
            }
        }

        /// <summary>
        /// Require a host: run its sweep once (waiting for the lock), or deliver its kept failure
        /// once. Never call from a type initializer unless this thread is inside a sweep.
        /// </summary>
        /// <param name="host">The host's state field.</param>
        /// <param name="sweep">The host's sweep.</param>
        /// <remarks>Returns at once when the host is Done, Retrying, or Running on this thread
        /// (an import cycle: the sweep is never re-run). Throws the sweep's ORIGINAL exception when the sweep throws here, or when a failure
        /// kept by <see cref="TryLoad"/> has not been delivered yet.</remarks>
        public static void Load(ref StaticInitHost host, Action sweep)
        {
            lock (Gate)
            {
                Require(ref host, sweep);
            }
        }

        /// <summary>
        /// The slow path of a read: require the host (as <see cref="Load"/>), then make the value
        /// readable: initialize it if Uninitialized; raise <see cref="NameError"/> if its
        /// initializer is running (on this thread) and it has not been stored to. On return the
        /// backing field holds a value the program gave it.
        /// </summary>
        /// <param name="host">The value's host state field.</param>
        /// <param name="sweep">The host's sweep.</param>
        /// <param name="state">The value's state field.</param>
        /// <param name="name">The name in the <see cref="NameError"/> message (<c>name 'x' is not defined</c>, CPython's text).</param>
        /// <param name="init">Assigns the initializer's result to the backing field.</param>
        public static void Read(ref StaticInitHost host, Action sweep, ref byte state, string name, Action init)
        {
            lock (Gate)
            {
                Require(ref host, sweep);
                switch (state)
                {
                    case StaticInitState.Uninitialized:
                        Initialize(ref state, init);
                        return;
                    case StaticInitState.Initializing:
                        throw NotDefined(name);
                    default:
                        return;
                }
            }
        }

        /// <summary>
        /// The slow path of a plain store (<c>x_set</c>): require the host (as <see cref="Load"/>),
        /// write the field, and mark the value stored (Uninitialized → Assigned,
        /// Initializing → StoredInitializing), all under the lock.
        /// </summary>
        /// <typeparam name="T">The value's type.</typeparam>
        /// <param name="host">The value's host state field.</param>
        /// <param name="sweep">The host's sweep.</param>
        /// <param name="state">The value's state field.</param>
        /// <param name="field">The value's backing field.</param>
        /// <param name="value">The value stored.</param>
        public static void Store<T>(ref StaticInitHost host, Action sweep, ref byte state, ref T field, T value)
        {
            lock (Gate)
            {
                Require(ref host, sweep);
                field = value;
                switch (state)
                {
                    case StaticInitState.Uninitialized:
                        state = StaticInitState.Assigned;
                        break;
                    case StaticInitState.Initializing:
                        state = StaticInitState.StoredInitializing;
                        break;
                }
            }
        }

        /// <summary>
        /// One step of a host's sweep: run the value's initializer at its declared position
        /// unless a forward read already did (Ready). A store made before it (Assigned) is
        /// readable while the initializer runs and is replaced by its result.
        /// </summary>
        /// <param name="state">The value's state field.</param>
        /// <param name="name">The name in the <see cref="NameError"/> message.</param>
        /// <param name="init">Assigns the initializer's result to the backing field.</param>
        public static void Run(ref byte state, string name, Action init)
        {
            lock (Gate)
            {
                switch (state)
                {
                    case StaticInitState.Uninitialized:
                    case StaticInitState.Assigned:
                        Initialize(ref state, init);
                        return;
                    case StaticInitState.Initializing:
                        throw NotDefined(name);
                    default:
                        return;
                }
            }
        }

        /// <summary>
        /// Whether the value is Ready (its initializer has returned). Ready is terminal, so a
        /// true answer stays true; read with acquire semantics.
        /// </summary>
        /// <param name="state">The value's state field.</param>
        /// <returns>True when the state is <see cref="StaticInitState.Ready"/>.</returns>
        public static bool IsReady(ref byte state) => Volatile.Read(ref state) == StaticInitState.Ready;

        /// <summary>Under the lock: run the host's sweep if it has not started, or deliver its kept failure once.</summary>
        private static void Require(ref StaticInitHost host, Action sweep)
        {
            switch (host.Phase)
            {
                case StaticInitHost.NotStarted:
                    host.Phase = StaticInitHost.Running;
                    try
                    {
                        sweep();
                    }
                    catch
                    {
                        host.Phase = StaticInitHost.Retrying;
                        throw;
                    }
                    host.Phase = StaticInitHost.Done;
                    return;
                case StaticInitHost.Failed:
                    var failure = host.Failure!;
                    host.Failure = null;
                    host.Phase = StaticInitHost.Retrying;
                    failure.Throw();
                    return;
                default:
                    return;
            }
        }

        /// <summary>Under the lock: run an initializer; on a throw put back the state it found and rethrow the original exception.</summary>
        private static void Initialize(ref byte state, Action init)
        {
            var found = state;
            state = found == StaticInitState.Assigned ? StaticInitState.StoredInitializing : StaticInitState.Initializing;
            try
            {
                init();
            }
            catch
            {
                state = found;
                throw;
            }
            Volatile.Write(ref state, StaticInitState.Ready);
        }

        private static NameError NotDefined(string name) => new NameError($"name '{name}' is not defined");
    }
}
