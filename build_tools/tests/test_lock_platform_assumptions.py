"""Platform facts the dotnet-serialized lock depends on, checked on the machine that runs it (#2458).

The lock's own probes (``test_dotnet_serialized_lock.py``) drive the whole wrapper, so a red there
cannot say whether the wrapper's logic or the platform under it is at fault. These race the bare
primitives instead. ``mkdir`` is what the lock used before the flock and what wrappers from before
it still use during the rollout; ``flock(1)`` is what it uses now. Each test prints the tool
identity so a CI log names the implementation that was raced (GNU coreutils and the Rust uutils
that newer Ubuntu images ship are both called ``mkdir``).
"""

from __future__ import annotations

import multiprocessing
import os
import shutil
import subprocess
from pathlib import Path

import pytest

ROUNDS = 300
RACERS = 8


def _identity(tool: str) -> str:
    path = shutil.which(tool)
    if path is None:
        return f"{tool}: not installed"
    try:
        out = subprocess.run(
            [tool, "--version"], capture_output=True, text=True, timeout=10
        ).stdout
    except OSError as ex:
        out = f"(--version failed: {ex})"
    first = out.splitlines()[0] if out else "(no --version output)"
    return f"{tool}: {os.path.realpath(path)} -- {first}"


def _mkdir_racer(root: str, barrier, wins) -> None:
    for i in range(ROUNDS):
        barrier.wait()
        if (
            subprocess.run(
                ["mkdir", os.path.join(root, f"r{i}")], stderr=subprocess.DEVNULL
            ).returncode
            == 0
        ):
            wins.put(i)


def test_mkdir_admits_exactly_one_racer_per_directory(tmp_path: Path):
    """``mkdir DIR`` must fail for all but one of several processes creating DIR at once."""
    print(
        _identity("mkdir"),
        _identity("mv"),
        "/bin/sh ->",
        os.path.realpath("/bin/sh"),
        sep="\n",
    )
    ctx = multiprocessing.get_context("fork")
    barrier = ctx.Barrier(RACERS)
    wins = ctx.Queue()
    procs = [
        ctx.Process(target=_mkdir_racer, args=(str(tmp_path), barrier, wins))
        for _ in range(RACERS)
    ]
    for p in procs:
        p.start()
    counts = [0] * ROUNDS
    for _ in range(ROUNDS):
        counts[wins.get(timeout=120)] += 1
    for p in procs:
        p.join(timeout=120)
    while not wins.empty():
        counts[wins.get()] += 1

    multiple = {i: n for i, n in enumerate(counts) if n > 1}
    assert all(n >= 1 for n in counts), "a round had no winner"
    assert not multiple, (
        f"mkdir admitted several racers in {len(multiple)}/{ROUNDS} rounds: {multiple}"
    )


@pytest.mark.skipif(shutil.which("flock") is None, reason="no flock(1) here (macOS)")
def test_flock_admits_one_holder_at_a_time(tmp_path: Path):
    """``flock -x FILE CMD`` from several processes must never run two CMDs at once."""
    print(_identity("flock"))
    lock = tmp_path / "lock"
    inside = tmp_path / "inside"
    report = tmp_path / "overlaps"
    critical = (
        f'if [ -e "{inside}" ]; then echo overlap >> "{report}"; fi; '
        f': > "{inside}"; sleep 0.002; rm -f "{inside}"'
    )
    loop = f"i=0; while [ $i -lt 40 ]; do flock -x \"{lock}\" sh -c '{critical}'; i=$((i + 1)); done"
    procs = [subprocess.Popen(["sh", "-c", loop]) for _ in range(RACERS)]
    for p in procs:
        assert p.wait(timeout=300) == 0

    assert not report.exists(), report.read_text()
