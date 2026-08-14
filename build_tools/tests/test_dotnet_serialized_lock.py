"""
Regression harness for the ``.claude/scripts/dotnet-serialized`` lock protocol (#1508).

Why this file exists
--------------------
The mutex this wrapper implements has been fixed three times for the same hazard —
``4804ea800``, ``b74f93913``, ``58c82ffa5`` — and each fix was verified by an ad-hoc shell
harness that was described in a commit message and then thrown away. **Zero** of them existed
in the repo, so every cycle re-derived the same fake-HOME/fake-dotnet rig from scratch and the
next regression had nothing standing in its way. That is the defect #1508 names, and a
committed harness is its fix; the individual lock bugs are only the symptoms.

Each probe below therefore names the commit or incident it pins. A probe that stops
distinguishing its fix from its absence is worse than no probe, so each one was mutation-tested
during development: the guarded behavior was broken, the probe was observed RED, and the
mutation reverted. Those mutations are recorded in the individual docstrings.

Design
------
* **Fake ``$HOME``** — the lock dir is ``${HOME}/.claude/locks/dotnet.lock`` and the generation
  counter sits beside it, so a per-test HOME buys complete isolation from the developer's real
  lock for free. Nothing here can wedge a real agent's run.
* **Fake ``dotnet`` on ``PATH``** — the wrapper resolves ``dotnet`` unqualified, so a stub script
  earlier on PATH lets a probe script the child's exact behavior (hang, swallow SIGTERM, print
  nothing, ...) without a 20-minute build.
* **Seconds-scale env knobs** — ``DOTNET_SERIALIZED_TIMEOUT`` / ``DOTNET_SERIALIZED_LOG_DIR``
  and the watchdog knobs. Production defaults are unchanged; the knobs exist only so these
  tests take seconds.
* **Portability is part of acceptance** — macOS bash 3.2 + BSD ``stat`` locally, GNU on CI.
  No ``$BASHPID``, no ``wait -n``, no ``date +%N``, no GNU-only ``stat`` spelling.

Running these locally
---------------------
Outside a sandbox. The protocol is built on ``kill -0`` liveness probes and ``ps``, both of
which a sandboxed shell is denied — and the denial does not surface as an error, it surfaces as
"that pid is dead", so a sandboxed run reports the orphan-steal probe RED against a wrapper that
is working correctly. The wrapper's own comment at the ``rm -rf`` fallback says the same thing.
In Claude Code that means ``dangerouslyDisableSandbox: true``; GitHub Actions is unsandboxed.
"""

from __future__ import annotations

import itertools
import os
import signal
import subprocess
import time
from pathlib import Path
from typing import Callable, Optional

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
WRAPPER = REPO_ROOT / ".claude" / "scripts" / "dotnet-serialized"

_COUNTER = itertools.count()


# --------------------------------------------------------------------------------------
# Rig
# --------------------------------------------------------------------------------------


