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
  it against its CPython source and records a verdict), the files in a ``port`` module that carry the
  header are exactly the files its row names (a lost header and an extra one are both red), and a
  ``behaviour`` module has none;
* the files the notices list under "Sharpy files holding these summaries:" are exactly the files
  whose API summaries repeat a CPython 3.15 Lib docstring (documentation, so no PSF header).

The claim scan only catches files that declare themselves ports; a port that makes no such claim (a
structural port with no comment saying so) is caught by the census, which forces a reading of each new
module. A Core file is not a module and is not in the census: its notice rests on the claim scan and on
review.

A file that only follows CPython's behaviour should say so ("matching", "the rule of"), not "port":
the word is a licence claim. Do not reword a comment to get past this test if the code is a port.
"""

from __future__ import annotations

import hashlib
import inspect
import re
import sys
from pathlib import Path
from typing import (
    Callable,
    Dict,
    FrozenSet,
    Iterable,
    List,
    Mapping,
    Optional,
    Set,
    Tuple,
)

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


CPYTHON_LABEL = "Sharpy files derived from it:"


def cpython_heading(lines: List[str]) -> int:
    """The index of the CPython section's heading ("CPython" over "=======")."""
    for i in range(len(lines) - 1):
        if lines[i] == "CPython" and lines[i + 1] == "=" * len("CPython"):
            return i
    raise ValueError("THIRD-PARTY-NOTICES.txt has no 'CPython' section heading")


def listed_files(notices: str, label: str = CPYTHON_LABEL) -> Set[str]:
    """The paths under the first `label` line of the CPython section (one per 4-space-indented
    line). Read from the CPython heading, so a section before it that uses the same label is never
    taken for the CPython list."""
    lines = notices.splitlines()
    start = lines.index(label, cpython_heading(lines))
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


def test_the_cpython_list_is_read_from_the_cpython_section():
    # The Unicode and PyYAML sections come before CPython's (its licence must stay the file's
    # tail). If one of them used the CPython label, a search from the top would read its list as
    # the CPython list; the search starts at the CPython heading instead.
    notices = (
        "PyYAML\n======\n\n"
        f"{CPYTHON_LABEL}\n\n    src/Sharpy.Stdlib/Yaml/Yaml.cs\n\n\n"
        "CPython\n=======\n\n"
        f"{CPYTHON_LABEL}\n\n    src/Sharpy.Core/PyFormat.cs\n    src/Sharpy.Core/FloorDiv.cs\n\n"
    )
    assert listed_files(notices) == {
        "src/Sharpy.Core/PyFormat.cs",
        "src/Sharpy.Core/FloorDiv.cs",
    }


def test_only_the_cpython_section_uses_the_cpython_label():
    lines = NOTICES.read_text(encoding="utf-8").splitlines()
    uses = [i for i, line in enumerate(lines) if line == CPYTHON_LABEL]
    assert len(uses) == 1 and uses[0] > cpython_heading(lines), (
        f"{CPYTHON_LABEL!r} is the CPython section's label; another section names its files "
        "with a label of its own"
    )


# ---- Sources other than CPython (Unicode, PyYAML): the notices and the file headers agree ----
#
# A file derived from a non-CPython source carries a header comment naming it and ending "see
# THIRD-PARTY-NOTICES.txt" (no PSF lines), and is listed under OTHER_LABEL in that source's section.
# The two sets must be equal. What this cannot see is a derived file with neither: review reads it.

OTHER_LABEL = "Sharpy files derived from this source:"
NOTICES_POINTER = "see THIRD-PARTY-NOTICES.txt"


def other_listed_files(notices: str) -> Set[str]:
    """The source paths under every OTHER_LABEL line (continuation lines name no path)."""
    lines = notices.splitlines()
    listed: Set[str] = set()
    for i, line in enumerate(lines):
        if line != OTHER_LABEL:
            continue
        block: List[str] = []
        for entry in lines[i + 1 :]:
            if not entry.strip():
                if block:
                    break
                continue
            if not entry.startswith("    "):
                break
            block.append(entry)
        listed.update(_ENTRY_PATH.findall("\n".join(block)))
    return listed


def other_headed_files(root: Path) -> Set[str]:
    return {
        path.relative_to(root).as_posix()
        for path in sources(root)
        for text in [path.read_text(encoding="utf-8")]
        if NOTICES_POINTER in text and not _has_header(text)
    }


def other_problems(listed: Set[str], headed: Set[str]) -> List[str]:
    problems = [
        f"{rel}: points at THIRD-PARTY-NOTICES.txt but is not under {OTHER_LABEL!r}"
        for rel in sorted(headed - listed)
    ]
    problems += [
        f"{rel}: under {OTHER_LABEL!r} but carries no header pointing at the notices"
        for rel in sorted(listed - headed)
    ]
    return problems


