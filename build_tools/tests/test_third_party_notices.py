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
* the licence text at the end of the notices file is the CPython 3.15.2 ``LICENSE.txt``, byte for byte;
* every listed file has a numbered entry (derived portions, clause-3 changes), and every entry names
  only listed files;
* ``STDLIB_CENSUS`` holds the #2602 audit's verdict for every Sharpy.Stdlib module with a CPython
  namesake: its keys are exactly the namesake modules on disk (a new module is red until someone reads
  it against its CPython source and records a verdict), a ``port`` module has a noticed file, and a
  ``behaviour`` module has none.

The claim scan only catches files that declare themselves ports; a port that makes no such claim (a
structural port with no comment saying so) is caught by the census, which forces a reading of each new
module. A Core file is not a module and is not in the census: its notice rests on the claim scan and on
review.

A file that only follows CPython's behaviour should say so ("matching", "the rule of"), not "port":
the word is a licence claim. Do not reword a comment to get past this test if the code is a port.
"""

from __future__ import annotations

import hashlib
import re
import sys
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


# ---- Numbered entries: every listed file has its portions and changes written down ----

_ENTRY_START = re.compile(r"^\d+\. ")
_ENTRY_PATH = re.compile(r"src/\S+?\.(?:cs|spy)\b")


def entry_files(notices: str) -> Set[str]:
    """The paths named in the headings of the numbered entries (a heading runs to its blank line)."""
    named: Set[str] = set()
    heading: List[str] = []
    for line in notices.splitlines():
        if _ENTRY_START.match(line):
            heading = [line]
        elif heading and line.strip():
            heading.append(line)
        elif heading:
            named.update(_ENTRY_PATH.findall(" ".join(heading)))
            heading = []
    return named


def test_every_listed_file_has_an_entry():
    notices = NOTICES.read_text(encoding="utf-8")
    listed = listed_files(notices)
    named = entry_files(notices)
    problems = [
        f"{rel}: listed but has no numbered entry" for rel in sorted(listed - named)
    ]
    problems += [
        f"{rel}: has an entry but is not listed" for rel in sorted(named - listed)
    ]
    assert not problems, "\n".join(problems)


# ---- The #2602 census: every Stdlib module with a CPython namesake, read against its source ----

STDLIB = "src/Sharpy.Stdlib"

# Module directory -> the #2602 audit's verdict. "port": code translated from CPython (same
# decomposition, names, constants, regexes or tables), so the module carries the PSF notice (header
# lines + a THIRD-PARTY-NOTICES.txt entry). "behaviour": the same semantics, independently written
# (over .NET, or a different algorithm); matching CPython's error messages or output format alone is
# behaviour. Read against CPython 3.15.2's Lib (and 3.14.0's C sources and Doc/) in November 2026.
# A new module is red here until it is read the same way: add its row, never a blanket default.
STDLIB_CENSUS: Dict[str, str] = {
    "Argparse": "behaviour",  # argparse.py: own parser and help layout
    "Base64": "behaviour",  # base64.py: System.Convert + textbook base32/base85 loops
    "Bisect": "behaviour",  # bisect.py: textbook binary search
    "Calendar": "behaviour",  # calendar.py: System.DateTime; HTMLCalendar emits CPython's markup
    "Collections": "behaviour",  # collections/__init__.py: wrappers over Sharpy Dict/List
    "Colorsys": "port",  # colorsys.py: every function, its names and YIQ coefficients
    "Configparser": "behaviour",  # configparser.py: own line parser; exception messages match
    "Csv": "behaviour",  # _csv.c: own quote state machine
    "Datetime": "port",  # _datetimemodule.c: DatetimeIso.cs (fromisoformat parsers)
    "Difflib": "port",  # difflib.py: SequenceMatcher, Differ, the diff functions
    "Email": "behaviour",  # email package: own header/body split
    "Fnmatch": "port",  # fnmatch.py: translate's bracket scan (spy source + generated C#)
    "Fractions": "port",  # fractions.py: Fraction.Format.cs, Fraction.cs limit_denominator
    "Functools": "behaviour",  # functools.py: reduce loop, own LRU cache
    "Glob": "behaviour",  # glob.py: Directory enumeration + own pattern regex
    "Gzip": "behaviour",  # gzip.py: System.IO.Compression
    "Hashlib": "behaviour",  # hashlib.py: System.Security.Cryptography
    "Heapq": "behaviour",  # heapq.py: textbook sift loops; merge is an own k-way heap
    "Hmac": "behaviour",  # hmac.py: System.Security.Cryptography
    "Html": "behaviour",  # html/parser.py: own scanner, WebUtility.HtmlDecode
    "Http": "behaviour",  # http/: HttpClient; HTTPStatus phrases are RFC text
    "Io": "behaviour",  # io.py: StringIO over StringBuilder
    "Ipaddress": "port",  # ipaddress.py: __format__, _private_networks tables (borderline)
    "Itertools": "port",  # Doc/library/itertools.rst: combinatorics equivalents (borderline)
    "Json": "behaviour",  # json/: own recursive-descent parser and emitter
    "Logging": "behaviour",  # logging/: minimal Logger
    "Math": "behaviour",  # mathmodule.c: System.Math; fsum is Kahan, not msum
    "Os": "port",  # os.py: makedirs, walk's stack order (borderline)
    "Pathlib": "behaviour",  # pathlib/: System.IO.Path
    "Platform": "behaviour",  # platform.py: RuntimeInformation
    "Pprint": "behaviour",  # pprint.py: own reflection-based layout
    "Random": "behaviour",  # random.py: System.Random, not Mersenne Twister
    "Re": "behaviour",  # re/: System.Text.RegularExpressions + pattern translation
    "Secrets": "behaviour",  # secrets.py: RandomNumberGenerator
    "Shlex": "behaviour",  # shlex.py: own POSIX split state machine
    "Shutil": "port",  # shutil.py: move, _basename, _destinsrc (borderline)
    "Socket": "behaviour",  # socketmodule.c: System.Net.Sockets
    "Sqlite3": "behaviour",  # _sqlite: Microsoft.Data.Sqlite
    "Statistics": "behaviour",  # statistics.py: naive float sums, not exact _sum
    "String": "behaviour",  # string/: the constants only
    "Struct": "behaviour",  # _struct.c: own format parser over BinaryPrimitives
    "Subprocess": "behaviour",  # subprocess.py: System.Diagnostics.Process
    "Sys": "behaviour",  # sys: Environment
    "Tarfile": "behaviour",  # tarfile.py: System.Formats.Tar
    "Tempfile": "behaviour",  # tempfile.py: Path.GetRandomFileName
    "Textwrap": "behaviour",  # textwrap.py: own greedy wrap, not TextWrapper
    "Threading": "port",  # threading.py: Barrier.cs (ThreadRegistry.cs is behaviour)
    "Time": "behaviour",  # timemodule.c: Stopwatch/DateTime
    "Unittest": "behaviour",  # unittest/: xUnit-backed assertions
    "Urllib": "port",  # urllib/parse.py: uses_netloc/uses_params tables (borderline)
    "Uuid": "behaviour",  # uuid.py: RFC 4122 over Guid/MD5/SHA1
    "Xml": "behaviour",  # xml/etree/: System.Xml.Linq
    "Zipfile": "behaviour",  # zipfile/: System.IO.Compression.ZipArchive
    "Zlib": "behaviour",  # zlibmodule.c: System.IO.Compression; textbook CRC32/Adler-32
    "Zoneinfo": "behaviour",  # zoneinfo/: TimeZoneInfo
}
VERDICTS = {"port", "behaviour"}


def stdlib_modules(root: Path) -> List[str]:
    """Module directories under src/Sharpy.Stdlib: a directory holding C# source (not bin/obj)."""
    base = root / STDLIB
    if not base.is_dir():
        return []
    found = []
    for child in sorted(base.iterdir()):
        if not child.is_dir() or child.name in ("bin", "obj"):
            continue
        if any(
            p.is_file() and "bin" not in p.parts and "obj" not in p.parts
            for p in child.rglob("*.cs")
        ):
            found.append(child.name)
    return found