class Rig:
    """A fake HOME + fake dotnet + private log dir, and helpers to drive the real wrapper."""

    def __init__(self, root: Path) -> None:
        self.root = root
        self.home = root / "home"
        self.bin = root / "bin"
        self.logs = root / "logs"
        for path in (self.home, self.bin, self.logs):
            path.mkdir(parents=True, exist_ok=True)
        self.lock_dir = self.home / ".claude" / "locks" / "dotnet.lock"
        self.generation_file = self.home / ".claude" / "locks" / "dotnet.generation"

    # -- fake dotnet ------------------------------------------------------------------

    def fake_dotnet(self, body: str) -> None:
        """
        Install ``body`` as the ``dotnet`` on PATH. ``$@`` is the wrapper's own arguments.

        Installed by atomic REPLACE, never by truncate-and-rewrite. ``/bin/sh`` executes a script
        by reading its open fd incrementally, so rewriting the file under a still-running stub
        makes it hit EOF and exit — which silently killed the orphan the orphan-steal probe had
        just deliberately created, and made a green wrapper look red. ``os.replace`` swaps the
        directory entry and leaves the running stub's inode alone.
        """
        script = self.bin / "dotnet"
        staged = self.bin / f"dotnet.staged.{next(_COUNTER)}"
        staged.write_text("#!/bin/sh\n" + body + "\n", encoding="utf-8")
        staged.chmod(0o755)
        os.replace(staged, script)

    # -- invocation -------------------------------------------------------------------

    def env(self, **overrides: str) -> dict[str, str]:
        env = dict(os.environ)
        env["HOME"] = str(self.home)
        env["PATH"] = f"{self.bin}{os.pathsep}{env['PATH']}"
        env["DOTNET_SERIALIZED_LOG_DIR"] = str(self.logs)
        env["DOTNET_SERIALIZED_TIMEOUT"] = "30"
        env.update(overrides)
        return env

    def run(self, *args: str, timeout: float = 120, **env_overrides: str):
        return subprocess.run(
            [str(WRAPPER), *args], env=self.env(**env_overrides),
            capture_output=True, text=True, timeout=timeout, cwd=str(self.root))

    def spawn(self, *args: str, **env_overrides: str) -> subprocess.Popen:
        return subprocess.Popen(
            [str(WRAPPER), *args], env=self.env(**env_overrides),
            stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, cwd=str(self.root))

    # -- lock introspection -----------------------------------------------------------

    def lock_pid(self) -> Optional[int]:
        return _read_pid(self.lock_dir / "pid")

    def child_pid(self) -> Optional[int]:
        return _read_pid(self.lock_dir / "child")

    def latest_log(self) -> Optional[Path]:
        link = self.logs / "dotnet-serialized-latest.log"
        return link.resolve() if link.exists() else None


@pytest.fixture()
def rig(tmp_path: Path):
    made = Rig(tmp_path)
    yield made
    # Kill anything a probe deliberately orphaned, so a failure cannot leak a live process.
    for pidfile in ("child", "pid"):
        pid = _read_pid(made.lock_dir / pidfile)
        if pid is not None and pid != os.getpid() and _alive(pid):
            _kill_tree(pid)


# --------------------------------------------------------------------------------------
# Helpers
# --------------------------------------------------------------------------------------


def _read_pid(path: Path) -> Optional[int]:
    try:
        return int(path.read_text(encoding="utf-8").strip())
    except (OSError, ValueError):
        return None


def _alive(pid: int) -> bool:
    try:
        os.kill(pid, 0)
    except (OSError, ProcessLookupError):
        return False
    return True


def _kill_tree(pid: int) -> None:
    """SIGKILL *pid*; best-effort, a probe's cleanup must never fail the probe."""
    try:
        os.kill(pid, signal.SIGKILL)
    except OSError:
        pass


def wait_for(predicate: Callable[[], bool], timeout: float = 30, what: str = "condition") -> None:
    """
    Poll until *predicate*, or **fail** the test.

    Raises rather than returning a bool on purpose: a wait helper that returns False and gets
    dropped into an ``if`` turns every downstream assertion vacuous, which is precisely how an
    absence assertion passes without observing anything.
    """
    deadline = time.time() + timeout
    while time.time() < deadline:
        if predicate():
            return
        time.sleep(0.05)
    raise AssertionError(f"timed out after {timeout}s waiting for {what}")


def dead_pid() -> int:
    """A pid that is definitely not running: spawn a trivial process and reap it."""
    proc = subprocess.Popen(["/bin/sh", "-c", "exit 0"])
    proc.wait()
    # Give the OS a beat to finish tearing it down before anyone probes it.
    for _ in range(100):
        if not _alive(proc.pid):
            break
        time.sleep(0.01)
    return proc.pid


def log_mtime(path: Path) -> float:
    """
    Modification time of *path*.

    Python's ``os.stat`` is portable, so the harness needs no ``stat(1)`` spelling of its own —
    but the WRAPPER does, and it must use the two-spelling form (``stat -f %m`` on BSD/macOS,
    ``stat -c %Y`` on GNU/Linux CI). :func:`test_wrapper_mtime_helper_works_on_this_platform`
    is what proves the wrapper's spelling resolves here.
    """
    return path.stat().st_mtime


# --------------------------------------------------------------------------------------
# Probes
# --------------------------------------------------------------------------------------


