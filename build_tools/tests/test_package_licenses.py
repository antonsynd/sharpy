"""Every NuGet licence expression names only licences nuget.org accepts.

nuget.org refuses a push whose `<PackageLicenseExpression>` names an SPDX id that is not
OSI-approved or FSF-free. `PSF-2.0` is such an id, and the v0.25.0 release lost its Core, Stdlib
and sharpyc pushes to it (HTTP 400); the CPython licence's OSI-approved SPDX id is `Python-2.0`.
The server check runs only at release time, so this test runs it on every change.
"""

from __future__ import annotations

import re
from pathlib import Path

import pytest

REPO = Path(__file__).resolve().parent.parent.parent

# SPDX ids checked against https://spdx.org/licenses/ (isOsiApproved or isFsfLibre true).
# Add an id only after checking that list; nuget.org refuses the rest. Unicode-3.0: isOsiApproved
# true (isFsfLibre unset) in spdx/license-list-data json/licenses.json, read 2026-11-20.
NUGET_ACCEPTED = {"Apache-2.0", "MIT", "Python-2.0", "Unicode-3.0"}

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


# Each package declares the licences of the third-party code it ships (THIRD-PARTY-NOTICES.txt):
# CPython ports (Python-2.0) in Core and Stdlib, Unicode case-folding data (Unicode-3.0) in Core,
# PyYAML's resolver regexes (MIT) in Stdlib, and all three in sharpyc, which ships Sharpy.Core.dll
# and Sharpy.Stdlib.dll. Owner, 2026-11-20, asked whether to add Unicode-3.0 to Core, MIT to Stdlib
# and both to sharpyc: "yes". PyYAML's MIT is a term of its own, ANDed on: it is not the MIT of
# Sharpy's `Apache-2.0 OR MIT`, so an id-set comparison against the default cannot see it, and the
# expressions are pinned whole. A change here is a change to what ships, read against the notices.
OWN_LICENCE = "(Apache-2.0 OR MIT)"
PACKAGE_EXPRESSIONS = {
    "Directory.Build.props": "Apache-2.0 OR MIT",
    "src/Sharpy.Core/Sharpy.Core.csproj": "(Apache-2.0 OR MIT) AND Python-2.0 AND Unicode-3.0",
    "src/Sharpy.Stdlib/Sharpy.Stdlib.csproj": "(Apache-2.0 OR MIT) AND Python-2.0 AND MIT",
    "src/Sharpy.Cli/Sharpy.Cli.csproj": (
        "(Apache-2.0 OR MIT) AND Python-2.0 AND Unicode-3.0 AND MIT"
    ),
}


def test_each_package_declares_the_ruled_expression():
    found: dict[str, list[str]] = {}
    for f, expr in _expressions():
        found.setdefault(f, []).append(" ".join(expr.split()))
    expected = {f: [e] for f, e in PACKAGE_EXPRESSIONS.items()}
    assert found == expected, (
        "PackageLicenseExpression differs from the owner's ruling; change what ships "
        "and THIRD-PARTY-NOTICES.txt together, then PACKAGE_EXPRESSIONS"
    )


def third_party_terms(expression: str) -> list[str]:
    """The AND-terms a package adds to Sharpy's own `(Apache-2.0 OR MIT)`, in order."""
    terms = [t.strip() for t in " ".join(expression.split()).split(" AND ")]
    return [t for t in terms if t != OWN_LICENCE]


def _package_ids() -> dict[str, str]:
    """Project file -> PackageId, for the projects that override the licence expression."""
    ids = {}
    for f in PACKAGE_EXPRESSIONS:
        m = re.search(
            r"<PackageId>([^<]+)</PackageId>",
            (REPO / f).read_text(encoding="utf-8"),
        )
        if m:
            ids[f] = m.group(1).strip()
    return ids


def readme_problems(line: str, packages: dict[str, list[str]]) -> list[str]:
    """`packages` maps a PackageId to its third-party ids. In the README line, each package's
    segment (from its backticked id to the next package's) names exactly its ids, backticked."""
    starts = sorted(
        (line.find(f"`{pkg}`"), pkg) for pkg in packages if f"`{pkg}`" in line
    )
    problems = [
        f"the line does not name the package `{pkg}`"
        for pkg in sorted(packages)
        if f"`{pkg}`" not in line
    ]
    known = NUGET_ACCEPTED | {i for ids in packages.values() for i in ids}
    for n, (pos, pkg) in enumerate(starts):
        end = starts[n + 1][0] if n + 1 < len(starts) else len(line)
        named = set(re.findall(r"`([^`]+)`", line[pos:end])) & known
        if named != set(packages[pkg]):
            problems.append(
                f"`{pkg}` is given {sorted(named)}, but its expression adds "
                f"{sorted(packages[pkg])}"
            )
    return problems


def _readme_line() -> str:
    readme = (REPO / "README.md").read_text(encoding="utf-8")
    lines = [
        line
        for line in readme.splitlines()
        if "THIRD-PARTY-NOTICES.txt" in line and "`sharpyc`" in line
    ]
    assert len(lines) == 1, (
        "README.md has no single line naming THIRD-PARTY-NOTICES.txt and the packages"
    )
    return lines[0]


