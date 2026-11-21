# Ruling Audit — What 460 Owner Rulings Say About the Written Laws

> **Date:** 2026-11-21
> **Commit:** `bfbeeee6a` (dev). Read-only: nothing was built or run.
> **Scope:** every owner ruling in §4 of both batching charters: R-A … R-AV
> (`remediation-round-2026-08-30-batching.md`) and R-AW … R-QR
> (`remediation-round-2026-09-20-batching.md`), 460 rows.
> **Question:** how many rulings did a written law already answer, which principles does the owner
> apply without having written them down, and where do rulings contradict each other or the laws?
> **Data:** [`ruling-audit-2026-11-21/`](ruling-audit-2026-11-21/) holds one record per ruling and
> the verified conflict and principle lists.

---

## 1. Verdict

1. **About three in ten language questions were answerable when they were asked.** Of the 275
   rulings about the language, the library or diagnostics, a law that existed at the time decides
   79. In 50 of those 79 the ruling never names the law.
2. **About four in ten were real choices** (102 of 275), and they are not arbitrary: 32 recurring
   principles cover 245 of the 460 rulings. None of the 32 is fully written down. The most used,
   "prefer the option that can be taken back", is argued in about 70 rows and stated in no law.
3. **The tie-breaks describe the past; they did not guide it.** T1–T4 were written on 2026-11-16,
   when the newest ruling was R-PM, the 430th of 460. 35 of the rulings before that follow them
   and 2 go against.
4. **The contradictions are few and concentrated.** 4 contradictions and 9 unexplained
   asymmetries between rulings, 9 rulings that go against a law's text, and 10 supersession chains
   with an unmarked earlier row. Walrus bindings, backed enums and the "spec is silent" case hold
   most of them (§5).
5. **Where the prose laws fail, they fail by being too coarse, not by being wrong.** Of 89
   ruling-against-law candidates, 42 are cases the law's text does not decide. Axiom 2, Critical
   Rule 7, tie-break T2 and the resolution rule account for 25 of them (§6).

## 2. Method and limits

| Pass | Who | What |
|---|---|---|
| 1 | ten classifiers, 46 rulings each | one record per ruling: kind, axis, the rule restated, basis, the law relied on, door type, option chosen |
| 2a | one verifier | 48 ruling-against-ruling candidates, each checked against both rows |
| 2b | one verifier | 89 ruling-against-law candidates, each checked against the row and the law text |
| 2c | one clusterer | the rulings no law decided, grouped by the principle behind the owner's pick |
| 3 | the lead | dated each law from `git log -S`; recomputed door types from the row text; read the rows behind every finding marked **read** in §5 |

Limits a reader should hold against every number here:

- **The labels are model judgements.** The first pass rated 62% of its own basis labels medium
  confidence. The second pass rejected 42 of the 137 candidate conflicts it checked.
- **"Answerable" is judged against today's law text, then corrected for timing.** A ruling counts
  as answerable when asked only if every law it rests on existed then. The dating is by first
  appearance of a marker phrase, so a law that existed earlier in other words is undercounted.
- **Door types are a keyword count** over the row text ("one-way", "two-way", "relaxable",
  "additive").
- **Nothing was run.** A finding says two texts disagree. It does not say what the compiler does.

## 3. What the rulings are about, and what decided them

| Kind | Rulings |
|---|---|
| Language semantics | 210 |
| Process: tier, placement, scheduling | 124 |
| Library behaviour | 51 |
| Test instruments | 26 |
| CLI and LSP | 21 |
| Internal implementation | 14 |
| Diagnostic wording only | 14 |

For the 275 rulings about language, library or diagnostics:

| What decided it | Rulings | Share |
|---|---|---|
| The owner's choice; no written law decided | 102 | 37% |
| A law that existed when the question was asked | 79 | 29% |
| Partly a law that existed then, partly one written later | 38 | 14% |
| An earlier ruling | 34 | 12% |
| Only a law written after the ruling | 14 | 5% |
| It goes against a written law | 8 | 3% |

When each law was written, against the ruling sequence:

| Law | First written | Rulings made before it |
|---|---|---|
| Axioms 1–3 and the resolution rule (original wording) | 2025-12-20 | none |
| Anti-patterns, Critical Rule 7 | 2026-02-02 | none |
| Verification contract §1–§9 | 2026-08-26 | none |
| Fidelity-scope table; contract §10–§11 | 2026-09-27 | R-A … R-CH (about 86) |
| Contract §12 (tiers) | 2026-10-28 | R-A … R-JR (about 280) |
| N1–N7, S1–S3 | 2026-11-01 | R-A … R-KD (about 290) |
| Tie-breaks T1–T4; today's wording of Axioms 1–3 | 2026-11-16 | R-A … R-PM (430) |

Three readings follow.

- **Laws are written from rulings, then left unused.** Of the 131 rulings a current law decides,
  88 do not name it. The laws work as a summary, not as the place an agent looks first.
- **The first option wins.** The owner took the first-listed option in 294 of the 348 rulings that
  record a letter, and in 27 of the 31 language rulings that were both a real choice and reversible.
- **Few rulings are irreversible.** 62 rows are marked one-way only, 33 carry both words, 199 are
  marked reversible and 166 say nothing.

## 4. The unwritten principles

The clusterer found 32. The full table with boundaries is in [Appendix A](#appendix-a-the-32-principles).
The ten most used that concern the language, library or diagnostics:

| Principle | Rulings | Against | Nearest written law |
|---|---|---|---|
| Prefer the reversible option: refuse a form not yet admitted; keep one already admitted | 43 | 10 | none |
| A desugared form, alternative spelling or family member gets its primitive's verdict | 25 | 4 | N7, T4 (partial) |
| One implementation per fact, shared by the compile-time check and the runtime | 18 | 0 | Critical Rule 2 (partial) |
| What C# cannot express is refused with a named code and a C#-style steer, not emulated | 17 | 3 | resolution rule (partial) |
| The compiler never invents a type or a conversion | 14 | 3 | Axiom 3 (partial) |
| One diagnostic code means one mistake with one cure | 13 | 0 | Critical Rule 10 (partial) |
| Two claims on one name are an error naming both; nothing wins silently | 11 | 2 | N2 (partial) |
| Tooling keeps the user's bytes unless it can prove the change legal | 11 | 1 | none |
| A Python surface is promoted to "compared" by whole family, where .NET reproduces it exactly | 10 | 1 | fidelity table (no criterion) |
| A documented feature that can be typed and lowered is implemented, not documented away | 9 | 3 | Critical Rule 7 (no exception path) |

Every principle with counter-examples needs its boundary written before it can be a law. The
clusterer proposed a boundary for each from the rows; those are in Appendix A and are the least
checked part of this audit.

## 5. Decisions for the owner

Each item is one question. **Read** means the lead read the rows named; **agent** means only the
second-pass verifier did.

### 5.1 Contradictions between live rulings

**C1. A walrus onto an existing `let`** (read). R-GV: "B5 amends R-BK (`:=` on an existing `let`
is an error, never a silent shadow) … never re-asked". R-LR, later: a walrus over a `let` "is
decided in P21b's plan … until then the harness pins the cells `Unruled`", rejecting "(c) refuse
now". R-BK ("declare-if-new, rebind-if-existing") is not marked amended. *Question:* is it an
error, a write-through, or open?

**C2. A spec that is silent on whether a program runs** (read). R-HH: "the spec is silent on case
… so the surface is COMPARED (programs run vs refused, spec deferring to Python)". R-HT: "the spec
has no grammar block and is silent (fidelity-scope D5: DEVIATION until promoted)". Same premise,
opposite reading of the fidelity table. *Question:* does spec silence mean "defers to Python" or
"unlisted"?

**C3. `defaultdict`'s repr** (read, law text). R-BN records `defaultdict(<factory>, {...})` as a
deviation. R-CJ and the fidelity table, later, list `defaultdict` by name among Stdlib reprs
compared with Python. R-BN is not struck, and the table still cites it. *Question:* does a repr
that names a runtime value with no source spelling keep its placeholder inside a compared family?

**C4. `== None` on a non-null CLR reference** (agent, low confidence). R-EH refuses "a None test
on a declared non-null reference `T`". R-ES allows `== None` "on every .NET reference type".
*Question:* is `== None` a None test or an operator call?