def test_non_cpython_sources_and_headers_agree():
    notices = NOTICES.read_text(encoding="utf-8")
    listed = other_listed_files(notices)
    headed = other_headed_files(REPO)
    # Positive control: both sections and the files they name are seen.
    assert {
        "src/Sharpy.Core/StringExtensions.Format.cs",
        "src/Sharpy.Stdlib/Yaml/YamlScalarResolver.cs",
    } <= listed, sorted(listed)
    problems = other_problems(listed, headed)
    assert not problems, "\n".join(problems)
    lines = notices.splitlines()
    uses = [i for i, line in enumerate(lines) if line == OTHER_LABEL]
    assert all(i < cpython_heading(lines) for i in uses), (
        f"{OTHER_LABEL!r} labels the sections before CPython's only"
    )


def test_non_cpython_check_refuses_either_side_alone(tmp_path: Path):
    notices = (
        f"PyYAML\n======\n\n{OTHER_LABEL}\n\n    src/Sharpy.Stdlib/Yaml/A.cs (only X)\n"
        "    src/Sharpy.Stdlib/Yaml/B.cs\n\n"
    )
    listed = other_listed_files(notices)
    assert listed == {"src/Sharpy.Stdlib/Yaml/A.cs", "src/Sharpy.Stdlib/Yaml/B.cs"}
    assert other_problems(listed, listed) == []
    unheaded = other_problems(listed, {"src/Sharpy.Stdlib/Yaml/A.cs"})
    assert len(unheaded) == 1 and "B.cs: under" in unheaded[0], unheaded
    unlisted = other_problems({"src/Sharpy.Stdlib/Yaml/A.cs"}, listed)
    assert len(unlisted) == 1 and "B.cs: points at" in unlisted[0], unlisted


# ---- The #2602 census: every Stdlib module with a CPython namesake, read against its source ----

STDLIB = "src/Sharpy.Stdlib"

# Module directory -> the #2602 audit's verdict. "port": code translated from CPython (same
# decomposition, names, constants, regexes or tables), so the module carries the PSF notice (header
# lines + a THIRD-PARTY-NOTICES.txt entry). "behaviour": the same semantics, independently written
# (over .NET, or a different algorithm); matching CPython's error messages or output format alone is
# behaviour. Read against CPython 3.15.2's Lib (and 3.14.0's C sources and Doc/) in November 2026.
# A new module is red here until it is read the same way: add its row, never a blanket default.
#
# A port row names its exact headed files (relative to src/Sharpy.Stdlib), and the files in the module
# that carry the PSF header must be exactly that set: a lost header (a ported file that drifted out of
# the notice) and an extra one (a behaviour file in a port module, headed by mistake) are both red. A
# behaviour row's set is empty. What is enforced is that equality, nothing more: a file without the
# header is not seen, so a new unheaded file in a port module stays green, and so does a row edited
# together with its header. The row is the audit's record; review reads it.
BEHAVIOUR: Tuple[str, FrozenSet[str]] = ("behaviour", frozenset())


def port(*files: str) -> Tuple[str, FrozenSet[str]]:
    return ("port", frozenset(files))