def namesake_modules(root: Path, stdlib_names: Iterable[str]) -> Set[str]:
    names = set(stdlib_names)
    return {m for m in stdlib_modules(root) if m.lower() in names}


def census_problems(
    root: Path,
    census: Dict[str, str],
    listed: Set[str],
    stdlib_names: Iterable[str],
) -> List[str]:
    problems = []
    on_disk = namesake_modules(root, stdlib_names)
    for module in sorted(on_disk - set(census)):
        problems.append(
            f"{STDLIB}/{module}: a CPython namesake with no STDLIB_CENSUS row; read it against "
            "CPython's source and record 'port' or 'behaviour' (#2602)"
        )
    for module in sorted(set(census) - on_disk):
        problems.append(
            f"{STDLIB}/{module}: in STDLIB_CENSUS but not a namesake module on disk; delete the row"
        )
    stdlib_root = root / STDLIB
    for module, verdict in sorted(census.items()):
        if verdict not in VERDICTS:
            problems.append(
                f"{module}: verdict {verdict!r} is not one of {sorted(VERDICTS)}"
            )
            continue
        if module not in on_disk:
            continue
        headed = sorted(
            path.relative_to(root).as_posix()
            for path in sources(root)
            if stdlib_root in path.parents
            and path.relative_to(stdlib_root).parts[0] == module
            and _has_header(path.read_text(encoding="utf-8"))
        )
        if verdict == "port":
            if not headed:
                problems.append(
                    f"{STDLIB}/{module}: verdict 'port' but no file in it carries the PSF header"
                )
            for rel in headed:
                if rel not in listed:
                    problems.append(
                        f"{rel}: in a 'port' module but not listed in THIRD-PARTY-NOTICES.txt"
                    )
        elif headed:
            problems.append(
                f"{STDLIB}/{module}: verdict 'behaviour' but {', '.join(headed)} carries the PSF "
                "header; the verdict or the header is stale"
            )
    return problems