class TestMutualExclusion:
    def test_eight_concurrent_wrappers_never_overlap(self, rig: Rig):
        """
        The 2026-08-12 measurement, made permanent.

        Eight concurrent wrappers were run against the pre-fix script and **FOUR** executed at
        once: every peer that lost the ``mkdir`` race checked staleness at the instant the winner
        had made the directory but not yet written its pid, read "no pid file ⇒ abandoned", and
        admitted itself. They are synchronised on that microsecond window, which is why the herd
        was four wide rather than one unlucky peer. Each concurrent ``dotnet test`` costs 5-10 GB,
        so four is an OOM.

        Mutation-tested, and the result is worth recording honestly: deleting the ``sleep 1``
        re-check from ``is_lock_stale``'s pidless branch does NOT reliably red this probe — it
        stayed green through the mutation, because reproducing the herd needs eight peers to
        collide inside a microseconds-wide window and an unloaded machine rarely obliges. The
        deterministic pin for that fix is
        :meth:`test_a_pidless_dir_that_gains_a_live_pid_within_the_beat_is_not_stolen`, which
        opens the window by hand. This probe is kept as the incident-SHAPED cell: it is the only
        one that would catch a mutual-exclusion break the narrow probes do not model.
        """
        events = rig.root / "events.txt"
        rig.fake_dotnet(
            f'start=$(python3 -c "import time;print(repr(time.time()))")\n'
            f'sleep 0.5\n'
            f'stop=$(python3 -c "import time;print(repr(time.time()))")\n'
            f'echo "RAN $start $stop" >> "{events}"\n'
            f'echo "Total: 1"\n'
        )

        procs = [rig.spawn("test", f"--peer{i}", DOTNET_SERIALIZED_TIMEOUT="120")
                 for i in range(8)]
        for proc in procs:
            proc.communicate(timeout=180)

        assert all(p.returncode == 0 for p in procs), [p.returncode for p in procs]

        intervals = []
        for line in events.read_text(encoding="utf-8").splitlines():
            _, start, stop = line.split()
            intervals.append((float(start), float(stop)))
        assert len(intervals) == 8, f"expected 8 runs, saw {len(intervals)}"

        intervals.sort()
        overlaps = [(intervals[i], intervals[i + 1])
                    for i in range(len(intervals) - 1)
                    if intervals[i + 1][0] < intervals[i][1]]
        assert not overlaps, f"concurrent dotnet runs admitted: {overlaps}"

    def test_a_pidless_lock_dir_is_reclaimed_only_after_the_re_check(self, rig: Rig):
        """
        An abandoned lock dir with no pid file must be reclaimed (a crash between ``mkdir`` and
        the pid write leaves exactly this, and refusing forever would wedge the fleet).
        """
        rig.lock_dir.mkdir(parents=True)
        rig.fake_dotnet('echo "Total: 1"')

        result = rig.run("test", DOTNET_SERIALIZED_TIMEOUT="30")

        assert result.returncode == 0, result.stderr
        assert "Total: 1" in result.stdout
        assert "stale dotnet lock" in result.stderr

    def test_a_pidless_dir_that_gains_a_live_pid_within_the_beat_is_not_stolen(self, rig: Rig):
        """
        The positive control for the probe above, and the half that actually pins the herd fix
        (58c82ffa5's companion): the window is microseconds wide, so a directory that acquires a
        LIVE pid before the re-check is a peer mid-acquisition, not an abandoned lock. Without
        the re-check this reads as stale and a second dotnet is admitted.

        Mutation-tested: removing the ``sleep 1`` / re-read makes this probe RED — the wrapper
        reclaims the directory immediately and runs, exiting 0 instead of timing out at 124.
        Reverted.
        """
        rig.lock_dir.mkdir(parents=True)
        rig.fake_dotnet('echo "Total: 1"')

        proc = rig.spawn("test", DOTNET_SERIALIZED_TIMEOUT="4")
        # Land the pid inside the wrapper's one-second re-check beat. The mkdir is not
        # redundant: a wrapper that has ALREADY stolen the lock has rm -rf'd this directory,
        # and the failure that matters is then the exit code below, not an errno from the
        # probe's own setup.
        time.sleep(0.3)
        rig.lock_dir.mkdir(parents=True, exist_ok=True)
        (rig.lock_dir / "pid").write_text(f"{os.getpid()}\n", encoding="utf-8")

        _, stderr = proc.communicate(timeout=60)

        assert proc.returncode == 124, f"lock was stolen from a live peer (exit {proc.returncode})"
        assert "Timed out waiting for dotnet lock" in stderr


