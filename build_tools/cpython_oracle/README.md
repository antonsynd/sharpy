# CPython test-suite classifier (`build_tools/cpython_oracle`)

Phase 0 of [#1030](https://github.com/antonsynd/sharpy/issues/1030) — the golden
oracle. This tool mines CPython's own `Lib/test/test_*.py` files and classifies
each **test method** by how portable it is to a Sharpy `.spy` test, so we port the
value-level methods and skip the ones that structurally cannot survive a
statically typed, int64, C#-scoped language.

The classifier never runs the CPython file — it parses with Python's `ast` module
and walks each test method.

## Categories

| Category | Meaning | Ports? |
|---|---|---|
| `PORTABLE` | Value-level assertions on typed APIs. Table-driven loops are portable-with-unrolling. | Yes |
| `NEEDS-REWRITE` | Portable in principle but leans on heterogeneous list/set literals, dunder-fixture classes, builtin subclassing, or `test.support` helpers beyond a small whitelist. | With manual work |
| `DIVERGENT` | Hits a documented Sharpy divergence (int64 ints, `//` truncation, `global`/`nonlocal`, metaclasses, `**kwargs`, `@classmethod`, multiple inheritance). Cross-referenced to `docs/deviations.yaml` ids. | No (by design) |
| `DYNAMIC` | `exec`/`eval`/`compile`, 3-arg `type()` factories, namespace mutation (`globals`/`locals`). | No |
| `IMPL-DETAIL` | Refcounts, `sys.getsizeof`, `gc` internals, `@cpython_only`/`@impl_detail`. | No (by definition) |

When a method carries reasons from several categories, the **most severe** wins
(`IMPL-DETAIL` > `DYNAMIC` > `DIVERGENT` > `NEEDS-REWRITE` > `PORTABLE`), but every
reason is listed in the report. The `DIVERGENT`/`DYNAMIC`/`IMPL-DETAIL` buckets
double as a systematic map of Sharpy's distance from Python.

## Oracle source (pinned CPython 3.12.x)

The oracle is pinned to **CPython 3.12.x**. Test files are **not vendored** into
this repo — they are PSF-2.0 material and re-syncs must be deliberate classifier
re-runs. The tool reads them from a local CPython 3.12 install.

Resolution order:

1. `--cpython-lib <dir>` on the CLI (a `.../lib/python3.12/test` directory).
2. `SHARPY_CPYTHON_TEST_DIR` environment variable.
3. The running interpreter's stdlib `test/` dir, if it is 3.12 and populated.
4. Common Homebrew / python.org framework locations.

If none resolve, install CPython 3.12 (`brew install python@3.12`) and pass its
test dir explicitly.

## Usage

```bash
# One-line yield summaries for several modules:
python -m build_tools.cpython_oracle classify --summary-only \
    test_bisect test_colorsys test_int \
    --cpython-lib /opt/homebrew/opt/python@3.12/.../python3.12/test

# Write per-module markdown yield reports:
python -m build_tools.cpython_oracle classify test_bisect test_textwrap \
    --cpython-lib <dir> --report-dir build_tools/cpython_oracle/reports

# Also emit .spy skeletons for the PORTABLE methods:
python -m build_tools.cpython_oracle classify test_bisect \
    --cpython-lib <dir> --skeleton-dir /tmp/skeletons

# YAML instead of markdown:
python -m build_tools.cpython_oracle classify test_int --format yaml --cpython-lib <dir>
```

### `.spy` skeletons

`--skeleton-dir` emits one `<module>_skeleton.spy` per module containing, for each
`PORTABLE` method, a provenance comment plus a signature-only `@test def …(): pass`
stub. **Bodies are not ported** — that is manual/agent work in #1030 phase 1. Each
skeleton carries the PSF-2.0 attribution required for CPython-derived material, and
function names are de-collided so the same-stem `Spy/` regen constraint holds.

## Dual-execution harness (`shim/` + `dual_run.py`)

Phase 3 of #1030. A CPython port only counts as a *golden oracle* if it also
passes under stock `python3` — that proves the ported assertions faithfully record
Python's behavior, not just Sharpy's. The runner executes each ported `.spy` file
under the current interpreter with the shim installed and reports pass/fail per
`@test` function:

```bash
# Run the whole ported subtree (recursive *.spy scan):
python -m build_tools.cpython_oracle.dual_run src/Sharpy.Stdlib.Tests/Spy/cpython/

# Or specific files, listing passes too:
python -m build_tools.cpython_oracle.dual_run -v \
    src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_bisect_tests.spy
```

Exit status: `0` all pass, `1` any test fails/errors, `2` usage/discovery error.

The `.spy` test dialect is a subset of Python (typed locals/annotations plus real
stdlib imports), so only two constructs need bridging, both in `shim/__init__.py`:

* `@test` (and the `@test.<sub>` family) — the Sharpy test-entry decorator,
  injected into each file's namespace by the runner.
* `from unittest import approx` — Sharpy exposes `approx`; CPython does not, so
  `shim.install()` monkeypatches the real `unittest` module. `approx` mirrors
  `Sharpy.Unittest.Approx` (abs precedence over places; places = round-to-N).

Everything else the pilots touch (`bisect`, `random`, `colorsys`, `textwrap`) is a
real CPython stdlib API with an identical name — no aliases needed. When a later
port hits a Sharpy-renamed symbol or kwarg, register it in `MODULE_ALIASES` /
`MODULE_KWARG_ALIASES` (applied by `apply_module_adapters()`); the runner needs no
change. A port that is *not* valid Python is a porting defect, not a shim gap.

Shim + runner tests live in `build_tools/tests/test_cpython_oracle_shim.py`. CI
runs the dual-execution as the `dual-execute-oracle` job in
`.github/workflows/python-build-tools.yml` (Python 3.12, no dotnet).

## Committed yield reports

`reports/` holds committed yield reports for the first candidate tranche. See
[`reports/README.md`](reports/README.md) for the headline numbers.

## Tests

Heuristic unit tests live in `build_tools/tests/test_cpython_oracle.py` (so the
existing `python-build-tools` workflow collects them) and run on synthetic
snippets, never real CPython files:

```bash
PYTHONPATH=. python -m pytest build_tools/tests/test_cpython_oracle.py -v
```

## Layout

| File | Purpose |
|---|---|
| `classifier.py` | AST walker + heuristics; `classify_source` / `classify_file`. |
| `report.py` | Markdown / YAML yield-report renderers. |
| `skeleton.py` | `.spy` skeleton emission for PORTABLE methods. |
| `oracle_sources.py` | CPython 3.12 source resolution + version pin. |
| `cli.py` / `__main__.py` | `python -m build_tools.cpython_oracle classify …`. |
