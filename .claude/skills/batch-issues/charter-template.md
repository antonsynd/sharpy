# Charter template — `.claude/plans/remediation-round-<YYYY-MM-DD>-batching.md`

Copy verbatim, fill every `<…>`, delete nothing. Sections are numbered so plans and comments can cite `§3.x`.

```markdown
# Remediation round <YYYY-MM-DD> — batching charter

**This is the INPUT for `/create-plan`, not a plan.** Each open group in §3 becomes one `/create-plan` invocation; point it at this file plus the group's issue numbers. The plan author re-reads every issue (bodies AND comments — rulings live in comments), pins a sha, and takes its own measurements with `sharpyc run`; the per-issue notes here are hypotheses until the plan re-measures.

**Predecessor:** `<previous charter path>` — its §2 done ledger, §4 rulings <R-x…R-y> and §6 notes remain authoritative and are NOT copied here. Rulings in this round continue the sequence at <R-next>.

**Census (<date> @ <sha>, tree <clean|dirty>, dev <==|!=> origin/dev, version <v> <==|!=> tag):** <N> open at batching; <k> closed by fold/duplicate/resolution (<list>); <m> filed by rulings (<list>). **<N'> open = <P> parked + <B> batched** (every batched issue appears in exactly one group in §3). All open issues read in full on <date>; <no dotnet run | probes @ sha in scratchpad path>.

**Standing round rules** (from CLAUDE.md / verification-contract.md — restate in every plan):
- Fix the class, not the cell (Rule 11); every batch names its violated contract and cell matrix.
- Every new guard is mutation-tested with red/green recorded in the commit body (Rule 12); absence assertions need a positive control; refusals are verified by direction with `run`.
- Commit gate = whole solution via `.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark"`; every fixer's pre-report filter includes the source-scanning rosters, FrontEndParity and the three GapDiscovery sweeps.
- Verify Python behavior first with `python3 -c` (3.12). Spec is authoritative; spec edits ship with executed examples. New SPY codes need `DiagnosticExplanations` entries.
- Regen generated artifacts EARLY; the `check_*_staleness.sh` scripts are the gate.
- New node-keyed `SemanticInfo` dictionaries join `MergeFrom`; new symbol-keyed facts freeze at `MaterializeCodeGenInfo`.

---

## 1. Execution order (authoritative)

| Order | Group | Plan file | Issues |
|---|---|---|---|
| 0 | **chores** (§3.0; independent, one commit each) | — | <#…> |
| 1 | **P<n> — <name>** | — | <#… (R-xx)> |
| … | … | … | … |
| — | parked / roadmap (<P>) | — | <#…> |
| — | release | — | <version vs tag> — `/bump-version` before any release-destined push |

**Ordering rationale (owner-confirmed <date>):** <silent-wrong → ICE-on-common-shape → refusal parity; Core-only early; acceptance→refusal groups late>. **Hard dependencies:** <A before B because B reads A's carrier/helper>.

---

## 2. Done ledger

<Nothing yet | table: Group | Plan | Range / gate `passed/failed/skipped @ sha (measured)` | Closed>. Fold/duplicate/resolution closures at batching: <list with reasons>.

---

## 3. Open groups — contracts, rows, notes for the plan author

### 3.0 Chores (no plan; one commit each; independent)
- **#N** — <seam; one-line cure; direction record or mutation>.

### 3.<k> P<n> — <name>
**Contract:** <one sentence: what is uniform across which axis>. **Matrix:** <axes × values>. **Standing harness:** <name | none; the plan adds one>. <Class guard to add, if any.>
- **#N** (<R-xx if ruled>) — <what is measured; the seam file:line; the cure; cells; allowlist rows that drain>.

### 3.<last> Parked / roadmap (<P>) — unchanged
<#… — every standing ruling in the predecessor §3.10 holds; carry-over notes.>

---

## 4. Rulings (owner, <date>; posted as issue comments — do NOT re-ask)

| Ruling | Issue | Content |
|---|---|---|
| R-xx | #N | **<chosen option in bold>** — <mechanism>. Rejected: <alternatives and why; which direction is one-way>. |

**Sequencing (owner, <date>):** <order as ruled>.

---

## 5. Class findings still open (<measured @ sha | grep-only>; hypotheses until the plan re-measures)
1. **<finding>** — <file:line; which group consumes it>.

---

## 6. Cross-cutting notes for plan authors
- **New SPY codes this round:** <code ranges per group>; each needs an explanation entry + test.
- **New materialized facts:** <carrier, keying (symbol|node), merge point>.
- **Spec files touched:** <per group>. Every example executed before commit.
- **Python-verification-first items:** <issues>.
- **Snapshot/regen-sensitive:** <groups>.
- **Allowlist drains as acceptance:** <issue → allowlist rows / KnownRed / N/A cells>.
- **Probe archive:** <path @ sha | none — grep-only>.

---

## 7. Charter changelog
- **<date>** — <created | extended> @ <sha>: <groups, rulings, closures, filings, census>.

**Open items pending owner confirmation:** <none | list>. **Next:** <`/create-plan` per §1 order>.
```