class TestOrphanedChild:
    def test_an_orphaned_dotnet_child_keeps_the_lock(self, rig: Rig):
        """
        Pins 58c82ffa5. Judging staleness by the owner SHELL fails open in exactly the case the
        lock exists for: kill an agent (or let it hit a usage limit) and its dotnet/testhost
        children are reparented to init and keep consuming 5-10 GB while the shell that started
        them is gone. The next wrapper read "owner PID gone", removed the lock, and admitted a
        SECOND concurrent run beside the orphan.

        Mutation-tested twice, because the two halves of the fix fail differently:
        removing the child-liveness branch from ``is_lock_stale`` (literal pre-58c82ffa5
        behavior) reds this at the steal — "lock stolen from an orphaned child (exit 0)";
        stubbing out the ``$LOCK_DIR/child`` write instead reds it at the recording step.
        Both reverted.

        Synchronisation note, learned the hard way here: the recorded pid appears BEFORE the
        stub runs. The wrapper writes ``echo $$ > $LOCK_DIR/child`` and only then ``exec``s
        dotnet, so waiting on the child FILE races the exec — an earlier draft swapped the stub
        in that window, the exec picked up the replacement, the "hanging" child exited at once,
        and the wrapper's entirely correct reclaim looked like a steal. Wait for the stub's own
        sentinel, and use ONE stub that dispatches on ``--hang`` so nothing is swapped mid-test.
        """
        started = rig.root / "child_started"
        rig.fake_dotnet(
            f'echo "Total: 1"\n'
            f'case " $* " in *" --hang "*) : > "{started}"; sleep 120 ;; esac\n'
        )

        holder = rig.spawn("test", "--hang", DOTNET_SERIALIZED_TIMEOUT="120")
        wait_for(started.exists, what="the fake dotnet to reach its hang (exec has happened)")
        orphan = rig.child_pid()
        assert orphan is not None and _alive(orphan)

        holder.kill()
        holder.wait(timeout=30)
        wait_for(lambda: not _alive(holder.pid), what="the holder shell to die")
        assert _alive(orphan), "the fake dotnet should have been orphaned, not reaped"

        # The lock now names a DEAD owner and a LIVE child. A peer must queue, not steal.
        queued = rig.run("test", timeout=60, DOTNET_SERIALIZED_TIMEOUT="4")
        assert queued.returncode == 124, (
            f"lock stolen from an orphaned child (exit {queued.returncode}); "
            f"stdout={queued.stdout!r}")

        # Once the orphan really is gone the lock is reclaimable — the fix must not wedge
        # the fleet permanently, which would be the opposite failure.
        _kill_tree(orphan)
        wait_for(lambda: not _alive(orphan), what="the orphan to die")
        reclaimed = rig.run("test", timeout=60, DOTNET_SERIALIZED_TIMEOUT="30")
        assert reclaimed.returncode == 0, reclaimed.stderr
        assert "Total: 1" in reclaimed.stdout