def _stdlib_module_names() -> Set[str]:
    names = getattr(sys, "stdlib_module_names", None)
    if names is None:
        pytest.skip(
            "sys.stdlib_module_names needs Python 3.10+; the oracle is CPython 3.15"
        )
    return set(names)


def test_census_positive_control():
    # The census sees the module tree: port modules and a behaviour module are all on disk.
    on_disk = namesake_modules(REPO, _stdlib_module_names())
    assert {"Colorsys", "Difflib", "Datetime", "Bisect"} <= on_disk


def test_stdlib_census_is_complete_and_honoured():
    listed = listed_files(NOTICES.read_text(encoding="utf-8"))
    problems = census_problems(REPO, STDLIB_CENSUS, listed, _stdlib_module_names())
    assert not problems, "\n".join(problems)


def _module(tmp_path: Path, name: str, text: str) -> None:
    path = tmp_path / STDLIB / name / f"{name}.cs"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


_HEADED = "".join(f"// {line}\n" for line in HEADER_LINES) + "namespace Sharpy { }\n"
_PLAIN = "namespace Sharpy { }\n"


def test_census_refuses_an_unread_namesake(tmp_path: Path):
    _module(tmp_path, "Pickle", _PLAIN)
    _module(tmp_path, "Grapheme", _PLAIN)  # no CPython namesake: needs no row
    problems = census_problems(tmp_path, {}, set(), {"pickle"})
    assert len(problems) == 1 and "Pickle: a CPython namesake" in problems[0], problems


def test_census_refuses_a_stale_row(tmp_path: Path):
    problems = census_problems(tmp_path, {"Pickle": "behaviour"}, set(), {"pickle"})
    assert len(problems) == 1 and "not a namesake module on disk" in problems[0], (
        problems
    )


def test_census_refuses_an_unnoticed_port(tmp_path: Path):
    _module(tmp_path, "Pickle", _PLAIN)
    problems = census_problems(tmp_path, {"Pickle": "port"}, set(), {"pickle"})
    assert (
        len(problems) == 1 and "no file in it carries the PSF header" in problems[0]
    ), problems


def test_census_refuses_an_unlisted_port(tmp_path: Path):
    _module(tmp_path, "Pickle", _HEADED)
    problems = census_problems(tmp_path, {"Pickle": "port"}, set(), {"pickle"})
    assert len(problems) == 1 and "not listed" in problems[0], problems


def test_census_refuses_a_headed_behaviour_module(tmp_path: Path):
    _module(tmp_path, "Pickle", _HEADED)
    listed = {f"{STDLIB}/Pickle/Pickle.cs"}
    problems = census_problems(tmp_path, {"Pickle": "behaviour"}, listed, {"pickle"})
    assert len(problems) == 1 and "verdict 'behaviour'" in problems[0], problems


def test_census_accepts_a_noticed_port(tmp_path: Path):
    _module(tmp_path, "Pickle", _HEADED)
    listed = {f"{STDLIB}/Pickle/Pickle.cs"}
    assert census_problems(tmp_path, {"Pickle": "port"}, listed, {"pickle"}) == []
