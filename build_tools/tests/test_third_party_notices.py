"""Guard for #2602: a file that says it is a port of CPython source carries the PSF notice.

PSF License Version 2 (clauses 2 and 3) asks a derivative work to keep the PSF licence and copyright
notice and to summarise its changes. Sharpy does that in two places: a header in each derived file and
an entry in the root ``THIRD-PARTY-NOTICES.txt``. Before this guard, a port was noticed only if
someone remembered (DatetimeIso.cs, #2574, was noticed after it merged; Fraction.Format.cs and
PyFormat.cs after an audit). This test closes the gap mechanically:

* every non-test source in ``src/Sharpy.Core`` and ``src/Sharpy.Stdlib`` that declares itself a port
  (``PORT_CLAIM``: "port of / ported from [the] CPython", "adapted from CPython", "translation of /
  translated from CPython", any case, the words possibly split across comment lines) carries the PSF
  header lines and is listed in the notices file, unless it is in ``EXEMPT`` with a reason;
* every file the notices list exists and carries the header, and every file carrying the header is
  listed (the two places cannot drift apart);
* the licence text at the end of the notices file is the CPython 3.15.2 ``LICENSE.txt``, byte for byte.

The guard only catches files that declare themselves ports; a port that makes no such claim (a
structural port with no comment saying so) needs the #2602 audit, not this test.

A file that only follows CPython's behaviour should say so ("matching", "the rule of"), not "port":
the word is a licence claim. Do not reword a comment to get past this test if the code is a port.
"""

from __future__ import annotations

import hashlib
import re
from pathlib import Path
from typing import Dict, Iterable, List, Set

import pytest

REPO = Path(__file__).resolve().parents[2]
NOTICES = REPO / "THIRD-PARTY-NOTICES.txt"
SCANNED = ("src/Sharpy.Core", "src/Sharpy.Stdlib")
SUFFIXES = {".cs", ".spy"}

# Between two words of a claim: whitespace, possibly a line break followed by a comment leader
# (`//`, `///`, `*`, `#`), as when a doc comment wraps mid-phrase.
# One character class (no nested quantifiers), so the scan stays linear on long whitespace runs.
_W = r"[\s/*#]+"
_CPYTHON = r"(?:the" + _W + r")?CPython\b"
PORT_CLAIM = re.compile(
    r"\bport(?:ed)?"
    + _W
    + r"(?:of|from)"
    + _W
    + _CPYTHON
    + r"|\badapted"
    + _W
    + r"from"
    + _W
    + _CPYTHON
    + r"|\btranslat\w+"
    + _W
    + r"(?:of|from)"
    + _W
    + _CPYTHON,
    re.IGNORECASE,
)
HEADER_LINES = (
    "Copyright (c) 2001 Python Software Foundation; All Rights Reserved",
    "Licensed under the PSF License Version 2; see THIRD-PARTY-NOTICES.txt",
)

# Repo-relative path -> why the file says "port" but carries no notice. Each row cites #2602.
# Empty: the one behaviour-only claim found (Str.cs, format_float_short's thresholds) was reworded.
EXEMPT: Dict[str, str] = {}

# The CPython 3.15.2 LICENSE.txt (lib/python3.15/LICENSE.txt), the last 277 lines of the notices file.
LICENSE_LINES = 277
LICENSE_SHA256 = "b0e25a78cffb43f4d92de8b61ccfa1f1f98ecbc22330b54b5251e7b6ba010231"


def sources(root: Path) -> List[Path]:
    found = []
    for scanned in SCANNED:
        base = root / scanned
        if not base.is_dir():
            continue
        for path in base.rglob("*"):
            if path.suffix not in SUFFIXES or not path.is_file():
                continue
            parts = path.relative_to(root).parts
            if "bin" in parts or "obj" in parts:
                continue
            found.append(path)
    return sorted(found)


def _has_header(text: str) -> bool:
    return all(line in text for line in HEADER_LINES)


def listed_files(notices: str) -> Set[str]:
    """The paths under 'Sharpy files derived from it:' (one per 4-space-indented line)."""
    lines = notices.splitlines()
    start = lines.index("Sharpy files derived from it:")
    listed: Set[str] = set()
    for line in lines[start + 1 :]:
        if not line.strip():
            if listed:
                break
            continue
        if not line.startswith("    "):
            break
        listed.add(line.strip())
    return listed


def claimants(root: Path, files: Iterable[Path]) -> Dict[str, str]:
    """Root-relative path -> the first port claim it makes (whitespace collapsed)."""
    out: Dict[str, str] = {}
    for path in files:
        match = PORT_CLAIM.search(path.read_text(encoding="utf-8"))
        if match:
            out[path.relative_to(root).as_posix()] = " ".join(match.group(0).split())
    return out