class TestLockOnlyVerbs:
    def test_acquire_lock_refuses_a_dead_holder(self, rig: Rig):
        """A dead holder would wedge every peer for the full TIMEOUT with nothing running."""
        result = rig.run("--acquire-lock", str(dead_pid()))

        assert result.returncode == 2
        assert "LIVE pid" in result.stderr
        assert not rig.lock_dir.exists()

    def test_acquire_lock_refuses_a_missing_holder(self, rig: Rig):
        result = rig.run("--acquire-lock")

        assert result.returncode == 2
        assert not rig.lock_dir.exists()

    def test_release_lock_refuses_a_holder_that_does_not_own_it(self, rig: Rig):
        acquired = rig.run("--acquire-lock", str(os.getpid()))
        assert acquired.returncode == 0
        assert rig.lock_pid() == os.getpid()

        wrong = rig.run("--release-lock", str(dead_pid()))

        assert wrong.returncode == 2
        assert "refusing to release" in wrong.stderr
        assert rig.lock_dir.exists(), "a refused release must leave the lock intact"

        right = rig.run("--release-lock", str(os.getpid()))
        assert right.returncode == 0
        assert not rig.lock_dir.exists()

    def test_lock_generation_is_monotonic_across_acquisitions(self, rig: Rig):
        """
        #1420's interruption detector reads this before and after a round. It lives OUTSIDE the
        lock dir because ``cleanup`` rm -rf's that directory on every release, so a counter
        inside it resets and can never be monotonic.
        """
        before = int(rig.run("--lock-generation").stdout.strip())
        rig.fake_dotnet('echo "Total: 1"')
        assert rig.run("test").returncode == 0
        assert rig.run("test").returncode == 0
        after = int(rig.run("--lock-generation").stdout.strip())

        assert after == before + 2

    def test_lock_only_verbs_consume_no_log_slot(self, rig: Rig):
        """
        The verbs exit before the tee pipeline. If they rotated the 3-slot deque, a bench_ab
        round (two acquire/release pairs each) would evict the logs an agent is meant to read.
        """
        assert rig.run("--acquire-lock", str(os.getpid())).returncode == 0
        assert rig.run("--release-lock", str(os.getpid())).returncode == 0
        assert rig.run("--lock-generation").returncode == 0

        assert not (rig.logs / "dotnet-serialized-index").exists()
        assert not list(rig.logs.glob("dotnet-serialized-*.log"))


