# Python-fidelity scope

> **Status:** Policy — ruled by the owner 2026-09-27 (`D1 a, D2 b, D3 b, D4 b, D5 a`; the decisions
> are spelled out under "Rulings" below and get their R-letters at the next `/batch-issues`, which
> also posts them on the open issues they decide). It governs what the verify rounds and sweeps FILE;
> it does not reopen any ruling already recorded in a batching charter (R-BN, R-BS, R-CC, R-CF, R-CG,
> R-CH stand).
> **Companions:** [verification-contract.md](verification-contract.md) §10 (file by class, lanes),
> the deviation ledger `docs/deviations.yaml`, [gap-discovery-contracts.md](gap-discovery-contracts.md).

## Why this file exists

Between 2026-09-06 and 2026-09-26, 323 issues were filed; 60 of them (19%) were Python-parity
cells in the long tail — exception message text, `repr` of stdlib types, error operand order,
`key()` call counts — found because every auditor compares every observable against `python3` and
files every divergence. CPython is an unbounded oracle, so that aperture never closes on its own.
This file names which surfaces Sharpy commits to matching (a divergence is a **bug**) and which are
**deviations by default** (a divergence is a ledger row or a class-tracker cell, not an issue, until
the owner promotes the surface). It closes the aperture deliberately instead of by attrition.
CLAUDE.md's anti-pattern "add X because Python has it" is the standing reason.

## The table

