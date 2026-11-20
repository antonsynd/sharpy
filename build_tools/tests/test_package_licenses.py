"""Every NuGet licence expression names only licences nuget.org accepts.

nuget.org refuses a push whose `<PackageLicenseExpression>` names an SPDX id that is not
OSI-approved or FSF-free. `PSF-2.0` is such an id, and the v0.25.0 release lost its Core, Stdlib
and sharpyc pushes to it (HTTP 400); the CPython licence's OSI-approved SPDX id is `Python-2.0`.
The server check runs only at release time, so this test runs it on every change.
"""

from __future__ import annotations

import re
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent.parent

# SPDX ids checked against https://spdx.org/licenses/ (isOsiApproved or isFsfLibre true).
# Add an id only after checking that list; nuget.org refuses the rest.
NUGET_ACCEPTED = {"Apache-2.0", "MIT", "Python-2.0"}

_EXPR = re.compile(
    r"<PackageLicenseExpression\b[^>]*>([^<]*)</PackageLicenseExpression>"
)
_OPERATORS = {"AND", "OR", "WITH"}
_SKIP_DIRS = {"bin", "obj", "node_modules", ".git", ".venv"}


def _project_files() -> list[Path]:
    return sorted(
        f
        for pattern in ("*.csproj", "*.props", "*.targets")
        for f in REPO.rglob(pattern)
        if not _SKIP_DIRS & set(f.relative_to(REPO).parts)
    )


def _expressions() -> list[tuple[str, str]]:
    found = []
    for f in _project_files():
        for m in _EXPR.finditer(f.read_text(encoding="utf-8")):
            found.append((str(f.relative_to(REPO)), m.group(1)))
    return found


def _ids(expression: str) -> set[str]:
    tokens = re.findall(r"[A-Za-z0-9.\-+]+", expression)
    return {t for t in tokens if t not in _OPERATORS}


def test_the_scan_finds_the_packaged_projects():
    # Positive control: the default and the three CPython-carrying overrides are all seen.
    files = {f for f, _ in _expressions()}
    assert {
        "Directory.Build.props",
        "src/Sharpy.Core/Sharpy.Core.csproj",
        "src/Sharpy.Stdlib/Sharpy.Stdlib.csproj",
        "src/Sharpy.Cli/Sharpy.Cli.csproj",
    } <= files


def test_every_licence_id_is_one_nuget_accepts():
    bad = [
        f"{f}: {expr!r} names {sorted(_ids(expr) - NUGET_ACCEPTED)}"
        for f, expr in _expressions()
        if _ids(expr) - NUGET_ACCEPTED
    ]
    assert not bad, "licence ids nuget.org refuses:\n" + "\n".join(bad)


def test_no_project_licenses_by_file():
    # A <PackageLicenseFile> bypasses the expression check; none ships today.
    files = [
        str(f.relative_to(REPO))
        for f in _project_files()
        if "<PackageLicenseFile" in f.read_text(encoding="utf-8")
    ]
    assert not files, (
        "PackageLicenseFile set; review its licence by hand, then update this test: "
        + ", ".join(files)
    )


def test_the_id_reader_splits_operators_and_parentheses():
    assert _ids("(Apache-2.0 OR MIT) AND Python-2.0") == {
        "Apache-2.0",
        "MIT",
        "Python-2.0",
    }
    assert _ids("(Apache-2.0 OR MIT) AND PSF-2.0") - NUGET_ACCEPTED == {"PSF-2.0"}


# The repository names the CPython licence by the id its packages declare (owner, 2026-11-20:
# "fix + guard"). `PSF-2.0` is the retired id. Only these files may still name it, each as history
# of the refusal (this test) or as a ruling quoted verbatim (the charter row R-NO), and each exactly
# as many times as listed: a new mention, or one removed, is red until this map is edited.
_RETIRED_IDS = re.compile(
    r"(?:(?<=%20)|(?<![A-Za-z0-9.\-+]))PSF-2\.0(?![A-Za-z0-9_]|\.[A-Za-z0-9])"
)
_MAY_NAME_RETIRED = {
    "build_tools/tests/test_package_licenses.py": 4,
    "docs/process/rounds/remediation-round-2026-09-20-batching.md": 3,
}


def _tracked_text_files() -> dict[str, str]:
    import subprocess

    out = subprocess.run(
        ["git", "-C", str(REPO), "ls-files", "-z"],
        check=True,
        capture_output=True,
    ).stdout.decode("utf-8")
    texts = {}
    for rel in filter(None, out.split("\0")):
        path = REPO / rel
        if not path.is_file():
            continue
        data = path.read_bytes()
        if b"\0" in data[:8192]:
            continue
        texts[rel] = data.decode("utf-8", errors="replace")
    return texts


def _third_party_ids() -> set[str]:
    """Ids a packaged expression adds to the project's own default (Directory.Build.props)."""
    exprs = _expressions()
    default = {i for f, e in exprs if f == "Directory.Build.props" for i in _ids(e)}
    return {i for _, e in exprs for i in _ids(e)} - default


def test_the_readme_names_the_licence_the_packages_declare():
    ids = _third_party_ids()
    assert ids == {"Python-2.0"}, (
        f"third-party ids in the package expressions: {sorted(ids)}; "
        "a new one is named in README.md's notices line, then added here"
    )
    readme = (REPO / "README.md").read_text(encoding="utf-8")
    # The id must sit on the line that states what the CPython ports are under.
    claims = [
        line
        for line in readme.splitlines()
        if "CPython" in line and "THIRD-PARTY-NOTICES.txt" in line
    ]
    assert claims, "README.md has no line naming CPython and THIRD-PARTY-NOTICES.txt"
    bad = [line for line in claims for i in sorted(ids) if f"`{i}`" not in line]
    assert not bad, (
        f"README.md's CPython licence line does not name {sorted(ids)}: {bad}"
    )


def test_no_file_names_the_retired_cpython_licence_id():
    texts = _tracked_text_files()
    assert "README.md" in texts and len(texts) > 1000, (
        "the tracked-file scan found too little"
    )
    bad = sorted(
        f"{rel}:{text.count(chr(10), 0, m.start()) + 1}"
        for rel, text in texts.items()
        if rel not in _MAY_NAME_RETIRED
        for m in _RETIRED_IDS.finditer(text)
    )
    assert not bad, (
        "the CPython licence's SPDX id is `Python-2.0` (charter R-NO); the retired id at:\n"
        + "\n".join(bad)
    )


def test_each_exempt_file_names_the_retired_id_as_often_as_listed():
    texts = _tracked_text_files()
    counts = {
        rel: len(_RETIRED_IDS.findall(texts.get(rel, ""))) for rel in _MAY_NAME_RETIRED
    }
    assert counts == _MAY_NAME_RETIRED, (
        f"update _MAY_NAME_RETIRED (a removed exemption is deleted): {counts}"
    )


def test_the_retired_id_pattern_matches_only_the_id():
    retired = "PSF" + "-2.0"
    for text in (
        f"are under {retired}, see",
        f"test suite ({retired}); see",
        f"are under {retired}.",
        f"`{retired}`",
        f"{retired}+",
        f"MIT)%20AND%20{retired}",
    ):
        assert _RETIRED_IDS.search(text), text
    for text in (
        f"{retired}1",
        f"X{retired}",
        f"{retired}.1",
        f"LicenseRef-{retired}",
        "PSF License Version 2",
    ):
        assert not _RETIRED_IDS.search(text), text
