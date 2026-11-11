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
import re
import shutil
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
        #: Process-group ids of everything started through :meth:`popen` (see there).
        self.groups: list[int] = []

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

    def popen(
        self,
        argv: list[str],
        env: Optional[dict[str, str]] = None,
        new_group: bool = True,
        **kwargs,
    ) -> subprocess.Popen:
        """
        Start *argv* as the leader of a NEW process group and record it for teardown.

        Every process the wrapper forks — its watchdog subshell, the dotnet child, ``tee``, a
        stub's ``sleep`` grandchild — inherits that group and keeps it after being reparented to
        init, so the fixture's ``killpg`` reaps everything a probe started even when the probe
        fails. Before this, a red run (or any run against a wrapper without the #2076 fix) left
        orphaned watchdogs looping ``sleep 30`` forever — eight were found on the machine from
        this file's own runs on 2026-09-30. Nothing outside the group is ever signalled.

        ``new_group=False`` keeps the test runner's group, for a probe that SIGSTOPs a member:
        once the wrapper dies its own group is ORPHANED (every member's parent is init), and the
        kernel sends SIGHUP + SIGCONT to an orphaned group that contains a stopped process — the
        stopped watchdog would be hung up and killed, and the probe would pass against the bug.
        The runner's group is never orphaned. Such a probe must kill its own pids in ``finally``.
        """
        proc = subprocess.Popen(
            argv,
            env=env if env is not None else self.env(),
            cwd=str(self.root),
            start_new_session=new_group,
            **kwargs,
        )
        if new_group:
            self.groups.append(proc.pid)
        return proc

    def run(self, *args: str, timeout: float = 120, **env_overrides: str):
        proc = self.popen(
            [str(WRAPPER), *args],
            env=self.env(**env_overrides),
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        )
        try:
            out, err = proc.communicate(timeout=timeout)
        except subprocess.TimeoutExpired:
            proc.kill()
            proc.communicate()
            raise
        return subprocess.CompletedProcess(proc.args, proc.returncode, out, err)

    def spawn(self, *args: str, **env_overrides: str) -> subprocess.Popen:
        return self.popen(
            [str(WRAPPER), *args],
            env=self.env(**env_overrides),
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        )

    def spawn_sleeper(self) -> subprocess.Popen:
        """A live process to stand in for a lock owner; reaped with the rig's groups."""
        return self.popen(["sleep", "120"])

    # -- lock introspection -----------------------------------------------------------

    def lock_pid(self) -> Optional[int]:
        return _read_pid(self.lock_dir / "pid")

    def child_pid(self) -> Optional[int]:
        return _read_pid(self.lock_dir / "child")

    def latest_log(self) -> Optional[Path]:
        """The log the pointer file names: the NEWEST invocation's (#2458)."""
        pointer = self.logs / "dotnet-serialized-latest.path"
        return (
            Path(pointer.read_text(encoding="utf-8").strip())
            if pointer.exists()
            else None
        )


LOG_LINE = re.compile(r"^dotnet-serialized: log -> (.+)$", re.MULTILINE)


def log_of(stdout: str) -> Path:
    """The log path a wrapper invocation printed for ITSELF; fails if it printed none."""
    found = LOG_LINE.findall(stdout)
    assert len(found) == 1, (
        f"expected exactly one log line, got {found!r} in {stdout!r}"
    )
    return Path(found[0])


@pytest.fixture()
def rig(tmp_path: Path):
    made = Rig(tmp_path)
    yield made
    # Kill anything a probe deliberately orphaned, so a failure cannot leak a live process.
    for pidfile in ("child", "pid"):
        pid = _read_pid(made.lock_dir / pidfile)
        if pid is not None and pid != os.getpid() and _alive(pid):
            _kill_tree(pid)
    # ...and every process group the rig started, which is what catches the rest: a watchdog
    # subshell, tee, a stub's grandchild. None of them is named by a pidfile.
    for pgid in made.groups:
        try:
            os.killpg(pgid, signal.SIGKILL)
        except OSError:
            pass  # the whole group already exited — the normal case for a green probe


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


def wait_for(
    predicate: Callable[[], bool], timeout: float = 30, what: str = "condition"
) -> None:
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


LOCKERS = ["flock", "python3", "perl"]


def _overlaps(events: Path) -> list:
    intervals = sorted(
        (float(start), float(stop))
        for _, start, stop in (
            line.split() for line in events.read_text(encoding="utf-8").splitlines()
        )
    )
    return intervals, [(a, b) for a, b in zip(intervals, intervals[1:]) if b[0] < a[1]]


def _timing_stub(rig: Rig, events: Path, seconds: str) -> None:
    rig.fake_dotnet(
        f'start=$(python3 -c "import time;print(repr(time.time()))")\n'
        f"sleep {seconds}\n"
        f'stop=$(python3 -c "import time;print(repr(time.time()))")\n'
        f'echo "RAN $start $stop" >> "{events}"\n'
        f'echo "Total: 1"\n'
    )


class _HeldFlock:
    """This test process holds the wrapper's flock, as a live holder would."""

    def __init__(self, rig: Rig) -> None:
        import fcntl

        path = rig.home / ".claude" / "locks" / "dotnet.flock"
        path.parent.mkdir(parents=True, exist_ok=True)
        self._fcntl = fcntl
        self._fd = os.open(str(path), os.O_WRONLY | os.O_CREAT | os.O_APPEND, 0o644)
        fcntl.flock(self._fd, fcntl.LOCK_EX)

    def release(self) -> None:
        if self._fd is not None:
            self._fcntl.flock(self._fd, self._fcntl.LOCK_UN)
            os.close(self._fd)
            self._fd = None


