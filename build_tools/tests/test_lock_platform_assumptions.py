"""The platform primitive the dotnet-serialized lock rests on, checked where it runs (#2458).

The wrapper serializes dotnet with ``flock(1)`` on one fixed file (python3 ``fcntl.flock`` or
perl ``flock`` where flock(1) is missing). ``test_dotnet_serialized_lock.py`` drives the whole
wrapper, so a red there cannot say whether the wrapper's logic or the platform under it is at
fault; this races the bare primitive.

Why it exists: the lock used to be ``mkdir DIR`` (exclusive create). On the ubuntu-26.04 GitHub
runner ``mkdir`` is uutils coreutils 0.10.0, and racing it from 8 processes admitted several
winners in 142 of 300 rounds (run 42954679999, job 130306860778), which is how the wrapper let two
dotnet runs overlap there. The mkdir measurement is kept below as printed diagnostics, next to
the ``mkdir(2)`` syscall raced the same way, so a CI log names the implementation and says whether
the syscall or the binary is at fault. It is not asserted: nothing relies on it any more.
"""

from __future__ import annotations

import multiprocessing
import os
import shutil
import subprocess
from pathlib import Path

import pytest

RACERS = 8
MKDIR_ROUNDS = 300
FLOCK_ITERATIONS = 40  # per racer: 320 critical sections in all


def _sh(command: str) -> str:
    out = subprocess.run(
        ["sh", "-c", command], capture_output=True, text=True, timeout=30
    )
    text = (out.stdout + out.stderr).strip()
    return text.splitlines()[0] if text else "(no output)"


def _mkdir_racer(root: str, use_syscall: bool, barrier, wins) -> None:
    for i in range(MKDIR_ROUNDS):
        path = os.path.join(root, f"r{i}")
        barrier.wait()
        if use_syscall:
            try:
                os.mkdir(path)
            except FileExistsError:
                continue
            wins.put(i)
        # No -p; the exit status alone decides, as the old wrapper's `while ! mkdir` did.
        elif subprocess.run(["mkdir", path], stderr=subprocess.DEVNULL).returncode == 0:
            wins.put(i)


def _mkdir_race(root: Path, use_syscall: bool) -> str:
    root.mkdir()
    ctx = multiprocessing.get_context("fork")
    barrier = ctx.Barrier(RACERS)
    wins = ctx.Queue()
    procs = [
        ctx.Process(target=_mkdir_racer, args=(str(root), use_syscall, barrier, wins))
        for _ in range(RACERS)
    ]
    for p in procs:
        p.start()
    for p in procs:
        p.join(timeout=300)
    counts = [0] * MKDIR_ROUNDS
    while not wins.empty():
        counts[wins.get()] += 1
    multiple = sum(1 for n in counts if n > 1)
    worst = max(counts)
    return f"{multiple}/{MKDIR_ROUNDS} rounds with more than one winner (worst {worst})"


def _diagnostics(tmp_path: Path) -> list[str]:
    taken = tmp_path / "taken"
    taken.mkdir()
    resolved = _sh('readlink -f "$(command -v mkdir)"')
    lines = [
        f"image:        {os.environ.get('ImageOS', '?')} {os.environ.get('ImageVersion', '')}",
        f"mkdir:        {_sh('command -v mkdir')} -> {resolved}",
        f"mkdir -V:     {_sh('mkdir --version')}",
        f"mkdir in sh:  {_sh('type mkdir')}",
        f"/bin/sh:      {os.path.realpath('/bin/sh')}",
        f"flock -V:     {_sh('flock --version') if shutil.which('flock') else 'not installed'}",
        f"fs of tmp:    {_sh(f'stat -f -c %T {tmp_path}')}; {_sh(f'df -T {tmp_path} | tail -1')}",
        f"mkdir on an existing dir, serially: exit {subprocess.run(['mkdir', str(taken)], stderr=subprocess.DEVNULL).returncode}",
        f"mkdir binary, raced:   {_mkdir_race(tmp_path / 'binary', use_syscall=False)}",
        f"mkdir(2) syscall, raced: {_mkdir_race(tmp_path / 'syscall', use_syscall=True)}",
    ]
    return lines


@pytest.mark.skipif(shutil.which("flock") is None, reason="no flock(1) here (macOS)")
def test_flock_admits_one_holder_at_a_time(tmp_path: Path, capsys):
    """``flock -x FILE CMD`` from several processes must never run two CMDs at once."""
    with capsys.disabled():
        print(
            "\n" + "\n".join("  [platform] " + line for line in _diagnostics(tmp_path))
        )

    lock = tmp_path / "lock"
    inside = tmp_path / "inside"
    overlaps = tmp_path / "overlaps"
    entered = tmp_path / "entered"
    critical = (
        f'if [ -e "{inside}" ]; then echo x >> "{overlaps}"; fi; '
        f': > "{inside}"; echo x >> "{entered}"; sleep 0.002; rm -f "{inside}"'
    )
    loop = (
        f"i=0; while [ $i -lt {FLOCK_ITERATIONS} ]; do "
        f"flock -x \"{lock}\" sh -c '{critical}' || exit 1; i=$((i + 1)); done"
    )
    procs = [subprocess.Popen(["sh", "-c", loop]) for _ in range(RACERS)]
    for p in procs:
        assert p.wait(timeout=300) == 0

    # Positive control: every critical section ran, so "no overlap" was observed, not assumed.
    assert len(entered.read_text().splitlines()) == RACERS * FLOCK_ITERATIONS
    assert not overlaps.exists(), (
        f"flock admitted two holders {len(overlaps.read_text().splitlines())} times"
    )