def test_the_readme_names_the_licences_each_package_declares():
    exprs = dict(_expressions())
    package_ids = _package_ids()
    assert set(package_ids) == set(PACKAGE_EXPRESSIONS) - {"Directory.Build.props"}
    packages = {
        package_ids[f]: third_party_terms(exprs[f]) for f in package_ids if f in exprs
    }
    problems = readme_problems(_readme_line(), packages)
    assert not problems, "README.md's notices line:\n" + "\n".join(problems)


def test_the_readme_check_refuses_a_missing_or_extra_id():
    packages = {"Core": ["Python-2.0", "Unicode-3.0"], "Stdlib": ["Python-2.0", "MIT"]}
    good = "Apache-2.0 OR MIT; `Core` adds `Python-2.0` and `Unicode-3.0`; `Stdlib` adds `Python-2.0` and `MIT`."
    assert readme_problems(good, packages) == []
    for bad in (
        good.replace(" and `MIT`", ""),  # PyYAML's MIT missing from Stdlib
        good.replace(" and `Unicode-3.0`", ""),
        good.replace("`Core` adds", "`Core` adds `MIT`,"),
        good.replace("; `Stdlib` adds `Python-2.0` and `MIT`", ""),
    ):
        assert readme_problems(bad, packages), bad
    assert third_party_terms("(Apache-2.0 OR MIT) AND Python-2.0 AND MIT") == [
        "Python-2.0",
        "MIT",
    ]


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


# ---- The per-module Stdlib projects never pack ----
#
# src/Sharpy.Stdlib/modules/ holds one tree-shaking project per module (#696). They compile code
# ported from CPython (Fnmatch, Itertools, Os, Shutil, Difflib, ...) but carry neither
# THIRD-PARTY-NOTICES.txt nor the Python-2.0 term that SharpyLang.Stdlib's package does, so a
# `dotnet pack` of one would ship a port under `Apache-2.0 OR MIT` alone (#2602 follow-up, owner
# 2026-11-20). Their shared Directory.Build.props sets IsPackable to false, and no project there
# may set it back.

MODULES = "src/Sharpy.Stdlib/modules"
_IS_PACKABLE = re.compile(r"<IsPackable\b[^>]*>([^<]*)</IsPackable>")


def unpackable_problems(modules: Path) -> list[str]:
    props = modules / "Directory.Build.props"
    text = props.read_text(encoding="utf-8") if props.is_file() else ""
    values = _IS_PACKABLE.findall(text)
    problems = []
    if [v.strip().lower() for v in values] != ["false"]:
        problems.append(
            f"{props.name}: sets IsPackable {values!r}; it must set it once, to false"
        )
    for project in sorted(modules.glob("*.csproj")):
        for value in _IS_PACKABLE.findall(project.read_text(encoding="utf-8")):
            if value.strip().lower() != "false":
                problems.append(
                    f"{project.name}: overrides IsPackable with {value!r}; the per-module "
                    "projects never pack"
                )
    return problems


def test_the_module_scan_sees_the_per_module_projects():
    # Positive control: the projects that compile CPython ports are among those checked.
    names = {p.name for p in (REPO / MODULES).glob("*.csproj")}
    assert {
        "Sharpy.Stdlib.Fnmatch.csproj",
        "Sharpy.Stdlib.Itertools.csproj",
        "Sharpy.Stdlib.Os.csproj",
        "Sharpy.Stdlib.Shutil.csproj",
        "Sharpy.Stdlib.Difflib.csproj",
    } <= names


def test_no_per_module_stdlib_project_is_packable():
    problems = unpackable_problems(REPO / MODULES)
    assert not problems, "\n".join(problems)


_PROPS = "<Project>\n  <PropertyGroup>\n    <IsPackable>false</IsPackable>\n  </PropertyGroup>\n</Project>\n"
_PROJECT = '<Project Sdk="Microsoft.NET.Sdk">\n</Project>\n'
_OVERRIDE = '<Project Sdk="Microsoft.NET.Sdk">\n  <PropertyGroup>\n    <IsPackable>true</IsPackable>\n  </PropertyGroup>\n</Project>\n'


def test_the_packable_check_accepts_the_shared_default(tmp_path: Path):
    (tmp_path / "Directory.Build.props").write_text(_PROPS, encoding="utf-8")
    (tmp_path / "Sharpy.Stdlib.Fnmatch.csproj").write_text(_PROJECT, encoding="utf-8")
    assert unpackable_problems(tmp_path) == []


def test_the_packable_check_refuses_a_project_override(tmp_path: Path):
    (tmp_path / "Directory.Build.props").write_text(_PROPS, encoding="utf-8")
    (tmp_path / "Sharpy.Stdlib.Os.csproj").write_text(_OVERRIDE, encoding="utf-8")
    problems = unpackable_problems(tmp_path)
    assert len(problems) == 1 and "Sharpy.Stdlib.Os.csproj: overrides" in problems[0], (
        problems
    )


@pytest.mark.parametrize(
    "props", ["<Project>\n</Project>\n", _PROPS.replace("false", "true")]
)
def test_the_packable_check_refuses_a_packable_default(tmp_path: Path, props: str):
    (tmp_path / "Directory.Build.props").write_text(props, encoding="utf-8")
    (tmp_path / "Sharpy.Stdlib.Fnmatch.csproj").write_text(_PROJECT, encoding="utf-8")
    problems = unpackable_problems(tmp_path)
    assert (
        len(problems) == 1 and "Directory.Build.props: sets IsPackable" in problems[0]
    ), problems