class TestMutualExclusion:
    """
    The contract: at most one dotnet runs at a time (#2458). The lock is an ``flock(2)`` held by
    the wrapper for its whole run, so these probes check the contract and the lock's edges, not
    a particular staleness rule. The mkdir lock it replaced admitted two runs 0.06-2.8 ms apart
    on CI three times in two days (and, on ubuntu-26.04 runners, two per waiter tick).
    """

    @pytest.mark.parametrize("locker", LOCKERS)
    def test_eight_concurrent_wrappers_never_overlap(self, rig: Rig, locker: str):
        """
        The 2026-08-12 measurement, made permanent: eight concurrent wrappers, each running a
        0.5 s fake dotnet, must run one at a time. Run once per flock tool, because macOS has no
        flock(1) and takes the python3 or perl path, which a Linux CI would otherwise never run.
        """
        if shutil.which(locker) is None:
            pytest.skip(f"{locker} is not installed here")
        events = rig.root / "events.txt"
        _timing_stub(rig, events, "0.5")

        procs = [
            rig.spawn(
                "test",
                f"--peer{i}",
                DOTNET_SERIALIZED_TIMEOUT="120",
                DOTNET_SERIALIZED_LOCKERS=locker,
            )
            for i in range(8)
        ]
        stderrs = [proc.communicate(timeout=180)[1] for proc in procs]

        assert all(p.returncode == 0 for p in procs), (
            [p.returncode for p in procs],
            stderrs,
        )
        intervals, overlaps = _overlaps(events)
        assert len(intervals) == 8, f"expected 8 runs, saw {len(intervals)}"
        assert not overlaps, f"concurrent dotnet runs admitted: {overlaps}"

    def test_a_held_lock_blocks_a_peer_until_it_is_released(self, rig: Rig):
        """Positive control for the probe above: the flock really is what queues a peer."""
        rig.fake_dotnet('echo "Total: 1"')
        held = _HeldFlock(rig)
        try:
            proc = rig.spawn("test", DOTNET_SERIALIZED_TIMEOUT="60")
            time.sleep(2)
            assert proc.poll() is None, "a peer ran while the lock was held"
        finally:
            held.release()
        out, err = proc.communicate(timeout=60)
        assert proc.returncode == 0, err
        assert "Total: 1" in out

    def test_a_waiter_gives_up_at_its_timeout_with_124(self, rig: Rig):
        rig.fake_dotnet('echo "Total: 1"')
        held = _HeldFlock(rig)
        try:
            started = time.time()
            result = rig.run("test", DOTNET_SERIALIZED_TIMEOUT="2")
            waited = time.time() - started
        finally:
            held.release()
        assert result.returncode == 124, result.stderr
        assert "Timed out waiting for dotnet lock" in result.stderr
        assert 1.5 <= waited < 20, f"waited {waited:.1f}s on a 2 s timeout"

    def test_leftover_files_of_a_dead_holder_do_not_block(self, rig: Rig):
        """
        A holder killed without releasing leaves its diagnostics behind. With no live child they
        name nothing that still runs, so the next wrapper discards them at once instead of
        judging them for a grace period.
        """
        rig.lock_dir.mkdir(parents=True)
        (rig.lock_dir / "pid").write_text(f"{dead_pid()}\n", encoding="utf-8")
        (rig.lock_dir / "child").write_text(f"{dead_pid()}\n", encoding="utf-8")
        rig.fake_dotnet('echo "Total: 1"')

        started = time.time()
        result = rig.run("test", DOTNET_SERIALIZED_TIMEOUT="30")

        assert result.returncode == 0, result.stderr
        assert "stale dotnet lock files" in result.stderr
        assert time.time() - started < 10

    def test_files_naming_a_live_owner_without_the_flock_still_block(self, rig: Rig):
        """
        A live owner recorded in the files but not holding the flock is a wrapper from before
        the flock, still running during the rollout. Queue behind it rather than beside it.
        """
        owner = rig.spawn_sleeper()
        rig.lock_dir.mkdir(parents=True)
        (rig.lock_dir / "pid").write_text(f"{owner.pid}\n", encoding="utf-8")
        rig.fake_dotnet('echo "Total: 1"')

        result = rig.run("test", DOTNET_SERIALIZED_TIMEOUT="3")

        assert result.returncode == 124, result.stderr
        assert rig.lock_pid() == owner.pid

    def test_a_dead_owners_lock_is_taken_by_exactly_one_of_many_waiters(self, rig: Rig):
        """
        The takeover herd (#2458): eight waiters that all find the same dead holder's files.
        Under the mkdir lock each removed the lock by name and re-created it, and a late removal
        deleted an early winner's new lock, so two ran. Exactly one may discard the files.
        """
        rig.lock_dir.mkdir(parents=True)
        (rig.lock_dir / "pid").write_text(f"{dead_pid()}\n", encoding="utf-8")
        events = rig.root / "events.txt"
        _timing_stub(rig, events, "0.3")

        procs = [
            rig.spawn("test", f"--peer{i}", DOTNET_SERIALIZED_TIMEOUT="120")
            for i in range(8)
        ]
        stderrs = [proc.communicate(timeout=180)[1] for proc in procs]

        assert all(p.returncode == 0 for p in procs), [p.returncode for p in procs]
        intervals, overlaps = _overlaps(events)
        assert len(intervals) == 8, f"expected 8 runs, saw {len(intervals)}"
        assert not overlaps, f"concurrent dotnet runs admitted: {overlaps}"
        assert sum("stale dotnet lock files" in err for err in stderrs) == 1, stderrs

    def test_a_dotnet_grandchild_does_not_keep_the_lock(self, rig: Rig):
        """
        dotnet leaves build servers (MSBuild nodes, VBCSCompiler) running for minutes after a
        build. If they inherited the lock's fd they would hold the lock with them and every
        peer would wait out its TIMEOUT behind a finished build.
        """
        rig.fake_dotnet(
            "(sleep 60 </dev/null >/dev/null 2>&1 &)\n"
            'if ( : >&9 ) 2>/dev/null; then echo "FD9-INHERITED"; fi\n'
            'echo "Total: 1"\n'
        )

        first = rig.run("test")
        second = rig.run("test", DOTNET_SERIALIZED_TIMEOUT="5")

        assert first.returncode == 0, first.stderr
        assert "FD9-INHERITED" not in first.stdout
        assert second.returncode == 0, (
            f"a finished run's grandchild kept the lock (exit {second.returncode})"
        )

    def test_a_holder_never_removes_files_it_no_longer_owns(self, rig: Rig):
        """
        Release removes the diagnostics only while they are this wrapper's. An unconditional
        ``rm -rf`` on exit deleted whatever sat at the path, a later owner's included.
        """
        other = rig.spawn_sleeper()
        rig.fake_dotnet(
            f'echo "{other.pid}" > "{rig.lock_dir / "pid"}"\necho "Total: 1"\n'
        )

        result = rig.run("test")

        assert result.returncode == 0, result.stderr
        assert rig.lock_pid() == other.pid, "the wrapper removed files it did not own"


#: A frozen copy of the wrapper from before the flock (dev @ fd876b842): it takes the lock with
#: `mkdir $LOCK_DIR`. Copies of it keep running in worktrees that have not rebased, so the new
#: wrapper must exclude them too.
PRE_FLOCK_WRAPPER = (
    Path(__file__).resolve().parent
    / "fixtures"
    / "pre_flock_wrapper"
    / "dotnet-serialized"
)