class TestRecordChild:
    """
    ``--record-child HOLDER_PID CHILD_PID`` (#1508, fix half).

    The lock-only verbs exist so bench_ab can hold the mutex across a round's two arms, but
    ``--acquire-lock`` returns before the wrapper's own ``$LOCK_DIR/child`` write and bench_ab
    then spawns the heavyweight benchmark dotnet itself — so the held lock named a pid whose
    death said nothing about the 5-10 GB process actually running. SIGKILL bench_ab mid-arm and
    the lock read stale with the benchmark still live: the exact steal 58c82ffa5 closed for the
    wrapper's own path, reopened 11 minutes later through the lock-only door.

    The verb keeps lock-dir layout knowledge in the one file that owns it; bench_ab passes pids
    and never touches ``$LOCK_DIR`` itself.
    """

    def test_refuses_a_holder_that_does_not_own_the_lock(self, rig: Rig):
        """Otherwise any process could redirect another holder's child pointer."""
        assert rig.run("--acquire-lock", str(os.getpid())).returncode == 0
        victim = subprocess.Popen(["/bin/sh", "-c", "sleep 60"])
        try:
            result = rig.run("--record-child", str(dead_pid()), str(victim.pid))

            assert result.returncode == 2
            assert "refusing" in result.stderr
            assert not (rig.lock_dir / "child").exists()
        finally:
            victim.kill()
            victim.wait()
            rig.run("--release-lock", str(os.getpid()))

    def test_refuses_a_dead_child(self, rig: Rig):
        """
        Recording a dead child is worse than recording nothing: ``is_lock_stale`` consults the
        child FIRST, so a dead one is a pointer that can only ever read "stale".
        """
        assert rig.run("--acquire-lock", str(os.getpid())).returncode == 0
        try:
            result = rig.run("--record-child", str(os.getpid()), str(dead_pid()))

            assert result.returncode == 2
            assert "LIVE" in result.stderr
            assert not (rig.lock_dir / "child").exists()
        finally:
            rig.run("--release-lock", str(os.getpid()))

    def test_refuses_when_no_lock_is_held(self, rig: Rig):
        victim = subprocess.Popen(["/bin/sh", "-c", "sleep 60"])
        try:
            result = rig.run("--record-child", str(os.getpid()), str(victim.pid))

            assert result.returncode == 2
        finally:
            victim.kill()
            victim.wait()

    def test_a_recorded_child_survives_a_sigkilled_holder(self, rig: Rig):
        """
        #1508's literal scenario, red-first for this phase.

        A stand-in for bench_ab: take the lock with ``--acquire-lock``, spawn a long-running
        "benchmark dotnet", record it, then get SIGKILLed mid-arm. The orphan keeps its memory,
        so a peer must QUEUE behind it rather than be admitted beside it.

        Mutation-tested: dropping the ``echo "$child" > $LOCK_DIR/child`` line from the verb
        while leaving its validation intact makes this RED at the recording assertion (no child
        pointer is ever written). Reverted. The complementary mutation — writing the pointer but
        having ``is_lock_stale`` ignore it — is the one recorded on
        :meth:`TestOrphanedChild.test_an_orphaned_dotnet_child_keeps_the_lock`, and that one
        reds at the steal itself; together they cover both halves of the mechanism.
        """
        started = rig.root / "bench_started"
        bench = rig.root / "fake_bench_ab.sh"
        bench.write_text(
            f'#!/bin/sh\n'
            f'"{WRAPPER}" --acquire-lock $$ || exit 1\n'
            f'sleep 300 &\n'
            f'CHILD=$!\n'
            f'"{WRAPPER}" --record-child $$ $CHILD || exit 1\n'
            f': > "{started}"\n'
            f'wait $CHILD\n', encoding="utf-8")
        bench.chmod(0o755)
        rig.fake_dotnet('echo "Total: 1"')

        holder = subprocess.Popen([str(bench)], env=rig.env(), cwd=str(rig.root))
        try:
            wait_for(started.exists, what="the fake bench_ab to record its benchmark child")
            benchmark_pid = rig.child_pid()
            assert benchmark_pid is not None and _alive(benchmark_pid)
            assert rig.lock_pid() == holder.pid

            holder.kill()
            holder.wait(timeout=30)
            assert _alive(benchmark_pid), "the benchmark should be orphaned, not reaped"

            queued = rig.run("test", timeout=60, DOTNET_SERIALIZED_TIMEOUT="4")
            assert queued.returncode == 124, (
                f"a peer was admitted beside an orphaned benchmark (exit {queued.returncode}); "
                f"this is the #1508 steal")
        finally:
            pid = rig.child_pid()
            if pid is not None and _alive(pid):
                _kill_tree(pid)


class TestZeroResultsGuard:
    def test_a_test_run_that_produced_no_results_is_not_a_pass(self, rig: Rig):
        """
        Pins #1273's guard. ``dotnet test --no-build`` against absent or stale binaries finds no
        test assemblies, reports nothing, and exits 0 — the worst false green available here.
        So does a ``--filter`` that matches nothing.
        """
        rig.fake_dotnet("exit 0")

        result = rig.run("test", "--filter", "MatchesNothing")

        assert result.returncode == 3
        assert "produced NO test results" in result.stderr

    def test_a_run_with_results_passes_through_unchanged(self, rig: Rig):
        """The positive control: the guard must not fire on an honest run."""
        rig.fake_dotnet('echo "Passed! - Failed: 0, Passed: 12, Total: 12"')

        result = rig.run("test")

        assert result.returncode == 0
        assert "Total: 12" in result.stdout

    def test_list_tests_is_exempt_because_no_results_is_its_correct_output(self, rig: Rig):
        rig.fake_dotnet('echo "SomeNamespace.SomeTest"')

        result = rig.run("test", "--list-tests")

        assert result.returncode == 0

    def test_a_nonzero_exit_is_reported_verbatim(self, rig: Rig):
        """The wrapper is a drop-in for dotnet: a real failure keeps its own exit code."""
        rig.fake_dotnet('echo "Failed! - Failed: 3, Passed: 9, Total: 12"\nexit 1')

        result = rig.run("test")

        assert result.returncode == 1


