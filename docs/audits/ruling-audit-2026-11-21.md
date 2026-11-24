# Ruling Audit — What 460 Design Decisions Say About the Written Rules

> **Date:** 2026-11-21 · **Commit:** `bfbeeee6a` (dev) · **Read-only:** nothing was built or run.
> **Reader:** someone who knows compilers, Python and C#, and does not know this project's
> vocabulary. Section 2 defines every project term used here.
> **Data:** [`ruling-audit-2026-11-21/`](ruling-audit-2026-11-21/) holds one record per decision
> and the checked lists of conflicts and principles.

---

## 1. The short version

Sharpy's language design is decided in two places. There is a small set of written rules (three
axioms, some tie-breaks, a naming and scoping rule set). And there is a much larger pile of
individual decisions the owner made when an agent got stuck and asked. This audit read all 460 of
those decisions and asked three questions.

**Did the written rules already answer the question?** For about three in ten language questions,
yes: a rule that existed at the time picks the answer the owner gave. In most of those cases the
decision never mentions the rule. The rule was there and nobody looked it up.

**When the rules did not answer, was the owner deciding at random?** No. About four in ten
language questions were real judgement calls, and the same 32 lines of reasoning come up again
and again. They cover 245 of the 460 decisions. None of the 32 is fully written down. The most
common one, "pick the option you can undo later", is used in about 70 decisions and appears in no
rule.

**Do the decisions contradict each other?** In a few places, and they cluster. There are 4 direct
contradictions, 9 pairs of similar cases decided differently with no stated reason, and 9
decisions that go against a written rule. The walrus operator, enums with values, and "what if
the spec says nothing" hold most of them. Section 6 shows each one with code.

The practical reading: the written rules are too short to steer by, the real rules live in the
owner's head and in a table nobody queries, and the fix is to write the 32 principles down and
make the paperwork machine-checked (section 8).

## 2. Vocabulary

| Term | Meaning |
|---|---|
| **Ruling** | One design decision by the owner, numbered R-A, R-B, … R-QR in the order made. Each records the question, the options, the one chosen and the ones rejected. Think of a court judgment. |
| **Charter** | The Markdown file holding the rulings table and the work queue. Two exist, one per planning round. |
| **Law** | Shorthand in this report for any written, general rule: an axiom, a tie-break, an N-rule, and so on. Think of a statute. The table below lists them. |
| **Supersede** | A later ruling replaces an earlier one. The earlier row is supposed to be struck through. |
| **Twin** | The Python program that is spelled the same and means the same as a Sharpy program. Python's behaviour counts as evidence only when a twin exists. `struct` and `out` parameters have none. |
| **Compared / deviation** | A behaviour is *compared* if Sharpy must match Python and a mismatch is a bug. It is a *deviation* if a mismatch is accepted and listed in `docs/deviations.yaml`. |
| **Door** | Whether a decision can be undone. *Two-way:* it can, cheaply. *One-way:* undoing it breaks programs people have written. Refusing a program is usually two-way (you can allow it later). Accepting one is usually one-way. |
| **Refuse / admit** | The compiler rejects a program with an error, or accepts it. |

The written laws, with the short names used below:

| Name | What it says | Where |
|---|---|---|
| Axiom 1 | Sharpy runs on .NET and inherits its runtime behaviour | `language_specification/introduction.md` |
| Axiom 2 | Sharpy uses Python's syntax; Python's behaviour is evidence where a twin exists | same |
| Axiom 3 | Every type is known at compile time; `None` needs `T?` | same |
| Resolution rule | When Axioms 1 and 2 conflict, .NET wins, unless the compiler can bridge the gap at zero cost | same |
| T1–T4 (tie-breaks) | In order: never a silently wrong value; a compile error beats a run-time error; a correct program keeps its value; one rule for every host | same |
| N1–N7 | Name lookup, shadowing and initialization order | `design/name-resolution.md` |
| Fidelity table | Which behaviours are compared with Python | `design/python-fidelity-scope.md` |
| Critical Rules, anti-patterns | Project rules, for example Rule 7: "change the implementation, never the spec" | `CLAUDE.md` |