class TestMixedFleet:
    """
    New (flock) and pre-flock (mkdir) wrappers on one machine during the rollout (#2458). The new
    wrapper owns ``$LOCK_DIR`` the way an old one does: it never deletes and re-creates it by
    name, renames an abandoned one aside, creates it exclusively, and gives a pidless directory
    one beat to gain its pid (an old wrapper between its ``mkdir`` and its pid write).
    """

    def _spawn(self, rig: Rig, old: bool, tag: str) -> subprocess.Popen:
        wrapper = PRE_FLOCK_WRAPPER if old else WRAPPER
        return rig.popen(
            [str(wrapper), "test", tag],
            env=rig.env(DOTNET_SERIALIZED_TIMEOUT="120"),
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        )

    @pytest.mark.parametrize("old_first", [True, False], ids=["old-first", "new-first"])
    def test_old_and_new_wrappers_never_overlap(self, rig: Rig, old_first: bool):
        events = rig.root / "events.txt"
        _timing_stub(rig, events, "0.3")
        procs = []
        for round_ in range(4):
            # Each round: one of each kind, the first a beat ahead, then a second pair in the
            # other order, so every handover between kinds happens in both directions.
            for old in (old_first, not old_first, not old_first, old_first):
                procs.append(
                    self._spawn(rig, old, f"--r{round_}-{'old' if old else 'new'}")
                )
                time.sleep(0.05)
        stderrs = [p.communicate(timeout=300)[1] for p in procs]

        assert all(p.returncode == 0 for p in procs), (
            [p.returncode for p in procs],
            stderrs,
        )
        intervals, overlaps = _overlaps(events)
        assert len(intervals) == len(procs)
        assert not overlaps, f"old and new wrappers ran together: {overlaps}"

    def test_a_pidless_dir_that_gains_a_live_pid_within_the_beat_is_not_taken(
        self, rig: Rig
    ):
        """An old wrapper between ``mkdir`` and its pid write must not be moved aside."""
        rig.lock_dir.mkdir(parents=True)
        owner = rig.spawn_sleeper()
        rig.fake_dotnet('echo "Total: 1"')

        proc = rig.spawn("test", DOTNET_SERIALIZED_TIMEOUT="4")
        time.sleep(0.4)  # inside the one-second beat
        (rig.lock_dir / "pid").write_text(f"{owner.pid}\n", encoding="utf-8")
        _, stderr = proc.communicate(timeout=60)

        assert proc.returncode == 124, (
            f"took a live old wrapper's lock (exit {proc.returncode})"
        )
        assert rig.lock_pid() == owner.pid

    def test_a_dir_that_stays_pidless_is_taken_after_the_beat(self, rig: Rig):
        """Positive control for the probe above: an abandoned pidless directory does not wedge."""
        rig.lock_dir.mkdir(parents=True)
        rig.fake_dotnet('echo "Total: 1"')

        started = time.time()
        result = rig.run("test", DOTNET_SERIALIZED_TIMEOUT="30")

        assert result.returncode == 0, result.stderr
        assert 1 <= time.time() - started < 15

    def test_losing_the_create_to_an_old_wrapper_means_waiting(self, rig: Rig):
        """
        Window (b): an old wrapper's ``mkdir`` lands between the new wrapper's verdict "free"
        and its own create. The race is microseconds wide, so a ``mkdir`` shim opens it by hand:
        on the first create of ``$LOCK_DIR`` it makes the directory itself with a live owner's
        pid, exactly as the old wrapper would, then reports what the real tool reports — EEXIST
        for an exclusive create, success for ``mkdir -p``. The new wrapper must then wait, not
        run. ``DOTNET_SERIALIZED_MKDIR_TOOL=mkdir`` routes the create through the binary (the fallback where
        neither python3 nor perl runs), which is the path the shim can reach.
        """
        owner = rig.spawn_sleeper()
        real_mkdir = shutil.which("mkdir")
        marker = rig.root / "raced"
        shim = rig.bin / "mkdir"
        shim.write_text(
            "#!/bin/sh\n"
            'for a; do target="$a"; done\n'
            f'if [ "$target" = "{rig.lock_dir}" ] && [ ! -e "{marker}" ]; then\n'
            f'  : > "{marker}"\n'
            f'  "{real_mkdir}" "$target"\n'
            f'  echo {owner.pid} > "$target/pid"\n'
            '  case " $* " in *" -p "*) exit 0 ;; esac\n'
            '  echo "mkdir: cannot create directory: File exists" >&2\n'
            "  exit 1\n"
            "fi\n"
            f'exec "{real_mkdir}" "$@"\n',
            encoding="utf-8",
        )
        shim.chmod(0o755)
        rig.fake_dotnet('echo "Total: 1"')

        result = rig.run(
            "test", DOTNET_SERIALIZED_TIMEOUT="4", DOTNET_SERIALIZED_MKDIR_TOOL="mkdir"
        )

        assert marker.exists(), "positive control: the shim's race never ran"
        assert result.returncode == 124, (
            f"ran beside an old wrapper that won the create (exit {result.returncode})"
        )
        assert rig.lock_pid() == owner.pid

    def test_release_without_a_holder_leaves_a_pidless_dir_alone(self, rig: Rig):
        """
        A pidless directory may be an old wrapper between ``mkdir`` and its pid write. An
        anonymous ``--release-lock`` matched "no pid" to "no holder" and removed it.
        """
        rig.lock_dir.mkdir(parents=True)

        result = rig.run("--release-lock")

        assert result.returncode == 2
        assert rig.lock_dir.exists()


class TestSignalledHolder:
    """
    A caller's timeout sends SIGTERM to the wrapper. Bash defers a signal trap while a FOREGROUND
    pipeline runs but runs the EXIT trap at once, so the old foreground ``dotnet | tee`` released
    the lock while dotnet kept running, and the next waiter started a second dotnet.
    """

    def test_sigterm_ends_dotnet_before_the_lock_is_released(self, rig: Rig):
        events = rig.root / "events.txt"
        started = rig.root / "first_started"
        now = 'python3 -c "import time;print(repr(time.time()))"'
        # The first run behaves like `dotnet test` on SIGTERM: it takes a moment to wind down.
        rig.fake_dotnet(
            'case " $* " in\n'
            '  *" --first "*)\n'
            f"    start=$({now})\n"
            f'    trap \'sleep 1; echo "RAN $start $({now})" >> "{events}"; exit 0\' TERM\n'
            f'    : > "{started}"\n'
            # Unsignalled, it runs 5 s and still records itself, so an overlap stays visible.
            "    sleep 5 &\n"
            "    wait\n"
            f'    echo "RAN $start $({now})" >> "{events}"\n'
            "    ;;\n"
            "  *)\n"
            f"    start=$({now}); sleep 0.2\n"
            f'    echo "RAN $start $({now})" >> "{events}"\n'
            '    echo "Total: 1" ;;\n'
            "esac\n"
        )
        first = rig.spawn("test", "--first", DOTNET_SERIALIZED_TIMEOUT="60")
        wait_for(started.exists, what="the first fake dotnet to start")
        peer = rig.spawn("test", "--second", DOTNET_SERIALIZED_TIMEOUT="60")
        time.sleep(0.5)

        first.send_signal(signal.SIGTERM)
        first.communicate(timeout=60)
        peer_out, peer_err = peer.communicate(timeout=60)

        intervals, overlaps = _overlaps(events)
        assert not overlaps, (
            f"a peer ran beside a SIGTERMed holder's dotnet: {overlaps}"
        )
        # The caller asked the build to stop: dotnet is ended at once (its TERM handler ran and
        # recorded its run) and the wrapper exits as a signalled run, 128 + 15.
        assert len(intervals) == 2, intervals
        assert first.returncode == 143, first.returncode
        assert peer.returncode == 0, peer_err


class TestHolderStatusLiveness:
    def test_a_lock_only_hold_is_reported_held_not_free(self, rig: Rig):
        """bench_ab's hold keeps no flock; calling it FREE invites someone to clear it."""
        assert rig.run("--acquire-lock", str(os.getpid())).returncode == 0
        try:
            out = rig.run("--holder-status").stdout
            assert "lock:    HELD" in out, out
            assert "FREE" not in out
        finally:
            rig.run("--release-lock", str(os.getpid()))

    def test_a_dead_holders_files_are_reported_free(self, rig: Rig):
        rig.lock_dir.mkdir(parents=True)
        (rig.lock_dir / "pid").write_text(f"{dead_pid()}\n", encoding="utf-8")
        out = rig.run("--holder-status").stdout
        assert "lock:    FREE" in out, out

    @pytest.mark.parametrize("locker", ["python3", "perl"])
    def test_the_status_line_appears_without_flock_1(self, rig: Rig, locker: str):
        if shutil.which(locker) is None:
            pytest.skip(f"{locker} is not installed here")
        assert rig.run("--acquire-lock", str(os.getpid())).returncode == 0
        try:
            out = rig.run("--holder-status", DOTNET_SERIALIZED_LOCKERS=locker).stdout
            assert "lock:    HELD" in out, out
        finally:
            rig.run("--release-lock", str(os.getpid()))


