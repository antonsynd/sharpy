# `__index__` Protocol Proposal (documented, deliberately not scheduled)

**Status:** Documented design, not adopted. Tracking issue: [#1611](https://github.com/antonsynd/sharpy/issues/1611).
**Owner ruling (2026-08-21, #1608 design session):** int positions stay `int` (or `int?` where
absence is legal) until a concrete type demands widening. This document exists so the design is
not re-derived each time the question comes up — filing it is explicitly not a commitment.

## What the protocol is

CPython's `__index__` lets a value present itself as an integer **losslessly** — distinct from
`__int__`, which may truncate (`float.__int__`). Positions that accept it in Python: sequence
indexing, slice bounds, `range()` arguments, sequence repetition counts, and the numeral
formatters (`hex`/`oct`/`bin`). CPython's slice error message names the contract: *"slice indices
must be integers or None or have an `__index__` method."*

## Why Sharpy does not need it today (verified 2026-08-21, at `1397fd242`)

The two practical beneficiaries in real Python code are numpy integer scalars and `bool`.
Neither creates demand here:

- **numpy scalars already type as `int`.** An ndarray element read (`a[0]`) flows into
  `Sharpy.List<T>.get_Item(Int32)` with no conversion — there is no distinct scalar-int type to
  bridge. (Verified by execution, not inspection.)
- **`bool` in int positions is a deliberate deviation**, not a gap. Python permits `xs[True]`
  because `bool` subclasses `int`; Sharpy's Type Safety axiom keeps them distinct, and the
  explicit spelling is `xs[int(flag)]`. (Before #1608's index-position rule, `xs[True]` was a
  silent SPY0908 ICE — refusing it loudly is the fix, not accepting it.)

With no beneficiary, adopting the protocol would be the "add X because Python has it"
anti-pattern (CLAUDE.md, Design Anti-Patterns).

## Why deferral costs nothing

Widening is compatible; narrowing is not. Today's rule — index positions take `int`, slice
bounds take anything assignable to `int?` (#1608) — is the strictest sane contract. If a
protocol is adopted later, those positions *additionally* accept the protocol interface: no
existing program changes meaning. Had we accepted something looser now, tightening later would
be a breaking change. Deferral is therefore free, and the strict rule is the safe default.

## Design sketch, if ever adopted

Follow the established dunder→interface synthesis pattern (`__len__` → `ISized`, `__bool__` →
`IBoolConvertible`, `__reversed__` → `IReverseEnumerable<T>`; see the emitter's protocol
synthesis, SPY1001):

1. **Core interface** (name illustrative): `Sharpy.IIntConvertible` with a single lossless
   conversion member. Lives in `Sharpy.Core`, C# 9.0 / netstandard2.1 conformant.
2. **Synthesis**: a class declaring `def __index__(self) -> int` gets the interface added
   implicitly at emission, exactly like the existing three protocols. Return-type rule: the
   declared return must be `int` (lossless by construction) — anything else refuses at the
   declaration site.
3. **Acceptance**: the type checker widens the *int-position* checks — sequence index, slice
   bounds (`int?` positions), `range()` arguments, sequence repetition counts — to also accept
   an `IIntConvertible` operand, and materializes a node-keyed lowering fact so codegen emits
   the conversion call without making decisions (Rule 2; the fact joins `SemanticInfo.MergeFrom`).
4. **CLR seam**: a CLR-discovered type could opt in via the same interface; no attribute or
   reflection magic is required beyond the interface check the classification pass already runs
   for other protocols.
5. **Spec first**: `docs/language_specification/` gains the protocol section before
   implementation (Rule 7), including the `__int__`-vs-`__index__` distinction (Sharpy would
   adopt only the lossless one).

## Adoption trigger

A concrete Sharpy or CLR type that is integer-like but not `int` and shows up at index/bound
positions in user code. When that happens, reopen #1611 with the type as evidence; the sketch
above is the starting point, not a constraint.