## 3. How the audit was done

1. **Classify.** Ten agents each read 46 rulings and wrote a record per ruling: what it is about,
   the decision restated in one sentence, and which of four things decided it (a written law, an
   earlier ruling, the owner's choice, or a choice against a law).
2. **Check conflicts.** Two agents took every suspected conflict (137 in total) and read the
   original text of both sides. They threw out 42 as not real.
3. **Find the patterns.** One agent took the rulings no law decided and grouped them by the
   reasoning behind the owner's pick.
4. **Verify.** The lead dated each law from git history, and read the original rows behind every
   conflict in section 6 marked *read*.

What to keep in mind about the numbers:

- **They are judgements by a language model, not measurements.** The classifiers marked 62% of
  their own labels "medium confidence".
- **"A law already answered it" is strict about timing.** A ruling counts only if every law it
  rests on existed when the question was asked.
- **Nothing was compiled or run.** A finding here means two documents disagree. It says nothing
  about what the compiler does today.

## 4. What the rulings are, and what decided them

Fewer than half the rulings are about the language itself:

| About | Rulings |
|---|---|
| What a Sharpy program means or whether it compiles | 210 |
| Project process: which work goes first, how much review it gets | 124 |
| Standard-library behaviour | 51 |
| Tests and test tooling | 26 |
| The command-line tool and editor support | 21 |
| Compiler internals a user cannot observe | 14 |
| The wording of an error message | 14 |

For the 275 rulings about the language, the library or error messages:

| What decided it | Rulings | Share |
|---|---|---|
| The owner chose; no written law picks an answer | 102 | 37% |
| A law that existed at the time picks the answer | 79 | 29% |
| Partly a law that existed, partly one written later | 38 | 14% |
| An earlier ruling | 34 | 12% |
| Only a law written after the ruling | 14 | 5% |
| The ruling goes against a written law | 8 | 3% |

Three things stand out.

**The laws came after the decisions.** This table shows when each law was first written, and how
many rulings already existed:

| Law | First written | Rulings already made |
|---|---|---|
| Axioms 1–3, resolution rule (original wording) | 2025-12-20 | 0 |
| Fidelity table | 2026-09-27 | about 86 |
| N1–N7 | 2026-11-01 | about 290 |
| Tie-breaks T1–T4 | 2026-11-16 | 430 |

The tie-breaks fit the earlier rulings well: 35 follow them and 2 do not. That makes them a good
summary of how the owner decides. It also means they guided almost nothing, because they did not
exist yet.

**A law being written does not mean it gets used.** Of the 131 rulings that a current law
decides, 88 never name that law.

**The owner usually agrees with the first option.** Where a ruling records which option was
picked, it was the first one listed in 294 of 348 cases (84%). Among language rulings that were
both a real choice and undoable, it was 27 of 31. And only 62 rulings are marked as one-way.

## 5. The rules nobody wrote down

These are the ten most-used principles about the language and library. "Against" counts rulings
where the principle applied and the owner went the other way; a principle with exceptions needs
its limits written before it can serve as a law. All 32, with proposed limits, are in Appendix A.

| # | Principle, in plain words | Used | Against |
|---|---|---|---|
| 1 | **Pick the undoable option.** If a form is not accepted yet, refuse it; if it is already accepted, keep accepting it. | 43 | 10 |
| 2 | **Sugar gets its expansion's verdict.** `a < b < c` behaves as `a < b and b < c` does; `x += 1` as `x = x + 1` does. No special cases per spelling. | 25 | 4 |
| 3 | **One implementation per fact.** If the compiler checks a format string, it calls the same function the runtime uses. | 18 | 0 |
| 4 | **Do not fake what C# cannot do.** Refuse it with a named Sharpy error that says what a C# programmer would write instead. | 17 | 3 |
| 5 | **The compiler never invents a type or a conversion.** If two branches disagree on type, ask for an annotation. | 14 | 3 |
| 6 | **One error code, one mistake, one fix.** | 13 | 0 |
| 7 | **Two things claiming one name is an error** that names both. Nothing wins silently by order. | 11 | 2 |
| 8 | **Editor tooling keeps the user's text** unless it can prove its change is legal. | 11 | 1 |
| 9 | **Match Python by whole family**, where .NET can reproduce the behaviour exactly. | 10 | 1 |
| 10 | **A documented feature gets implemented**, not removed from the docs. | 9 | 3 |

Principle 1 is the important one. It is the owner's most common argument, it would have settled
about 30 questions without asking, and it appears in no law. Its ten exceptions share a shape:
the owner drops it when the undoable option would leave Sharpy printing something different from
Python without telling anyone.

## 6. Where the decisions disagree

Each item shows the two texts and the question that settles them. *Read* means the lead read the
original rows. *Agent* means only the checking agent did.

### 6.1 Direct contradictions

**C1. Walrus onto a `let` variable** (read)

```python
let x = 1
if (x := 2) > 0:
    print(x)
```

- R-GV: "`:=` on an existing `let` is an error, never a silent shadow … never re-asked".
- R-LR, about a month later: the case "is decided in P21b's plan … until then the harness pins the
  cells `Unruled`". It rejects "refuse now".
- R-BK, the original: `:=` is "declare-if-new, rebind-if-existing". Never marked as changed.

One ruling says error, one says undecided, one says rebind. *Question:* which?

**C2. The spec says nothing about whether a program is legal** (read)

```python
from GREETER import hello     # the file is greeter.spy
print(01)                     # a leading zero
```

Python rejects both lines. The spec mentions neither case.

- R-HH, for the import: "the spec is silent … so the surface is COMPARED". Sharpy now rejects it.
- R-HT, for the literal: "the spec … is silent (… DEVIATION until promoted)". Sharpy accepts it.

Both rulings start from "the spec is silent" and read the fidelity table in opposite ways.
*Question:* when the spec is silent, does Python decide, or does the current behaviour stand?

**C3. Printing a `defaultdict`** (read)

- R-BN: it prints as `defaultdict(<factory>, {...})`, an accepted difference from Python.
- R-CJ and the fidelity table, a week later: the `repr` of every standard-library type, with
  `defaultdict` named, must match Python.

R-BN is not struck, and the table still cites it. *Question:* is the placeholder still allowed?

**C4. `== None` on a non-nullable .NET object** (agent, low confidence)

R-EH refuses "a None test on a declared non-null reference". R-ES allows `== None` "on every .NET
reference type". *Question:* is `== None` a None test, or just an operator call?

### 6.2 Similar cases, different answers, no stated reason

**U1. What is an enum with values?** (read)

```python
enum Color:        # int-backed
    RED = 1
enum Level:        # int-backed
    LOW = 1
enum Verb:         # string-backed
    GET = "GET"
enum Word:         # string-backed
    GET = "GET"
```

| | `Color.RED == Level.LOW` (two types, same value) | `<` within one type |
|---|---|---|
| int-backed | `False` (R-ON) | allowed, .NET order (R-OW) |
| string-backed | `True` (R-QG) | compile error (R-PK) |

Python has three enum classes. A plain `Enum` gives `False` and no ordering. `IntEnum` and
`StrEnum` give `True` and ordering. So Sharpy's int-backed enum is a plain `Enum` for `==` and an
`IntEnum` for `<`, and its string-backed enum is the reverse. Each ruling made sense alone.
Together they leave no answer to "what will the next operator do?". *Question:* what is the model?

**U2. Comparing objects that define no equality** (read)

```python
class P:
    pass

a = P()
b = P()
a == b        # R-PI: compile error, "for every spelling, wrapper and site"
a in [b]      # R-PL: allowed; compares by identity, as Python documents
```

*Question:* is `in` one of the "sites" R-PI covers?

**U3. Printing a wrapped value** (read)

```python
print(Some(1))    # 1        (R-CH)
print(Ok(1))      # Ok(1)    (R-IM)
```

Both are built-in wrapper types. R-CH's reason was that `1` was already the output and changing
it would break programs, which explains the history and not the difference. *Question:* one rule?

**U4. Accepting something Python rejects on purpose** (R-HT read, R-FA agent)

- R-FA: `hmac.new(key)` with no digest is refused, because accepting it is the choice that cannot
  be undone once people rely on it.
- R-HT: `print(01)` is accepted, described as "the one-way choice, taken knowingly".

*Question:* what makes the second acceptable and the first not?

**U5. Assigning a plain value to an optional variable** (agent)

```python
x: int? = Some(1)
x = 5                 # R-G: error; write Some(5). "T? converts implicitly in NO direction"
if x is not None:
    x = 5             # R-T: allowed; wrapped for you
```

**U6. A default value C# cannot store as a constant** (agent)

```python
@dataclass
class Bag:
    items: list[int] = []            # R-A: allowed; a fresh list per instance

def add(x: int, to: list[int] = []) -> None:   # R-R: compile error
    ...
```

There is a good reason (Python shares one list across calls, so a fresh one per call would change
behaviour). Neither ruling states it.

**U7. Which exception `None in network` raises** (agent, low confidence). R-NX says
`AttributeError`, as Python does for those three library types. R-OS and R-OP say the general rule
is `TypeError`.

### 6.3 A ruling says one thing, the law still says another

**L1. Walrus inside a comprehension** (read)

```python
def f(xs: list[int]) -> int:
    last: int = 0
    ys = [last := x for x in xs]
    return last        # Python returns the final x
```

- N5, `comprehensions.md` and `walrus_operator.md` all say the walrus variable is local to the
  comprehension, "nothing leaks", and a program like this is a compile error.
- R-MH, made after N5: it "writes through as python does", so this returns the final `x`.

The ruling knowingly overrode the law, and the three documents were never updated. An agent
reading the spec implements the opposite of what was decided.

**L2. "Module-level variables must be `const`"** (read). R-BL makes a non-`const` module variable
an error. Since then R-GV says the const-only rule "is superseded", N1 lists module variables as
normal declarations, and N6's own example mutates one. R-BL carries no mark.

**L3. A conditional used as a statement** (agent). R-IW keeps a refusal "because Axiom 1". The
resolution rule says Axiom 1 gives way when a zero-cost rewrite exists, and rewriting to an
`if`/`else` is one. The ruling does not say why that rewrite does not count.

**L4. Run-time checks where the tie-break wants a compile error** (agent). R-BW and R-CD were
made before tie-break T2 existed and now go against it, unmarked.

### 6.4 Principles that pull against each other

- **Refuse, or imitate Python at run time?** Principles 1 and 4 say refuse. Three rulings (R-AI,
  R-QL, R-OP) chose to imitate Python at run time instead, and tie-break T2 has no exception for
  them.
- **Which choice is the one-way door?** R-FH says refusing a call Python accepts is one-way. R-HU
  and R-IA say accepting Python's value is. The rows suggest the difference is whether the value
  is known at compile time. No row says so.
- **Is the C# API stable before version 1.0?** R-GW and R-KM break it freely ("no users yet").
  R-AD and R-EW protect it.
- **What goes first in the queue?** "Silently wrong results first" in 13 rulings; "user-visible
  features first" in R-PM and R-GX.

### 6.5 Stale rows

Eleven rulings, in ten chains, were replaced or narrowed by a later one and never marked: R-DX, R-DW, R-FR, R-PY,
R-LK, R-BI, R-CN, R-BK, R-BL, R-Z, R-NY. The charter tells agents not to re-ask a settled ruling,
so an agent that finds only the old row will follow it.

## 7. Where the written laws run out

In 42 cases a ruling looked like it broke a law, and on reading both, the law simply did not say.
Appendix B lists all 42 with the sentence each law would need.

| Law | Cases | What it does not say |
|---|---|---|
| Axiom 2 | 9 | If Python runs a program, must Sharpy accept it, or only never give a different result? And which Python enum class is the twin of a Sharpy enum? |
| Critical Rule 7 | 8 | "Never change the spec" has no exception, but the owner changes the spec by ruling. It needs: the owner may, an agent may not. |
| Tie-break T2 | 5 | Does a compile-time check still win if it would also reject some correct programs? |
| N5 | 4 | Its "closed list" of differences from Python is missing three block-scope cases, and it collides with N6 on loop targets under `global`. |
| Resolution rule | 3 | "Zero-cost" is not defined. Does a rewrite that complicates .NET interop count? |
| Anti-patterns | 3 | "One way to do a thing" does not cover a second spelling that existing programs already use. |

## 8. What to do about it

1. **Decide section 6 in one sitting.** It is sixteen questions: C1–C4, U1–U7, L3 and the four in
   6.4. L1, L2 and 6.5 need no decision; the documents just need to match the rulings.
2. **Write the principles down, with their limits.** Appendix A is a first draft.
3. **Let agents take the undoable option without asking.** They record what they picked; the
   owner reviews a batch and reverses any. The 84% first-option rate says most reviews will be
   quick. One-way decisions still wait for the owner.
4. **Make every ruling declare what it is:** it follows law X, or it is an exception to law X
   because of Y, or it is a new law. The last two must change the law's text in the same commit.
5. **Record replacement on the old row,** not only the new one.
6. **Build the two tables already filed** (#2486 for bindings, #2733 for sugar and its expansion).
   C1, L1 and L2 are exactly the cases they would catch.

### Which of this can a script enforce?

Fourteen kinds of slip showed up in this audit. A required, validated record per ruling would
close most of them:

| Slip found | Size | Enforceable by |
|---|---|---|
| Ruling names no law | 88 of 131 | Schema: a required `basis` and `laws` field |
| Door type not stated | 166 | Schema: a required enum |
| Recommended option not recorded | most rows | Schema: a required field |
| Law not linked to the rulings it came from | several laws | Schema: `source_rulings` on each law |
| Old row not marked as replaced | 10 | Cross-reference: `supersedes` must have a matching mark |
| Exception ruling, law text unchanged | 5 | Cross-reference: an `exception` must appear in the law's own entry |
| Spec changed by ruling, with no stated route | 8 | Cross-reference: a spec diff must cite a ruling |
| Two verdicts for one case | 4 | Cross-reference, once each ruling names its case from a fixed list |
| Similar cases, different verdicts, no reason | 9 | Cross-reference, same condition, plus a required `differs_because` |
| New law contradicts an old ruling | 4 | Cross-reference, same condition |
| Process question a table could answer | part of 124 | Partly: review level can be computed; queue order cannot |
| Question asked though a law answers it | 79 | Partly: a script can list the related laws and rulings and require a reply to each; it cannot judge the reply |
| Law too vague to decide | 42 | No: this is judgement |
| Two principles clash | 4 | No: this is judgement |

Ten of fourteen can be fully checked, two partly, two not at all. Three of the ten depend on every
ruling naming its case from a fixed list of language features, which is the work #2486 and #2733
describe. A script can prove a step was done and recorded consistently. It cannot prove the
judgement inside the record was right.

**Answered on 2026-11-24.** The owner ruled on the questions of sections 6 and 7; the answers are
rulings R-QS to R-RK in the round charter and in `docs/process/rulings/rulings.yaml`. The text above
is left as the audit found it. Still open: L4 and the "refuse, or imitate Python at run time" clash
of 6.4 (a limit on tie-break T2), and the eleven unmarked rows of 6.5 that those rulings did not touch.

**Built on 2026-11-22.** The records, the law table and the checks are in
[`docs/process/rulings/`](../process/rulings/README.md), checked by `build_tools/check_rulings.py`
and `build_tools/ruling_evidence.py`. The checks reproduce this audit's verified conflicts from the
data alone; those wait in `known-findings.yaml` for the rulings of section 6.

## Appendix A. The 32 principles

"Rulings" counts the rows the grouping agent judged to turn on the principle; it read at least
three per group and every counter-example. "Against" counts rulings where the principle applied
and the owner went the other way. The statements and boundaries are the agent's wording, kept
compact so they can be copied into a law table; they are drafts.

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