class TestCannotLock:
    def test_an_unopenable_lock_file_exits_125(self, rig: Rig):
        (rig.home / ".claude" / "locks" / "dotnet.flock").mkdir(parents=True)
        rig.fake_dotnet('echo "Total: 1"')
        result = rig.run("test")
        assert result.returncode == 125, result.stderr
        assert "cannot open" in result.stderr

    def test_no_working_flock_tool_exits_125(self, rig: Rig):
        rig.fake_dotnet('echo "Total: 1"')
        result = rig.run("test", DOTNET_SERIALIZED_LOCKERS="no-such-tool")
        assert result.returncode == 125, result.stderr
        assert "no working flock tool" in result.stderr


class TestOrphanedChild:
    def test_an_orphaned_dotnet_child_keeps_the_lock(self, rig: Rig):
        """
        Pins 58c82ffa5. Judging staleness by the owner SHELL fails open in exactly the case the
        lock exists for: kill an agent (or let it hit a usage limit) and its dotnet/testhost
        children are reparented to init and keep consuming 5-10 GB while the shell that started
        them is gone. The next wrapper read "owner PID gone", removed the lock, and admitted a
        SECOND concurrent run beside the orphan.

        Mutation-tested twice, because the two halves of the fix fail differently:
        removing the child-liveness branch (now in ``wait_out_previous_holder``; literal
        pre-58c82ffa5 behavior) reds this at the steal — "lock stolen from an orphaned child (exit 0)";
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
        wait_for(
            started.exists, what="the fake dotnet to reach its hang (exec has happened)"
        )
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
            f"stdout={queued.stdout!r}"
        )

        # Once the orphan really is gone the lock is reclaimable — the fix must not wedge
        # the fleet permanently, which would be the opposite failure.
        _kill_tree(orphan)
        wait_for(lambda: not _alive(orphan), what="the orphan to die")
        reclaimed = rig.run("test", timeout=60, DOTNET_SERIALIZED_TIMEOUT="30")
        assert reclaimed.returncode == 0, reclaimed.stderr
        assert "Total: 1" in reclaimed.stdout


class TestOrphanReaper:
    """
    #2458 (audit §5). A holder killed mid-run leaves its dotnet child to init, and its watchdog
    exits with it (#2076), so before the reaper the orphan held the lock until it ended on its
    own and every waiter died at TIMEOUT (124). A waiter now applies the stall test on the dead
    holder's behalf. The two negatives are the reaper's limits: a live holder's run, and a run
    whose log is still moving, are never killed.
    """

    REAPER_KNOBS = dict(
        DOTNET_SERIALIZED_STALL_SECONDS="2",
        DOTNET_SERIALIZED_WATCHDOG_GRACE="1",
    )

    def _orphan(
        self, rig: Rig, body: str, kill_holder: bool = True
    ) -> tuple[int, subprocess.Popen]:
        started = rig.root / "child_started"
        rig.fake_dotnet(
            f'case " $* " in *" --hang "*) : > "{started}"; {body} ;; esac\n'
            'echo "Total: 1"\n'
        )
        holder = rig.spawn("test", "--hang", DOTNET_SERIALIZED_TIMEOUT="120")
        wait_for(started.exists, what="the fake dotnet to reach its hang")
        child = rig.child_pid()
        assert child is not None and _alive(child)
        if kill_holder:
            holder.kill()
            holder.wait(timeout=30)
            wait_for(lambda: not _alive(holder.pid), what="the holder shell to die")
            assert rig.lock_pid() == holder.pid, (
                "the dead holder must still own the lock"
            )
        return child, holder

    def test_a_dead_holders_silent_child_is_reaped(self, rig: Rig):
        """
        Mutation-tested: making ``reap_orphan`` return 1 before it kills reds this at
        "orphan not reaped (exit 124)"; restored, green.
        """
        child, _ = self._orphan(rig, "sleep 120")
        time.sleep(3)  # the holder's log is now silent past STALL_SECONDS

        peer = rig.run(
            "test", timeout=60, DOTNET_SERIALIZED_TIMEOUT="20", **self.REAPER_KNOBS
        )

        assert peer.returncode == 0, (
            f"orphan not reaped (exit {peer.returncode}); stderr={peer.stderr!r}"
        )
        assert "reaping orphaned dotnet PID %d" % child in peer.stderr
        wait_for(lambda: not _alive(child), what="the reaped child to be gone")
        assert "Total: 1" in log_of(peer.stdout).read_text(encoding="utf-8")

    def test_a_live_holders_silent_child_is_never_reaped(self, rig: Rig):
        child, holder = self._orphan(rig, "sleep 120", kill_holder=False)
        time.sleep(3)

        peer = rig.run(
            "test", timeout=60, DOTNET_SERIALIZED_TIMEOUT="4", **self.REAPER_KNOBS
        )

        assert peer.returncode == 124, (
            f"a live holder's run was taken (exit {peer.returncode})"
        )
        assert _alive(child) and _alive(holder.pid)
        assert "reaping" not in peer.stderr

    def test_a_dead_holders_child_whose_log_moves_is_never_reaped(self, rig: Rig):
        child, _ = self._orphan(rig, "while :; do echo tick; sleep 0.3; done")

        peer = rig.run(
            "test", timeout=60, DOTNET_SERIALIZED_TIMEOUT="6", **self.REAPER_KNOBS
        )

        assert peer.returncode == 124, (
            f"a child with a moving log was killed (exit {peer.returncode})"
        )
        assert _alive(child)
        assert "reaping" not in peer.stderr


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

    def test_release_lock_requires_a_holder(self, rig: Rig):
        """With no holder named, a release could only guess whose lock it was ending."""
        assert rig.run("--acquire-lock", str(os.getpid())).returncode == 0
        try:
            result = rig.run("--release-lock")
            assert result.returncode == 2
            assert rig.lock_pid() == os.getpid()
        finally:
            rig.run("--release-lock", str(os.getpid()))

    def test_acquire_lock_holds_until_released(self, rig: Rig):
        """The wrapper exits at once; the lock must outlive it until --release-lock."""
        rig.fake_dotnet('echo "Total: 1"')
        assert rig.run("--acquire-lock", str(os.getpid())).returncode == 0
        try:
            queued = rig.run("test", DOTNET_SERIALIZED_TIMEOUT="2")
            assert queued.returncode == 124, queued.stderr
        finally:
            assert rig.run("--release-lock", str(os.getpid())).returncode == 0
        admitted = rig.run("test", DOTNET_SERIALIZED_TIMEOUT="10")
        assert admitted.returncode == 0, admitted.stderr

    def test_acquire_lock_is_freed_when_its_holder_dies(self, rig: Rig):
        """A holder SIGKILLed without releasing must not wedge every peer for TIMEOUT."""
        rig.fake_dotnet('echo "Total: 1"')
        holder = rig.spawn_sleeper()
        assert rig.run("--acquire-lock", str(holder.pid)).returncode == 0
        queued = rig.run("test", DOTNET_SERIALIZED_TIMEOUT="2")
        assert queued.returncode == 124, "positive control: the lock was held"

        holder.kill()
        holder.wait(timeout=30)
        admitted = rig.run("test", DOTNET_SERIALIZED_TIMEOUT="10")

        assert admitted.returncode == 0, admitted.stderr
        assert "stale dotnet lock files" in admitted.stderr

    def test_lock_generation_is_monotonic_across_acquisitions(self, rig: Rig):
        """
        #1420's interruption detector reads this before and after a round. It lives OUTSIDE the
        lock dir because every release removes that directory, so a counter inside it resets and
        can never be monotonic.
        """
        before = int(rig.run("--lock-generation").stdout.strip())
        rig.fake_dotnet('echo "Total: 1"')
        assert rig.run("test").returncode == 0
        assert rig.run("test").returncode == 0
        after = int(rig.run("--lock-generation").stdout.strip())

        assert after == before + 2

    def test_lock_only_verbs_consume_no_log_slot(self, rig: Rig):
        """
        The verbs exit before the tee pipeline. If they wrote a log or moved the pointer, a
        bench_ab round (two acquire/release pairs each) would repoint the file an agent reads.
        """
        assert rig.run("--acquire-lock", str(os.getpid())).returncode == 0
        assert rig.run("--release-lock", str(os.getpid())).returncode == 0
        assert rig.run("--lock-generation").returncode == 0

        assert not (rig.logs / "dotnet-serialized-latest.path").exists()
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
        Recording a dead child is worse than recording nothing: it replaces the pointer to
        whatever is still running under the lock, so the next holder would not wait for it.
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

    def test_a_release_with_a_live_recorded_child_keeps_the_lock(self, rig: Rig):
        """
        bench_ab releases in a ``finally``, so an exception between arms can release while its
        recorded benchmark still runs. The files stay, and a peer waits on that child.
        """
        assert rig.run("--acquire-lock", str(os.getpid())).returncode == 0
        benchmark = rig.spawn_sleeper()
        assert (
            rig.run("--record-child", str(os.getpid()), str(benchmark.pid)).returncode
            == 0
        )
        assert rig.run("--release-lock", str(os.getpid())).returncode == 0
        rig.fake_dotnet('echo "Total: 1"')

        queued = rig.run("test", DOTNET_SERIALIZED_TIMEOUT="3")
        assert queued.returncode == 124, (
            "a peer ran beside a released holder's live child"
        )

        benchmark.kill()
        benchmark.wait(timeout=30)
        admitted = rig.run("test", DOTNET_SERIALIZED_TIMEOUT="10")
        assert admitted.returncode == 0, admitted.stderr

    def test_a_recorded_child_survives_a_sigkilled_holder(self, rig: Rig):
        """
        #1508's literal scenario, red-first for this phase.

        A stand-in for bench_ab: take the lock with ``--acquire-lock``, spawn a long-running
        "benchmark dotnet", record it, then get SIGKILLed mid-arm. The orphan keeps its memory,
        so a peer must QUEUE behind it rather than be admitted beside it.

        Mutation-tested: dropping the ``echo "$child" > $LOCK_DIR/child`` line from the verb
        while leaving its validation intact makes this RED at the recording assertion (no child
        pointer is ever written). Reverted. The complementary mutation — writing the pointer but
        having the next holder ignore it — is the one recorded on
        :meth:`TestOrphanedChild.test_an_orphaned_dotnet_child_keeps_the_lock`, and that one
        reds at the steal itself; together they cover both halves of the mechanism.
        """
        started = rig.root / "bench_started"
        bench = rig.root / "fake_bench_ab.sh"
        bench.write_text(
            f"#!/bin/sh\n"
            f'"{WRAPPER}" --acquire-lock $$ || exit 1\n'
            f"sleep 300 &\n"
            f"CHILD=$!\n"
            f'"{WRAPPER}" --record-child $$ $CHILD || exit 1\n'
            f': > "{started}"\n'
            f"wait $CHILD\n",
            encoding="utf-8",
        )
        bench.chmod(0o755)
        rig.fake_dotnet('echo "Total: 1"')

        holder = rig.popen([str(bench)])
        try:
            wait_for(
                started.exists, what="the fake bench_ab to record its benchmark child"
            )
            benchmark_pid = rig.child_pid()
            assert benchmark_pid is not None and _alive(benchmark_pid)
            assert rig.lock_pid() == holder.pid

            holder.kill()
            holder.wait(timeout=30)
            assert _alive(benchmark_pid), "the benchmark should be orphaned, not reaped"

            queued = rig.run("test", timeout=60, DOTNET_SERIALIZED_TIMEOUT="4")
            assert queued.returncode == 124, (
                f"a peer was admitted beside an orphaned benchmark (exit {queued.returncode}); "
                f"this is the #1508 steal"
            )
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

    def test_list_tests_is_exempt_because_no_results_is_its_correct_output(
        self, rig: Rig
    ):
        rig.fake_dotnet('echo "SomeNamespace.SomeTest"')

        result = rig.run("test", "--list-tests")

        assert result.returncode == 0

    def test_a_nonzero_exit_is_reported_verbatim(self, rig: Rig):
        """The wrapper is a drop-in for dotnet: a real failure keeps its own exit code."""
        rig.fake_dotnet('echo "Failed! - Failed: 3, Passed: 9, Total: 12"\nexit 1')

        result = rig.run("test")

        assert result.returncode == 1


class TestLogging:
    def test_output_is_tee_d_to_the_invocations_own_log(self, rig: Rig):
        rig.fake_dotnet('echo "Total: 1"\necho "to stderr" >&2')

        result = rig.run("test")

        assert result.returncode == 0
        log = log_of(result.stdout)
        assert rig.latest_log() == log, (
            "the pointer file names the newest invocation's log"
        )
        text = log.read_text(encoding="utf-8")
        assert "Total: 1" in text
        assert "to stderr" in text, (
            "stderr is merged into the log (2>&1) so failures are in it"
        )

    def test_two_concurrent_runs_do_not_clobber_each_others_log(self, rig: Rig):
        """
        #2458 (audit §5): the 3-slot rotation let two concurrent runs share a slot, and the
        ``-latest.log`` symlink could name a peer's run. Each invocation now owns its log.

        A holds the lock while B queues behind it, so both are live at once — the case a slot
        clobbered. Mutation-tested: a fixed ``LOG_FILE`` name in the wrapper reds this at
        "two invocations shared one log"; restored, green.
        """
        started = rig.root / "a_started"
        release = rig.root / "a_release"
        rig.fake_dotnet(
            f'echo "begin $2"\n'
            f'if [ "$2" = A ]; then : > "{started}"; '
            f'while [ ! -e "{release}" ]; do sleep 0.1; done; fi\n'
            f'echo "Total: 1 end $2"\n'
        )
        a = rig.spawn("test", "A")
        wait_for(started.exists, what="A to hold the lock and run")
        pointer = rig.logs / "dotnet-serialized-latest.path"
        inode_a = pointer.stat().st_ino
        b = rig.spawn("test", "B")
        # B replaces the pointer (write + rename, so a new inode) before it queues.
        wait_for(
            lambda: pointer.stat().st_ino != inode_a, what="B to publish its log path"
        )
        assert rig.lock_pid() == a.pid, (
            "B must be queued behind A, not running beside it"
        )
        release.touch()
        out_a, _ = a.communicate(timeout=60)
        out_b, _ = b.communicate(timeout=60)
        assert (a.returncode, b.returncode) == (0, 0)

        log_a, log_b = log_of(out_a), log_of(out_b)
        assert log_a != log_b, f"two invocations shared one log: {log_a}"
        text_a = log_a.read_text(encoding="utf-8")
        text_b = log_b.read_text(encoding="utf-8")
        assert "begin A" in text_a and "Total: 1 end A" in text_a and " B" not in text_a
        assert "begin B" in text_b and "Total: 1 end B" in text_b and " A" not in text_b
        assert rig.latest_log() == log_b, "the pointer names the newest invocation"

    def test_retention_deletes_only_old_wrapper_logs_in_the_log_dir(self, rig: Rig):
        rig.fake_dotnet('echo "Total: 1"')
        week_ago = time.time() - 8 * 24 * 3600
        old_log = rig.logs / "dotnet-serialized-20200101-000000-1.log"
        old_other = rig.logs / "last-test-run.log"
        fresh_log = rig.logs / "dotnet-serialized-fresh.log"
        for f in (old_log, old_other, fresh_log):
            f.write_text("x", encoding="utf-8")
        for f in (old_log, old_other):
            os.utime(f, (week_ago, week_ago))

        assert rig.run("test").returncode == 0

        assert not old_log.exists(), "a wrapper log older than 7 days is deleted"
        assert old_other.exists(), "nothing but the wrapper's own logs is touched"
        assert fresh_log.exists(), "a log younger than 7 days is kept"


class TestStallWatchdog:
    """
    The hung-holder watchdog (#1481).

    On 2026-08-12 a holder wedged the lock for **2h26m**: the driver process was still alive
    after its run had printed a summary, so the lock correctly stayed held
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
        assert (
            log is not None and "watchdog" in log.read_text(encoding="utf-8").lower()
        ), "the kill must be in the LOG too — the log is what an agent reads afterwards"

    def test_a_child_that_swallows_sigterm_and_exits_0_still_reports_failure(
        self, rig: Rig
    ):
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
            "sleep 120 &\n"
            "wait $!\n"
            "exit 0\n"  # reports success however it was interrupted — that is the mask
        )

        result = rig.run("test", timeout=45, **self.WATCHDOG_KNOBS)

        assert result.returncode == 4, (
            f"a killed run reported success (exit {result.returncode}) — this is #1481's "
            f"SIGTERM mask; stderr={result.stderr!r}"
        )

    def test_the_forced_exit_beats_the_zero_results_guard(self, rig: Rig):
        """
        A watchdog-killed ``test`` run usually ALSO has no "Total:" line, so the #1273
        zero-results guard would label it exit 3 — "zero tests ran" — which misattributes a kill
        to a build problem and sends the reader hunting the wrong cause. 4 must win.
        """
        rig.fake_dotnet("sleep 120\n")

        result = rig.run("test", timeout=45, **self.WATCHDOG_KNOBS)

        assert result.returncode == 4, (
            f"got {result.returncode} (3 = zero-results guard won)"
        )

    def test_an_honest_slow_run_is_never_killed(self, rig: Rig):
        """
        Cell (c), the negative control. A run that keeps producing output must survive
        indefinitely — a watchdog that cannot tell quiet-but-working from wedged is a
        fleet-wide outage, not a fix.
        """
        rig.fake_dotnet(
            "i=0\n"
            'while [ $i -lt 8 ]; do echo "progress $i"; sleep 1; i=$((i + 1)); done\n'
            'echo "Total: 1"\n'
        )

        result = rig.run(
            "test",
            timeout=45,
            DOTNET_SERIALIZED_STALL_SECONDS="4",
            DOTNET_SERIALIZED_WATCHDOG_POLL="1",
            DOTNET_SERIALIZED_WATCHDOG_GRACE="1",
        )

        assert result.returncode == 0, (
            f"an honest run was killed: stderr={result.stderr!r}"
        )
        assert "progress 7" in result.stdout

    def test_a_fast_run_is_not_delayed_by_the_monitor(self, rig: Rig):
        """The monitor must be torn down with the run, not waited out."""
        rig.fake_dotnet('echo "Total: 1"')

        started = time.time()
        result = rig.run(
            "test",
            timeout=45,
            DOTNET_SERIALIZED_STALL_SECONDS="600",
            DOTNET_SERIALIZED_WATCHDOG_POLL="20",
        )
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

        assert not rig.lock_dir.exists(), (
            "a watchdog kill must not leak the lock it was fixing"
        )

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
            "spelling prints nothing on the other platform, which reads as 'no progress'"
        )