### 5.2 Parallel cases ruled differently with no stated rule

**U1. What a backed enum is** (read).

| Backing | `==` across two enum types | Ordering on one type |
|---|---|---|
| int | `False`, as Python's plain `Enum` (R-ON) | .NET order, as `IntEnum` (R-OW) |
| string | by value, as `StrEnum` (R-QG) | refused, as plain `Enum` (R-PK) |

Only R-PK gives a reason ("there is no .NET order to keep"). No Python enum class gives either
row. *Question:* does each backing map to one Python class for every operator, or is the rule
"keep the .NET behaviour where one exists, else plain `Enum`"? The second does not fit R-QG.

**U2. Equality on a type with no `__eq__`** (read). R-PI: `==` "stays REFUSED, with one rule and
one code for every spelling, wrapper and site". R-PL: a container search "checks identity before
`__eq__` for reference-type elements … value types keep `Equals`". So `x == y` is refused while
`x in [y]` answers. R-PI's rejected option (b) was identity for reference types. *Question:* are
`in` and container `==` sites of R-PI's rule?

**U3. `str` of a built-in wrapper** (read). R-CH: `str(Some(1))` prints `1`. R-IM: `str` of a
`Result` prints `Ok(1)`. R-CH's reason was the cost of changing existing output, which does not
separate the two types. *Question:* one rule for both?

**U4. Accepting what CPython refuses on purpose** (R-HT read, R-FA agent). R-FA refuses
`hmac.new` without `digestmod` because "a kept acceptance is the one-way direction". R-HT keeps
`01` as an integer literal, "the one-way choice, taken knowingly". *Question:* what separates the
two? The verifier's candidate: record a deviation only when no valid CPython program changes
meaning and the acceptance is harmless.

**U5. Storing a plain `T` into a `T?` name** (agent). R-G: `T?` "converts implicitly in NO
direction". R-T: inside a narrowing the store "takes the PAYLOAD type as its slot and re-wraps".
*Question:* is the narrowed store the one stated exception?

**U6. A default C# cannot emit as a constant** (agent). R-A lowers it per instance for a dataclass
field. R-R refuses it for a parameter. The likely reason (Python shares one evaluated parameter
default) is in neither row.

**U7. Which exception a `None` needle raises** (agent, low confidence). R-NX follows CPython's
`AttributeError` for three Stdlib receivers. R-OS and R-OP make the boundary raise `TypeError`.

### 5.3 Rulings that go against a law's current text

**L1. A comprehension's walrus over a same-function local** (read). R-MH: it "writes through as
python does", rejecting "(a) refuse as the sentence said". N5 still says the binding "is local to
the comprehension, so where a visible outer name is read after it, the program is refused".
`comprehensions.md` and `walrus_operator.md` say the same as N5: "nothing leaks". The ruling names
its departure; neither the law nor the spec was changed.

**L2. Module-scope bindings are `const`** (read). R-BL refuses non-const module bindings with the
steer "module-scope bindings are `const`". R-GV says "ruling A (module `const`-only) … [is]
superseded"; N1 lists `let`, `var` and module variables as module-level declarations; N6's example
writes a mutable module `count`. R-BL is not marked.

**L3. A None-typed conditional statement** (agent). R-IW rejects lowering it to `if`/`else` and
ends "Axiom 1 reason stands". The resolution rule says Axiom 1 yields where a zero-cost bridge
exists, and the row does not say why this lowering is not one.

**L4. Run-time checks kept where T2 now prefers a compile error** (agent). R-BW and R-CD predate
T2 and are not marked. R-AI and R-OP chose a run-time error deliberately, with reasons T2 has no
room for (§6).

### 5.4 Clashes between the unwritten principles

- **Refuse or emulate when Python runs the program.** "Prefer the reversible option" and "refuse
  what C# cannot express" say refuse. R-AI, R-QL and R-OP emulate at run time.
- **Which side is the one-way door.** R-FH says refusing a call Python accepts is one-way. R-HU
  and R-IA say admitting Python's value is. Whether the value is known at compile time separates
  them in the rows, but no row says so.
- **The C# consumer surface before 1.0.** R-GW and R-KM break it early because there are no
  users. R-AD and R-EW protect it.