def unnoticed_ports(root: Path, listed: Set[str], exempt: Dict[str, str]) -> List[str]:
    problems = []
    for rel, claim in sorted(claimants(root, sources(root)).items()):
        if rel in exempt:
            continue
        if not _has_header((root / rel).read_text(encoding="utf-8")):
            problems.append(
                f"{rel}: says {claim!r} but lacks the PSF header lines {HEADER_LINES}"
            )
        if rel not in listed:
            problems.append(
                f"{rel}: says {claim!r} but is not listed in THIRD-PARTY-NOTICES.txt"
            )
    return problems


def test_scan_sees_the_known_ports():
    # Positive control: the scan finds the three noticed ports, so an empty result elsewhere means
    # "none", not "the scanner looked nowhere".
    found = claimants(REPO, sources(REPO))
    for rel in (
        "src/Sharpy.Stdlib/Datetime/DatetimeIso.cs",
        "src/Sharpy.Stdlib/Fractions/Fraction.Format.cs",
        "src/Sharpy.Core/PyFormat.cs",
    ):
        assert rel in found, f"{rel}: the port-claim scan no longer sees it"


def test_every_self_declared_port_is_noticed():
    listed = listed_files(NOTICES.read_text(encoding="utf-8"))
    problems = unnoticed_ports(REPO, listed, EXEMPT)
    assert not problems, "\n".join(problems)


CLAIM_PHRASINGS = [
    "/// A port of CPython's _PyLong_Format.",
    "// an exact port of CPython 3.14's fractions.py",
    "// ported from CPython's Objects/listsort.txt",
    "// a port of the CPython implementation",
    "// Ported From The CPython source",
    "// adapted from CPython's heapq",
    "// adapted from the CPython module",
    "// a translation of CPython's bisect",
    "// translated from CPython's C code",
    "/// This method is a port of\n    /// CPython's list.sort.",
    "// ported\n// from\n// the CPython tree",
    "/* translated from\n * CPython's dtoa.c */",
    "# adapted from\n# CPython's textwrap",
]

NOT_CLAIMS = [
    "// matching CPython's float_floor_div",
    "// the rule of CPython's format_float_short",
    "// support of CPython-compatible output",
    "// reported from CPython's tests",
    "// modelled on CPython 3.12's threading.py",
]


def _fixture(tmp_path: Path, text: str, with_header: bool) -> Path:
    path = tmp_path / "src" / "Sharpy.Core" / "Ported.cs"
    path.parent.mkdir(parents=True, exist_ok=True)
    header = "".join(f"// {line}\n" for line in HEADER_LINES) if with_header else ""
    path.write_text(
        header + "namespace Sharpy\n{\n    " + text + "\n}\n", encoding="utf-8"
    )
    return path


@pytest.mark.parametrize("text", CLAIM_PHRASINGS)
def test_claim_phrasing_without_a_notice_is_red(tmp_path: Path, text: str):
    _fixture(tmp_path, text, with_header=False)
    problems = unnoticed_ports(tmp_path, listed=set(), exempt={})
    assert any("lacks the PSF header" in p for p in problems), problems
    assert any("not listed" in p for p in problems), problems


@pytest.mark.parametrize("text", CLAIM_PHRASINGS)
def test_claim_phrasing_with_a_notice_is_green(tmp_path: Path, text: str):
    _fixture(tmp_path, text, with_header=True)
    assert (
        unnoticed_ports(tmp_path, listed={"src/Sharpy.Core/Ported.cs"}, exempt={}) == []
    )


@pytest.mark.parametrize("text", NOT_CLAIMS)
def test_behaviour_wording_is_not_a_claim(tmp_path: Path, text: str):
    _fixture(tmp_path, text, with_header=False)
    assert unnoticed_ports(tmp_path, listed=set(), exempt={}) == []


def test_notices_and_headers_agree():
    listed = listed_files(NOTICES.read_text(encoding="utf-8"))
    assert listed, "THIRD-PARTY-NOTICES.txt lists no files"
    problems = []
    for rel in sorted(listed):
        path = REPO / rel
        if not path.is_file():
            problems.append(
                f"{rel}: listed in THIRD-PARTY-NOTICES.txt but does not exist"
            )
        elif not _has_header(path.read_text(encoding="utf-8")):
            problems.append(
                f"{rel}: listed in THIRD-PARTY-NOTICES.txt but lacks the PSF header"
            )
    for path in sources(REPO):
        rel = path.relative_to(REPO).as_posix()
        if _has_header(path.read_text(encoding="utf-8")) and rel not in listed:
            problems.append(
                f"{rel}: carries the PSF header but is not listed in THIRD-PARTY-NOTICES.txt"
            )
    assert not problems, "\n".join(problems)


def test_exemptions_are_live_and_cited():
    found = claimants(REPO, sources(REPO))
    for rel, reason in EXEMPT.items():
        assert "#2602" in reason, f"{rel}: an exemption cites #2602"
        assert rel in found, (
            f"{rel}: exempt but no longer claims to be a port; delete the row"
        )


def test_licence_text_is_the_cpython_file():
    data = NOTICES.read_bytes()
    tail = b"".join(data.splitlines(keepends=True)[-LICENSE_LINES:])
    assert hashlib.sha256(tail).hexdigest() == LICENSE_SHA256
