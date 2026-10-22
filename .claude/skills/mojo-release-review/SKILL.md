---
name: mojo-release-review
description: Review a Mojo release's notes against Sharpy — triage every item, probe the Sharpy surface it names against python3, record the review under docs/design/mojo-release-reviews/, and file issues by class
argument-hint: <version, e.g. v1.3.0> [--no-issues] [--checkout]
---

Read one Mojo release's notes (https://mojolang.org/releases/<version>/) and decide, item by item,
what Sharpy should steal, borrow, take inspiration from, or leave alone — with every claim about
Sharpy's current behavior **measured** on a fresh `sharpyc` build, never recalled.

**Usage:** `/mojo-release-review v1.3.0` — `--no-issues` writes the review but files nothing;
`--checkout` also sparse-clones `modular/modular` for stdlib source citations.

**Outputs:**
1. `docs/design/mojo-release-reviews/<version>.md` — the review record (template below). This
   directory is the index of prior reviews; read the newest one first so rejected items are not
   re-litigated and recommendations already shipped are marked as such.
2. GitHub issues for every bug the probes surface, filed by class with `lane:*` + `reach:*`
   labels (verification-contract.md §10), plus a comment on the class tracker for every
   DEVIATION-by-default finding (python-fidelity-scope.md D5).
3. Optionally a published artifact page in the style of the earlier reviews (the owner reads
   those in the Claude app); the `.md` record is the one that must exist.

Prior reviews: `Borrowing from KGEN` (2026-08-18, Mojo 1.0 compiler architecture, artifact only)
and `v1.2.0.md` (2026-10-22, release notes for 1.0–1.2).

## Steps

### 1. Fetch the notes and the pages they link

The release page is a Docusaurus site. Save the HTML and flatten it; the Markdown-suffix trick
works for manual/reference pages but **not** for release pages:

```bash
S=/tmp/claude-0/.../scratchpad/mojo   # the session scratchpad
mkdir -p "$S" && cd "$S"
curl -sSL "https://mojolang.org/releases/$VERSION/" -o "$VERSION.html"
python3 -I - "$VERSION" <<'EOF'
import html, re, sys
v = sys.argv[1]; s = open(v + '.html', encoding='utf-8').read()
s = re.sub(r'<script.*?</script>|<style.*?</style>', '', s, flags=re.S)
s = re.sub(r'<(h[1-6])[^>]*>', lambda m: '\n\n' + '#' * int(m.group(1)[1]) + ' ', s)
s = re.sub(r'<li[^>]*>', '\n- ', s); s = re.sub(r'<(p|pre|div|tr|br)[^>]*>', '\n', s)
s = re.sub(r'<code[^>]*>', '`', s); s = re.sub(r'</code>', '`', s)
s = html.unescape(re.sub(r'<[^>]+>', '', s)); s = re.sub(r'\n{3,}', '\n\n', s)
i = s.find('# Mojo ' + v); open(v + '.txt', 'w').write(s[i:] if i >= 0 else s)
EOF
curl -sSL https://mojolang.org/llms.txt -o llms.txt          # index of every doc page
curl -sSL https://mojolang.org/docs/reference/compound-statements.md -o compound-statements.md   # any doc page + .md
```

Read the whole `.txt` (every section: highlights, language, library, tooling, removed, fixed).
The "Fixed" section is the richest source of probes: each Mojo bug names a cell to run on Sharpy.

`--checkout`: `git clone --depth 1 --filter=blob:none --sparse https://github.com/modular/modular.git`
then read files on demand with `git show HEAD:Mojo/stdlib/std/math/math.mojo > ../math.mojo`
(sparse-checkout set does not materialize reliably under the blob filter). Stdlib lives under
`Mojo/stdlib/std/`, docs under `Mojo/docs/site/`. Record the commit in the review header.

### 2. Build once, then probe everything

```bash
.claude/scripts/dotnet-serialized build src/Sharpy.Cli/Sharpy.Cli.csproj --nologo -v q   # dangerouslyDisableSandbox: true
dotnet src/Sharpy.Cli/bin/Debug/net10.0/sharpyc.dll run probe.spy      # the assembly is sharpyc.dll, not Sharpy.Cli.dll
```

If no SDK is on PATH: `curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir /root/.dotnet`
and export `PATH=/root/.dotnet:$PATH DOTNET_ROOT=/root/.dotnet`.

For every item whose verdict rests on "Sharpy already does X" or "Sharpy lacks X", write a probe
`.spy` in the scratchpad (one file per surface, `print` the cells), run it, and run the same cells
through `python3 -I` as the oracle (`/verify-python`). Claims about .NET itself
(`char.IsWhiteSpace`, `double.ExpM1`, …) are checked in `dotnet fsi` on the same SDK. A verdict
without a probe is a guess; the hallucination-defense agent may be used to refute claims.

Keep the probe files; the review cites them, and the issues quote the cells.

### 3. Triage every item into one of five bins

| Bin | Meaning | What the review says |
|---|---|---|
| **Ahead** | Sharpy already has it, often from Python or .NET | one line + the probe that proved it |
| **Covered** | a different Sharpy mechanism does the job | name the mechanism (`@[...]` attributes, FeatureFlags, …) |
| **Borrow** | a rule, practice or feature worth adopting | a recommendation card (below) |
| **Reject** | wrong-fit: non-Python syntax, no CLR carrier, feature creep | one line naming the axiom or anti-pattern (CLAUDE.md), so it is not re-litigated |
| **N/A** | GPU, SIMD, ownership, allocators, MLIR, telemetry | grouped in one line |

Check the open design issues before rejecting a language item: several Mojo ideas already have a
`design:` tracker (e.g. target-typed `.member` access, `ref` bindings). Cite it instead.

### 4. Classify every divergence before filing

- COMPARED surface (python-fidelity-scope.md table) with a print-visible or compile-visible
  difference from python3 → a bug. Find its class tracker first
  (`class-tracker` label, open), post the cell there if one exists, else file ONE issue per class
  with a cell table, `lane:*` and `reach:*` labels, the measured sha, and the Mojo item that led
  to it. Never one issue per cell.
- DEVIATION-by-default surface (exception wording, exit codes, `hash()` values, `repr` of an
  un-oracled type, …) → a `docs/deviations.yaml` row or a comment on the tracker, never an issue.
- A design ruling the notes suggest (a printing convention, a syntax) → a comment on the relevant
  `design:` or class issue, to be ruled at `/batch-issues`.

### 5. Write the review record

`docs/design/mojo-release-reviews/<version>.md`, sections in this order:

```
# Mojo <version> — what Sharpy can borrow
> Reviewed <date> · Mojo notes <url> · modular @ <sha> (if checked out) · sharpy @ <sha> · python <ver> as oracle
## At a glance            (the item → Mojo did → Sharpy today (measured) → verdict table)
## Already ahead / covered
## Recommendations        (R-cards: Mojo mechanism · Build in Sharpy · Caveats · priority P0/P1/P2)
## Rejected               (one line each, with the reason)
## Bugs found             (B-list with issue numbers; deviation-row candidates listed separately)
## Method                 (probe files, build, oracle versions)
```

Priority follows the verification contract: measured print-visible divergence in a COMPARED
surface = P0; parser/CS-leak parity gap = P1; docs/editor/tooling borrow = P2.

### 6. File, link, commit

File the issues (unless `--no-issues`), put their numbers into the review, `/commit` the review
(and any skill/README changes) and `/push`. Do not fix the bugs in the review's commit: the review
is a reading, each fix is its own plan.
