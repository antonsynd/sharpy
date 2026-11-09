#!/usr/bin/env python3
"""Reduce and merge the line coverage of the coverage-on CI runs (#2512, plan-88736c D1).

Coverage is measured and published, never gated: no threshold reads these numbers.

    coverage_reduce.py --name core [--results TestResults]
        Per covered job, after each `dotnet test --collect "Code Coverage"`: reads every
        `*.cobertura.xml` under the results directory, writes `<results>/coverage-<name>.json`
        (valid and covered line numbers per module and file) and deletes the cobertura it read, so
        the next test step of the same job reduces only its own. `<name>` is the step's trx name.

    coverage_reduce.py --merge trx
        In ci-summary, over the downloaded artifacts: per test project, the UNION of covered
        (module, file, line) across its shards (a line two shards cover counts once), one table in
        $GITHUB_STEP_SUMMARY, and `::error::` (exit 1) when an expected job has no coverage file, a
        file has 0 valid lines, or a coverage-off run carries a coverage file.

The expected list mirrors the counts step's: cli, core, stdlib, lsp-shard-<n>, compiler-shard-<n>
(from the LSP_SHARD_/COMPILER_SHARD_ workflow env), none when CODE is "false" (a README-only change)
or COVERAGE_ON is not "true". Sweeps and windows are not covered. Stdout lines start `COVERAGE`,
never `GATE-COUNTS`/`GATE-FAILED`, which `.claude/scripts/ci-gate` parses.
"""

from __future__ import annotations

import argparse
import glob
import json
import os
import sys
import xml.etree.ElementTree as ET
from typing import Dict, List, Set, Tuple

PROJECTS = [
    ("cli", "Cli"),
    ("core", "Core"),
    ("stdlib", "Stdlib"),
    ("lsp-shard-", "Lsp"),
    ("compiler-shard-", "Compiler"),
]


def reduce_cobertura(
    paths: List[str], root: str
) -> Dict[str, Dict[str, List[List[int]]]]:
    """module -> file -> [valid lines, covered lines]; the same line in two classes or files once."""
    acc: Dict[str, Dict[str, Tuple[Set[int], Set[int]]]] = {}
    prefix = os.path.abspath(root) + os.sep
    for path in paths:
        for package in ET.parse(path).getroot().iter("package"):
            files = acc.setdefault(package.get("name", "?"), {})
            # Class-level <lines> only: the method-level copies repeat them.
            for cls in package.iter("class"):
                name = cls.get("filename", "?")
                name = name[len(prefix) :] if name.startswith(prefix) else name
                valid, covered = files.setdefault(name, (set(), set()))
                for line in cls.findall("lines/line"):
                    number = int(line.get("number", "0"))
                    valid.add(number)
                    if int(line.get("hits", "0")) > 0:
                        covered.add(number)
    return {
        m: {f: [sorted(v), sorted(c)] for f, (v, c) in files.items()}
        for m, files in acc.items()
    }


def reduce_main(name: str, results: str) -> int:
    paths = sorted(
        glob.glob(os.path.join(results, "**", "*.cobertura.xml"), recursive=True)
    )
    if not paths:
        # Not an error here: the merge in ci-summary names the job ("<name>: no coverage").
        print(f"::warning::COVERAGE {name}: no cobertura under {results}")
        return 0
    modules = reduce_cobertura(paths, os.getcwd())
    out = os.path.join(results, f"coverage-{name}.json")
    with open(out, "w") as f:
        json.dump({"name": name, "modules": modules}, f, separators=(",", ":"))
    for path in paths:
        os.remove(path)
    valid = sum(len(v) for files in modules.values() for v, _ in files.values())
    covered = sum(len(c) for files in modules.values() for _, c in files.values())
    print(
        f"COVERAGE {name}: {len(paths)} cobertura -> {out}: {covered}/{valid} lines, modules {sorted(modules)}"
    )
    return 0


def project_of(name: str) -> str:
    for prefix, project in PROJECTS:
        if name == prefix or (prefix.endswith("-") and name.startswith(prefix)):
            return project
    return name


def shards(prefix: str) -> List[int]:
    return sorted(int(k[len(prefix) :]) for k in os.environ if k.startswith(prefix))


def pct(covered: int, valid: int) -> str:
    return f"{covered}/{valid} ({100.0 * covered / valid:.1f}%)" if valid else "-"


