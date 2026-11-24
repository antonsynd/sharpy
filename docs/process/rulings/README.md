# Ruling records

An owner ruling is a row of prose in a charter. These files hold the same rulings as data, so a
script can check what prose cannot: that a ruling names its law, that a replaced ruling says so,
that two rulings do not give one case two answers.

Background: [the 2026-11-21 ruling audit](../../audits/ruling-audit-2026-11-21.md).

| File | Holds |
|---|---|
| `rulings.yaml` | One record per ruling |
| `laws.yaml` | The written laws (with where their text is) and the audit's draft principles |
| `axes.yaml` | The closed lists a *case* is named from |
| `policy.yaml` | Which decisions an agent may take without the owner. A draft; the owner's to set |
| `known-findings.yaml` | Findings that wait for the owner. A ratchet |

```bash
python3 build_tools/check_rulings.py                 # the gate; no compiler needed
python3 build_tools/check_rulings.py --list-known    # also print what is waiting
python3 build_tools/ruling_evidence.py recheck       # re-run every recorded program (needs sharpyc)
```

Both also run as tests under `build_tools/tests/`, so CI runs them.

## Status

The 460 records marked `seed:` were written by the audit. Their labels are a language model's
reading of each charter row and the owner has not reviewed them. The charter row stays the
authority for a seed ruling's text. Fifteen cases are filled in, all from conflicts the audit
verified by reading the rows, each with a program that was run.

The five families in `axes.yaml` are hand-listed. Issues #2486, #2487 and #2733 are where the
lists get derived from the compiler; replace these with theirs as they land.

## Adding a ruling

A record without `seed:` must provide all of this, and the check fails if it does not:

```yaml
- id: R-QS
  date: 2026-11-22
  issue: "#2800"
  kind: language            # language | library | diagnostic | implementation | cli-lsp | test-instrument | process
  rule: A walrus onto an existing let is a compile error.
  options:                  # at least two; exactly one chosen, exactly one recommended
    - {text: refuse, chosen: true, recommended: true}
    - {text: rebind, as today}
  basis: follows            # follows | precedent | exception | new-law | choice
  laws: [R-GV]              # what it rests on: law ids or ruling ids (not needed for `choice`)
  considered:               # every other ruling or law with a case in the same family
    - {id: R-BK, why: "R-BK is an ordinary local; this target is a let"}
    - {id: R-LR, why: "R-LR parked the question; this ruling answers it"}
    - {id: R-MH, why: "R-MH is the comprehension position"}
    - {id: N5, why: "N5's case is the comprehension position"}
  decided_by: owner         # owner | agent
  door: two-way             # must equal what the evidence gives; see below
  breaks_running: true
  undo_breaks: false
  supersedes: []            # the other record must carry the matching superseded_by / amended_by
  amends: [R-LR]
  cases:                    # required for language and library rulings
    - family: walrus-target
      coords: {position: statement, target: let}
      verdict: refuse       # run | refuse | open
      evidence:             # paste from: ruling_evidence.py measure prog.spy [--python twin.py]
        program: |
          def main() -> None:
              let x = 1
              if (x := 2) > 0:
                  print(x)
        sharpy: |           # what the compiler did when measured
          runs:2
        python: no-twin
        after: refused:SPY0611    # what the ruling decides it will do
```

An observation is `runs:<stdout>`, `refused:<code>`, `raises:<type>` (followed by whatever was
printed first) or, for python only, `no-twin`. The compiler can also give `crashes:<code>`: an
internal error such as SPY0908, or one of the emitter's own failures in the SPY05xx range. That is a defect to report. It is valid as `sharpy`, and a ruling
can never decide it as `after`.

Three fields are computed, not chosen. `ruling_evidence.py door --before ... --after ...` prints
them, and the `door` check compares:

- `breaks_running`: a program that runs today is refused, or prints or raises something else.
- `undo_breaks`: taking the decision back later would break programs written under it.
- `door`: `one-way` when `undo_breaks`, else `two-way`.

They are taken over all the cases: a ruling breaks running programs if any case does, and is
one-way if any case is.

An agent may record a ruling itself (`decided_by: agent`) only for the decision classes
`policy.yaml` marks `true`. Anything else needs `decided_by: owner`.

## What a failure means

| Check | Meaning | Cure |
|---|---|---|
| `schema` | A required field is missing or malformed | Provide it |
| `charter-totality` | A charter row has no record | Add the record |
| `law-anchor` | A law's text moved or was reworded | Update `anchor`, or restore the text |
| `reference` | An id, family, axis or value that does not exist | Fix it, or add the value to `axes.yaml` |
| `supersession` | Only one of two rulings records that one replaced the other | Add the other side |
| `charter-mark` | A replaced ruling's charter row does not say so | Strike the row, or name the later ruling on it |
| `exception-in-law` | A ruling departs from a law that does not name it: the law's own section of its source, or a draft's `against` list | Name the ruling there |
| `collision` | Two live rulings give one case two verdicts | Rule; then supersede or amend one |
| `invariance` | A law says an axis does not matter and two cases differ along it | Rule; then change a case, or record `differs_because` with `exception_to` |
| `door` | The stated door is not what the evidence gives | Use the computed values |
| `considered` | A related ruling was not answered (one made on a later date need not be) | Say why it does not decide this one |
| `owner-required` | An agent took a decision `policy.yaml` reserves | The owner decides |

A finding that needs the owner goes in `known-findings.yaml` with a reference. Only `charter-mark`,
`exception-in-law`, `collision` and `invariance` findings can wait there; anything else is a
malformed record and is fixed. A collision or invariance row also carries `where`, the case, copied
from the check's output, so one row covers exactly one finding. When it is ruled, change the
records and delete the row; a row whose finding no longer fires fails the check.

## What this does not check

Whether a label is right. A `basis: follows` naming a law that does not decide the case passes.
The checks prove a step was done and recorded consistently, and that recorded programs still do
what the record says. The judgement inside a record still needs a reader.
