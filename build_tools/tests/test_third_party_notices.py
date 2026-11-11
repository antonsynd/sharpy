"""Guard for #2602: a file that says it is a port of CPython source carries the PSF notice.

PSF License Version 2 (clauses 2 and 3) asks a derivative work to keep the PSF licence and copyright
notice and to summarise its changes. Sharpy does that in two places: a header in each derived file and
an entry in the root ``THIRD-PARTY-NOTICES.txt``. Before this guard, a port was noticed only if
someone remembered (DatetimeIso.cs, #2574, was noticed after it merged; Fraction.Format.cs and
PyFormat.cs after an audit). This test closes the gap mechanically:

* every non-test source in ``src/Sharpy.Core`` and ``src/Sharpy.Stdlib`` that declares itself a port
  ("port of CPython", "ported from CPython", "exact port of CPython", any case) carries the PSF
  header lines and is listed in the notices file, unless it is in ``EXEMPT`` with a reason;
* every file the notices list exists and carries the header, and every file carrying the header is
  listed (the two places cannot drift apart);
* the licence text at the end of the notices file is the CPython 3.15.2 ``LICENSE.txt``, byte for byte.

A file that only follows CPython's behaviour should say so ("matching", "the rule of"), not "port":
the word is a licence claim. Do not reword a comment to get past this test if the code is a port.
"""

from __future__ import annotations

import hashlib
import re
from pathlib import Path
from typing import Dict, List, Set

REPO = Path(__file__).resolve().parents[2]
NOTICES = REPO / "THIRD-PARTY-NOTICES.txt"
SCANNED = [REPO / "src" / "Sharpy.Core", REPO / "src" / "Sharpy.Stdlib"]
SUFFIXES = {".cs", ".spy"}

PORT_CLAIM = re.compile(
    r"(?:exact\s+)?port\s+of\s+CPython|ported\s+from\s+CPython", re.IGNORECASE
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


def _sources() -> List[Path]:
    found = []
    for root in SCANNED:
        for path in root.rglob("*"):
            if path.suffix not in SUFFIXES or not path.is_file():
                continue
            parts = path.relative_to(REPO).parts
            if "bin" in parts or "obj" in parts:
                continue
            found.append(path)
    return sorted(found)


def _rel(path: Path) -> str:
    return path.relative_to(REPO).as_posix()


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


def claimants(sources: List[Path]) -> Dict[str, str]:
    """Repo-relative path -> the first port claim it makes."""
    out: Dict[str, str] = {}
    for path in sources:
        match = PORT_CLAIM.search(path.read_text(encoding="utf-8"))
        if match:
            out[_rel(path)] = match.group(0)
    return out


def test_scan_sees_the_known_ports():
    # Positive control: the scan finds the three noticed ports, so an empty result elsewhere means
    # "none", not "the scanner looked nowhere".
    found = claimants(_sources())
    for rel in (
        "src/Sharpy.Stdlib/Datetime/DatetimeIso.cs",
        "src/Sharpy.Stdlib/Fractions/Fraction.Format.cs",
        "src/Sharpy.Core/PyFormat.cs",
    ):
        assert rel in found, f"{rel}: the port-claim scan no longer sees it"


def test_every_self_declared_port_is_noticed():
    listed = listed_files(NOTICES.read_text(encoding="utf-8"))
    problems = []
    for rel, claim in sorted(claimants(_sources()).items()):
        if rel in EXEMPT:
            continue
        text = (REPO / rel).read_text(encoding="utf-8")
        if not _has_header(text):
            problems.append(
                f"{rel}: says {claim!r} but lacks the PSF header lines {HEADER_LINES}"
            )
        if rel not in listed:
            problems.append(
                f"{rel}: says {claim!r} but is not listed in THIRD-PARTY-NOTICES.txt"
            )
    assert not problems, "\n".join(problems)


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
    for path in _sources():
        rel = _rel(path)
        if _has_header(path.read_text(encoding="utf-8")) and rel not in listed:
            problems.append(
                f"{rel}: carries the PSF header but is not listed in THIRD-PARTY-NOTICES.txt"
            )
    assert not problems, "\n".join(problems)


def test_exemptions_are_live_and_cited():
    found = claimants(_sources())
    for rel, reason in EXEMPT.items():
        assert "#2602" in reason, f"{rel}: an exemption cites #2602"
        assert rel in found, (
            f"{rel}: exempt but no longer claims to be a port; delete the row"
        )


def test_licence_text_is_the_cpython_file():
    data = NOTICES.read_bytes()
    tail = b"".join(data.splitlines(keepends=True)[-LICENSE_LINES:])
    assert hashlib.sha256(tail).hexdigest() == LICENSE_SHA256