- **Queue order.** "Silent-wrong first" (13 rulings) against user-facing first in R-PM and R-GX.

### 5.5 Bookkeeping

Ten earlier rows are replaced or narrowed by a later one without a mark on the earlier row:
R-DX (by R-ES), R-DW (by R-EH), R-FR (by R-FV), R-PY (by R-QA), R-LK (by R-LV and R-MB), R-BI and
R-CN (by R-GV), R-BK and R-BL (by R-GV), R-Z (by R-OV), R-NY (by R-OO). An agent that reads only
the earlier row acts on a dead ruling.

## 6. Where the written laws are too coarse

| Law | Rulings it failed to decide | What its text lacks |
|---|---|---|
| Axiom 2 | 9 | Whether a Python twin obliges Sharpy to accept the program, or only forbids a different value. Which Python class is the twin of an int-backed or string-backed enum. |
| Critical Rule 7 | 8 | "Change the implementation, never the spec" has no exception, yet R-BR, R-OB, R-MD and R-QR amend the spec by ruling. It needs: an owner ruling may amend the spec; an agent never does. |
| Tie-break T2 | 5 | Whether a compile-time check that would also refuse correct programs still beats a run-time error (R-AI, R-EH, R-OC, R-OP, R-OU). |
| N5 | 4 | Its "closed list" of deviations omits the block-scope cases of R-LB, R-LF and R-LG; R-MI and R-LS show it colliding with N6 on a loop, `with` or `except` target under `global`/`nonlocal`. |
| Resolution rule | 3 | "Zero-cost" is undefined: does a bridge with an interop or two-rules cost count (R-HU, R-HJ, R-HW)? |
| Anti-patterns | 3 | "One way to do a thing" does not say what happens when removing a spelling breaks running programs (R-DZ). |