class TestLogging:
    def test_output_is_tee_d_to_the_rotating_deque(self, rig: Rig):
        rig.fake_dotnet('echo "Total: 1"\necho "to stderr" >&2')

        result = rig.run("test")

        assert result.returncode == 0
        log = rig.latest_log()
        assert log is not None
        text = log.read_text(encoding="utf-8")
        assert "Total: 1" in text
        assert "to stderr" in text, "stderr is merged into the log (2>&1) so failures are in it"

    def test_the_deque_rotates_over_three_slots(self, rig: Rig):
        rig.fake_dotnet('echo "Total: 1"')
        for _ in range(4):
            assert rig.run("test").returncode == 0

        slots = sorted(p.name for p in rig.logs.glob("dotnet-serialized-[0-9].log"))
        assert slots == [
            "dotnet-serialized-0.log", "dotnet-serialized-1.log", "dotnet-serialized-2.log"]


class TestStallWatchdog:
    """
    The hung-holder watchdog (#1481).

    On 2026-08-12 a holder wedged the lock for **2h26m**: the driver process was still alive
    after its run had printed a summary, so ``is_lock_stale`` correctly refused to reclaim
    (a live child is exactly what the lock protects) and every queued agent died at the
    45-minute waiter timeout. The wrapper judges progress by the tee log's mtime rather than by
    wall clock, because a cap tight enough to catch the hang would kill honest 17-minute runs.

    The load-bearing constraint, and the reason the threshold is measured rather than guessed:
    a GREEN ``dotnet test`` is almost silent. Measured over a full Sharpy.Compiler.Tests run at
    f20e180dc — 13,722 passed, 14m40s — the tee log received 863 bytes in FOUR writes, with a
    maximum quiet gap of **576 s** between them. Anything under ~10 minutes would kill healthy
    runs.
    """

    WATCHDOG_KNOBS = dict(
        DOTNET_SERIALIZED_STALL_SECONDS="3",
        DOTNET_SERIALIZED_WATCHDOG_POLL="1",
        DOTNET_SERIALIZED_WATCHDOG_GRACE="1",
    )

    def test_a_stalled_child_is_killed_and_the_run_exits_4(self, rig: Rig):
        """Cell (a): output stops, the child stays alive, nobody is left waiting on it."""
        rig.fake_dotnet('echo "Total: 1"\nsleep 120\n')

        result = rig.run("test", timeout=45, **self.WATCHDOG_KNOBS)

        assert result.returncode == 4, f"stderr={result.stderr!r}"
        assert "watchdog" in result.stderr.lower()
        assert "must not be treated as a pass" in result.stderr
        log = rig.latest_log()
        assert log is not None and "watchdog" in log.read_text(encoding="utf-8").lower(), (
            "the kill must be in the LOG too — the log is what an agent reads afterwards")

    def test_a_child_that_swallows_sigterm_and_exits_0_still_reports_failure(self, rig: Rig):
        """
        Cell (b) — the incident replay, and the reason a naive watchdog is WORSE than the wedge.

        ``dotnet test`` handles SIGTERM and exits 0: measured on the 2026-08-12 incident at 181
        failures with EXIT=0. So a watchdog that kills the child and then trusts its exit code
        converts a wedged FAILING run into a reported pass. The exit code is forced, not read.

        Mutation test (repo rule: break the guarded thing and watch it fail): removing the
        forced ``EXIT_CODE=4`` assignment — leaving the watchdog to kill and report but not to
        override the verdict — turns all four kill cells RED, this one at exit 0, the exact
        false green from the incident. Reverted.
        """
        rig.fake_dotnet(
            'trap "exit 0" TERM\n'
            'echo "Total: 1"\n'
            'sleep 120 &\n'
            'wait $!\n'
            'exit 0\n'  # reports success however it was interrupted — that is the mask
        )

        result = rig.run("test", timeout=45, **self.WATCHDOG_KNOBS)

        assert result.returncode == 4, (
            f"a killed run reported success (exit {result.returncode}) — this is #1481's "
            f"SIGTERM mask; stderr={result.stderr!r}")

    def test_the_forced_exit_beats_the_zero_results_guard(self, rig: Rig):
        """
        A watchdog-killed ``test`` run usually ALSO has no "Total:" line, so the #1273
        zero-results guard would label it exit 3 — "zero tests ran" — which misattributes a kill
        to a build problem and sends the reader hunting the wrong cause. 4 must win.
        """
        rig.fake_dotnet('sleep 120\n')

        result = rig.run("test", timeout=45, **self.WATCHDOG_KNOBS)

        assert result.returncode == 4, f"got {result.returncode} (3 = zero-results guard won)"

    def test_an_honest_slow_run_is_never_killed(self, rig: Rig):
        """
        Cell (c), the negative control. A run that keeps producing output must survive
        indefinitely — a watchdog that cannot tell quiet-but-working from wedged is a
        fleet-wide outage, not a fix.
        """
        rig.fake_dotnet(
            'i=0\n'
            'while [ $i -lt 8 ]; do echo "progress $i"; sleep 1; i=$((i + 1)); done\n'
            'echo "Total: 1"\n'
        )

        result = rig.run("test", timeout=45, DOTNET_SERIALIZED_STALL_SECONDS="4",
                         DOTNET_SERIALIZED_WATCHDOG_POLL="1",
                         DOTNET_SERIALIZED_WATCHDOG_GRACE="1")

        assert result.returncode == 0, f"an honest run was killed: stderr={result.stderr!r}"
        assert "progress 7" in result.stdout

    def test_a_fast_run_is_not_delayed_by_the_monitor(self, rig: Rig):
        """The monitor must be torn down with the run, not waited out."""
        rig.fake_dotnet('echo "Total: 1"')

        started = time.time()
        result = rig.run("test", timeout=45, DOTNET_SERIALIZED_STALL_SECONDS="600",
                         DOTNET_SERIALIZED_WATCHDOG_POLL="20")
        elapsed = time.time() - started

        assert result.returncode == 0
        assert elapsed < 15, f"the run waited on its own monitor ({elapsed:.1f}s)"

    def test_the_lock_is_released_after_a_watchdog_kill(self, rig: Rig):
        """
        The watchdog does not release the lock itself — killing the child ends the pipeline and
        the ordinary cleanup path runs, so there is exactly one release site and no
        double-release race. What matters to the fleet is only that the lock DOES come free.
        """
        rig.fake_dotnet('echo "Total: 1"\nsleep 120\n')

        assert rig.run("test", timeout=45, **self.WATCHDOG_KNOBS).returncode == 4

        assert not rig.lock_dir.exists(), "a watchdog kill must not leak the lock it was fixing"

    def test_the_stall_default_carries_its_measurement(self):
        """
        The threshold is a measured number and must stay attributable. 3 x 576 s = 1728 s, from
        a full Sharpy.Compiler.Tests run at f20e180dc. A successor who wants to tighten it needs
        to know it was measured on a nearly-silent green suite, not guessed — otherwise the
        first "obviously too generous" edit reintroduces killing honest runs.
        """
        source = WRAPPER.read_text(encoding="utf-8")

        assert "DOTNET_SERIALIZED_STALL_SECONDS:-1728" in source
        assert "576" in source and "measured" in source
        assert "stat -f %m" in source and "stat -c %Y" in source, (
            "the watchdog's mtime read must keep the two-spelling stat fallback; a single "
            "spelling prints nothing on the other platform, which reads as 'no progress'")


def test_the_portable_stat_fallback_resolves_on_this_platform(tmp_path: Path):
    """
    There is no portable ``stat`` spelling: macOS/BSD wants ``stat -f %m``, GNU/Linux wants
    ``stat -c %Y``, and each rejects the other's flag. This suite runs on BOTH (developer macOS,
    ubuntu CI), so the two-spelling fallback any shell-side mtime read must use is executed here
    rather than assumed. A single spelling does not error loudly on the wrong platform — it
    prints nothing, which downstream reads as a legitimate value.
    """
    probe = tmp_path / "probe.txt"
    probe.write_text("x", encoding="utf-8")

    result = subprocess.run(
        ["/bin/sh", "-c", f'stat -f %m "{probe}" 2>/dev/null || stat -c %Y "{probe}" 2>/dev/null'],
        capture_output=True, text=True)

    assert result.stdout.strip().isdigit(), (
        f"neither stat spelling produced an mtime here: {result!r}")
