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

_EXPR = re.compile(r"<PackageLicenseExpression>([^<]*)</PackageLicenseExpression>")
_OPERATORS = {"AND", "OR", "WITH"}


def _expressions() -> list[tuple[str, str]]:
    files = [REPO / "Directory.Build.props", *sorted((REPO / "src").rglob("*.csproj"))]
    found = []
    for f in files:
        for m in _EXPR.finditer(f.read_text(encoding="utf-8")):
            found.append((str(f.relative_to(REPO)), m.group(1)))
    return found


def _ids(expression: str) -> set[str]:
    tokens = re.findall(r"[A-Za-z0-9.\-+]+", expression)
    return {t for t in tokens if t not in _OPERATORS}


def test_the_scan_finds_the_packaged_projects():
    # Positive control: the default and the three PSF-carrying overrides are all seen.
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


def test_the_id_reader_splits_operators_and_parentheses():
    assert _ids("(Apache-2.0 OR MIT) AND Python-2.0") == {
        "Apache-2.0",
        "MIT",
        "Python-2.0",
    }
    assert _ids("(Apache-2.0 OR MIT) AND PSF-2.0") - NUGET_ACCEPTED == {"PSF-2.0"}