def _wrapper_subshells(parent: int) -> list[int]:
    """
    Children of wrapper *parent* that are still running the wrapper script itself.

    Once the fake dotnet has exec'd (its sentinel exists) and ``tee`` has exec'd, the only child
    of a holding wrapper whose command line EQUALS the wrapper's own is the background
    ``watchdog`` subshell — a forked subshell keeps its parent's argv. The pipeline's first
    element shows as the fake ``dotnet`` stub and the second as ``tee``. Equality, not a
    ``dotnet-serialized`` substring: tee's argv is ``tee .../dotnet-serialized-<ts>-<pid>.log``.
    """
    out = subprocess.run(
        ["ps", "-A", "-o", "pid=,ppid=,command="],
        capture_output=True,
        text=True,
        check=True,
    ).stdout
    rows = [line.split(None, 2) for line in out.splitlines()]
    rows = [r for r in rows if len(r) == 3]
    own = [r[2] for r in rows if r[0] == str(parent)]
    if not own:
        return []
    return [int(r[0]) for r in rows if r[1] == str(parent) and r[2] == own[0]]


class TestWatchdogScope:
    """
    A watchdog guards exactly the run it was started for and dies with its wrapper (#2076).

    Observed 2026-09-24/25: orphaned watchdog subshells (``ppid == 1``, child ``sleep 30``)
    accumulated for up to 21 h. The wrapper only kills its watchdog when the ``dotnet | tee``
    pipeline returns normally, so a wrapper killed mid-run (a Bash-tool timeout, an agent killed
    at a usage limit) leaves the watchdog behind. That watchdog re-read the GLOBAL
    ``$LOCK_DIR/child`` every poll, so once a later holder recorded ITS child the orphan saw a
    live pid, never exited, and compared its OWN dead run's log mtime against the stall
    threshold — and then killed the later holder's healthy run, reported as exit 4.

    The cure has two halves and each has its own probe:

    * **dies with its wrapper** — :meth:`test_an_orphaned_watchdog_exits_while_its_child_runs`;
    * **guards its own child only** — :meth:`test_the_watchdog_ignores_a_rewritten_child_pointer`
      (the pid is latched once, so a rewrite of the global pointer is not followed);

    and :meth:`test_an_orphaned_watchdog_never_signals_the_next_holder` is the issue's literal
    end-to-end scenario, which either half alone cures.

    Mutation-tested against ``.claude/scripts/dotnet-serialized`` (both runs recorded in the
    commit body): reverting the whole watchdog fix reds all three; removing only the
    wrapper-liveness exit reds the first; removing only the latch (re-reading the pointer every
    poll) reds the second. Each restored → green.
    """

    def test_an_orphaned_watchdog_exits_while_its_child_runs(self, rig: Rig):
        """
        Kill the wrapper while its dotnet keeps running and keeps writing to the log. The child
        is alive and the log is fresh, so nothing but the wrapper's death can tell the watchdog
        its job is over — before #2076 it looped for as long as the orphaned child lived, and
        then (see the end-to-end probe) for as long as ANYONE held the lock.
        """
        started = rig.root / "a_started"
        rig.fake_dotnet(f': > "{started}"\nwhile :; do echo tick; sleep 0.2; done\n')

        holder = rig.popen(
            [str(WRAPPER), "test"],
            env=rig.env(
                DOTNET_SERIALIZED_STALL_SECONDS="2",
                DOTNET_SERIALIZED_WATCHDOG_POLL="1",
                DOTNET_SERIALIZED_WATCHDOG_GRACE="1",
            ),
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        watchdog: Optional[int] = None
        child: Optional[int] = None
        try:
            wait_for(started.exists, what="the fake dotnet to start")
            child = rig.child_pid()
            assert child is not None and _alive(child)
            wait_for(
                lambda: len(_wrapper_subshells(holder.pid)) == 1,
                what="exactly one watchdog subshell under the holder",
            )
            watchdog = _wrapper_subshells(holder.pid)[0]
            # Positive control: the thing whose absence is asserted below exists right now.
            assert _alive(watchdog)

            holder.kill()
            holder.wait(timeout=30)
            assert _alive(child), (
                "the fake dotnet should have been orphaned, not reaped"
            )

            wait_for(
                lambda: not _alive(watchdog),
                timeout=10,
                what="the orphaned watchdog to exit with its wrapper (#2076)",
            )
            assert _alive(child), (
                "the watchdog must simply exit — the orphaned child still holds the lock by "
                "design (#1508), and is not the dead wrapper's watchdog's to kill"
            )
        finally:
            for pid in (watchdog, child):
                if pid is not None and _alive(pid):
                    _kill_tree(pid)

    def test_the_watchdog_ignores_a_rewritten_child_pointer(self, rig: Rig):
        """
        The watchdog guards the child it was started for, not whatever ``$LOCK_DIR/child``
        names at the moment. Here the wrapper stays ALIVE (so the liveness exit cannot mask
        anything) and its own child stalls; after the watchdog has had a poll to record its
        child, the global pointer is rewritten to a decoy — the shape of a later holder
        recording its child. The stalled run must be killed and reported (exit 4), and the
        decoy must be untouched. Before #2076 the watchdog followed the pointer: it killed the
        decoy and left the stalled run wedged.
        """
        started = rig.root / "a_started"
        # exec: the recorded child IS the sleep, so no grandchild outlives it holding tee's pipe.
        rig.fake_dotnet(f': > "{started}"\necho "Total: 1"\nexec sleep 120\n')

        holder = rig.spawn(
            "test",
            DOTNET_SERIALIZED_STALL_SECONDS="4",
            DOTNET_SERIALIZED_WATCHDOG_POLL="1",
            DOTNET_SERIALIZED_WATCHDOG_GRACE="1",
        )
        decoy = rig.popen(["sleep", "120"])
        child: Optional[int] = None
        try:
            wait_for(started.exists, what="the fake dotnet to start")
            child = rig.child_pid()
            assert child is not None and _alive(child)
            # Polls land ~1 s apart from the watchdog's start, which precedes this sentinel; 2.5 s
            # leaves at least one poll to record the child and none yet past the 4 s threshold.
            time.sleep(2.5)
            (rig.lock_dir / "child").write_text(f"{decoy.pid}\n", encoding="utf-8")

            _, err = holder.communicate(timeout=30)

            assert holder.returncode == 4, (
                f"the stalled run was not killed (exit {holder.returncode}); stderr={err!r}"
            )
            assert f"killed PID {child} " in err, f"the wrong pid was reported: {err!r}"
            assert decoy.poll() is None, (
                "the watchdog signalled a pid it was not started for"
            )
        finally:
            if holder.poll() is None:
                holder.kill()
                holder.wait(timeout=30)
            if child is not None and _alive(child):
                _kill_tree(child)
            decoy.kill()
            decoy.wait(timeout=30)

    def test_an_orphaned_watchdog_never_signals_the_next_holder(self, rig: Rig):
        """
        The issue's literal scenario: kill the wrapper mid-run, let a second holder take the
        lock, and the first watchdog must exit without signalling the second run.

        Made deterministic with SIGSTOP. Unfrozen, the pre-#2076 orphan exited harmlessly if one
        of its polls happened to land in the sub-second window where the pointer still named
        the dead first child — so an unfrozen probe was a coin toss against the bug. Freezing
        the orphan across the handover and thawing it once the second child is recorded and the
        first log has been quiet past the threshold puts its next poll exactly where the
        incident's did.
        """
        a_started = rig.root / "a_started"
        b_started = rig.root / "b_started"
        rig.fake_dotnet(
            'case " $* " in\n'
            f'  *" --first "*) : > "{a_started}"; while :; do echo tick; sleep 0.2; done ;;\n'
            f'  *" --second "*) : > "{b_started}"; i=0\n'
            '    while [ $i -lt 8 ]; do echo "progress $i"; sleep 1; i=$((i + 1)); done\n'
            '    echo "Total: 1" ;;\n'
            "esac\n"
        )

        first = rig.popen(
            [str(WRAPPER), "test", "--first"],
            env=rig.env(
                DOTNET_SERIALIZED_STALL_SECONDS="2",
                DOTNET_SERIALIZED_WATCHDOG_POLL="1",
                DOTNET_SERIALIZED_WATCHDOG_GRACE="1",
            ),
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            new_group=False,
        )  # SIGSTOP below: see Rig.popen; the finally kills every pid
        watchdog: Optional[int] = None
        first_child: Optional[int] = None
        second: Optional[subprocess.Popen] = None
        try:
            wait_for(a_started.exists, what="the first fake dotnet to start")
            first_child = rig.child_pid()
            assert first_child is not None and _alive(first_child)
            wait_for(
                lambda: len(_wrapper_subshells(first.pid)) == 1,
                what="exactly one watchdog subshell under the first holder",
            )
            watchdog = _wrapper_subshells(first.pid)[0]
            os.kill(watchdog, signal.SIGSTOP)

            first.kill()
            first.wait(timeout=30)
            _kill_tree(first_child)
            wait_for(lambda: not _alive(first_child), what="the first child to die")
            first_child_died = time.time()

            second = rig.spawn(
                "test",
                "--second",
                DOTNET_SERIALIZED_STALL_SECONDS="600",
                DOTNET_SERIALIZED_WATCHDOG_POLL="1",
            )
            wait_for(b_started.exists, what="the second holder's fake dotnet to start")
            second_child = rig.child_pid()
            assert second_child is not None and _alive(second_child)
            # Past the orphan's 2 s threshold on its own (dead) run's log, so a pre-#2076
            # orphan would fire on its very next poll.
            time.sleep(max(0.0, first_child_died + 3.0 - time.time()))
            assert _alive(watchdog), (
                "positive control: the orphan must still exist to be thawed"
            )
            os.kill(watchdog, signal.SIGCONT)

            out, err = second.communicate(timeout=60)

            assert second.returncode == 0, (
                f"the second holder's run was signalled (exit {second.returncode}) — the #2076 "
                f"orphan killed a healthy run; stderr={err!r}"
            )
            assert "progress 7" in out and "watchdog" not in err.lower()
            wait_for(
                lambda: not _alive(watchdog),
                timeout=10,
                what="the orphaned watchdog to exit",
            )
        finally:
            if watchdog is not None and _alive(watchdog):
                os.kill(watchdog, signal.SIGCONT)
                _kill_tree(watchdog)
            if first_child is not None and _alive(first_child):
                _kill_tree(first_child)
            if second is not None and second.poll() is None:
                second.kill()
                second.wait(timeout=30)


def test_the_portable_stat_fallback_resolves_on_this_platform(tmp_path: Path):
    """
    There is no portable ``stat`` spelling: GNU/Linux wants ``stat -c %Y``, BSD/macOS wants
    ``stat -f %m``, and the GNU spelling MUST come first — BSD rejects ``-c`` and falls through,
    but GNU does NOT reject ``-f`` (it prints filesystem status with exit 0), so a BSD-first order
    silently returns non-mtime output on Linux. This suite runs on BOTH (developer macOS, ubuntu
    CI), so we execute the WRAPPER's ACTUAL ``file_mtime`` command — extracted from the script,
    never a hand-copied duplicate that could drift from it and pass while the real one is broken.
    """
    probe = tmp_path / "probe.txt"
    probe.write_text("x", encoding="utf-8")

    source = WRAPPER.read_text(encoding="utf-8")
    match = re.search(r"stat -[cf] %[mY][^\n]*", source)
    assert match, "could not find the file_mtime stat fallback in the wrapper"
    command = match.group(0).replace('"$1"', f'"{probe}"')

    result = subprocess.run(["/bin/sh", "-c", command], capture_output=True, text=True)

    assert result.stdout.strip().isdigit(), (
        f"the wrapper's stat fallback did not produce an mtime on this platform: "
        f"command={command!r} {result!r}"
    )


class TestHolderIdentity:
    """
    The lock's holder-identity files and the ``--holder-status`` verb (#1867, R-AS).

    A waiter that finds the lock held has NO way to identify the holder — the lock
    directory only has ``pid`` and ``child``. These tests guard the three new identity
    files (``cwd``, ``started``, ``log``) and the ``--holder-status`` verb that reads them.

    Documented mutations:

    * **Mutation 1:** Remove the ``log`` publish (``echo "$LOG_FILE" > "$LOCK_LOG_FILE"``)
      → ``test_holder_status_live`` errors (log file missing from lock dir), the status
      verb reports "no log published (lock-only holder)" for a normal run.
    * **Mutation 2:** Invert the growth comparison (``_growth -gt 0`` → ``_growth -le 0``)
      → the LIVE/NO-GROWTH pair swaps: ``test_holder_status_live`` sees NO GROWTH and
      ``test_holder_status_no_growth`` sees LIVE.
    """

    def test_holder_publishes_identity_files(self, rig: Rig):
        """While a run is held, cwd/started/log exist with correct values."""
        started_sentinel = rig.root / "child_started"
        rig.fake_dotnet(
            f': > "{started_sentinel}"\n'
            f"i=0\n"
            f'while [ $i -lt 25 ]; do echo "line $i"; sleep 0.2; i=$((i + 1)); done\n'
            f'echo "Total: 1"\n'
        )

        holder = rig.spawn("test")
        try:
            wait_for(started_sentinel.exists, what="fake dotnet to start")

            assert rig.lock_dir.exists(), "lock dir should exist while held"

            cwd_file = rig.lock_dir / "cwd"
            started_file = rig.lock_dir / "started"
            log_file = rig.lock_dir / "log"

            assert cwd_file.exists(), "cwd file not published"
            assert started_file.exists(), "started file not published"
            assert log_file.exists(), "log file not published"

            cwd_val = cwd_file.read_text(encoding="utf-8").strip()
            assert cwd_val == str(rig.root.resolve()), (
                f"cwd mismatch: got {cwd_val!r}, expected {str(rig.root.resolve())!r}"
            )

            started_val = int(started_file.read_text(encoding="utf-8").strip())
            now = int(time.time())
            assert abs(now - started_val) <= 5, (
                f"started timestamp {started_val} is not within 5s of now ({now})"
            )

            log_val = log_file.read_text(encoding="utf-8").strip()
            assert log_val.endswith(".log"), (
                f"log pointer doesn't end with .log: {log_val!r}"
            )
            assert os.path.isabs(log_val) or log_val.startswith(str(rig.logs)), (
                f"log pointer doesn't look like a slot path: {log_val!r}"
            )
        finally:
            holder.terminate()
            holder.wait(timeout=30)

    def test_holder_status_live(self, rig: Rig):
        """A holder producing output is reported LIVE by --holder-status."""
        started_sentinel = rig.root / "child_started"
        rig.fake_dotnet(
            f': > "{started_sentinel}"\n'
            f"i=0\n"
            f'while [ $i -lt 100 ]; do echo "line $i"; sleep 0.2; i=$((i + 1)); done\n'
            f'echo "Total: 1"\n'
        )

        holder = rig.spawn("test", DOTNET_SERIALIZED_STALL_SECONDS="600")
        try:
            wait_for(started_sentinel.exists, what="fake dotnet to start")
            time.sleep(0.5)

            result = rig.run(
                "--holder-status",
                DOTNET_SERIALIZED_PROBE_SECONDS="2",
                DOTNET_SERIALIZED_STALL_SECONDS="600",
            )

            assert result.returncode == 0, f"--holder-status failed: {result.stderr}"
            assert "LIVE" in result.stdout, (
                f"expected LIVE in output, got: {result.stdout!r}"
            )
            assert "grew by" in result.stdout, (
                f"expected 'grew by' in output, got: {result.stdout!r}"
            )
        finally:
            holder.terminate()
            holder.wait(timeout=30)

    def test_holder_status_no_growth(self, rig: Rig):
        """A holder producing no output is reported NO GROWTH."""
        started_sentinel = rig.root / "child_started"
        rig.fake_dotnet(f'echo "Total: 1"\n: > "{started_sentinel}"\nsleep 30\n')

        holder = rig.spawn("test", DOTNET_SERIALIZED_STALL_SECONDS="600")
        try:
            wait_for(started_sentinel.exists, what="fake dotnet to start")
            time.sleep(0.5)

            result = rig.run(
                "--holder-status",
                DOTNET_SERIALIZED_PROBE_SECONDS="1",
                DOTNET_SERIALIZED_STALL_SECONDS="600",
            )

            assert result.returncode == 0, f"--holder-status failed: {result.stderr}"
            assert "NO GROWTH" in result.stdout, (
                f"expected NO GROWTH in output, got: {result.stdout!r}"
            )
        finally:
            holder.terminate()
            holder.wait(timeout=30)

    def test_holder_status_lock_only(self, rig: Rig):
        """A --acquire-lock holder publishes no log; the verb says so."""
        acquired = rig.run("--acquire-lock", str(os.getpid()))
        assert acquired.returncode == 0

        try:
            result = rig.run("--holder-status")

            assert result.returncode == 0, f"--holder-status failed: {result.stderr}"
            assert "no log published (lock-only holder)" in result.stdout, (
                f"expected 'no log published' message, got: {result.stdout!r}"
            )
        finally:
            rig.run("--release-lock", str(os.getpid()))

    def test_identity_files_absent_after_release(self, rig: Rig):
        """After the wrapper finishes, the lock dir (and all identity files) are gone."""
        rig.fake_dotnet('echo "Total: 1"')

        result = rig.run("test")

        assert result.returncode == 0, f"run failed: {result.stderr}"
        assert not rig.lock_dir.exists(), (
            "lock dir should be gone after release (cleanup removes it)"
        )
