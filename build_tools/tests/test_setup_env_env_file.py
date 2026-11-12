"""The env file ``build_tools/setup_env.sh`` writes survives any path it is given.

``.venv/sharpy-env.sh`` is sourced by every shell. The checkout path, ``--dotnet-dir`` and
``--dotnet11-dir`` are written into it, so a path that could end a quoted string or expand there
would break shell start-up or run a command at every start. The test runs the script's own
"env file" block (extracted from the script, not copied) with hostile paths, sources the result in
a clean bash under ``set -u``, and checks each value comes back byte for byte and nothing ran.
"""

from __future__ import annotations

import os
import subprocess
from pathlib import Path

import pytest

SCRIPT = Path(__file__).resolve().parents[1] / "setup_env.sh"
BLOCK_START = 'ENV_FILE="$VENV/sharpy-env.sh"\n'
BLOCK_END = "if ((PROFILE)); then\n  # FIRST line"

# Each case is a directory-name fragment; {pwn} is replaced by a path the fragment tries to create.
HOSTILE = {
    "plain": b"plain",
    "space": b"with space",
    "double-quote": b'a"b',
    "single-quote": b"a'b",
    "backslash": b"a\\b",
    "command-substitution": b"a$(touch {pwn})b",
    "backtick": b"a`touch {pwn}`b",
    "parameter-expansion": b"a${HOME}b",
    "glob-and-bang": b"a*!b",
    "tab": b"a\tb",
    "inner-newline": b"a\nb",
    "non-utf8": b"a\xff\xfeb",
}


def env_file_block() -> str:
    text = SCRIPT.read_text(encoding="utf-8")
    assert text.count(BLOCK_START) == 1, (
        "the env-file block's first line moved; update BLOCK_START"
    )
    start = text.index(BLOCK_START)
    end = text.index(BLOCK_END, start)
    return text[start:end]


def test_the_block_is_found_and_writes_the_three_exports():
    block = env_file_block()
    for name in ("DOTNET_ROOT=", "DOTNET11_ROOT=", "PYTHONPATH=", "bin/activate"):
        assert name in block, f"{name} is no longer written by the extracted block"


@pytest.mark.parametrize("case", sorted(HOSTILE))
def test_every_path_round_trips_and_nothing_runs(tmp_path: Path, case: str):
    pwn = tmp_path / "pwned"
    fragment = HOSTILE[case].replace(b"{pwn}", os.fsencode(pwn))
    base = os.fsencode(tmp_path)
    root = base + b"/root-" + fragment
    dotnet = base + b"/dotnet-" + fragment
    dotnet11 = base + b"/dotnet11-" + fragment
    venv = root + b"/.venv"
    os.makedirs(venv + b"/bin")
    Path(os.fsdecode(venv + b"/bin/activate")).write_text(
        "ACTIVATED=yes\n", encoding="utf-8"
    )

    harness = tmp_path / "write-env.sh"
    harness.write_text(
        "set -euo pipefail\nDOTNET11=1\n" + env_file_block(), encoding="utf-8"
    )
    env = {
        b"PATH": os.fsencode(os.environ["PATH"]),
        b"ROOT": root,
        b"VENV": venv,
        b"DOTNET_DIR": dotnet,
        b"DOTNET11_DIR": dotnet11,
    }
    subprocess.run(["bash", str(harness)], env=env, check=True)
    assert not pwn.exists(), "writing the env file ran a command taken from a path"

    out = tmp_path / "out"
    reader = (
        'set -u; . "$1"; '
        'printf "%s\\0%s\\0%s\\0%s\\0%s" '
        '"$DOTNET_ROOT" "$DOTNET11_ROOT" "$PYTHONPATH" "${PATH%%:\\$HOME*}" "$ACTIVATED" > "$2"'
    )
    clean = {b"HOME": b"/nonexistent-home", b"PATH": b"/usr/bin:/bin"}
    sourced = subprocess.run(
        ["bash", "-c", reader, "_", os.fsdecode(venv + b"/sharpy-env.sh"), str(out)],
        env=clean,
        capture_output=True,
        check=False,
    )
    assert sourced.returncode == 0, sourced.stderr.decode("utf-8", "replace")
    assert not pwn.exists(), "sourcing the env file ran a command taken from a path"

    got_dotnet, got_dotnet11, got_pythonpath, got_path, activated = (
        out.read_bytes().split(b"\0")
    )
    assert got_dotnet == dotnet
    assert got_dotnet11 == dotnet11
    assert got_pythonpath == root
    # PATH is "<dotnet dir>:$HOME/.dotnet/tools:…": everything before the first ":/nonexistent-home".
    assert got_path.split(b":/nonexistent-home")[0] == dotnet
    assert activated == b"yes"
