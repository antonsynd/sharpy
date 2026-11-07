<!-- Verified by /verify-plan on 2026-09-03 @ f7c7d3d97 -->
<!-- Verification result: PASS WITH CORRECTIONS -->
<!-- Adequacy: CLASS -->

# Plan 299c1b — P2 plan B: Batch 2 numeric promotion fidelity (#1699, #1700)

Source: `.claude/plans/remediation-round-2026-08-30-batching.md` — order-table row **1b "P2 plan B — Batch 2 numeric"** (Addendum 2026-09-03; ruling R-O split P2 into two plans). Companion: `.claude/plans/plan-14853b.md` (P2 plan A — Batch 1 store seam + chore #1679 + R-G enforcement). Governed by `docs/design/verification-contract.md` (every section below applies it) and CLAUDE.md Rules 1–12.

**Pinned sha:** every measurement below is `@ f7c7d3d97 (measured)` with `sharpyc run` (CLI built from the clean tree at that sha) unless labelled otherwise. `f7c7d3d97` differs from the charter's `fd70955fe` only in `editors/vscode/package-lock.json`, so compiler source is identical. Probe sources: session scratchpad `probes/*.spy`, results in `probes/LEDGER.txt`; the C# ground truth was compiled as a net10.0 console project (`cs-truth/Program.cs`, results in `cs-truth/RESULTS.md`).

**Hard dependency: plan A Phase 2 (the `ImplicitConversions` helper — the integer-constant predicate extracted from `TypeChecker.IsImplicitConstantConversion` into a class the operator checker can call) lands first.** Phase 2 of this plan consumes it. Phases 0–1 and 3 do not depend on plan A and may be implemented in a worktree in parallel, but the gate for this plan is measured on a tree that contains plan A.

**Standing round rules (restated from the charter):** fix the class, not the cell; every new guard is mutation-tested with red/green recorded in the commit body; absence assertions carry a positive control; refusals are verified by direction with `run` against the prior commit; the commit gate is the whole solution via `.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark"`; regen spy-stdlib C#, spy-test C#, stdlib docs after any Semantic/CodeGen change and read the diff as findings; a Core change is C# 9.0 on `netstandard2.1`.

---

## Context

Two issues, one contract (the sibling of #1666's): **the recorded type of a primitive binary expression IS the type the CLR produces for the C# the emitter writes, and a pair for which C# has no predefined operator is refused by name — the C# compiler never discovers it first.**

- **#1699** — `PrimitiveCatalog.GetPromotedType` implements C# §12.4.7 only for same-width signed⊗unsigned pairs; every different-width mixed pair falls to a priority table that prefers the unsigned operand. `uint32 + int16` records `uint32` while C# computes `long` (CS0266 at the store), `uint64 + int32` records `uint64` while C# has no operator (CS0034). The 2026-09-03 owner comment widened the class: comparison (`uint64 > int32`, `uint64 == int32` → CS0034) and bitwise (`uint64 & int32` → CS0019) operators are in it, shifts are exempt, and a **constant operand** converts BEFORE promotion (§10.2.11 then §12.4.7) — so `uint32 + 1` is `uint` in C# but Sharpy records `int64` (a live false SPY0220), and `uint64 + 1` works today only by accident of the priority rule.
- **#1700** — `uint64 ** uint64` is typed `int64` and lowered to `CheckedIntPow((long)x, (long)y)`; a base above 2⁶³ wraps negative in the cast. Measured: `x: uint64 = 9223372036854775808UL; print(x ** 1)` prints `-9223372036854775808` (silent wrong value).

Owner rulings that govern this plan (comments on the issues; do not re-ask): #1699 comment 2026-09-03 (operator-family axis, constant-operand axis, arm 2 applies to variable operands only, Batch 1 → Batch 2 dependency, `**` is a call and #1699's refusal must not reach it); charter Batch-2 note ("#1700 must decide the `x ** 2` exponent rule").

## Current State (measured @ f7c7d3d97 with `run`)

| # | program | Sharpy @ f7c7d3d97 | C# (net10.0, measured) | verdict |
|---|---|---|---|---|
| f1 | `a: uint32; b: int16; c: uint32 = a + b` | SPY0908 / CS0266 | `uint + short` → `long` | ICE |
| f7 | same pair, `print(a + b)` | runs `9` (records `uint32`) | `long` | silent type mismatch (only a store exposes it) |
| f10 | same pair, `c: int64 = a + b` | runs `9` | `long` | control (works by accident) |
| f2 | `a: uint64; b: int32; print(a + b)` | SPY0908 / CS0034 | no operator | ICE |
| f18 | `int32 + uint64` | SPY0908 / CS0034 | no operator | ICE (order-symmetric) |
| f9 | `int64 + uint64` | SPY0222 "does not support operator" | no operator | control — the existing null row refuses by name |
| f5 | `uint64 > int32` | SPY0908 / CS0034 | no operator | ICE — comparison arm |
| f11 | `uint64 == int32` | SPY0908 / CS0034 | no operator | ICE — equality arm |
| f6 | `uint64 & int32` | SPY0908 / CS0019 | no operator | ICE — bitwise arm |
| f17 | `uint32 & int16` into `uint32` | SPY0908 / CS0266 | `long` | ICE — bitwise arm, other width |
| f3 | `b: uint32; c: uint32 = b + 1` | **SPY0220 int64 → uint32** | `uint + 1` → `uint` | false refusal, live |
| f16 | `uint32 - 1`, `uint32 * 2` into `uint32` | SPY0220 ×2 | `uint` | false refusal |
| f4 | `a: uint64; print(a + 1); d: uint64 = a + 1` | runs `6 6` | `ulong` | works by accident (priority rule) |
| f8 | `uint64 + K` (`const K: int = 3`) | runs `8` | `ulong` | works by accident |
| f14 | `uint64 + (1 << 2)` (folded constant) | runs `11` | `ulong` | works by accident |
| f12 | `uint64 > 1`, `uint64 & 1` | runs `True 1` | legal | works by accident |
| f13 | `uint64 + (-1)` | SPY0908 / CS0034 | no operator (−1 does not convert to ulong) | ICE — negative constant |
| f15 | `uint16 + int8` into `int32` | runs `9` | `int` | control (narrow floor) |
| — | `uint64 << int32` | runs `10` | legal (count is `int`) | control — shifts exempt |
| g1 | `x: uint64 = 9223372036854775808UL; y: uint64 = 1; print(x ** y)` | prints `-9223372036854775808` | n/a (Sharpy lowering) | **silent wrong value** (#1700) |
| g3 | `z: uint64 = x ** y` (both uint64) | SPY0220 int64 → uint64 | n/a | honest refusal of the current typing (pinned by `NarrowWidthArithmeticMatrixTests` `uint64/power-refused`) |
| g4 | `z: uint64 = x ** 2` | SPY0220 int64 → uint64 | n/a | same class, constant exponent |
| g2 / g5 | `print(x ** 2)`, `print(x ** n)` (n: int32) | run `9` | n/a | controls (typed int64 today) |
| g6 | `uint32 ** uint32` | runs `9` (int64) | n/a | control — stays int64 (no `uint` overload; spec row) |
| **m1** | `min(u64, i32)` (`a: uint64 = 5; b: int32 = 3`) | **SPY0908 / CS1503** "cannot convert from 'ulong' to `IEnumerable<int>`" | no operator | ICE — [CORRECTED: measured by the verifier @ f7c7d3d97; the plan called this cell unmeasured] |
| **m2** | `min(u32, i16)` into `int64` | **SPY0908 / CS1503** "cannot convert from 'uint' to `IEnumerable<short>`" | `long` | **ICE that a checker-only fix cannot clear** — `Builtins.Min<T>(T first, T second, params T[] rest)` (`Min.cs:173`) needs ONE `T`, and C# infers none from (uint, short); the emitter prints no type argument. Recorded `int64` is not applied (§4). See Phase 1 Task 3 |
| m3 | `min(2, 3.0)` | runs `2.0` | `double` | control — C# infers `T = double` because `int → double` is implicit; inference fails exactly for the pairs whose promoted type is a THIRD type |
| f19 | `a: uint64 = 5; print(a + 4294967296)` (long-typed constant) | **SPY0222** "uint64 does not support '+' with int64" | `UInt64` (§10.2.11: non-negative `long` constant → `ulong`) | false refusal, live — [ADDED: measured] |
| f20 | `b: uint32 = 5; b += 1; print(b)` | runs `6` | `uint` | control (augmented narrowing admits it today) — [ADDED: measured] |
| f21 | `a: uint32 = 5; c: int64 = a + 4294967296` / `c: int64 = a + (-1)` | run `4294967301` / `4` | `Int64` | controls — [ADDED: measured] |
| f22 | `uint64 > uint32`, `uint64 + uint8` into `uint64` | run `True` / `8` | legal | controls — [ADDED: measured] |
| **f23** | `d: decimal = 1; f: float = 2.0; print(d < f)` | **SPY0908 / CS0019** | no operator (decimal ⊗ double) | ICE — the comparison arm's `Bool` also covers the pair promotion already refuses for `+`; Decision 2 turns it into SPY0222 (ICE → refusal). [ADDED: adequacy — comparison × non-integer pairs] |
| g7 | `x: uint64 = 3; print(x ** 0)`, `x **= 2`, `n ** x` (`n: int32 = 3`, `x: uint64 = 2`), `y ** -1` (`y: uint64 = 3`) | run `1` / `9` / `9` / `0` | n/a | controls — [ADDED: measured] |
| g8 | `print(2UL ** 63)` | **SPY0328** "does not fit a 64-bit integer" | n/a | false refusal, live (fits `ulong`) — [ADDED: measured] |
| g9 | `y == None` (`y: int | None`) | SPY0222 "Did you mean 'is None'?" | n/a | control for the comparison-arm change — the `None` shortcut fires before the numeric arm — [ADDED: measured] |

Code facts (survey `survey-numeric.md`, code-read @ f7c7d3d97):

- `PrimitiveCatalog.GetPromotedType(PrimitiveInfo, PrimitiveInfo)` — `Semantic/Registry/PrimitiveCatalog.cs:218-255`: the mixed-signedness arm is guarded by `left.SizeInBits == right.SizeInBits` (`:236`); otherwise `GetPromotionPriority` (`:187-211`) wins, and it ranks every unsigned width above the same-width signed one (`ulong` 35 > `long` 34 > `uint` 33 > `int` 32 …). Callers: `TypeInferenceService.InferNumericResultType` (`:1604`, the only operator consumer) and `BuiltinReturnTypeInference.InferMinMaxReturnType` (`:142`, variadic `min`/`max`, #1014). The unit table `PrimitiveCatalogTests.cs:172-182` has three mixed rows, all same-width.
- All four operator families are typed in ONE method, `TypeInferenceService.TryInferBuiltinBinaryOp` (`:294-357`): shifts `:298-302` promote the left operand alone (correct); bitwise `:305-317` consult promotion (a null falls through to SPY0222 — correct); arithmetic `:320-346` consult promotion; **comparison `:348-353` returns `Bool` unconditionally** for any `IsNumeric` pair — `ulong`/`int` is such a pair, so it reaches the emitter as a native `<`/`==`. `OperatorValidator.cs:165-180` short-circuits every comparison whose left operand is primitive, so no SPY0402 either.
- **No constant-operand handling exists in operator typing.** `IsImplicitConstantConversion` (`TypeChecker.Utilities.cs:640`) has seven call sites, all store/binding positions; `InferBinaryOpType` is memoized on the type triple `(left, op, right)` (`TypeInferenceService.cs:80-92`), so the constant axis must be decided in `CheckBinaryOp` (`TypeChecker.Expressions.Operators.cs:14`; the `InferBinaryOpType` call it must precede is at `:108` [CORRECTED: was ":107" for both]) BEFORE the call, by choosing the effective operand types. **Verified contract of the existing predicate (`Utilities.cs:640-680`):** an `int` constant converts to `sbyte`/`byte`/`short`/`ushort`/`uint`/`ulong` when its VALUE fits; a `long` constant converts to `ulong` only, and only when non-negative; it returns **false** for `int` and `long` targets (standard conversions, deliberately excluded) and for any other source type (a `u`-suffixed constant is already `uint`); `const` references and folded shapes go through `IntegerConstantEvaluator` with the resolver delegate — so the pre-step reads "ICC false ⇒ keep the constant's own type", which is right for every cell in the ground truth (`int64 + 1` → (long, int) → long either way).
- **`min`/`max` value form has an EMITTER half** [ADDED: measured @ f7c7d3d97]: `Builtins.Min<T>(T first, T second, params T[] rest)` (`src/Sharpy.Core/Min.cs:173`) is emitted with NO type argument, so C# must infer one `T` — it cannot for (uint, short) or (ulong, int), and both `min(u32, i16)` and `min(u64, i32)` are SPY0908/CS1503 today regardless of what the checker records. The emitter already prints explicit type arguments for a call when `SemanticInfo.GetInferredTypeArguments(call)` is set (`RoslynEmitter.Expressions.Access.cs:1173`, set by `SetInferredTypeArguments` `SemanticInfo.cs:868`, merged); the builtin-call route must reach that reader (or a sibling) for the recorded promoted type to be APPLIED (§4 recorded ≠ applied).
- `**`: `InferPowerResultType` (`TypeInferenceService.cs:1591-1599`) → `ApplyIntegerPowWidth` (`:1637-1640`) collapses `UInt` AND `ULong` to `Long` because `CheckedIntPow` has only `(int,int)` (`Sharpy.Core/Pow.cs:83`) and `(long,long)` (`:132`). The lowering kind is chosen at TWO copy-pasted sites — binary `TypeChecker.Expressions.Operators.cs:236-260` and augmented `TypeChecker.Statements.cs:459-483` — keyed on `resultType == SemanticType.Long`, unlike `//`/`%` which share `ClassifyFlooredArithmetic` (`Operators.cs:316`). The emitter arm `RoslynEmitter.Operators.cs:1097-1135` (`GeneratePowerValue`) casts BOTH operands to `int` or `long` (two-way ternary `:1117`) and its `default` throw names the four legal kinds. `TryFoldIntegerPower` (`Operators.cs:359-390`) folds constant⊗constant to `Int`/`Long`/SPY0328 and returns before any lowering tag. `_operatorLowerings` is already in `MergeFrom` (`SemanticInfo.cs:1683`).
- Tests: `NarrowWidthArithmeticMatrixTests` (850 lines; generated matrix width × op × store × target; every generator declares both operands at the SAME width — §5.4 of the survey; the `uint64` group `:538-593` pins `p: int64 = a ** b` → `49` and `c: uint64 = a ** b` → SPY0220 citing #1700; `MatrixIsTotalOverItsAxes` `:700` hard-codes `+ 7` for the uint64 group). `PrimitiveCatalogTests` `:275-283` pins `long ⊗ ulong → null`. `RoslynEmitterOperatorLoweringTests.cs:291-292` pins the two emitted `CheckedIntPow` spellings. No fixture has an unsigned operand pair; no allowlist cites #1699/#1700; `grep 1699` over `src/` and `docs/` is empty.
- Spec: `arithmetic_operators.md:310-330` "Numeric Type Promotion" has NO mixed-signedness row and its key rule 1 ("the larger integer type") is the priority table restated; `:369-403` "Unsigned result widths" and `:227` both state `uint64 ** uint64 → int64`; `comparison_operators.md` is 13 lines with no operand rules; `bitwise_operators.md:25-26` describes `& | ^` only by contrast; `integer_literals.md:53` says "Unsigned suffixes follow C# promotion rules" with no table.

**C# ground truth (Roslyn, net10.0 SDK, `cs-truth/RESULTS.md`, 45 cells):** after §10.2.11 constant conversion, §12.4.7 binary numeric promotion for `+ - * / % & | ^ == != < > <= >=` is: if either operand is `ulong`, the other must be implicitly convertible to `ulong` (any unsigned type, or a non-negative constant) or the operator is ambiguous (CS0034/CS0019); else if either is `uint` and the other is `sbyte`/`short`/`int` → `long`; else if either is `uint` → `uint`; else if either is `long` → `long`; else `int`. Shifts: left operand alone, count `int`. Measured rows: `uint+short`, `uint+int`, `uint+sbyte`, `uint&short`, `uint|int`, `uint+(-1)`, `uint+4294967296` → `Int64`; `uint+byte`, `uint+ushort`, `uint+1`, `uint-1`, `uint*2`, `uint+K`, `uint+(1<<2)`, `uint+2147483648` → `UInt32`; `ulong+byte`, `ulong+uint`, `ulong+1`, `ulong+K`, `ulong+KL(long const)`, `ulong+4294967296`, `ulong&1`, `ulong%3`, `ulong^byte`, `ulong<<int`, `ulong+(int)K` → `UInt64`; `ulong+int`, `ulong+short`, `ulong+sbyte`, `ulong+long`, `int+ulong`, `ulong/int`, `ulong*int`, `ulong+(-1)`, `ulong>int`, `ulong==int` → CS0034; `ulong&int` → CS0019; `uint>short`, `uint==int`, `ulong>1`, `ulong<uint` → `Boolean`; `ushort+sbyte` → `Int32`.

## Defect Class

- **Violated contract:** for every primitive operand pair and every non-shift binary operator family, the checker's recorded result type equals the CLR result type of the emitted native operator (or the bound `Sharpy.Core` overload for lowered operators), and a pair C# cannot bind is refused by name (SPY0222) — uniformly across {arithmetic, comparison, bitwise} × {variable, constant} operands.
- **Meta-class:** SPY0908 as a net (the C# compiler is doing the refusing) plus **silent wrong behavior** (`**` wrap; `print(a + b)` records a type the CLR does not produce) → **standard cures:** semantic-time refusal at ONE promotion seam; a conformance matrix over the sibling cells so the class cannot recur (verification-contract.md §1).
- **Known member cells:** #1699 (arithmetic, two width pairs), #1700 (`uint64 ** uint64` base > 2⁶³).
- **Sibling cells this plan must also cover (the issue repro lists are symptom reports):**
  - width × signedness: all 8 integer types × 8 (64 ordered pairs; the result is order-symmetric — assert both orders);
  - operator family: arithmetic `+ - *` (and `/ // %` through their own lowering widths), comparison `== != < > <= >=`, bitwise `& | ^`; shifts as exempt controls;
  - operand kind: {variable, int literal, `const` reference, folded constant (`1 << 2`), negative constant, long-typed constant (`4294967296`), suffixed constant (`4294967295u`)};
  - `**`: {base, exponent} ∈ {int32, int64, uint32, uint64} × {variable, non-negative constant, negative constant}, base above 2⁶³, exponent 0/1/2, result overflow, augmented `**=`;
  - consumers of the promoted type: `min`/`max` (`InferMinMaxReturnType`), unary `-`/`~` (unchanged, controls), augmented forms of every operator (`+=` etc. reach `InferAugmentedAssignmentType` → the same promotion).
- **Standing harness that should have caught it:** `NarrowWidthArithmeticMatrixTests` is the right host (it already cites #1698/#1700) but every generator fixes both operands at one width — Phase 4 adds the mixed-signedness and constant-operand generators and rosters the class in `docs/design/gap-discovery-contracts.md`. No gap-discovery sweep ratchets this class (no allowlist cites #1699/#1700); the differential-execution allowlist rows 78-82 (min/max mixed-numeric deviation) are the only promotion entries and stay.
- **Owner rulings:** #1699 [comment 2026-09-03](https://github.com/antonsynd/sharpy/issues/1699) (five consequences, quoted in Context); charter Addendum 2026-09-03 "Batch 2: constant-operand axis; #1700 must decide the `x ** 2` exponent rule".

## Adversarial Review (pre-mortem)

- **Alternative root cause:** "the emitter needs a cast" — refuted: `f7` shows the checker records `uint32` for `uint32 + int16`; the C# `a + b` IS `long`; a cast to `uint` would silently truncate. The type is wrong at the checker. Second alternative: "the priority table is right and only the same-width guard is too narrow" — refuted by `uint + short → long` (the priority table would say `uint`). The discriminating probe is the 8×8 unit table in Phase 1 against the C# ground truth.
- **How the fix could be inert:** (a) fixing `GetPromotedType` alone leaves the comparison arm returning `Bool` for `(ulong, int)` — `f5`/`f11` stay ICEs; Phase 1 Task 2 makes the comparison arm consult the same promotion, and mutation M3 (comparison arm ignores the null) must turn the `uint64 > int32` cell red. (b) The constant pre-step decided in `CheckBinaryOp` but not at the augmented site (`InferAugmentedAssignmentType`) leaves `u32 += 1` inconsistent — Phase 2 routes both through one helper and the matrix has an augmented column. (c) The `**` classifier added at the binary site only — the augmented `**=` site is a copy; Phase 3 replaces both with one `ClassifyIntegerPower` (mutation M5).
- **Before/after by direction (every newly-refused program named):** `uint64 + int32`, `int32 + uint64`, `uint64 > int32`, `uint64 == int32`, `uint64 & int32`, `uint64 + (-1)`: SPY0908 → SPY0222 (ICE → diagnostic). `uint32 + int16` in a `uint32` store, `uint32 & int16` in a `uint32` store: SPY0908 → SPY0220 (the recorded type is now `int64`, so the store refuses by name; `int64` stores keep running). Newly accepted: `uint32 + 1` / `- 1` / `* 2` into `uint32` (SPY0220 → runs), `z: uint64 = x ** y`, `z: uint64 = x ** 2` (SPY0220 → runs). Changed value: `x ** 1` for `x > 2⁶³` (wrong → right). **No program that ran correctly before is refused after** — checked cell by cell in Phase 0's direction table; `min(u64, i32)` [CORRECTED: measured @ f7c7d3d97 — SPY0908/CS1503, and so is `min(u32, i16)`; both flip ICE → (refusal, runs) only if Phase 1 Task 3 also records the type argument the emitter prints]. Also newly classified: `decimal < float64` SPY0908/CS0019 → SPY0222 (ICE → refusal; python3 compares `Decimal(1) < 2.0` fine, so this is an Axiom-1 refusal the spec's comparison section must state); `uint64 + 4294967296` SPY0222 → runs; `2UL ** 63` SPY0328 → runs.
- **Blast radius:** `Sharpy.Stdlib.Tests` (stdlib `.spy` and hand-written stdlib that do unsigned arithmetic — `zlib`/`binascii`/`struct`/`hashlib`-style modules; a program that compiled before still types the same or better by construction of the C# rule, but the regen diff is read as findings), `Sharpy.Cli.Tests`, `FrontEndParityTests` (hover shows the new recorded types), the three GapDiscovery sweeps (InteropConformance, MetamorphicCorpus, DifferentialExecution — the fresh-seed ILCompiles generator emits mixed-width arithmetic and is the sweep most likely to move), `Sharpy.Core.Tests` (new `CheckedIntPow` overloads). Warm/cold: `_binaryOpCache` is per-service instance; the effective-type pre-step keeps its key a pure type triple, so the LSP's warm re-analysis sees no new state.
- **Generated artifacts touched:** spy-stdlib C#, spy-test C#, stdlib docs, oracle ledger — regenerate immediately after Phase 1 (the first codegen-visible typing change) and again after Phase 3; each regen commit records "no diff @ sha" or the diff read as findings.

## Design Decisions

1. **One promotion seam, C# §12.4.7 verbatim, in `PrimitiveCatalog.GetPromotedType`.** The `SizeInBits ==` guard and the priority table are replaced by the rule stated in the ground truth: `ulong` requires an unsigned partner (else null); `uint` with `sbyte`/`short`/`int` → `long`; `uint` otherwise → `uint`; `long` → `long`; else `int`. Floats/decimal keep today's arms. The priority table survives only for the float/decimal ranks (or is deleted if the new arms cover them — the implementer decides, guarded by the existing decimal tests `PrimitiveCatalogTests.cs:206-264`). `min`/`max` (`InferMinMaxReturnType`) consume the same seam by construction and gain a matrix cell; the `builtin_functions.md:340-343` deviation note is re-executed.
2. **Comparison legality is promotion legality.** `TryInferBuiltinBinaryOp`'s comparison arm returns `Bool` only when `InferNumericResultType(left, right)` is non-null; a null falls through to SPY0222 exactly as bitwise does today. Axiom 1: C# binds `<`/`==` through the same binary numeric promotion; there is no separate rule to invent.
3. **The constant-operand axis is decided at the operator's syntax node, not in the memoized type service.** `CheckBinaryOp` (and the augmented path in `CheckAssignment`) compute the *effective* operand types before calling `InferBinaryOpType`: if exactly one operand is an integer constant expression and it converts implicitly to the OTHER operand's primitive type under §10.2.11 (plan A's `ImplicitConversions.IsImplicitIntegerConstantConversion(value, source, target, resolveConstant)` [CORRECTED: plan A Decision 2 spells it with the `Integer` infix and a fourth `resolveConstant` delegate — the const-reference operand kind in this plan's matrix needs that delegate, so the operator site passes the checker's symbol-table resolver exactly as the store seam does] — the same evaluator, range check and `const` folding the store seam uses), that operand's effective type is the other operand's type. Both constants: unchanged (folding/overflow rules SPY0348/SPY0328 already own that cell). The literal's own recorded type is NOT rewritten (C# performs the conversion itself — the same reasoning the store seam's integer arm documents), so hover and the constant-overflow checks are untouched. Shifts skip the pre-step (the count is not converted to the left operand's type).
4. **`**` is a call and follows its overload family, never the operator refusal.** Contract for integer `**`: *the emitted `CheckedIntPow` call takes each operand at its own width — no operand is narrowed by the emitted cast.* `Sharpy.Core/Pow.cs` gains three overloads mirroring #1662's `FloorDiv(ulong, ulong)` shape: `CheckedIntPow(ulong x, ulong y)`, `CheckedIntPow(ulong x, long y)`, `CheckedIntPow(long x, ulong y)`. Typing/lowering rule after the constant pre-step and the narrow floor: (int,int) → `int` / `IntegerPowInt`; (int|long, int|long) with a long → `long` / `IntegerPowLong`; uint32 involved without ulong → `int64` / `IntegerPowLong` (unchanged spec row: no `uint` overload); (ulong, unsigned or non-negative constant) → `uint64` / `IntegerPowULong` (`(ulong)`,`(ulong)` casts); (ulong, signed variable) → `uint64` / `IntegerPowULongExponentLong` (`(ulong)`,`(long)`); (signed, ulong) → `int64` / `IntegerPowLongExponentULong` (`(long)`,`(ulong)`). `ApplyIntegerPowWidth` loses its `ULong` arm. ONE classifier `ClassifyIntegerPower(leftType, rightType)` in `TypeChecker.Expressions.Operators.cs`, consumed by the binary site AND the augmented site (the `//`/`%` `ClassifyFlooredArithmetic` pattern), deleting the two copies. Negative-exponent semantics stay the spec's (`2 ** -1 == 0`): the `(ulong, long)` overload keeps the `y < 0` truncating-double branch; the `(ulong, ulong)` and `(long, ulong)` overloads have no negative branch (unreachable). Constant fold: `TryFoldIntegerPower` returns the classifier's result type when the folded value fits it (`ULong` for a ulong-typed pair whose result fits `ulong`); the lowering pass's constant re-derivation must carry a `ulong` constant — Phase 3 Task 4 verifies/extends it.
5. **Diagnostics:** no new code. Mixed-signedness refusals reuse SPY0222 (`ReportUnsupportedBinaryOperator`) with a steer suffix naming the cure (`— cast one operand: 'int64(a)' or 'uint64(b)'`), and `DiagnosticExplanations.Semantic.cs:74-77` gains the mixed-signedness sentence (Rule 10 keeps `DiagnosticExplanationsTests` green). Stores of the corrected types refuse with the existing SPY0220.
6. **Rule 2:** every decision is semantic-side. The new lowering kinds ride `_operatorLowerings` (already in `MergeFrom`); the emitter's `GeneratePowerValue` reads the kind and chooses casts by kind (switch, not ternary); its `default` throw enumerates the seven legal kinds so a missing arm is loud.

## Implementation Checklist

- [x] Phase 0 Task 1: Measure the base — lead — scratchpad probes confirmed
- [x] Phase 0 Task 2: Spec: numeric promotion rewritten — lead — aae5af50d
- [x] Phase 1 Task 1: GetPromotedType implements §12.4.7 — semantic-expert — 7a54e79dc
- [x] Phase 1 Task 2: Comparison arm consults promotion — semantic-expert — 7a54e79dc
- [x] Phase 1 Task 3: min/max consumer cell — semantic-expert — 6c7cedd4b
- [x] Phase 1 Task 4: Regen — lead — no diff @ 7a54e79dc
- [x] Phase 2 Task 1: Effective operand types — semantic-expert — 102a37d3c
- [x] Phase 3 Task 1: Core overloads — core-library-expert — eddec34c6
- [x] Phase 3 Task 2: One classifier, three kinds — semantic-expert — 7c38f1627
- [x] Phase 3 Task 3: Emitter reads the kind — codegen-expert — 7842af495
- [x] Phase 3 Task 4: Constant fold carries uint64 — lead — 2a49e531d
- [x] Phase 3 Task 5: Regen — lead — 17746f7cd (stdlib docs: 3 new overload entries)
- [x] Phase 4 Task 1: Matrix generators + fixtures — test-expert — 41ef73498
- [x] Phase 4 Task 2: Roster + explanations — lead — 6bcfe861b
- [x] Phase 4 Task 3: Close-out — lead — 24,147/0/3 @ d8ec94a19 (measured); all checklist items done

## Implementation

### Phase 0: Pin, measure, and write the spec first

**Goal:** the direction table and the spec exist before any code moves; every executed example in the spec is measured against HEAD before and after.

**Acceptance (class-level, measurable):**
- Direction table: every cell in Current State plus `min(u64, i32)`, `max(u32, i16)`, augmented `u32 += 1`, `u64 += i32`, `u32 &= i16`, `u64 **= 2`, `u64 **= n`, `n ** u64`, `2UL ** 63`, `2UL ** 64` measured with `run` at the plan's base sha and recorded in `probes/LEDGER.txt` (the close-out cites it).
- Spec sections rewritten with executed examples whose outputs discriminate (an example that prints the same thing with the bug present proves nothing — `print(a + b)` for `uint32 + int16` is NOT an example; `c: uint32 = a + b` refused and `c: int64 = a + b` accepted are).
- Execution evidence: each spec example's stdout/diagnostic recorded in the commit body `@ sha`.

#### Tasks

1. **Measure the base** — `probes/*.spy` (scratchpad), `probes/LEDGER.txt`
   - Re-run the Current State ledger at the plan's base sha (plan A's final sha) and add the unmeasured cells listed above; every `**` cell also runs under `python3` for the value oracle where the result fits (`(2**63)**1`, `3**2`, `2**63`).
   - Acceptance: the ledger file; no commit (scratchpad).

2. **Spec: numeric promotion rewritten to §12.4.7 + §10.2.11** — `docs/language_specification/arithmetic_operators.md` (§"Numeric Type Promotion" `:310-330`, §"Unsigned result widths" `:369-403`, the `**` row `:227`), `docs/language_specification/comparison_operators.md` (new §"Operand types"), `docs/language_specification/bitwise_operators.md` (`& | ^` promotion table by reference), `docs/language_specification/integer_literals.md:53`, `docs/language_specification/builtin_functions.md:340-343`
   - Replace key rule 1 with the C# rule; add the full mixed-signedness table (`int32 ⊗ uint32 → int64`, `uint32 ⊗ int8/int16/int32 → int64`, `uint32 ⊗ uint8/uint16 → uint32`, `uint64 ⊗ unsigned → uint64`, `uint64 ⊗ signed → ❌ SPY0222` with the cast steer); add the constant-operand rule with `b: uint32 = 5; c: uint32 = b + 1` (accepted) and `a: uint64 = 5; print(a + (-1))` (SPY0222) as executed examples; state that comparison and bitwise operators follow the same table and shifts do not.
   - `**`: replace the two `uint64 ** uint64 → int64` statements with the Decision-4 table; example: `x: uint64 = 9223372036854775808UL; print(x ** 1)` → `9223372036854775808`, `z: uint64 = x ** 2` (OverflowError at runtime — the example prints the caught error), `print(3 ** 2)`, `y: uint64 = 3; print(y ** -1)` → `0` if the plan keeps the spec's truncating rule for the `(ulong, long)` overload (executed).
   - Every example executed at HEAD BEFORE the code lands is recorded as "refused/ICE before" in the commit body, and re-executed after Phase 3 (Rule 7 + contract §8).
   - Commit: `docs(spec): numeric promotion follows C# §12.4.7 with §10.2.11 constant conversion; comparison and bitwise operators share the table; integer ** binds its own-width overload (#1699, #1700)`

### Phase 1: The promotion seam (#1699 arms 1–2, comparison and bitwise families)

**Goal:** `GetPromotedType` is §12.4.7; the comparison arm consults it; `uint64 ⊗ signed` is refused by name in every family.

**Acceptance (class-level, measurable):**
- Matrix that goes green: `PrimitiveCatalogTests.GetPromotedType_MatchesCSharpBinaryNumericPromotion` — an explicit 8×8 ordered-pair table (64 rows, both orders present, expected values from the C# ground truth; `null` for the 7 `ulong ⊗ signed` pairs ×2 orders) — plus `NarrowWidthArithmeticMatrixTests.MixedSignednessPrograms` (Phase 4) cells for families {arith, cmp, bitwise} × pairs {uint32/int16, uint32/int32, uint32/uint16, uint64/int32, uint64/int64, uint64/uint32, int64/uint64}.
- Guard mutation: M1 restore the `left.SizeInBits == right.SizeInBits` guard → the 8×8 table red on every different-width mixed row (record the count); M3 comparison arm returns `Bool` unconditionally → the `uint64 > int32` and `uint64 == int32` program cells red (SPY0908 where SPY0222 is expected); restore → green.
- Allowlist entries that drain: none exist. Positive control for the absence assertion "no SPY0908 in the matrix": the matrix's per-cell assertion requires the named diagnostic at the exact line and asserts the diagnostic COUNT (existing `:647-649` pattern), so a leaked SPY0908 fails the count.
- Execution evidence: `f1 f2 f5 f6 f11 f17 f18` flip ICE → named refusal; `f7` records `int64` (probe `c: int64 = a + b` runs, `c: uint32 = a + b` SPY0220); `f9`, `f10`, `f15`, shift control unchanged — all `@ sha (measured)`.

#### Tasks

1. **`GetPromotedType` implements §12.4.7** — `src/Sharpy.Compiler/Semantic/Registry/PrimitiveCatalog.cs:218-255` (+ `:187-211`)
   - Replace the same-width special case and the integer half of the priority rule with the ground-truth rule (Decision 1); keep the decimal/float arms and their tests. Doc comment cites ECMA-334 §12.4.7 and this plan's measured table.
   - `PrimitiveCatalogTests`: replace the three same-width mixed rows and `GetPromotedType_ReturnsNullForLongUlongMixedTypes` with the 8×8 theory (both orders) and keep the decimal theories. Commit body records M1.
   - Acceptance: the 8×8 table green; existing decimal rows green.
   - Commit: `fix(semantic): GetPromotedType is C# §12.4.7 — unsigned⊗narrower-signed promotes to int64, uint64⊗signed has no operator, full 8×8 table (#1699)`

2. **Comparison arm consults promotion** — `src/Sharpy.Compiler/Semantic/TypeInferenceService.cs:348-353`
   - `Equal … GreaterThanOrEqual => InferNumericResultType(left, right) is null ? null : SemanticType.Bool` (the lifted-nullable and `None` shortcuts at `:126-165` run before this arm and are unaffected — add the `x == None` and `int? < int` controls to the matrix so the mutation can't hide there).
   - `ReportUnsupportedBinaryOperator` gains the cast steer suffix for a primitive-integer pair; `DiagnosticExplanations.Semantic.cs:74-77` gains the mixed-signedness sentence.
   - Acceptance: `uint64 > int32`, `uint64 == int32`, `uint64 & int32` → SPY0222 with the steer (by direction: SPY0908 before); **`decimal < float64` → SPY0222 (SPY0908/CS0019 before — measured; the steer for this pair names `decimal(f)`/`float(d)`, not the integer cast)** [ADDED: adequacy — the comparison arm's `IsNumeric` block also covers decimal ⊗ float, the one non-integer pair promotion refuses; `float32 ⊗ float64`, `int ⊗ float`, `decimal ⊗ int` stay `bool` as controls]; `uint32 > int16`, `uint64 > uint32`, `uint64 < 1` still `bool`; `y == None` stays the existing "Did you mean 'is None'?" refusal (the `None` shortcut precedes the numeric arm — measured). Commit body records M3.
   - Commit: `fix(semantic): comparison operators refuse the pairs C# cannot bind — one promotion answer for arithmetic, comparison and bitwise (#1699)`

3. **`min`/`max` consumer cell** — `src/Sharpy.Compiler/Semantic/Registry/BuiltinReturnTypeInference.cs:128-145`
   - With the new seam, `min(u64, i32)` promotes to null and falls through to the iterable-element path; it is SPY0908/CS1503 today (measured — the plan's Phase 0 row); make it a named refusal at the builtin's own argument check (SPY0220 family message naming both types) rather than an Unknown leak.
   - [CORRECTED: recorded ≠ applied] **The accepted mixed cells need an emitter-visible fact.** `min(u32, i16)` is SPY0908/CS1503 today and STAYS one if the checker merely records `int64`: `Builtins.Min<T>(T, T, params T[])` is emitted without a type argument and C# cannot infer `T` from (uint, short) (measured; `min(2, 3.0)` works only because `int → double` lets C# pick `double`). Record the promoted type as the call's type argument — `SetInferredTypeArguments(call, [promoted])` (`SemanticInfo.cs:868`, already in `MergeFrom`, already read by the emitter's generic-call path at `RoslynEmitter.Expressions.Access.cs:1173`) — and confirm the builtin `min`/`max` value-form route reaches that reader so the emitted call is `Builtins.Min<long>(a, b)` (Rule 2: the checker decides `long`, the emitter prints it). Do it whenever the promoted type differs from either argument's type, so the rule has one shape. Cells in the matrix, each asserting the PRINTED value: `min(u64, i32)` refused, `min(u32, i16)` → `3` stored into `int64`, `max(u64, u32)` → `5` stored into `uint64`, `min(2, 3.0)` → `2.0` (control). Mutation **M7** [ADDED]: drop the recorded type argument → the `min(u32, i16)` cell red (SPY0908/CS1503) while `min(2, 3.0)` stays green (positive control that the fact, not luck, carries the mixed cell).
   - Commit: `fix(semantic): min/max over a signed⊗uint64 pair is refused by name; a promoted min/max records its type argument so the emitted call binds (#1699)`

4. **Regen after the first typing change** — `build_tools/regenerate_spy_stdlib.sh`, spy-test regen, `build_tools stdlib generate --force`, oracle ledger
   - Read the diff as findings (contract §7). Any stdlib program whose recorded type changed is listed in the commit body with its before/after behavior.
   - Commit: `chore(regen): spy-stdlib C#, spy-test C#, stdlib docs after the promotion seam (no diff @ <sha> | findings: …)`

### Phase 2: The constant-operand axis (§10.2.11 before §12.4.7) — depends on plan A's `ImplicitConversions`

**Goal:** a constant operand converts to the other operand's type before promotion, in every family, at the binary AND augmented sites, through the ONE integer-constant predicate plan A extracted.

**Acceptance (class-level, measurable):**
- Matrix that goes green: `NarrowWidthArithmeticMatrixTests.ConstantOperandPrograms` — target {uint32, uint64} × operator family {arith `+ - *`, cmp `> ==`, bitwise `& |`} × constant kind {int literal, `const` reference, folded `(1 << 2)`, negative literal, long-typed literal `4294967296`, `u`-suffixed literal} × site {binary expression stored into the target type, augmented `op=`} with expected outcomes from the ground truth (`uint32 + 1` → `uint32`; `uint32 + (-1)` → `int64`; `uint64 + (-1)` → SPY0222; `uint64 + 4294967296` → `uint64`; `uint32 + 4294967296` → `int64`).
- Guard mutation: M2 delete the pre-step (pass the literal's own type) → every `uint32 ⊗ literal` store cell red (SPY0220 int64 → uint32) and every `uint64 + literal` cell red (SPY0222); restore → green. Positive control: the negative-literal cells must stay refused with the pre-step present.
- Execution evidence: `f3`, `f16` flip SPY0220 → run and print `6 4 10`; `f4 f8 f12 f14` unchanged output; `f13` ICE → SPY0222 — `@ sha (measured)`.

#### Tasks

1. **Effective operand types at the operator node** — `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.Operators.cs` (`CheckBinaryOp`, before the `InferBinaryOpType` call at `:108` [CORRECTED: was :107]), `src/Sharpy.Compiler/Semantic/TypeChecker.Statements.cs` (augmented path before the `InferAugmentedAssignmentType` call at `:382`)
   - One private helper `EffectiveOperandTypes(BinaryOperator op, Expression left, SemanticType leftType, Expression right, SemanticType rightType)` returning the pair to hand to `InferBinaryOpType`: for a non-shift operator where exactly one side is an integer constant expression (`IntegerConstantEvaluator` through plan A's `ImplicitConversions`), if `ImplicitConversions.IsImplicitIntegerConstantConversion(constantExpr, constantType, otherType, resolveConstant)` [CORRECTED: plan A's spelling; the delegate is the checker's const resolver] then that side's effective type is `otherType`. Plan A's `StoreSeamConformanceTests` scan (a) already allowlists `TypeChecker.Expressions.Operators.cs::EffectiveOperandTypes` as a caller — keep the helper's name and file exactly that. Both sites call it; nothing else changes. The augmented site's RHS value is the constant candidate (`x += 1`); the target is never a constant.
   - Acceptance: the Phase-2 matrix; commit body records M2.
   - Commit: `fix(semantic): a constant operand converts to the other operand's type before numeric promotion (§10.2.11 then §12.4.7), at the binary and augmented sites (#1699)`

2. **Constant-overflow interplay control** — `TypeChecker.Expressions.Operators.cs:178-184` (`CheckConstantIntegerOverflow`)
   - Cells: `uint64 + 18446744073709551615` (fits ulong → accepted), `uint32 + 4294967296` (long constant, promotes to int64 — accepted, typed int64), both constants `4294967295u + 1` (existing SPY0348 path — unchanged control). No code change expected; the cells pin that the pre-step did not reroute the both-constant path.
   - Commit: folded into Task 1's matrix commit (Phase 4) — no separate commit.

### Phase 3: `**` binds its own-width overload (#1700)

**Goal:** no integer `**` operand is narrowed by the emitted cast; `uint64 ** e` is typed `uint64`; one classifier feeds both sites.

**Acceptance (class-level, measurable):**
- Matrix that goes green: `NarrowWidthArithmeticMatrixTests.IntegerPowerPrograms` — base {int32, int64, uint32, uint64} × exponent {same type, int32 var, uint64 var, literal 2, literal 0, literal 1, `const`, negative literal (`-1`)} × site {print, store into the recorded type, augmented `**=`}, expected type/value per Decision 4, with `x = 9223372036854775808UL`, `x ** 1` → `9223372036854775808`, `x ** 2` → OverflowError (caught and printed), `x ** 0` → `1`; `Sharpy.Core.Tests` `PowTests` for the three overloads (values, OverflowError parity, negative-exponent rule for `(ulong, long)`).
- Guard mutation: M4 emitter casts the `IntegerPowULong` operands to `long` (the old shape) → `x ** 1` cell red (prints negative); M5 augmented site bypasses `ClassifyIntegerPower` and keys on `resultType == Long` as before → `u64 **= 2` cell red (SPY0908/CS0266 or wrong kind); M6 `ApplyIntegerPowWidth` keeps the `ULong → Long` arm → `z: uint64 = x ** y` cell red (SPY0220). Each recorded red/green in the commit body.
- Execution evidence: `g1` prints `9223372036854775808`; `g3`, `g4` run; `g2`, `g5`, `g6` unchanged values; `n ** x` (n int32, x uint64 = 2) prints `9` — `@ sha (measured)`; `RoslynEmitterOperatorLoweringTests` pins the three new emitted spellings.

#### Tasks

1. **Core overloads** — `src/Sharpy.Core/Pow.cs`
   - Add `CheckedIntPow(ulong x, ulong y)`, `CheckedIntPow(ulong x, long y)`, `CheckedIntPow(long x, ulong y)` mirroring `:83`/`:132` (checked exponentiation-by-squaring, `OverflowError("integer exponentiation result too large for ulong|long")`); the `(ulong, long)` overload keeps the `y < 0` truncating branch (`(ulong)System.Math.Pow(x, y)`), the other two have none. C# 9.0 on `netstandard2.1` (block namespace, fully qualified `System.*`, complete XML docs — `GenerateDocumentationFile` + warnings-as-errors).
   - `src/Sharpy.Core.Tests` `PowTests`: `(2^63, 1) == 2^63`, `(2^63, 2)` → OverflowError, `(3UL, 2L) == 9`, `(3UL, -1L) == 0`, `(3L, 2UL) == 9`, `(long.MinValue, 1UL)`, `(0UL, 0UL) == 1`.
   - Commit: `feat(core): CheckedIntPow(ulong,ulong), (ulong,long) and (long,ulong) overloads — no ** operand is narrowed by a cast (#1700)`

2. **One classifier, three kinds** — `src/Sharpy.Compiler/Semantic/SemanticInfo.cs:2310-2340` (enum), `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.Operators.cs:236-260`, `src/Sharpy.Compiler/Semantic/TypeChecker.Statements.cs:459-483`, `src/Sharpy.Compiler/Semantic/TypeInferenceService.cs:1591-1640`
   - Add `IntegerPowULong`, `IntegerPowULongExponentLong`, `IntegerPowLongExponentULong` to `OperatorLoweringKind` (doc comments name the cast pair). New `ClassifyIntegerPower(leftType, rightType)` next to `ClassifyFlooredArithmetic` returns the kind per Decision 4; both the binary and the augmented site call it (the two copies are deleted). `InferPowerResultType` returns the type the classifier's overload returns (uint64 for a ulong base with an unsigned/non-negative-constant or signed exponent; int64 for a signed base with a ulong exponent; unchanged otherwise); `ApplyIntegerPowWidth` drops the `ULong` arm. The constant pre-step (Phase 2) runs first, so `x ** 2` is `(ulong, ulong)`.
   - `DispatchSiteInventoryTests` / `SwitchArmScan`: if the new classifier is a kind-dispatching switch the inventory sees, roster it `guarded-by:` the Phase-4 matrix (#1716 standing rule).
   - Commit: `fix(semantic): one ClassifyIntegerPower for the binary and augmented sites; uint64 ** e types uint64 and records its own-width overload (#1700)`

3. **Emitter reads the kind** — `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Operators.cs:1097-1135`, `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterOperatorLoweringTests.cs:291-292`
   - `GeneratePowerValue`: the two-way `castKind` ternary becomes a switch over the five integer kinds yielding `(baseCast, exponentCast)`; the `default` throw message lists all seven kinds. Still `SyntaxFactory` only; no type decision — the kind is the decision. Operands go through `EmittedTreePrecedence.Operand` factories as the existing arms do (the SPY0524 net watches the new casts).
   - Pin the three new spellings in `RoslynEmitterOperatorLoweringTests`. Commit body records M4.
   - Commit: `fix(codegen): GeneratePowerValue casts each ** operand by the recorded kind — (ulong,ulong), (ulong,long), (long,ulong) arms (#1700)`

4. **Constant fold carries uint64** — `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.Operators.cs:359-390`, the lowering pass's constant re-derivation (grep `IrConstant` under `src/Sharpy.Compiler/Lowering/` for the `**` fold consumer, E2 #1056)
   - `TryFoldIntegerPower` returns `ULong` when either operand type is `uint64` and the result fits `ulong` (keeps `Int`/`Long` for signed pairs; SPY0328 otherwise). [CORRECTED: verified @ f7c7d3d97 — the IR path CANNOT emit a `ulong` literal today: `LoweringPass.TryLowerFoldedPower` (`:227-244`) returns `null` above `long.MaxValue` and boxes `(long)result`; `RoslynEmitter.Expressions.cs:731` `EmitFoldedConstant` has arms for `bool`/`double`/`long`-typed-`Long` only, and its `long l` fallback prints `Literal((int)l)` — a `ULong`-typed constant that fits `long` would be silently emitted as an `int` literal. Add: a `ulong` box in `TryLowerFoldedPower` when the recorded type is `ULong` (mirroring the checker's fold conditions, as its doc comment requires), and a `ulong u => Literal(u)` arm in `EmitFoldedConstant` printing the `UL` suffix; the `long l` fallback must not receive a `ULong`-typed value (assert on `constant.Type`).] Cells: `print(2UL ** 63)` → `9223372036854775808` (SPY0328 today, measured); `2UL ** 64` → SPY0328; `k: uint64 = 4UL ** 2; print(k)` → `16`. Cells: `print(2UL ** 63)` → `9223372036854775808`; `2UL ** 64` → SPY0328; `k: uint64 = 4UL ** 2; print(k)` → `16`.
   - If the IR path cannot carry `ulong` without a broader change, the fallback is explicit: the fold returns `null` for a ulong-typed pair (runtime overload computes it; SPY0328 for that pair becomes a runtime OverflowError) — this is a documented, plan-visible choice, recorded in the commit body with the probe that forced it.
   - Commit: `fix(semantic): constant ** over a uint64 operand folds to uint64 when the result fits (#1700)`

5. **Regen after the `**` change** — same artifacts as Phase 1 Task 4.
   - Commit: `chore(regen): … after the ** overload family (no diff @ <sha> | findings: …)`

### Phase 4: Matrix, roster, close-out

**Goal:** the class has a standing harness; the fixtures discriminate; the close-out cites measured evidence.

**Acceptance (class-level, measurable):**
- `NarrowWidthArithmeticMatrixTests` gains three generators — `MixedSignednessPrograms`, `ConstantOperandPrograms`, `IntegerPowerPrograms` — and `MatrixIsTotalOverItsAxes` is extended to count them (the `+ 7` literal at `:700` becomes a computed total over declared axes; every (pair, family, operand-kind) is a live cell or a declared `NaCell` with a reason — e.g. shifts under the constant axis are N/A "count, not value"). Expected outcomes come from an explicit table in the test, NOT from a re-implementation of the rule.
- The uint64 group's two `**` cells (`:561-564`, `:582-592`) are rewritten to the new answers (`c: uint64 = a ** b` accepted, prints `49`; the `int64` store now needs `int64(a ** b)`), and the class doc's base classification gets an addendum (not an edit) noting the #1700 measurement.
- Fixtures: `expressions/numeric_promotion_mixed_signedness.spy` + `.expected` (accepted cells, values from C#/python3) and `errors/numeric_promotion_uint64_signed_refused_1699.spy` + `.error` with `@line:col` (one refusal per line — the `.error` matcher binds the first match, so split same-message expectations); `expressions/integer_power_uint64_1700.spy` + `.expected` (`9223372036854775808`, OverflowError caught, `9`, `0`) — every fixture python3-verified where Python has the construct.
- Roster: `docs/design/gap-discovery-contracts.md` Classes table gains "Numeric promotion fidelity (#1666, #1699, #1700) | the recorded type of a primitive binary expression IS the CLR result type (or the bound Core overload's); pairs C# cannot bind are refused by name; a constant operand converts first | `NarrowWidthArithmeticMatrixTests` (width × op × store × target, mixed-signedness, constant-operand and integer-power generators) + `PrimitiveCatalogTests` 8×8 | none".
- Mutations M1–M6 recorded in the respective commit bodies; the matrix commit records the total cell count and that each mutation names the cells it reddens.
- Whole-solution gate `@ sha (measured)`; the three sweeps' counts named separately.

#### Tasks

1. **Matrix generators + fixtures** — `src/Sharpy.Compiler.Tests/Semantic/NarrowWidthArithmeticMatrixTests.cs`, `src/Sharpy.Compiler.Tests/Integration/TestFixtures/{expressions,errors}/…`
   - Commit: `test(semantic): mixed-signedness, constant-operand and integer-power generators join the narrow-width matrix; fixtures by direction (#1699, #1700)`
2. **Roster + explanations** — `docs/design/gap-discovery-contracts.md`, `.github/copilot-instructions.md` (one sentence next to the numeric-typing note: "operand typing = `EffectiveOperandTypes` → `GetPromotedType`; `**` = `ClassifyIntegerPower`"), `DiagnosticExplanations.Semantic.cs` (if not done in Phase 1)
   - Commit: `docs(design): roster the numeric-promotion class and its matrix (#1699, #1700)`
3. **Close-out** — spec examples re-executed at the final sha; issues closed via `/close-issues` with the acceptance bullets and evidence; `probes/LEDGER.txt` final column.
   - Commit: none (comments on the issues).

## Testing Strategy

- **New fixtures:** the three named in Phase 4 (positive `.expected` fixtures whose values change if the bug is present: `c: uint32 = b + 1; print(c)` → `6` is refused with the bug; `print(x ** 1)` prints a negative number with the bug). **For every negative fixture the positive control that must keep passing:** `int64 + uint64` SPY0222 (already refused — control that the message shape is unchanged), `uint32 > int16` runs `True`, `uint64 << int32` runs `10`, `uint64 > 1` runs `True`.
- **Edge cells outside the repro lists:** both operand orders; `uint8`/`uint16` partners (`uint32 + uint8 → uint32`, `uint64 + uint8 → uint64`); the long-typed literal `4294967296` (converts to `ulong`, promotes `uint` to `long`); `u`/`UL`-suffixed constants; `const` references and folded constants; negative constants; the `x == None` and `int? < int` comparison shortcuts (controls that the comparison change did not reach the lifted/None arms); `min`/`max`; augmented forms `+= &= **=` on `uint32`/`uint64` targets; `**` with exponent 0/1/2/negative and base > 2⁶³; `n ** x` with a uint64 exponent; constant `2UL ** 63` / `2UL ** 64`.
- **Outputs that discriminate:** stores into the recorded type (refused vs accepted), printed values for `**`, the diagnostic code+message at the exact line. `print(a + b)` alone is never a cell.
- **Spec deliverables (equal weight to code):** `arithmetic_operators.md` (promotion table, unsigned widths, `**` rows), `comparison_operators.md` (new operand section), `bitwise_operators.md`, `integer_literals.md:53`, `builtin_functions.md` min/max note — each example executed before (recorded) and after.
- **Blast radius and regen timing:** regen after Phase 1 Task 3 and after Phase 3 Task 4 (not at push time); the whole-solution gate (`.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark"`) runs in the background per project with `--logger "console;verbosity=normal"`; the three GapDiscovery sweeps (`InteropConformance`, `MetamorphicCorpus`, `DifferentialExecution`) and `FrontEndParityTests` are named in the close-out with their own counts; a red is attributed only after a control run at the base sha in a worktree under `sharpy.worktrees/`.
- **Mutations (all recorded in commit bodies):** M1 same-width guard restored; M2 constant pre-step deleted; M3 comparison arm unconditional `Bool`; M4 emitter `(long)` casts for the ulong kinds; M5 augmented `**` site bypasses the classifier; M6 `ApplyIntegerPowWidth` keeps the `ULong` arm; M7 [ADDED] min/max type-argument fact dropped (`min(u32, i16)` red with CS1503, `min(2, 3.0)` green); M8 [ADDED] `EmitFoldedConstant`'s `ulong` arm removed (`2UL ** 63` cell red). Restore mutations with `cp` from a copy, never `git checkout`/`restore` (shared tree); rebuild after any in-tree mutation and clear fixture `bin/` before re-measuring.

## Issues to Close

- **#1699** — mixed-signedness promotion records a type the CLR does not produce — closed by Phase 1 Tasks 1–3 + Phase 2 Task 1 + Phase 4 Task 1; **close criterion:** the 8×8 table and the mixed-signedness + constant-operand generators green with M1/M2/M3/M7 recorded red; direction table shows every ICE cell → SPY0222/SPY0220 (including `decimal < float64` and both `min` ICE cells) and every false SPY0220/SPY0222 cell → runs (including `uint64 + 4294967296`), `min(u32, i16)` PRINTS its value through an emitted type argument, no working cell refused; spec examples executed `@ sha`; whole-solution gate `@ sha (measured)` [CORRECTED: min/max emitter half and the decimal/float comparison cell added].
- **#1700** — `**` on uint64 operands evaluated in int64 — closed by Phase 3 Tasks 1–4 + Phase 4 Task 1; **close criterion:** `print(x ** 1)` for `x = 9223372036854775808UL` prints `9223372036854775808` `@ sha`; `z: uint64 = x ** y` and `x ** 2` accepted; the integer-power generator green with M4/M5/M6 recorded red; Core `PowTests` green on both TFMs; the two rewritten uint64 matrix cells and the four spec statements moved together; regen diff read `@ sha`.

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Adequacy:** CLASS
**Verified on:** 2026-09-03 @ f7c7d3d97
**Plan file:** `.claude/plans/plan-299c1b.md`

Method: every referenced file globbed; every `file:line` opened (a read-only agent checked 47 references, the verifier re-read the operator, promotion, power, fold, IR and emitter sites it deferred); the 27 numeric probes of the prior session's ledger re-run at HEAD with `sharpyc run` (every return code and printed value matched, `scratchpad/verify/RERUN_LEDGER.txt`); 18 further probes for the cells the plan called unmeasured (`scratchpad/verify/np2`, `np3`); the C# ground truth confirmed to be a compiled net10.0 program (`cs-truth/Program.cs`), and its constant/conditional rows re-derived in a fresh scratch project; python3 3.12 for the `**` oracles; `gh issue view` for #1699/#1700 and the 2026-09-03 #1699 comment (present; five consequences match Context).

### Corrections Made
- **Phase 1 Task 3 — `min`/`max` has an emitter half.** Before: "`min(u32, i16)` typed `int64`". After: record the promoted type as the call's type argument (`SetInferredTypeArguments`, an existing merged carrier the emitter reads) so `Builtins.Min<long>(a, b)` binds; mutation M7. Evidence: `min(u32, i16)` and `min(u64, i32)` are SPY0908/CS1503 today because `Min<T>(T, T, params T[])` is emitted without a type argument and C# infers no `T` from (uint, short); a checker-only fix leaves the ICE (contract §4).
- **Phase 3 Task 4 — the IR cannot carry `ulong` today.** Before: "verify … add the arm if it cannot". After: measured — `TryLowerFoldedPower` boxes `(long)` and refuses above `long.MaxValue`; `EmitFoldedConstant` has no `ulong` arm and its `long` fallback prints `(int)l`; both arms specified; mutation M8.
- **Current State — nine cells the plan listed as unmeasured, measured:** m1/m2 (`min` ICEs), f19 (`uint64 + 4294967296` false SPY0222), f20 (`u32 += 1` runs), f21/f22 controls, f23 (`decimal < float64` ICE), g7 controls, g8 (`2UL ** 63` false SPY0328), g9 (`== None` shortcut unaffected).
- **Decision 3 / Phase 2 Task 1 — helper name.** `IsImplicitConstantConversion(expr, src, tgt)` → plan A's `IsImplicitIntegerConstantConversion(value, source, target, resolveConstant)`; the const-reference operand kind needs the delegate. The verified §10.2.11 contract of the predicate is recorded in Code facts (false for `int`/`long` targets — harmless for the pre-step).
- **Line references:** `CheckBinaryOp` :107 → method at :14, `InferBinaryOpType` call at :108; augmented call at `Statements.cs:382`.

### Adequacy
- **Contract:** named — recorded type of a primitive binary expression IS the CLR result type (or the bound Core overload's); unbindable pairs refused by name; constant operands convert first.
- **Matrix:** larger than the repro lists — 8×8 ordered pairs × {arith, cmp, bitwise} × 7 operand kinds × {binary, augmented}; `**` base × exponent × site; consumers `min`/`max`, unary, augmented. The verifier added one axis: comparison × non-integer numeric pairs (`decimal ⊗ float64` is the one pair promotion refuses, ICE today) `[ADDED: adequacy]`.
- **Cure:** seam — `GetPromotedType` = §12.4.7 (arith/cmp/bitwise all consult it), `EffectiveOperandTypes` at the two operator nodes, `ClassifyIntegerPower` for both `**` sites, plus explicit tables in tests — the "SPY0908 as a net" and "silent wrong behavior" rows.
- **Harness:** `NarrowWidthArithmeticMatrixTests` (verified: every generator fixes both operands at one width; `+ 7` literal at :700) gains three generators; `PrimitiveCatalogTests` 8×8; roster row in Phase 4. Allowlist delta: none (verified `grep 1699|1700` over `src/` and `docs/` is empty; `differential-exec-allowlist.txt:78-82` are the min/max deviation rows and stay).

### Falsifiability
- `PrimitiveCatalogTests.GetPromotedType_MatchesCSharpBinaryNumericPromotion`: mutation named (M1); expected values from an explicit table, not a re-implementation; positive controls (decimal theories kept).
- `MixedSignednessPrograms`: mutation named (M3 for the comparison arm — the only mutation that distinguishes "promotion fixed" from "comparison consults it"); positive controls (`int64 + uint64` message unchanged, `uint32 > int16` runs, shifts run); by-direction specified (ICE → SPY0222/SPY0220; the `int64`-store twin of each `uint32 ⊗ narrow-signed` pair keeps running).
- `ConstantOperandPrograms`: mutation named (M2); positive control (negative literals stay refused with the pre-step present); by-direction specified (`uint32 + 1` SPY0220 → runs; `uint64 + (-1)` ICE → SPY0222; `uint64 + 4294967296` SPY0222 → runs).
- `IntegerPowerPrograms` + `PowTests`: mutations named (M4 emitter casts, M5 augmented copy, M6 width arm, M8 fold arm); discriminating outputs (`x ** 1` prints a negative number with the bug); python3 oracles verified (`(2**63)**1`, `2**63`, `3**2`, `2**0`); the negative-exponent rule is the spec's own (`arithmetic_operators.md:226`, `2 ** -1 == 0`; measured `y ** -1` → `0` today).
- `min`/`max` cells: mutation named (M7); positive control (`min(2, 3.0)` stays green without the fact — C# infers `double`); by-direction (both mixed cells ICE today).
- Absence assertion "no SPY0908 in the matrix": positive control is the per-cell exact-diagnostic + count assertion (`:647-649`, verified) — a leaked SPY0908 fails the count.

### Warnings
- **Phase 2 depends on plan A Phase 2 by NAME** — `ImplicitConversions.IsImplicitIntegerConstantConversion` with the resolver delegate, and plan A's scan allowlists exactly `TypeChecker.Expressions.Operators.cs::EffectiveOperandTypes`; a different name or file fails plan A's `StoreSeamConformanceTests`.
- **The comparison-arm change reaches every numeric pair**, not only integers — `decimal ⊗ float64` flips ICE → SPY0222 (correct by direction; add the spec sentence to `comparison_operators.md` and the steer variant).
- **`EmitFoldedConstant`'s `long` fallback prints `(int)l` for any non-`Long` type** — a `ULong`-typed fold routed there is silent truncation; assert on `constant.Type` in the new arm.
- **`GetPromotedType` rewrite changes same-width narrow answers** (`sbyte ⊗ byte` → `int` directly instead of `short` floored to `int`); `InferMinMaxReturnType` sees the raw promoted type — add `min(int8, uint8)` (→ `int32`, prints the value) as a consumer control so the change is observed, not assumed.
- **Warm/cold:** `_binaryOpCache` keys stay a pure type triple ✓; the new min/max type-argument fact is node-keyed on an existing merged table ✓ — no new side table in this plan.

### Missing Steps Added
- Phase 1 Task 3: the type-argument materialization + M7; `min(2, 3.0)` and `min(int8, uint8)` controls.
- Phase 1 Task 2: `decimal < float64` cell and spec sentence; `y == None` control.
- Phase 3 Task 4: `ulong` box in `TryLowerFoldedPower` + `ulong` arm in `EmitFoldedConstant` + M8.
- Phase 0: the measured rows m1–m3, f19–f23, g7–g9 become the base ledger (no need to re-measure them, cite `@ f7c7d3d97 (measured)`).

### Unchecked Claims
- Whether the builtin `min`/`max` value-form emission route (`RoslynEmitter.Expressions.Access.cs:237`, `TryGenerateMinMaxValueFormWithKey`) reaches the `GetInferredTypeArguments` reader at `:1173` — the reader exists; the route was not traced. Phase 1 Task 3 must confirm it or add the read.
- `OperatorValidator.cs:165-180` short-circuit and the `:126-165` lifted-nullable/None shortcuts — line ranges confirmed by the agent; the shortcut ORDER relative to the numeric arm was verified only by the `y == None` probe.
- The `DispatchSiteInventoryTests`/`SwitchArmScan` roster mechanics for a new `ClassifyIntegerPower` switch — `SwitchArmScan` was not located by name (`DispatchSiteScan.cs` exists); the #1716 standing rule applies whichever scan sees it.
- Stdlib blast radius (`zlib`/`binascii`/`struct`-style unsigned arithmetic) — not grepped; the Phase 1 Task 4 regen diff is the measurement.