All 42 rows, with the sentence each law would need, are in [Appendix B](#appendix-b-the-42-cases-a-law-did-not-decide).

## 7. What this suggests

1. **Rule on §5 as one batch.** Sixteen questions (C1–C4, U1–U7, L3, and the four clashes of
   §5.4) replace an open-ended trickle. L1, L2 and §5.5 need no ruling, only the text brought
   into line.
2. **Write the principles down with their boundaries.** Appendix A is a draft. The reversible-option
   principle and its boundary would alone have covered about 30 questions.
3. **Let the agent proceed on a reversible choice.** It takes its recommendation, records a
   provisional ruling, and the owner reviews those in a batch. The record supports this: the first
   option was taken 84% of the time.
4. **Make each ruling say what it is.** One of: follows law X; is an exception to law X, for this
   reason; or is a new law. A ruling of the third kind is promoted to the law text in the same
   change. Today 88 of 131 derivable rulings name no law, and nine go against a law's
   text: four say so, one does not, and four predate the law and were never marked.
5. **Mark supersession on the earlier row**, checked mechanically: a row that says "amends R-XX"
   requires a mark on R-XX.
6. **Feed the instruments already filed.** The binding rule table (#2486, #2487) and the
   construct-law table (#2733) are where C1, L1 and L2 would have gone red. Both are queued behind
   the work that keeps producing these rulings.

## Appendix A. The 32 principles

"Rulings" counts rows the clusterer judged to turn on the principle; it read at least three per
group and every counter-example. "Against" counts rulings where the principle applied and the
owner went the other way.

| Principle | Scope | Rulings | Against | Nearest written law | Statement | Boundary the rulings suggest |
|---|---|---|---|---|---|---|
| `reversible-door` | language | 43 | 10 | — | Among open options pick the one from which the others stay additive: refuse a form not yet admitted rather than admit it, and keep admitting a form already admitted rather than restrict it. | Yields when the reversible option leaves a silent difference from Python on a COMPARED surface, a silently wrong or order-dependent value (T1), or withdraws a documented surface; then the one-way fix is taken and recorded by direction. |
| `uniform-verdict-across-forms` | language | 25 | 4 | N7 | A desugared form, an alternative spelling, another host, or another member of an operator or declaration family gets exactly its primitive's verdict, value and diagnostic code; no form-specific refusal or exemption. | Uniformity is judged by Sharpy's own type and conversion rules and by the mechanism (what gates, what converts), not by Python's family lines; a second spelling may survive to keep a correct program running (T3). |
| `one-authority-per-fact` | language | 18 | 0 | CR2 | Each fact or behaviour has one implementation every consumer calls: a compile-time check calls the runtime's pure function, an operator and its library function share one engine, and one authority spells each name or identity. | — |
| `csharp-limit-named-refusal` | language | 17 | 3 | RR | When C# cannot express a construct, refuse it before Roslyn with a named Sharpy code and the steer a C# programmer would write; do not emulate it with a lowering users could depend on. | Emulation is taken when the lowering is unobservable from Sharpy (R-AV) or when refusing would leave ordinary Python programs either refused en masse or printing a different value (R-QL, R-AI). |
| `no-invented-type-or-conversion` | language | 14 | 3 | A3 | The compiler never invents a type or conversion: a common type is the slot's or an operand's own; anything beyond value-preserving widening is spelled by the user; disagreeing candidates are refused, asking for an annotation. | Compile-time constants that fit the target, stores into a narrowed Optional, and nullability-oblivious foreign values convert implicitly; everything else is spelled. |
| `diagnostic-code-per-cure` | diagnostics | 13 | 0 | CR10 | A diagnostic code means one failure with one cure: reuse it, naming the kind in the message, for the same mistake; mint a new code when the cure differs; never filter a code by route. | — |
| `silent-wrong-first` | process | 13 | 3 | batch-issues SKILL step 3 (not a listed law) | A group that removes a silently wrong value is queued ahead of groups fixing refusals, crashes or tooling; tooling and tests-only residues wait behind user-facing silent-wrong work, regressions excepted. | Order applies within a band; across arcs the owner weighs user-facing value, and a silent-wrong cell waits when its class fix is already scheduled. |
| `group-by-class-and-seam` | process | 12 | 1 | VC10 | Cells join the open tracker and group whose contract owns their class; defects on one seam form one group with one generator, built by the group that consumes it. | A new group is formed when the cure reuses facts a just-landed group built (R-JY); otherwise cells go to existing trackers (R-JW). |
| `collision-is-error` | language | 11 | 2 | N2 | When two declarations, imports or source files claim one name in one scope, refuse and name both; neither order, kind nor star-ness picks a silent winner, and no deterministic rename is applied. | When the other party is an open-ended external set (CLR namespaces, references), whose growth would newly refuse existing code, the local name wins with a default-on warning naming the escape. |
| `tooling-keeps-user-bytes` | diagnostics | 11 | 1 | — | An editor, formatter or refactoring route keeps the user's written spelling and bytes unless it can prove the change legal and meaning-preserving; it re-spells only when forced, and otherwise is not offered. | A formatter run the user asked for may canonicalize within a stated bound (R-ET); repair routes acting on an unparseable document may not. |
| `promote-fidelity-where-exact` | library | 10 | 1 | FS | Promote a Python surface to COMPARED by whole type or message family, covering what .NET reproduces exactly or Core alone can cure; the remainder becomes one family deviation row. | Where an exact reproduction would rewrite genuine values (R-NS, R-FL's 128+N), the inexact part stays a deviation even inside a promoted family. |
| `measure-before-mechanism` | process | 10 | 1 | VC5 | Add a mechanism, optimisation, CI leg or runtime-target move only after a measurement shows the need; record an unmeasured hazard. Performance is reported for the owner to judge, not gated automatically. | Correctness checks on every platform the product ships for are not optional. |
| `documented-surface-implemented` | language | 9 | 3 | CR7 | A documented feature that can be typed and lowered is implemented rather than documented away; the spec is rewritten only where the feature has no CLR carrier or admitting it would be the one-way door. | The spec yields when it promises something with no CLR carrier (R-BR) or when a later principle (Python fidelity, R-OB) supersedes its text by ruling. |
| `loose-none-fails-like-python` | language | 8 | 2 | T2 | A possibly-None `T \| None` operand in an operation Python accepts stays legal, warns, and fails as CPython does; a literal None where the type cannot hold None is a compile error. | Applies to the loose `T \| None` spelling only; Sharpy's strict `T?` is refused until narrowed. It overrides T2 for that spelling. |
| `untypeable-api-is-deviation` | library | 8 | 2 | FS | A Python library surface that cannot be typed statically, or whose Python value occurs only in misuse, becomes one recorded deviation row with a steer, not dynamic invocation, generated factories or a widened nullable type. | A typeable surface with no other route is implemented; only untypeable, misuse-only, or alternative-covered surfaces deviate. |
| `land-semantics-before-instruments` | process | 8 | 1 | VC11 | Do not build an instrument, generator or stopgap on semantics a ruled change will flip; land the semantic change first, and move an existing structure intact before redesigning it. | An instrument that hides missing refusals goes first, since the band it audits depends on it. |
| `coverage-over-ci-capacity` | process | 8 | 0 | VC2 | Coverage a contract requires is kept and CI capacity raised to carry it; every known failure is a counted, cited row, and a missing precondition reports Skipped, never a vacuous pass. | — |
| `clr-values-keep-clr-shape` | language | 7 | 2 | CV | A CLR value keeps its CLR identity and nullability-oblivious shape; Sharpy may add capabilities but never subtracts what the CLR shape shows, and converts it only explicitly or with a check at a declared boundary. | Builtin types that are Sharpy types over a CLR backing follow Sharpy rules; the principle covers foreign values only. |
| `repr-is-construction-spelling` | library | 7 | 1 | FS | A value's str/repr is its source construction spelling with payload reprs; where the runtime lacks the source information, print a fixed documented placeholder, never CLR text or compiler-injected names. | A placeholder is a deviation only until a cheap mechanism carries the information (R-DG); then the name is kept. |
| `one-mechanical-name-rule` | language | 7 | 2 | AP | Name mangling is one mechanical rule for every name; exceptions are spelled with the backtick escape and collisions it creates are refused, never handled by tables, per-name renames or silent fallbacks. | A special case already visible to C# consumers is not reopened (R-AD). |
| `host-invariant-output` | library | 6 | 1 | T4 | A program's verdict and output never depend on the machine: locale, filesystem case, or assemblies a process happened to load; an OS that cannot reproduce a behaviour gets its own deviation row. | Where an OS lacks the data (Windows time-zone names), the difference is confined to that OS and recorded. |
| `sentinel-defaults-not-magic` | library | 6 | 1 | AP | A Python default or calling convention C# cannot express is modelled by overloads, a nullable parameter with a body coalesce, or a lambda steer; never an in-band magic value or dynamic invocation. | Overloads are synthesized only at rung 1 in library source; user-written defaults are refused with a steer. |
| `identity-never-runs-user-code` | language | 6 | 1 | — | `is` is identity only and never runs user code or a type test; `==` always runs the type's declared equality and is refused when none is declared; enum members count as identities. | Container search follows CPython's documented identity-first semantics (R-PL); value-type operands of `is` answer False with a warning. |
| `contained-fix-ships-now` | process | 6 | 3 | CR13 | A contained fix to a false refusal or crash on an ordinary program, with a closed matrix and no seam an in-flight group owns, ships now as a chore, not with its distant group. | Not when a scheduled ruled fix would rewrite the interim change, or when the fix carves a seam out of another group's contract. |
| `pre-1.0-break-early` | process | 6 | 2 | — | Before 1.0 there is no compatibility promise: breaking respellings land early, unconsumed public APIs that cannot be made correct are deleted, stale hand models are dropped, and ruled designs ship without a feature flag. | Rows disagree on whether the C# consumer surface is protected before 1.0; R-GW and R-KM say no users exist, R-AD and R-EW protect it. |
| `process-extensions-are-one-off` | process | 5 | 1 | VC12 | An extension of a process limit (an extra verify pass, a raised ratchet pin) is granted once on the owner's quoted word and is never precedent. | None stated; recurrence suggests the cap itself needs a written extension clause. |
| `clr-limit-saturates-not-refuses` | library | 4 | 2 | A1 | A run-time argument Python accepts but a CLR limit cannot hold saturates, or raises CPython's nearest exception, recorded as one family deviation row; the call is not refused. | Values in source text, known at compile time, are refused (T2); only run-time values saturate. R-FH calls refusing Python-accepted calls the one-way option, R-HU calls admitting Python's value the one-way option: the door reading flips with compile-time knowability. |
| `bug-dependent-program-not-correct` | language | 4 | 1 | T3 | Under T3, a program whose current value comes from a defect, or whose verdict differs from its primitive twin's, is not a correct program; changing it needs a direction record, not a deviation row. | Status-quo behaviour that is consistent and documented (R-OW) is kept as a deviation, not treated as a bug. |
| `tier-below-letters-at-charter` | process | 4 | 0 | VC12 | A group's tier may be set below its derived letter at charter time when full-tier dimensions have no subject (tests-only, mechanical respellings); once planning begins tiers only rise. | — |
| `static-check-only-definite` | language | 3 | 1 | T2 | A static check refuses only a definite mismatch; where the operand kind, format spec or target symbol is not statically known, it stays silent and the run-time check answers. | Holds where the missing fact is unknowable at compile time; T2 governs wherever the fact is known. |
| `users-first-release` | process | 3 | 0 | — | A defect that breaks the documented install on a supported host is fixed at once as its own chore and released as a patch, and a known workaround is posted on the issue immediately. | — |
| `backed-enum-flavour` | language | 2 | 2 | — | A backed enum behaves as the Python enum flavour of its backing (IntEnum, StrEnum) for every operator. | No consistent boundary: string-backed enums are StrEnum for == and Enum for ordering, int-backed the reverse. R-PK's reason ('no .NET order to keep') suggests 'keep .NET behaviour where it exists, else refuse', which R-ON does not fit. |

## Appendix B. The 42 cases a law did not decide

| Law | Ruling | What the law would need to say |
|---|---|---|
| A1 | R-H | A1 fixes the meaning of what compiles; it does not oblige Sharpy to accept every C#-legal shape, and a narrower acceptance names its reason. |
| A1 | R-JP | A1 covers compile-time binding (overload resolution, lookup) as well as run-time behaviour; a departure names its defect and cost. |
| A2 | R-BU | A twin forbids Sharpy giving a different value; whether it also obliges Sharpy to accept the program, or a ruling may refuse it with a steer, is stated. |
| A2 | R-CW | A twin forbids Sharpy giving a different value; whether it also obliges Sharpy to accept the program, or a ruling may refuse it with a steer, is stated. |
| A2 | R-EA | A twin forbids Sharpy giving a different value; whether it also obliges Sharpy to accept the program, or a ruling may refuse it with a steer, is stated. |
| A2 | R-OA | A twin forbids Sharpy giving a different value; whether it also obliges Sharpy to accept the program, or a ruling may refuse it with a steer, is stated. |
| A2 | R-ON | Each Sharpy enum's python twin is named: int-backed is IntEnum or Enum, string-backed is StrEnum or Enum, for every operator. |
| A2 | R-OW | Each Sharpy enum's python twin is named: int-backed is IntEnum or Enum, string-backed is StrEnum or Enum, for every operator. |
| A2 | R-PI | A twin forbids Sharpy giving a different value; whether it also obliges Sharpy to accept the program, or a ruling may refuse it with a steer, is stated. |
| A2 | R-PK | Each Sharpy enum's python twin is named: int-backed is IntEnum or Enum, string-backed is StrEnum or Enum, for every operator. |
| A2 | R-QG | Each Sharpy enum's python twin is named: int-backed is IntEnum or Enum, string-backed is StrEnum or Enum, for every operator. |
| A3 | R-DW | A nullable-oblivious CLR type has unknown nullability: writes of None are allowed, reads into T are checked at the boundary. |
| A3 | R-OP | None requires explicit opt-in, spelled T? (strict) or T \| None (loose); a plain T never holds None. |
| AP | R-AG | A Python surface is added when [stated test, e.g. corpus use, a twin with no .NET cost]; otherwise it is a 'not provided' ledger row. |
| AP | R-IR | A Python surface is added when [stated test, e.g. corpus use, a twin with no .NET cost]; otherwise it is a 'not provided' ledger row. |
| AP | R-ND | A Python surface is added when [stated test, e.g. corpus use, a twin with no .NET cost]; otherwise it is a 'not provided' ledger row. |
| CR7 | R-AG | An owner ruling may amend the spec; agents never do, and the ruling row says 'spec edit by ruling (rule 7)'. |
| CR7 | R-BR | An owner ruling may amend the spec; agents never do, and the ruling row says 'spec edit by ruling (rule 7)'. |
| CR7 | R-DQ | An owner ruling may amend the spec; agents never do, and the ruling row says 'spec edit by ruling (rule 7)'. |
| CR7 | R-IW | An owner ruling may amend the spec; agents never do, and the ruling row says 'spec edit by ruling (rule 7)'. |
| CR7 | R-MD | An owner ruling may amend the spec; agents never do, and the ruling row says 'spec edit by ruling (rule 7)'. |
| CR7 | R-MW | An owner ruling may amend the spec; agents never do, and the ruling row says 'spec edit by ruling (rule 7)'. |
| CR7 | R-NW | An owner ruling may amend the spec; agents never do, and the ruling row says 'spec edit by ruling (rule 7)'. |
| CR7 | R-OB | An owner ruling may amend the spec; agents never do, and the ruling row says 'spec edit by ruling (rule 7)'. |
| FS | R-HH | A spec silent on whether a program compiles is [deferring to python3 / unlisted, DEVIATION until promoted]; FS states which. |
| FS | R-HT | A spec silent on whether a program compiles is [deferring to python3 / unlisted, DEVIATION until promoted]; FS states which. |
| N5 | R-LB | Block scope is a designed deviation: a binding made in a block body, loop bodies included per iteration, ends with the block; a later read reaching an outer binding is refused. |
| N5 | R-LE | A local is not visible inside its own initializer, lambda bodies included; a recursive local is a nested def. |
| N5 | R-LF | Block scope is a designed deviation: a binding made in a block body, loop bodies included per iteration, ends with the block; a later read reaching an outer binding is refused. |
| N5 | R-LG | Block scope is a designed deviation: a binding made in a block body, loop bodies included per iteration, ends with the block; a later read reaching an outer binding is refused. |
| N6 | R-LS | A for, with or except target naming a global- or nonlocal-declared name is refused, with a steer to assign inside the body. |
| N6 | R-MI | A for, with or except target naming a global- or nonlocal-declared name is refused, with a steer to assign inside the body. |
| RR | R-HJ | Zero-cost means no run-time cost, no new public C# surface and no second rule for one surface; where such a bridge exists Sharpy takes Python's meaning. |
| RR | R-HU | Zero-cost means no run-time cost, no new public C# surface and no second rule for one surface; where such a bridge exists Sharpy takes Python's meaning. |
| RR | R-HW | Zero-cost means no run-time cost, no new public C# surface and no second rule for one surface; where such a bridge exists Sharpy takes Python's meaning. |
| T1 | R-FH | T1 covers every run-time value, not only initializers; a ledger row does not make a silently different value acceptable unless the ruling names T1. |
| T2 | R-AI | T2 applies where the compile-time check refuses only programs that would fail; a check that also refuses correct programs is weighed under T3 instead. |
| T2 | R-EH | T2 applies where the compile-time check refuses only programs that would fail; a check that also refuses correct programs is weighed under T3 instead. |
| T2 | R-OC | T2 applies where the compile-time check refuses only programs that would fail; a check that also refuses correct programs is weighed under T3 instead. |
| T2 | R-OP | T2 applies where the compile-time check refuses only programs that would fail; a check that also refuses correct programs is weighed under T3 instead. |
| T2 | R-OU | T2 applies where the compile-time check refuses only programs that would fail; a check that also refuses correct programs is weighed under T3 instead. |
| T4 | R-PY | A1's run-time default outranks T4; T4 chooses only among options A1 and N3 leave open. |

## Appendix C. Data files

| File | Content |
|---|---|
| `records.tsv` | One row per ruling: kind, axis, basis, law, door, option, units, the rule restated, the principle, tensions seen by the first pass |
| `principles.json` | The 32 principles with members, checked rows, counter-examples, boundary and gap |
| `conflicts-rulings.json` | 48 ruling-against-ruling candidates with their class and quoted rows, including the 8 that are not conflicts |
| `conflicts-laws.json` | 89 ruling-against-law candidates with their class and quotes, and a per-law summary |