STDLIB_CENSUS: Dict[str, Tuple[str, FrozenSet[str]]] = {
    "Argparse": BEHAVIOUR,  # argparse.py: own parser and help layout
    "Base64": BEHAVIOUR,  # base64.py: System.Convert + textbook base32/base85 loops
    "Bisect": BEHAVIOUR,  # bisect.py: textbook binary search
    "Calendar": BEHAVIOUR,  # calendar.py: System.DateTime; HTMLCalendar emits CPython's markup
    "Collections": BEHAVIOUR,  # collections/__init__.py: wrappers over Sharpy Dict/List
    # colorsys.py: every function, its names and YIQ coefficients
    "Colorsys": port("Colorsys/Colorsys.cs"),
    "Configparser": BEHAVIOUR,  # configparser.py: own line parser; exception messages match
    "Csv": BEHAVIOUR,  # _csv.c: own quote state machine
    # _datetimemodule.c: DatetimeIso.cs (fromisoformat parsers)
    "Datetime": port("Datetime/DatetimeIso.cs"),
    # difflib.py: SequenceMatcher, Differ, the diff functions
    "Difflib": port(
        "Difflib/SequenceMatcher.cs",
        "Difflib/Differ.cs",
        "Difflib/DifflibFunctions.cs",
    ),
    "Email": BEHAVIOUR,  # email package: own header/body split
    # fnmatch.py: translate's bracket scan (spy source + generated C#)
    "Fnmatch": port("Fnmatch/FnmatchModule.cs"),
    # fractions.py: Fraction.Format.cs, Fraction.cs limit_denominator
    "Fractions": port("Fractions/Fraction.Format.cs", "Fractions/Fraction.cs"),
    "Functools": BEHAVIOUR,  # functools.py: reduce loop, own LRU cache
    "Glob": BEHAVIOUR,  # glob.py: Directory enumeration + own pattern regex
    "Gzip": BEHAVIOUR,  # gzip.py: System.IO.Compression
    "Hashlib": BEHAVIOUR,  # hashlib.py: System.Security.Cryptography
    "Heapq": BEHAVIOUR,  # heapq.py: textbook sift loops; merge is an own k-way heap
    "Hmac": BEHAVIOUR,  # hmac.py: System.Security.Cryptography
    "Html": BEHAVIOUR,  # html/parser.py: own scanner, WebUtility.HtmlDecode
    "Http": BEHAVIOUR,  # http/: HttpClient; HTTPStatus phrases are RFC text
    "Io": BEHAVIOUR,  # io.py: StringIO over StringBuilder
    # ipaddress.py: __format__, _private_networks tables (borderline)
    "Ipaddress": port(
        "Ipaddress/AddressFormat.cs",
        "Ipaddress/IPv4Address.cs",
        "Ipaddress/IPv6Address.cs",
    ),
    # Doc/library/itertools.rst: combinatorics equivalents (borderline)
    "Itertools": port("Itertools/Itertools.cs"),
    "Json": BEHAVIOUR,  # json/: own recursive-descent parser and emitter
    "Logging": BEHAVIOUR,  # logging/: minimal Logger
    "Math": BEHAVIOUR,  # mathmodule.c: System.Math; fsum is Kahan, not msum
    # os.py: makedirs, walk's stack order (borderline)
    "Os": port("Os/Os.cs"),
    "Pathlib": BEHAVIOUR,  # pathlib/: System.IO.Path
    "Platform": BEHAVIOUR,  # platform.py: RuntimeInformation
    "Pprint": BEHAVIOUR,  # pprint.py: own reflection-based layout
    "Random": BEHAVIOUR,  # random.py: System.Random, not Mersenne Twister
    "Re": BEHAVIOUR,  # re/: System.Text.RegularExpressions + pattern translation
    "Secrets": BEHAVIOUR,  # secrets.py: RandomNumberGenerator
    "Shlex": BEHAVIOUR,  # shlex.py: own POSIX split state machine
    # shutil.py: move, _basename, _destinsrc (borderline)
    "Shutil": port("Shutil/Shutil.cs"),
    "Socket": BEHAVIOUR,  # socketmodule.c: System.Net.Sockets
    "Sqlite3": BEHAVIOUR,  # _sqlite: Microsoft.Data.Sqlite
    "Statistics": BEHAVIOUR,  # statistics.py: naive float sums, not exact _sum
    "String": BEHAVIOUR,  # string/: the constants only
    "Struct": BEHAVIOUR,  # _struct.c: own format parser over BinaryPrimitives
    "Subprocess": BEHAVIOUR,  # subprocess.py: System.Diagnostics.Process
    "Sys": BEHAVIOUR,  # sys: Environment
    "Tarfile": BEHAVIOUR,  # tarfile.py: System.Formats.Tar
    "Tempfile": BEHAVIOUR,  # tempfile.py: Path.GetRandomFileName
    "Textwrap": BEHAVIOUR,  # textwrap.py: own greedy wrap, not TextWrapper
    # threading.py: Barrier.cs (ThreadRegistry.cs is behaviour)
    "Threading": port("Threading/Barrier.cs"),
    "Time": BEHAVIOUR,  # timemodule.c: Stopwatch/DateTime
    "Unittest": BEHAVIOUR,  # unittest/: xUnit-backed assertions
    # urllib/parse.py: uses_netloc/uses_params tables (borderline)
    "Urllib": port("Urllib/UrllibModule.cs"),
    "Uuid": BEHAVIOUR,  # uuid.py: RFC 4122 over Guid/MD5/SHA1
    "Xml": BEHAVIOUR,  # xml/etree/: System.Xml.Linq
    "Zipfile": BEHAVIOUR,  # zipfile/: System.IO.Compression.ZipArchive
    "Zlib": BEHAVIOUR,  # zlibmodule.c: System.IO.Compression; textbook CRC32/Adler-32
    "Zoneinfo": BEHAVIOUR,  # zoneinfo/: TimeZoneInfo
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
    census: Dict[str, Tuple[str, FrozenSet[str]]],
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
    for module, row in sorted(census.items()):
        if not (
            isinstance(row, tuple)
            and len(row) == 2
            and isinstance(row[1], (set, frozenset))
            and all(isinstance(rel, str) and rel for rel in row[1])
        ):
            problems.append(
                f"{module}: row {row!r} is not (verdict, set of file paths); "
                "use BEHAVIOUR or port(...)"
            )
            continue
        verdict, recorded = row
        if verdict not in VERDICTS:
            problems.append(
                f"{module}: verdict {verdict!r} is not one of {sorted(VERDICTS)}"
            )
            continue
        if verdict == "port" and not recorded:
            problems.append(
                f"{module}: verdict 'port' but the row names no headed file"
            )
        if verdict == "behaviour" and recorded:
            problems.append(
                f"{module}: verdict 'behaviour' but the row names files "
                f"{sorted(recorded)}; a behaviour row's set is empty"
            )
            continue
        for rel in sorted(recorded):
            if Path(rel).parts[0] != module:
                problems.append(
                    f"{module}: the row names {rel}, which is not in the module"
                )
        if module not in on_disk:
            continue
        headed = {
            path.relative_to(stdlib_root).as_posix()
            for path in sources(root)
            if stdlib_root in path.parents
            and path.relative_to(stdlib_root).parts[0] == module
            and _has_header(path.read_text(encoding="utf-8"))
        }
        if verdict == "behaviour":
            if headed:
                problems.append(
                    f"{STDLIB}/{module}: verdict 'behaviour' but "
                    f"{', '.join(sorted(headed))} carries the PSF header; the verdict or the "
                    "header is stale"
                )
            continue
        for rel in sorted(recorded - headed):
            problems.append(
                f"{STDLIB}/{rel}: in {module}'s port set but carries no PSF header (lost "
                "header, or a stale row)"
            )
        for rel in sorted(headed - recorded):
            problems.append(
                f"{STDLIB}/{rel}: carries the PSF header but is not in {module}'s port set; "
                "record it if it is a port, else drop the header"
            )
        for rel in sorted(recorded | headed):
            if f"{STDLIB}/{rel}" not in listed:
                problems.append(
                    f"{STDLIB}/{rel}: in a 'port' module but not listed in "
                    "THIRD-PARTY-NOTICES.txt"
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


def _module(tmp_path: Path, name: str, text: str, file: str = "") -> None:
    path = tmp_path / STDLIB / name / (file or f"{name}.cs")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


_HEADED = "".join(f"// {line}\n" for line in HEADER_LINES) + "namespace Sharpy { }\n"
_PLAIN = "namespace Sharpy { }\n"
_PICKLE = "Pickle/Pickle.cs"
_LISTED = {f"{STDLIB}/{_PICKLE}"}


def test_census_refuses_an_unread_namesake(tmp_path: Path):
    _module(tmp_path, "Pickle", _PLAIN)
    _module(tmp_path, "Grapheme", _PLAIN)  # no CPython namesake: needs no row
    problems = census_problems(tmp_path, {}, set(), {"pickle"})
    assert len(problems) == 1 and "Pickle: a CPython namesake" in problems[0], problems


def test_census_refuses_a_stale_row(tmp_path: Path):
    problems = census_problems(tmp_path, {"Pickle": BEHAVIOUR}, set(), {"pickle"})
    assert len(problems) == 1 and "not a namesake module on disk" in problems[0], (
        problems
    )


def test_census_refuses_a_port_row_with_no_files(tmp_path: Path):
    _module(tmp_path, "Pickle", _PLAIN)
    problems = census_problems(tmp_path, {"Pickle": port()}, set(), {"pickle"})
    assert len(problems) == 1 and "names no headed file" in problems[0], problems


def test_census_refuses_a_behaviour_row_with_files(tmp_path: Path):
    _module(tmp_path, "Pickle", _PLAIN)
    census = {"Pickle": ("behaviour", frozenset({_PICKLE}))}
    problems = census_problems(tmp_path, census, set(), {"pickle"})
    assert len(problems) == 1 and "set is empty" in problems[0], problems


def test_census_refuses_a_row_naming_another_module(tmp_path: Path):
    _module(tmp_path, "Pickle", _HEADED)
    census = {"Pickle": port(_PICKLE, "Shelve/Shelve.cs")}
    problems = census_problems(tmp_path, census, _LISTED, {"pickle"})
    assert any("not in the module" in p for p in problems), problems


def test_census_refuses_an_unnoticed_port(tmp_path: Path):
    _module(tmp_path, "Pickle", _PLAIN)
    census = {"Pickle": port(_PICKLE)}
    problems = census_problems(tmp_path, census, _LISTED, {"pickle"})
    assert len(problems) == 1 and "carries no PSF header" in problems[0], problems


def test_census_refuses_a_lost_header_in_a_multi_file_port(tmp_path: Path):
    # The per-module census missed this: one file of a port module losing its header (and its
    # notice lines) while a sibling keeps one.
    _module(tmp_path, "Pickle", _HEADED)
    _module(tmp_path, "Pickle", _PLAIN, "Unpickler.cs")
    census = {"Pickle": port(_PICKLE, "Pickle/Unpickler.cs")}
    problems = census_problems(tmp_path, census, _LISTED, {"pickle"})
    assert any(
        "Pickle/Unpickler.cs: in Pickle's port set but carries no PSF header" in p
        for p in problems
    ), problems


def test_census_refuses_an_extra_header_in_a_port_module(tmp_path: Path):
    _module(tmp_path, "Pickle", _HEADED)
    _module(tmp_path, "Pickle", _HEADED, "Registry.cs")
    listed = _LISTED | {f"{STDLIB}/Pickle/Registry.cs"}
    problems = census_problems(tmp_path, {"Pickle": port(_PICKLE)}, listed, {"pickle"})
    assert len(problems) == 1 and "not in Pickle's port set" in problems[0], problems


def test_census_refuses_an_unlisted_port(tmp_path: Path):
    _module(tmp_path, "Pickle", _HEADED)
    problems = census_problems(tmp_path, {"Pickle": port(_PICKLE)}, set(), {"pickle"})
    assert len(problems) == 1 and "not listed" in problems[0], problems


def test_census_refuses_a_headed_behaviour_module(tmp_path: Path):
    _module(tmp_path, "Pickle", _HEADED)
    problems = census_problems(tmp_path, {"Pickle": BEHAVIOUR}, _LISTED, {"pickle"})
    assert len(problems) == 1 and "verdict 'behaviour'" in problems[0], problems


def test_census_accepts_a_noticed_port(tmp_path: Path):
    _module(tmp_path, "Pickle", _HEADED)
    _module(
        tmp_path, "Pickle", _PLAIN, "Registry.cs"
    )  # a behaviour file in a port module
    census = {"Pickle": port(_PICKLE)}
    assert census_problems(tmp_path, census, _LISTED, {"pickle"}) == []


@pytest.mark.parametrize("row", ["port", port(""), ("port",), ("port", [_PICKLE])])
def test_census_names_a_malformed_row(tmp_path: Path, row):
    _module(tmp_path, "Pickle", _HEADED)
    problems = census_problems(tmp_path, {"Pickle": row}, _LISTED, {"pickle"})
    assert (
        len(problems) == 1 and "is not (verdict, set of file paths)" in problems[0]
    ), problems


# ---- API summaries adapted from CPython docstrings (#2602 follow-up, owner 2026-11-20) ----
#
# Some API summaries (a .spy docstring, a C# <summary>) repeat CPython docstring sentences word for
# word. That is documentation, not code: the files keep no PSF header (a "behaviour" census row stays
# header-free), and THIRD-PARTY-NOTICES.txt credits them in one section whose file list must equal
# the set this matcher finds.
#
# The rule (#2777 review, widened by the #2780 review): `doc_keys` turns a text into keys, one per
# paragraph and one per sentence of each paragraph, with `*` and backticks (reST markup) removed,
# whitespace collapsed, case and a final full stop ignored. The index holds every key of at least
# DOCSTRING_MIN characters from (a) the module, class and function docstrings of CPython's Lib (its
# test packages, idlelib and site-packages are not read) and (b) the __doc__ of every built-in or
# C-implemented module (sys.builtin_module_names and lib-dynload, test modules aside), of its
# functions and classes, and of their methods and descriptors. A Sharpy file is a match when a key
# of one of its .spy docstrings or C# summaries is in the index. C# generated from a .spy file is
# skipped: the .spy file is listed for it, as the notices file says. Paraphrases, and text from
# CPython's Doc/*.rst, are not matched (the notices file says so).
#
# The oracle is the CPython minor in .python-version. Without that interpreter the guard skips
# locally and fails on CI (`CI` set), so moving the oracle can never turn it into a silent skip.

DOCSTRING_LABEL = "Sharpy files holding these summaries:"
DOCSTRING_HEADING = "API documentation summaries adapted from CPython docstrings"
DOCSTRING_MIN = 30
PYTHON_VERSION_FILE = REPO / ".python-version"


def doc_keys(text):
    """The match keys of a docstring or summary: each paragraph, and each sentence in it."""
    import re

    keys = []
    for para in text.strip().split("\n\n"):
        flat = " ".join(re.sub(r"[*`]", "", para).split())
        for part in [flat] + re.split(r"(?<=[.!?])\s+(?=[A-Z])", flat):
            keys.append(part.rstrip(".").lower())
    return keys


# Run under the oracle's python (`-I`): prints {"version": ..., "index": {key: where}}, `where` being
# "Lib/<path>" or "<module>.<name>". argv[1], when not empty, is the Lib directory to read instead of
# the interpreter's own; argv[2] == "--no-runtime" skips the built-in and C-implemented modules.
_INDEX_SCRIPT = (
    inspect.getsource(doc_keys)
    + r"""
import ast, importlib, inspect, json, os, sys

MIN = %d
lib = sys.argv[1] if len(sys.argv) > 1 and sys.argv[1] else os.path.dirname(os.__file__)
runtime = not (len(sys.argv) > 2 and sys.argv[2] == "--no-runtime")
index = {}


def add(doc, where):
    for key in doc_keys(doc):
        if len(key) >= MIN:
            index.setdefault(key, where)


for dirpath, dirnames, filenames in os.walk(lib):
    dirnames.sort()
    for name in sorted(filenames):
        if not name.endswith(".py"):
            continue
        path = os.path.join(dirpath, name)
        rel = os.path.relpath(path, lib).replace(os.sep, "/")
        if "/test" in "/" + rel or "site-packages" in rel or "idlelib" in rel:
            continue
        try:
            with open(path, encoding="utf-8") as f:
                tree = ast.parse(f.read())
        except Exception:
            continue
        for node in ast.walk(tree):
            if isinstance(node, (ast.Module, ast.ClassDef, ast.FunctionDef, ast.AsyncFunctionDef)):
                doc = ast.get_docstring(node)
                if doc:
                    add(doc, "Lib/" + rel)


def members(module, label):
    for name, obj in sorted(vars(module).items()):
        if not (inspect.isroutine(obj) or inspect.isclass(obj)):
            continue
        if isinstance(obj.__doc__, str):
            add(obj.__doc__, label + "." + name)
        if not inspect.isclass(obj):
            continue
        for attr, member in sorted(vars(obj).items()):
            if not (
                inspect.isroutine(member)
                or inspect.isdatadescriptor(member)
                or isinstance(member, (classmethod, staticmethod))
            ):
                continue
            doc = getattr(member, "__doc__", None)
            if isinstance(doc, str):
                add(doc, label + "." + name + "." + attr)


if runtime:
    names = set(sys.builtin_module_names)
    dynload = os.path.join(lib, "lib-dynload")
    if os.path.isdir(dynload):
        names.update(
            f.split(".")[0] for f in os.listdir(dynload) if f.endswith((".so", ".pyd"))
        )
    for name in sorted(names):
        if name.startswith(("_test", "_xx", "xx")):
            continue
        try:
            module = importlib.import_module(name)
        except Exception:
            continue
        if isinstance(module.__doc__, str):
            add(module.__doc__, name)
        members(module, name)

print(json.dumps({"version": list(sys.version_info[:3]), "index": index}))
"""
    % DOCSTRING_MIN
)

_SPY_DOCSTRING = re.compile(r'"""(.*?)"""', re.S)
_CS_SUMMARY = re.compile(r"<summary>(.*?)</summary>", re.S)
_GENERATED = "// Generated from"


def summary_keys(path: Path, text: str) -> List[str]:
    """The match keys of a file's .spy docstrings or C# summaries."""
    if path.suffix == ".spy":
        return [k for m in _SPY_DOCSTRING.finditer(text) for k in doc_keys(m.group(1))]
    if text.startswith(_GENERATED):
        return []
    keys = []
    for m in _CS_SUMMARY.finditer(text):
        keys += doc_keys(re.sub(r"<[^>]+>", "", re.sub(r"\s*///\s*", " ", m.group(1))))
    return keys


def docstring_matches(root: Path, index: Dict[str, str]) -> Dict[str, List[str]]:
    """Root-relative path -> "'key' <- where" for each of its keys found in the CPython index."""
    found: Dict[str, List[str]] = {}
    for path in sources(root):
        hits = sorted(
            {
                f"{key!r} <- {index[key]}"
                for key in summary_keys(path, path.read_text(encoding="utf-8"))
                if key in index
            }
        )
        if hits:
            found[path.relative_to(root).as_posix()] = hits
    return found


def docstring_problems(listed: Set[str], found: Dict[str, List[str]]) -> List[str]:
    problems = [
        f"{rel}: holds CPython docstring text ({found[rel][0]}) but is not under "
        f"{DOCSTRING_LABEL!r} in THIRD-PARTY-NOTICES.txt"
        for rel in sorted(set(found) - listed)
    ]
    problems += [
        f"{rel}: under {DOCSTRING_LABEL!r} in THIRD-PARTY-NOTICES.txt but holds no CPython "
        "docstring text; delete the line"
        for rel in sorted(listed - set(found))
    ]
    return problems


def oracle_minor(version_file: Path) -> Tuple[int, int]:
    """The CPython minor .python-version names (its first line that is not a comment)."""
    for line in version_file.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        m = re.match(r"(\d+)\.(\d+)", line)
        if m:
            return int(m.group(1)), int(m.group(2))
        break
    raise ValueError(f"{version_file}: no CPython minor")


def resolve_oracle(
    oracle: Tuple[int, int],
    version: Tuple[int, ...],
    executable: str,
    which: Callable[[str], Optional[str]],
    environ: Mapping[str, str],
) -> Tuple[str, str]:
    """("run", python), or ("skip" | "fail", reason): fail on CI, where the oracle must exist."""
    if tuple(version[:2]) == oracle:
        return "run", executable
    name = "python%d.%d" % oracle
    found = which(name)
    if found:
        return "run", found
    reason = (
        f"the docstring oracle is CPython {oracle[0]}.{oracle[1]} (.python-version); this is "
        f"Python {version[0]}.{version[1]} and {name} is not on PATH"
    )
    return ("fail" if environ.get("CI") else "skip"), reason


def _run_index(python: str, lib: str = "", runtime: bool = True) -> Dict[str, object]:
    import json
    import subprocess

    argv = [python, "-I", "-c", _INDEX_SCRIPT, lib] + (
        [] if runtime else ["--no-runtime"]
    )
    out = subprocess.run(argv, check=True, capture_output=True, text=True).stdout
    return json.loads(out)


@pytest.fixture(scope="module")
def cpython_docstrings() -> Dict[str, str]:
    import os
    import shutil

    oracle = oracle_minor(PYTHON_VERSION_FILE)
    action, value = resolve_oracle(
        oracle, sys.version_info, sys.executable, shutil.which, os.environ
    )
    if action == "fail":
        pytest.fail(value)
    if action == "skip":
        pytest.skip(value)
    result = _run_index(value)
    assert tuple(result["version"][:2]) == oracle, result["version"]
    return result["index"]


def test_docstring_matcher_sees_the_known_summaries(cpython_docstrings):
    # Positive control: an empty result elsewhere means "none", not "looked nowhere". One Lib
    # docstring and one C-implemented one (math.cosh) are in the index, and files holding each are
    # found.
    assert (
        cpython_docstrings.get("push item onto heap, maintaining the heap invariant")
        == "Lib/heapq.py"
    )
    assert cpython_docstrings.get("return the hyperbolic cosine of x") == "math.cosh"
    found = docstring_matches(REPO, cpython_docstrings)
    assert {
        "src/Sharpy.Stdlib/spy/heapq.spy",
        "src/Sharpy.Stdlib/spy/math_module.spy",
        "src/Sharpy.Stdlib/Pathlib/Path.cs",
    } <= set(found), sorted(found)


def test_docstring_notice_lists_exactly_the_files_holding_cpython_docstrings(
    cpython_docstrings,
):
    notices = NOTICES.read_text(encoding="utf-8")
    listed = listed_files(notices, DOCSTRING_LABEL)
    found = docstring_matches(REPO, cpython_docstrings)
    problems = docstring_problems(listed, found)
    assert not problems, "\n".join(problems)


def test_docstring_notice_names_the_oracle_minor():
    notices = NOTICES.read_text(encoding="utf-8")
    section = notices[notices.index(DOCSTRING_HEADING) :]
    section = section[: section.index("The CPython license text follows")]
    major, minor = oracle_minor(PYTHON_VERSION_FILE)
    assert f"CPython {major}.{minor}" in section, (
        "the docstring section names the oracle CPython minor of .python-version"
    )


def test_docstring_index_rule_over_a_stand_in_lib(tmp_path: Path):
    # The index rule, run by this interpreter over a stand-in Lib (no oracle needed): every
    # paragraph and sentence over the minimum, reST markup removed, test packages not read.
    (tmp_path / "frob.py").write_text(
        '"""Frobnication helpers for widgets and gadgets.\n\nMore text."""\n'
        "def frob(x):\n"
        '    """Return the frobnicated value of the given *widget*.\n\n'
        "    Super-frob; frobnicate a widget and all its ``parts``. Short one.\n"
        '    """\n'
        'def short(x):\n    """Return x unchanged here."""\n',
        encoding="utf-8",
    )
    (tmp_path / "test").mkdir()
    (tmp_path / "test" / "test_frob.py").write_text(
        '"""A test module docstring that is long enough."""\n', encoding="utf-8"
    )
    index = _run_index(sys.executable, str(tmp_path), runtime=False)["index"]
    assert index == {
        "frobnication helpers for widgets and gadgets": "Lib/frob.py",
        "return the frobnicated value of the given widget": "Lib/frob.py",
        "super-frob; frobnicate a widget and all its parts. short one": "Lib/frob.py",
        "super-frob; frobnicate a widget and all its parts": "Lib/frob.py",
    }


def test_docstring_index_reads_c_implemented_modules():
    # The runtime half, under this interpreter: built-in functions and methods are indexed.
    index = _run_index(sys.executable, "/nonexistent-lib")["index"]
    assert index.get("return the identity of an object") == "builtins.id"
    assert index.get("return a shallow copy of the list") == "builtins.list.copy"


_FAKE_INDEX = {
    "return the frobnicated value of the given widget": "Lib/frob.py",
    "super-frob; frobnicate a widget and all its parts": "Lib/frob.py",
    "serialize obj to a json formatted str": "Lib/json/__init__.py",
}


def _source(tmp_path: Path, rel: str, text: str) -> None:
    path = tmp_path / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


@pytest.mark.parametrize(
    "rel, text",
    [
        (
            "src/Sharpy.Stdlib/spy/frob.spy",
            'def frob(x: int) -> int:\n    """Return the frobnicated value of the\n'
            '    given widget.\n\n    Sharpy details."""\n',
        ),
        (  # a later paragraph's sentence
            "src/Sharpy.Stdlib/spy/frob.spy",
            'def frob(x: int) -> int:\n    """Frob it.\n\n'
            '    Super-frob; frobnicate a widget and all its parts. Sharpy adds more."""\n',
        ),
        (  # reST markup on the Sharpy side
            "src/Sharpy.Stdlib/spy/frob.spy",
            'def dumps(obj: object) -> str:\n    """Serialize ``obj`` to a JSON formatted ``str``."""\n',
        ),
        (
            "src/Sharpy.Stdlib/Frob/Frob.cs",
            "/// <summary>\n/// Return the <c>frobnicated</c> value of the given widget.\n/// </summary>\n",
        ),
        (  # a later sentence of a C# summary
            "src/Sharpy.Core/Frob.cs",
            "/// <summary>Frobs. Return the frobnicated value of the given widget. More.</summary>\n",
        ),
    ],
)
def test_docstring_check_refuses_an_unlisted_copy(tmp_path: Path, rel: str, text: str):
    _source(tmp_path, rel, text)
    found = docstring_matches(tmp_path, _FAKE_INDEX)
    assert set(found) == {rel}, found
    problems = docstring_problems(set(), found)
    assert len(problems) == 1 and "is not under" in problems[0], problems
    assert docstring_problems({rel}, found) == []


def test_docstring_check_refuses_a_stale_line_and_skips_generated_csharp(
    tmp_path: Path,
):
    _source(
        tmp_path,
        "src/Sharpy.Stdlib/Frob/Frob.cs",
        _GENERATED + " src/Sharpy.Stdlib/spy/frob.spy\n"
        "/// <summary>Return the frobnicated value of the given widget.</summary>\n",
    )
    found = docstring_matches(tmp_path, _FAKE_INDEX)
    assert found == {}
    problems = docstring_problems({"src/Sharpy.Stdlib/Frob/Frob.cs"}, found)
    assert len(problems) == 1 and "delete the line" in problems[0], problems


def test_oracle_minor_reads_the_version_file(tmp_path: Path):
    path = tmp_path / ".python-version"
    path.write_text("# a comment\n\n3.16\n", encoding="utf-8")
    assert oracle_minor(path) == (3, 16)
    assert oracle_minor(PYTHON_VERSION_FILE)[0] == 3


@pytest.mark.parametrize(
    "version, which, environ, expected",
    [
        ((3, 16, 0), None, {}, ("run", "/py/self")),  # this interpreter is the oracle
        ((3, 15, 2), "/bin/python3.16", {}, ("run", "/bin/python3.16")),
        ((3, 15, 2), None, {}, ("skip", "python3.16 is not on PATH")),
        ((3, 15, 2), None, {"CI": "true"}, ("fail", "python3.16 is not on PATH")),
    ],
)
def test_the_oracle_skips_locally_and_fails_on_ci(version, which, environ, expected):
    action, value = resolve_oracle(
        (3, 16), version, "/py/self", lambda name: which, environ
    )
    assert action == expected[0] and expected[1] in value, (action, value)