def merge_main(directory: str) -> int:
    on = os.environ.get("COVERAGE_ON") == "true"
    code = os.environ.get("CODE") != "false"
    expected: List[str] = []
    if on and code:
        expected = (
            ["cli", "core", "stdlib"]
            + [f"lsp-shard-{n}" for n in shards("LSP_SHARD_")]
            + [f"compiler-shard-{n}" for n in shards("COMPILER_SHARD_")]
        )
    found = {
        os.path.basename(p)[len("coverage-") : -len(".json")]: p
        for p in glob.glob(
            os.path.join(directory, "**", "coverage-*.json"), recursive=True
        )
    }
    problems: List[str] = []
    if not on:
        if found:
            problems.append(
                f"a coverage-off run carries coverage files: {', '.join(sorted(found))}"
            )
        else:
            print(
                "COVERAGE off: this run collected none (on for a mainline push or a dispatch with coverage: true)"
            )
    elif not expected and not found:
        print("COVERAGE none expected: a README-only change runs no covered job")
    for name in expected:
        if name not in found:
            problems.append(f"{name}: no coverage")

    # (project, module, file) -> (valid, covered): the union over the project's shards.
    acc: Dict[Tuple[str, str, str], Tuple[Set[int], Set[int]]] = {}
    shard_count: Dict[str, int] = {}
    for name in sorted(found):
        with open(found[name]) as f:
            modules = json.load(f)["modules"]
        if sum(len(v) for files in modules.values() for v, _ in files.values()) == 0:
            problems.append(
                f"{name}: 0 valid lines (the include in build_tools/coverage.runsettings matched nothing)"
            )
        project = project_of(name)
        shard_count[project] = shard_count.get(project, 0) + 1
        for module, files in modules.items():
            for path, (valid, covered) in files.items():
                v, c = acc.setdefault((project, module, path), (set(), set()))
                v.update(valid)
                c.update(covered)

    if acc and on:
        order = [p for _, p in PROJECTS] + sorted(
            {p for p, _, _ in acc} - {p for _, p in PROJECTS}
        )
        projects = [p for p in order if p in shard_count]
        modules_seen = sorted({m for _, m, _ in acc})
        table: Dict[Tuple[str, str], List[int]] = {}
        union: Dict[Tuple[str, str], Tuple[Set[int], Set[int]]] = {}
        for (project, module, path), (v, c) in acc.items():
            cell = table.setdefault((project, module), [0, 0])
            cell[0] += len(c)
            cell[1] += len(v)
            uv, uc = union.setdefault((module, path), (set(), set()))
            uv.update(v)
            uc.update(c)
        union_cells = {m: [0, 0] for m in modules_seen}
        for (module, _), (v, c) in union.items():
            union_cells[module][0] += len(c)
            union_cells[module][1] += len(v)
        lines = [
            "### Line coverage (measured, not gated)",
            "",
            "| test project | " + " | ".join(modules_seen) + " |",
            "|---|" + "---:|" * len(modules_seen),
        ]
        for project in projects:
            label = (
                project
                if shard_count[project] == 1
                else f"{project} (union of {shard_count[project]} shards)"
            )
            cells = [pct(*table.get((project, m), [0, 0])) for m in modules_seen]
            lines.append(f"| {label} | " + " | ".join(cells) + " |")
            for m in modules_seen:
                covered, valid = table.get((project, m), [0, 0])
                print(f"COVERAGE {project} {m} covered={covered} valid={valid}")
        lines.append(
            "| **union (all projects)** | "
            + " | ".join(pct(*union_cells[m]) for m in modules_seen)
            + " |"
        )
        for m in modules_seen:
            print(
                f"COVERAGE union {m} covered={union_cells[m][0]} valid={union_cells[m][1]}"
            )
        lines += [
            "",
            "Sweeps and the windows job are not covered (a sweep's 825 s doubled passes its 25-min limit).",
        ]
        print("\n".join(lines))
        summary = os.environ.get("GITHUB_STEP_SUMMARY")
        if summary:
            with open(summary, "a") as f:
                f.write("\n".join(lines) + "\n")
    for p in problems:
        print(f"::error::COVERAGE {p}")
    return 1 if problems else 0


def main(argv: List[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--name", help="reduce: the trx name of the test step just run")
    group.add_argument(
        "--merge",
        metavar="DIR",
        help="merge: the directory the artifacts were downloaded to",
    )
    parser.add_argument(
        "--results",
        default="TestResults",
        help="reduce: the dotnet test results directory",
    )
    args = parser.parse_args(argv)
    return (
        merge_main(args.merge) if args.merge else reduce_main(args.name, args.results)
    )


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