| Surface | Default | Where a divergence goes | Basis |
|---|---|---|---|
| Value of an expression; stdout of `print`/`str` for Core builtins (`int`, `float`, `str`, `bool`, `None`, `list`, `dict`, `set`, `frozenset`, `tuple`, `bytes`, `range`) | **COMPARED** | issue, lane `silent-wrong` | the language; `DifferentialExecutionTests`' contract |
| Which programs run vs. are refused | **COMPARED against the spec**; `python3` is the oracle only where the spec defers to Python | issue, lane `false-refusal` / `missing-refusal` | Rule 7 |
| Exception TYPE raised, and the exception hierarchy (`except LookupError` catches `KeyError`) | **COMPARED** | issue, lane `silent-wrong` or `runtime-throw` | control flow depends on it |
| Numeric semantics: `//`, `%`, `**`, the divmod identity, PEP 3101 format specs | **COMPARED** | issue | axioms table; `FormatSpecDifferentialTests` |
| `str`/`repr` of Core builtins, Sharpy enums, `Optional`, `Template`/`Interpolation`, user classes without `__str__` | **COMPARED** as ruled (R-BN, R-BS, R-CF, R-CH) | issue | rulings stand |
| Type NAMES inside runtime messages (`'str' object …`, never `'String'`) | **COMPARED** (R-CF, #2035) | issue, lane `message` | ruled |
| Exception MESSAGE text beyond the type name (`list index out of range`, `pop from empty list`, `KeyError` quoting) | **DEVIATION by default** (D1 a) | a `docs/deviations.yaml` row, or a cell on the class tracker; no new issue | unbounded; no program's control flow reads it |
| `str`/`repr` of EVERY Stdlib type (`Path`, `HTTPStatus`, `ipaddress`, `defaultdict`, iterators, `Timedelta`, …) | **COMPARED** (D2 b) | issue, lane `silent-wrong`, ONE class tracker per Stdlib type family | python3 is the oracle; the P11f repr channel (`IRepr`) is the mechanism |
| Which OPERAND a library refusal names, and comparison COUNT inside a sort (`sorted(["b", None])` names `str`/`NoneType` in timsort order) | **DEVIATION by default** (D3 b) | ledger row | .NET algorithm, Axiom 1 |
| `key=` CALL COUNT in `sorted`/`list.sort`/`min`/`max`: once per element, never per comparison | **COMPARED** (D3 b) | issue, lane `silent-wrong` | a key with side effects or cost is observable; CPython documents once-per-element |
| Culture, locale and platform text (`str`/`format`/`strftime` of dates, numbers, `%c`/`%x`) | **COMPARED** (D4 b): every Core/Stdlib text path is culture-INVARIANT, matching CPython's default C locale; current-culture output is a bug | issue, lane `silent-wrong` | .NET `CultureInfo.InvariantCulture` everywhere; one rule, no per-call opt-out |
| CPython implementation details: `id()`, addresses in `repr`, `__sizeof__`, refcount-timed finalization, `sys` internals | **NOT COMPARED** | nothing | not a language surface |
| Diagnostic wording of SPY codes | **N/A** (Sharpy's own) | `DiagnosticExplanations` | — |

**Default for an unlisted surface (D5 a):** a surface this table does not name is **DEVIATION until
promoted** — the aperture opens only by a ruling, never by an auditor's `python3` diff.

**Promotion:** a DEVIATION row becomes COMPARED only by an owner ruling at `/batch-issues`
(recorded as R-xx in the charter and in this table, naming the type family or message family it
covers). Demotion never happens silently: a COMPARED surface the team decides not to match is a
ruling too, with the `deviations.yaml` entry it creates.

## Rulings (owner, 2026-09-27; R-letters assigned at the 2026-09-30 `/batch-issues`, charter `remediation-round-2026-09-20-batching.md` §4)

| Decision | Ruling | What it decides today |
|---|---|---|
| D1 exception message text beyond the type name | **R-CI: a — DEVIATION by default** | #2108 closed to the ledger row `collection-error-message-texts`; the message half of #2100 is a row, not a cell; a spec section that quotes a message keeps it COMPARED |
| D2 `str`/`repr` of Stdlib types | **R-CJ: b — COMPARED for all Stdlib types now, one tracker per type family** | #2104 is the Stdlib repr tracker (P11g); every Stdlib type family gets an `IRepr`/`ToString` pair asserted against python3 |
| D3 evaluation order / count | **R-CK: b — split:** operand order in refusals stays DEVIATION; `key=` call count is COMPARED | #2089 closed to the ledger row `sort-refusal-operand-order`; #2090 is the bug (P34, decorate-sort-undecorate in Core, once per element) |
| D4 culture / locale / platform text | **R-CL: b — COMPARED, invariant culture everywhere** | #2056 is the bug (P35) in the direction "`str(dt)` becomes invariant"; the consistency rule (`str` ≡ plain f-string hole) follows |
| D5 default for an unlisted surface | **R-CM: a — DEVIATION until promoted by a ruling** | the next unforeseen long-tail cell is a ledger row, not an issue; first row under it: `int-float-real-imag-members` (#2059, R-DC) |

Rejected: D1 b (match every message text — unbounded, no control flow reads it); D2 a (per-family
promotion — leaves a repr channel half-built); D3 a/c (all-deviation hides an observable side-effect
count; all-compared pins .NET's comparison order to timsort's); D4 a (culture as deviation with only a
consistency rule — a program's output would depend on the host's locale); D5 b (compared-until-demoted
is the aperture that produced 60 long-tail issues in three weeks). Doors: D1/D3-order/D5 are
relaxable later (deviation → compared is additive); D2/D3-count/D4 change printed output once
(one-way for output, counted per fixture when each lands).

## How the round skills read this file

- `/verify-implementation` auditors and `/implement-plan` fixers: a `python3` divergence on a
  DEVIATION-by-default surface is a **cell comment on the class tracker** (or a ledger row when no
  tracker exists), never a new issue. A divergence on a COMPARED surface is filed under
  verification-contract.md §10 — by class, labeled at creation.
- `/batch-issues`: a group whose issues sit on DEVIATION-by-default rows gets ONE decision first —
  promote or record — before any seam is discussed.
- `/create-plan` / `/verify-plan`: the plan's Defect Class names the surface's row; a plan that
  fixes a DEVIATION-by-default surface without a promotion ruling is fixing a non-bug.
- Sweeps: `DifferentialExecutionTests` compares stdout (the COMPARED rows) and reads
  `docs/deviations.yaml` for the rest — unchanged.

## Ledger discipline

`docs/deviations.yaml` is the record for DEVIATION rows (schema in its header; every example line
executed at HEAD). A deviation cited by a spec section links here. The oracle ledger
(`build_tools/cpython_oracle/ledger.yaml`) is regenerated by `/push`'s gate when either changes.
